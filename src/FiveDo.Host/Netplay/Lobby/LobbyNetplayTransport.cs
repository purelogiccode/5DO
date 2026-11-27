using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using FiveDo.Lobby.Protocol;

namespace FiveDo.Host.Netplay.Lobby;

/// <summary>
/// A UDP transport that registers with a lobby, punches a direct NAT path, and falls
/// back to the lobby's opaque UDP relay when direct traffic does not arrive.
/// </summary>
/// <remarks>
/// The transport owns the same socket used for emulator input/hash datagrams. Lobby
/// registration, punch, and relay-registration packets are consumed here; every other
/// datagram is passed to the lockstep session unchanged.
/// </remarks>
public sealed class LobbyNetplayTransport : INetplayTransport
{
    private readonly Socket _socket;
    private readonly IPEndPoint _rendezvous;
    private readonly IPEndPoint? _relay;
    private readonly ulong _roomId;
    private readonly Guid _token;
    private readonly byte _slot;
    private readonly LobbyTransportMode _configuredMode;
    private readonly TimeSpan _directTimeout;
    private readonly TimeSpan _registerInterval;
    private readonly TimeSpan _punchInterval;
    private readonly TimeSpan _relayHelloInterval;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly byte[] _receiveBuffer = new byte[LobbyProtocol.MaxDatagramSize];
    private TimeSpan _lastRegister;
    private TimeSpan _lastPunch = TimeSpan.MinValue;
    private TimeSpan _lastRelayHello = TimeSpan.MinValue;
    private TimeSpan _peerKnownAt = TimeSpan.MinValue;
    private IPEndPoint? _remote;
    private IPEndPoint? _peer;
    private ulong _punchNonce;
    private bool _registered;
    private bool _relayRegistered;
    private bool _directEstablished;
    private bool _relayActive;
    private bool _disposed;

    /// <summary>Initializes a lobby-assisted transport.</summary>
    /// <param name="options">Lobby and transport options.</param>
    /// <exception cref="ArgumentException">Thrown when options are invalid or IPv4 is unavailable.</exception>
    public LobbyNetplayTransport(LobbyTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new ArgumentException("A lobby host is required.", nameof(options));
        }

        if (options.RendezvousPort is < 1 or > ushort.MaxValue
            || options.RelayPort is < 0 or > ushort.MaxValue
            || options.LocalPort is < 0 or > ushort.MaxValue
            || options.Slot > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The lobby ports or player slot are out of range.");
        }

        if (options.RoomId == 0 || options.Token == Guid.Empty)
        {
            throw new ArgumentException("A room id and slot token are required.", nameof(options));
        }

        if (options.Mode == LobbyTransportMode.Relay && options.RelayPort == 0)
        {
            throw new ArgumentException("Relay mode requires a relay port.", nameof(options));
        }

        _roomId = options.RoomId;
        _token = options.Token;
        _slot = options.Slot;
        _configuredMode = options.Mode;
        _directTimeout = options.DirectTimeout;
        _registerInterval = options.RegisterInterval;
        _punchInterval = options.PunchInterval;
        _relayHelloInterval = options.RelayHelloInterval;
        IPAddress address = ResolveIPv4(options.Host);
        _rendezvous = new IPEndPoint(address, options.RendezvousPort);
        _relay = options.RelayPort > 0 ? new IPEndPoint(address, options.RelayPort) : null;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            _socket.Bind(new IPEndPoint(IPAddress.Any, options.LocalPort));
            _socket.ReceiveBufferSize = 64 * 1024;
            _socket.SendBufferSize = 64 * 1024;
            if (_configuredMode == LobbyTransportMode.Relay)
            {
                _relayActive = true;
                _remote = _relay;
                SendRelayHello();
            }

            SendRegister();
            _lastRegister = _clock.Elapsed;
        }
        catch
        {
            _socket.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public bool HasRemote => _remote is not null;

    /// <summary>Gets the local UDP endpoint.</summary>
    public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint!;

    /// <summary>Gets the current remote endpoint, when known.</summary>
    public IPEndPoint? RemoteEndPoint => _remote;

    /// <summary>Gets the last peer endpoint reported by the lobby.</summary>
    public IPEndPoint? PeerEndPoint => _peer;

    /// <summary>Gets the observed transport path.</summary>
    public LobbyTransportMode TransportMode => _relayActive
        ? LobbyTransportMode.Relay
        : _directEstablished ? LobbyTransportMode.Direct : _configuredMode;

    /// <summary>Gets whether the relay acknowledged this endpoint.</summary>
    public bool RelayRegistered => _relayRegistered;

    /// <inheritdoc />
    public void Send(ReadOnlySpan<byte> packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_remote is null)
        {
            throw new InvalidOperationException("The lobby netplay endpoint is not known yet.");
        }

        _socket.SendTo(packet, _remote);
    }

    /// <inheritdoc />
    public bool TryReceive(Span<byte> destination, out int length)
    {
        length = 0;
        if (_disposed)
        {
            return false;
        }

        Maintain();
        while (true)
        {
            bool readable;
            try
            {
                readable = _socket.Poll(0, SelectMode.SelectRead);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            if (!readable)
            {
                return false;
            }

            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            int received;
            try
            {
                received = _socket.ReceiveFrom(_receiveBuffer, ref from);
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            if (received <= 0 || from is not IPEndPoint source)
            {
                continue;
            }

            ReadOnlySpan<byte> packet = _receiveBuffer.AsSpan(0, received);
            if (LobbyDatagramCodec.IsLobbyDatagram(packet))
            {
                if (LobbyDatagramCodec.TryDecode(packet, out LobbyDatagram datagram))
                {
                    Handle(datagram, source);
                }

                continue;
            }

            if (!IsGamePacketFromAllowedSource(source) || packet.Length > destination.Length)
            {
                continue;
            }

            packet.CopyTo(destination);
            length = packet.Length;
            return true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SendBye();
        _socket.Dispose();
    }

    private static IPAddress ResolveIPv4(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? address)
            && address.AddressFamily == AddressFamily.InterNetwork)
        {
            return address;
        }

        IPAddress[] addresses = Dns.GetHostAddresses(host);
        IPAddress? match = addresses.FirstOrDefault(
            static candidate => candidate.AddressFamily == AddressFamily.InterNetwork);
        return match ?? throw new ArgumentException("The lobby host has no IPv4 address.", nameof(host));
    }

    private void Maintain()
    {
        TimeSpan now = _clock.Elapsed;
        if (!_registered || now - _lastRegister >= _registerInterval)
        {
            SendRegister();
            _lastRegister = now;
        }

        if (_configuredMode == LobbyTransportMode.Relay)
        {
            ActivateRelay();
        }
        else if (_peer is not null)
        {
            bool timeoutElapsed = _peerKnownAt != TimeSpan.MinValue
                && now - _peerKnownAt >= _directTimeout;
            if (_configuredMode == LobbyTransportMode.Auto
                && !_directEstablished
                && !_relayActive
                && _relay is not null
                && timeoutElapsed)
            {
                ActivateRelay();
            }

            if (!_relayActive
                && !_directEstablished
                && (_lastPunch == TimeSpan.MinValue || now - _lastPunch >= _punchInterval))
            {
                SendPunch();
                _lastPunch = now;
            }
        }

        if (_relayActive)
        {
            if (!ReferenceEquals(_remote, _relay))
            {
                _remote = _relay;
            }

            if (_lastRelayHello == TimeSpan.MinValue || now - _lastRelayHello >= _relayHelloInterval)
            {
                SendRelayHello();
                _lastRelayHello = now;
            }
        }
    }

    private void Handle(LobbyDatagram datagram, IPEndPoint source)
    {
        if (datagram.RoomId != _roomId)
        {
            return;
        }

        switch (datagram.Type)
        {
            case LobbyDatagramType.RegisterAck:
                if (!IsFromRendezvous(source))
                {
                    return;
                }

                _registered = true;
                if (datagram.Nonce != 0)
                {
                    _punchNonce = datagram.Nonce;
                }

                if (datagram.PeerEndPoint is not null)
                {
                    SetPeer(datagram.PeerEndPoint);
                }

                break;
            case LobbyDatagramType.Peer:
                if (!IsFromRendezvous(source))
                {
                    return;
                }

                if (datagram.Nonce != 0)
                {
                    _punchNonce = datagram.Nonce;
                }

                SetPeer(datagram.PeerEndPoint);
                break;
            case LobbyDatagramType.Punch:
                if (_peer is null)
                {
                    SetPeer(source);
                }

                break;
            case LobbyDatagramType.RelayAck:
                if (_relay is null || !EndpointsEqual(source, _relay))
                {
                    return;
                }

                _relayRegistered = true;
                if (_relayActive || _configuredMode == LobbyTransportMode.Relay)
                {
                    _relayActive = true;
                    _remote = _relay;
                }

                break;
            case LobbyDatagramType.Bye:
                _peer = null;
                _directEstablished = false;
                if (!_relayActive)
                {
                    _remote = null;
                }

                break;
        }
    }

    private void SetPeer(IPEndPoint? peer)
    {
        if (peer is null || peer.Port == 0)
        {
            return;
        }

        bool changed = _peer is null || !EndpointsEqual(_peer, peer);
        _peer = peer;
        if (changed)
        {
            _peerKnownAt = _clock.Elapsed;
            _directEstablished = false;
        }

        if (_configuredMode == LobbyTransportMode.Relay)
        {
            ActivateRelay();
            return;
        }

        if (!_relayActive)
        {
            _remote = peer;
        }
    }

    private void ActivateRelay()
    {
        if (_relay is null || _relayActive)
        {
            return;
        }

        _relayActive = true;
        _remote = _relay;
        _directEstablished = false;
        SendRelayHello();
        _lastRelayHello = _clock.Elapsed;
    }

    private bool IsGamePacketFromAllowedSource(IPEndPoint source)
    {
        if (_relayActive)
        {
            return _relay is not null && EndpointsEqual(source, _relay);
        }

        if (_peer is not null && EndpointsEqual(source, _peer))
        {
            _directEstablished = true;
            return true;
        }

        if (_peer is null)
        {
            SetPeer(source);
            _directEstablished = true;
            return true;
        }

        return false;
    }

    private void SendRegister()
    {
        SendDatagram(
            new LobbyDatagram(
                LobbyDatagramType.Register,
                _slot,
                _roomId,
                _token,
                0,
                null,
                _relay?.Port ?? 0,
                _configuredMode),
            _rendezvous);
    }

    private void SendPunch()
    {
        if (_peer is null)
        {
            return;
        }

        SendDatagram(
            new LobbyDatagram(
                LobbyDatagramType.Punch,
                _slot,
                _roomId,
                Guid.Empty,
                _punchNonce,
                null,
                0,
                _configuredMode),
            _peer);
    }

    private void SendRelayHello()
    {
        if (_relay is null)
        {
            return;
        }

        SendDatagram(
            new LobbyDatagram(
                LobbyDatagramType.RelayHello,
                _slot,
                _roomId,
                _token,
                _punchNonce,
                null,
                _relay.Port,
                LobbyTransportMode.Relay),
            _relay);
    }

    private void SendBye()
    {
        var bye = new LobbyDatagram(
            LobbyDatagramType.Bye,
            _slot,
            _roomId,
            _token,
            _punchNonce,
            null,
            _relay?.Port ?? 0,
            _configuredMode);
        SendDatagram(bye, _rendezvous);
        if (_relayActive && _relay is not null)
        {
            SendDatagram(bye, _relay);
        }
    }

    private void SendDatagram(LobbyDatagram datagram, IPEndPoint endpoint)
    {
        Span<byte> buffer = stackalloc byte[LobbyDatagramCodec.Size];
        int length = LobbyDatagramCodec.Encode(buffer, datagram);
        try
        {
            _ = _socket.SendTo(buffer[..length], endpoint);
        }
        catch (SocketException)
        {
            // Registration and punching are best-effort; the next interval retries.
        }
        catch (ObjectDisposedException)
        {
            // Shutdown race.
        }
    }

    private bool IsFromRendezvous(IPEndPoint source)
    {
        return EndpointsEqual(source, _rendezvous);
    }

    private static bool EndpointsEqual(IPEndPoint left, IPEndPoint right)
    {
        return left.Port == right.Port && left.Address.Equals(right.Address);
    }
}
