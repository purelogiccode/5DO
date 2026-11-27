using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;
using Microsoft.Extensions.Logging;

namespace FiveDo.Lobby;

/// <summary>
/// A per-room TCP relay used for the emulator control/state channel. The relay is
/// byte-transparent after a 32-byte connect header and an 8-byte paired preamble.
/// </summary>
public sealed class TcpRelay : IDisposable
{
    private readonly Lock _pendingGate = new();
    private readonly Dictionary<int, TcpClient> _pending = [];
    private readonly TcpListener _listener;
    private readonly Room _room;
    private readonly int _maxMinutes;
    private readonly ILogger<TcpRelay> _logger;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _acceptLoop;
    private bool _disposed;

    internal TcpRelay(
        Room room,
        int portStart,
        int portEnd,
        int maxMinutes,
        ILogger<TcpRelay> logger)
    {
        _room = room;
        _maxMinutes = maxMinutes;
        _logger = logger;
        _listener = Bind(portStart, portEnd);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cancellation.Token));
    }

    /// <summary>Gets the bound TCP port.</summary>
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
        try
        {
            _listener.Stop();
        }
        catch (SocketException exception)
        {
            LobbyLog.TcpListenerAlreadyStopped(_logger, Port, exception);
        }

        TcpClient[] pending;
        lock (_pendingGate)
        {
            pending = [.. _pending.Values];
            _pending.Clear();
        }

        foreach (TcpClient client in pending)
        {
            client.Dispose();
        }

        // The accept loop exits on cancellation. Do not block a caller that may be the
        // accept thread; the task observes the cancelled token and releases its resources.
        _ = _acceptLoop.ContinueWith(
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

    private static TcpListener Bind(int portStart, int portEnd)
    {
        if (portStart == 0)
        {
            var ephemeral = new TcpListener(IPAddress.Any, 0);
            ephemeral.Start();
            return ephemeral;
        }

        if (portEnd < portStart)
        {
            throw new InvalidOperationException("The TCP relay port range is invalid.");
        }

        for (var port = portStart; port <= portEnd; port++)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                return listener;
            }
            catch (SocketException)
            {
                // Try the next port in the configured range.
            }
        }

        throw new InvalidOperationException(
            $"No free TCP relay port in the range {portStart}-{portEnd}.");
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException
                or ObjectDisposedException
                or SocketException)
            {
                break;
            }

            client.NoDelay = true;
            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var keepOpen = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var header = new byte[LobbyRelayProtocol.ConnectSize];
            await client.GetStream().ReadExactlyAsync(header, timeout.Token).ConfigureAwait(false);
            if (!LobbyRelayProtocol.TryDecodeConnect(header, out ulong roomId, out Guid token, out byte slot)
                || roomId != _room.UdpRoomId
                || !_room.TryAuthenticate(token, out int authenticatedSlot)
                || authenticatedSlot != slot)
            {
                LobbyLog.TcpConnectRejected(_logger, _room.JoinCode);
                return;
            }

            _room.Touch(DateTimeOffset.UtcNow);
            TcpClient? peer = null;
            var pair = false;
            lock (_pendingGate)
            {
                if (_pending.TryGetValue(slot, out TcpClient? previous))
                {
                    previous.Dispose();
                }

                _pending[slot] = client;
                keepOpen = true;
                if (_pending.TryGetValue(0, out TcpClient? host)
                    && _pending.TryGetValue(1, out TcpClient? join))
                {
                    _pending.Remove(0);
                    _pending.Remove(1);
                    peer = slot == 0 ? join : host;
                    pair = true;
                }
            }

            if (pair)
            {
                TcpClient host = slot == 0 ? client : peer!;
                TcpClient join = slot == 0 ? peer! : client;
                await PairAsync(host, join, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or IOException
            or SocketException
            or ObjectDisposedException
            or EndOfStreamException)
        {
            LobbyLog.TcpClientDisconnected(_logger, _room.JoinCode, exception);
        }
        finally
        {
            if (!keepOpen)
            {
                client.Dispose();
            }
        }
    }

    private async Task PairAsync(TcpClient host, TcpClient join, CancellationToken cancellationToken)
    {
        try
        {
            NetworkStream hostStream = host.GetStream();
            NetworkStream joinStream = join.GetStream();
            await hostStream.WriteAsync(LobbyRelayProtocol.PairedPreamble, cancellationToken).ConfigureAwait(false);
            await joinStream.WriteAsync(LobbyRelayProtocol.PairedPreamble, cancellationToken).ConfigureAwait(false);
            LobbyLog.TcpRelayPaired(_logger, _room.JoinCode);
            using var pairCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_maxMinutes > 0)
            {
                pairCancellation.CancelAfter(TimeSpan.FromMinutes(_maxMinutes));
            }

            Task up = CopyAsync(hostStream, joinStream, pairCancellation.Token);
            Task down = CopyAsync(joinStream, hostStream, pairCancellation.Token);
            await Task.WhenAny(up, down).ConfigureAwait(false);
            pairCancellation.Cancel();
            host.Dispose();
            join.Dispose();
            await Task.WhenAll(up, down).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or IOException
            or SocketException
            or ObjectDisposedException)
        {
            LobbyLog.TcpRelayPairClosed(_logger, _room.JoinCode, exception);
        }
        finally
        {
            host.Dispose();
            join.Dispose();
        }
    }

    private static async Task CopyAsync(
        NetworkStream source,
        NetworkStream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or IOException
            or SocketException
            or ObjectDisposedException)
        {
            // The peer closed or the room was disposed; the other copy loop will stop too.
        }
    }
}
