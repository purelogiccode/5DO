using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;
using Microsoft.Extensions.Logging;

namespace FiveDo.Lobby;

/// <summary>
/// A per-room UDP relay. Clients send a <see cref="LobbyDatagramType.RelayHello"/>
/// to register their public relay endpoint; after that, every non-lobby datagram from
/// the endpoint is forwarded unchanged to the other player.
/// </summary>
public sealed class UdpRelay : IDisposable
{
    private readonly Socket _socket;
    private readonly Room _room;
    private readonly WindowRateLimiter _rateLimiter;
    private readonly ILogger<UdpRelay> _logger;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _receiveLoop;
    private bool _disposed;

    internal UdpRelay(
        Room room,
        int portStart,
        int portEnd,
        int maxBytesPerSecond,
        ILogger<UdpRelay> logger)
    {
        _room = room;
        _rateLimiter = new WindowRateLimiter(maxBytesPerSecond);
        _logger = logger;
        _socket = Bind(portStart, portEnd);
        Port = ((IPEndPoint)_socket.LocalEndPoint!).Port;
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cancellation.Token));
    }

    /// <summary>Gets the bound UDP port.</summary>
    public int Port { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _socket.Dispose();
        _ = _receiveLoop.ContinueWith(
            static (task, state) =>
            {
                _ = task.Exception;
                ((CancellationTokenSource)state!).Dispose();
            },
            _cancellation,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static Socket Bind(int portStart, int portEnd)
    {
        if (portStart == 0)
        {
            var ephemeral = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            ephemeral.Bind(new IPEndPoint(IPAddress.Any, 0));
            return ephemeral;
        }

        if (portEnd < portStart)
        {
            throw new InvalidOperationException("The UDP relay port range is invalid.");
        }

        for (var port = portStart; port <= portEnd; port++)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                return socket;
            }
            catch (SocketException)
            {
                socket.Dispose();
            }
        }

        throw new InvalidOperationException(
            $"No free UDP relay port in the range {portStart}-{portEnd}.");
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
                result = await _socket
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
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (LobbyDatagramCodec.TryDecode(packet, out LobbyDatagram datagram))
        {
            if (datagram.RoomId != _room.UdpRoomId
                || !_room.TryAuthenticate(datagram.Token, out int authenticatedSlot)
                || authenticatedSlot != datagram.Slot)
            {
                return;
            }

            switch (datagram.Type)
            {
                case LobbyDatagramType.RelayHello:
                case LobbyDatagramType.Register:
                case LobbyDatagramType.Keepalive:
                    _ = _room.SetRelayEndpoint(authenticatedSlot, from, now);
                    Send(
                        new LobbyDatagram(
                            LobbyDatagramType.RelayAck,
                            (byte)authenticatedSlot,
                            _room.UdpRoomId,
                            Guid.Empty,
                            _room.UdpNonce,
                            _room.GetPeerRelayEndpoint(authenticatedSlot),
                            Port,
                            LobbyTransportMode.Relay),
                        from);
                    break;
                case LobbyDatagramType.Bye:
                    _room.ClearRelayEndpoint(authenticatedSlot);
                    break;
            }

            return;
        }

        if (!_room.TryFindSlotByRelayEndpoint(from, out int slot))
        {
            return;
        }

        IPEndPoint? peer = _room.GetPeerRelayEndpoint(slot);
        if (peer is null || !_rateLimiter.TryAcquire(packet.Length))
        {
            return;
        }

        _room.Touch(now);
        Send(packet, peer);
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
            _ = _socket.SendTo(packet, endpoint);
        }
        catch (SocketException exception)
        {
            LobbyLog.UdpRelaySendFailed(_logger, Port, endpoint, exception);
        }
        catch (ObjectDisposedException)
        {
            // Shutdown race.
        }
    }
}
