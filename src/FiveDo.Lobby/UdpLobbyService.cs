using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FiveDo.Lobby;

/// <summary>
/// The UDP rendezvous service. It learns each player's public endpoint, shares peer
/// endpoints, and coordinates direct punch attempts. It does not forward game packets.
/// </summary>
public sealed class UdpLobbyService : BackgroundService
{
    private readonly RoomManager _rooms;
    private readonly LobbyOptions _options;
    private readonly WindowRateLimiter _rateLimiter;
    private readonly ILogger<UdpLobbyService> _logger;
    private Socket? _socket;

    /// <summary>Initializes the rendezvous service.</summary>
    /// <param name="rooms">Room registry.</param>
    /// <param name="options">Lobby options.</param>
    /// <param name="logger">Logger.</param>
    public UdpLobbyService(
        RoomManager rooms,
        IOptions<LobbyOptions> options,
        ILogger<UdpLobbyService> logger)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _rooms = rooms;
        _options = options.Value;
        _rateLimiter = new WindowRateLimiter(_options.MaxRendezvousDatagramsPerSecond);
        _logger = logger;
    }

    /// <summary>Gets the bound UDP port.</summary>
    public int Port { get; private set; }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _options.Validate();
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, _options.UdpPort));
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        _socket = socket;
        Port = ((IPEndPoint)socket.LocalEndPoint!).Port;
        LobbyLog.UdpRendezvousListening(_logger, Port);
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _socket?.Dispose();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ReceiveLoopAsync(stoppingToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[LobbyProtocol.MaxDatagramSize];
        EndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (!cancellationToken.IsCancellationRequested)
        {
            SocketReceiveFromResult result;
            try
            {
                result = await _socket!
                    .ReceiveFromAsync(buffer, SocketFlags.None, any, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException
                or ObjectDisposedException
                or SocketException)
            {
                break;
            }

            if (result.ReceivedBytes <= 0 || result.RemoteEndPoint is not IPEndPoint from)
            {
                continue;
            }

            Process(buffer.AsSpan(0, result.ReceivedBytes), from);
        }
    }

    private void Process(ReadOnlySpan<byte> packet, IPEndPoint from)
    {
        if (!_rateLimiter.TryAcquire(1)
            || !LobbyDatagramCodec.TryDecode(packet, out LobbyDatagram datagram))
        {
            return;
        }

        if (datagram.Type is not (LobbyDatagramType.Register
            or LobbyDatagramType.Keepalive
            or LobbyDatagramType.Bye))
        {
            return;
        }

        if (!_rooms.TryGetRoom(datagram.RoomId, out Room room)
            || room.IsExpired(DateTimeOffset.UtcNow)
            || !room.TryAuthenticate(datagram.Token, out int slot)
            || slot != datagram.Slot)
        {
            return;
        }

        if (datagram.Type == LobbyDatagramType.Bye)
        {
            room.ClearUdpEndpoint(slot);
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _ = room.SetUdpEndpoint(slot, from, now);
        IPEndPoint? peer = room.GetPeerUdpEndpoint(slot);
        Send(
            new LobbyDatagram(
                LobbyDatagramType.RegisterAck,
                (byte)slot,
                room.UdpRoomId,
                Guid.Empty,
                room.UdpNonce,
                peer,
                room.UdpRelayPort,
                LobbyTransportMode.Auto),
            from);
        if (peer is null)
        {
            return;
        }

        SendPeer(room, slot, from, peer);
        for (var other = 0; other < room.MaxPlayers; other++)
        {
            if (other != slot && room.GetPeerUdpEndpoint(other) is not null)
            {
                SendPeer(room, other, peer, from);
            }
        }
    }

    private void SendPeer(Room room, int slot, IPEndPoint destination, IPEndPoint peer)
    {
        Send(
            new LobbyDatagram(
                LobbyDatagramType.Peer,
                (byte)slot,
                room.UdpRoomId,
                Guid.Empty,
                room.UdpNonce,
                peer,
                room.UdpRelayPort,
                LobbyTransportMode.Auto),
            destination);
    }

    private void Send(LobbyDatagram datagram, IPEndPoint endpoint)
    {
        Span<byte> buffer = stackalloc byte[LobbyDatagramCodec.Size];
        int length = LobbyDatagramCodec.Encode(buffer, datagram);
        Send(buffer[..length], endpoint);
    }

    private void Send(ReadOnlySpan<byte> packet, IPEndPoint endpoint)
    {
        try
        {
            _ = _socket?.SendTo(packet, endpoint);
        }
        catch (SocketException exception)
        {
            LobbyLog.UdpRendezvousSendFailed(_logger, endpoint, exception);
        }
        catch (ObjectDisposedException)
        {
            // Shutdown race.
        }
    }
}
