using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using FiveDo.Lobby.Protocol;

namespace FiveDo.LobbyProbe;

internal sealed class ProbeUdpPeer : IDisposable
{
    private readonly Socket _socket;
    private readonly IPEndPoint _rendezvous;
    private readonly IPEndPoint? _relay;
    private readonly ulong _roomId;
    private readonly Guid _token;
    private readonly byte _slot;
    private readonly LobbyTransportMode _configuredMode;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly byte[] _buffer = new byte[LobbyProtocol.MaxDatagramSize];
    private TimeSpan _lastRegister;
    private TimeSpan _lastPunch = TimeSpan.MinValue;
    private TimeSpan _lastRelayHello = TimeSpan.MinValue;
    private TimeSpan _peerKnownAt = TimeSpan.MinValue;
    private IPEndPoint? _peer;
    private IPEndPoint? _remote;
    private ulong _punchNonce;
    private bool _relayActive;
    private bool _relayRegistered;
    private bool _directEstablished;
    private bool _disposed;

    public ProbeUdpPeer(
        RoomConnectionInfo room,
        string token,
        byte slot,
        LobbyTransportMode mode,
        int localPort = 0)
    {
        _roomId = ulong.Parse(room.UdpRoomId, System.Globalization.NumberStyles.HexNumber);
        _token = Guid.ParseExact(token, "N");
        _slot = slot;
        _configuredMode = mode;
        IPAddress address = ResolveIPv4(room.UdpRendezvous.Host);
        _rendezvous = new IPEndPoint(address, room.UdpRendezvous.Port);
        _relay = room.UdpRelay.Port > 0 ? new IPEndPoint(address, room.UdpRelay.Port) : null;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, localPort));
        _socket.ReceiveBufferSize = 64 * 1024;
        _socket.SendBufferSize = 64 * 1024;
        if (_configuredMode == LobbyTransportMode.Relay && _relay is not null)
        {
            _relayActive = true;
            _remote = _relay;
            SendRelayHello();
        }

        SendRegister();
        _lastRegister = _clock.Elapsed;
    }

    public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint!;

    public IPEndPoint? PeerEndPoint => _peer;

    public LobbyTransportMode TransportMode => _relayActive
        ? LobbyTransportMode.Relay
        : _directEstablished ? LobbyTransportMode.Direct : _configuredMode;

    public bool RelayRegistered => _relayRegistered;

    public bool Send(ReadOnlySpan<byte> payload)
    {
        Maintain();
        if (_remote is null)
        {
            return false;
        }

        _socket.SendTo(payload, _remote);
        return true;
    }

    public IReadOnlyList<byte[]> Poll()
    {
        Maintain();
        var payloads = new List<byte[]>();
        while (true)
        {
            bool readable;
            try
            {
                readable = _socket.Poll(0, SelectMode.SelectRead);
            }
            catch (ObjectDisposedException)
            {
                return payloads;
            }

            if (!readable)
            {
                return payloads;
            }

            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            int length;
            try
            {
                length = _socket.ReceiveFrom(_buffer, ref from);
            }
            catch (SocketException)
            {
                return payloads;
            }

            if (length <= 0 || from is not IPEndPoint source)
            {
                continue;
            }

            ReadOnlySpan<byte> packet = _buffer.AsSpan(0, length);
            if (LobbyDatagramCodec.IsLobbyDatagram(packet))
            {
                if (LobbyDatagramCodec.TryDecode(packet, out LobbyDatagram datagram))
                {
                    Handle(datagram, source);
                }

                continue;
            }

            if (!IsGamePacketFromAllowedSource(source))
            {
                continue;
            }

            payloads.Add(packet.ToArray());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
        if (now - _lastRegister >= TimeSpan.FromSeconds(2))
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
            bool timeout = _peerKnownAt != TimeSpan.MinValue
                && now - _peerKnownAt >= TimeSpan.FromSeconds(3);
            if (_configuredMode == LobbyTransportMode.Auto
                && !_directEstablished
                && !_relayActive
                && _relay is not null
                && timeout)
            {
                ActivateRelay();
            }

            if (!_relayActive
                && !_directEstablished
                && (_lastPunch == TimeSpan.MinValue || now - _lastPunch >= TimeSpan.FromMilliseconds(250)))
            {
                SendPunch();
                _lastPunch = now;
            }
        }

        if (_relayActive && _relay is not null)
        {
            _remote = _relay;
            if (_lastRelayHello == TimeSpan.MinValue || now - _lastRelayHello >= TimeSpan.FromSeconds(2))
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
                if (!EndpointsEqual(source, _rendezvous))
                {
                    return;
                }

                if (datagram.Nonce != 0)
                {
                    _punchNonce = datagram.Nonce;
                }

                SetPeer(datagram.PeerEndPoint);
                break;
            case LobbyDatagramType.Peer:
                if (!EndpointsEqual(source, _rendezvous))
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
                _relayRegistered = true;
                if (_relay is not null && EndpointsEqual(source, _relay))
                {
                    _relayActive = true;
                    _remote = _relay;
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
        }
        else if (!_relayActive)
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
            // Best effort.
        }
    }

    private static bool EndpointsEqual(IPEndPoint left, IPEndPoint right)
    {
        return left.Port == right.Port && left.Address.Equals(right.Address);
    }
}
