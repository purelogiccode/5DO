using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FiveDo.Lobby;

/// <summary>
/// WebSocket signaling for room presence and transport status. The 5DO data plane does
/// not require this socket; it exists so desktop shells and XGrid can show room state.
/// </summary>
public static class LobbySignalEndpoint
{
    private const int MaxMessageBytes = 64 * 1024;

    /// <summary>Maps the signaling endpoint.</summary>
    /// <param name="app">Application builder.</param>
    public static void MapLobbySignal(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.MapGet(LobbyProtocol.SignalPath, HandleAsync).RequireRateLimiting("signal");
    }

    private static async Task HandleAsync(
        HttpContext context,
        RoomManager rooms,
        ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger("FiveDo.Lobby.Signal");
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string roomCode = context.Request.Query["room"].ToString();
        string token = context.Request.Query["token"].ToString();
        if (!rooms.TryGetRoom(roomCode, out Room room)
            || !room.TryAuthenticate(token, out int slot))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (room.IsExpired(DateTimeOffset.UtcNow))
        {
            context.Response.StatusCode = StatusCodes.Status410Gone;
            return;
        }

        using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        room.SetSignalConnected(slot, true);
        room.Touch(DateTimeOffset.UtcNow);
        var channel = Channel.CreateBounded<SignalMessage>(
            new BoundedChannelOptions(128)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            });
        void OnChanged(object? sender, RoomEventArgs roomEvent)
        {
            _ = sender;
            SignalMessage? message = CreateSignal(roomEvent, room);
            if (message is not null)
            {
                _ = channel.Writer.TryWrite(message);
            }
        }

        room.Changed += OnChanged;
        RoomStatus snapshot = room.Snapshot(DateTimeOffset.UtcNow);
        _ = channel.Writer.TryWrite(
            new SignalMessage
            {
                Type = "welcome",
                RoomId = room.JoinCode,
                Slot = slot,
                Room = snapshot,
                Players = snapshot.Players,
                Message = "Signaling connected.",
            });

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        Task send = SendLoopAsync(socket, channel.Reader, cancellation.Token);
        Task receive = ReceiveLoopAsync(socket, room, slot, channel.Writer, cancellation.Token);
        try
        {
            _ = await Task.WhenAny(send, receive).ConfigureAwait(false);
            cancellation.Cancel();
            await Task.WhenAll(send, receive).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or WebSocketException
            or IOException
            or ObjectDisposedException)
        {
            LobbyLog.SignalSocketClosed(logger, room.JoinCode, exception);
        }
        finally
        {
            room.Changed -= OnChanged;
            room.SetSignalConnected(slot, false);
            await CloseAsync(socket, logger).ConfigureAwait(false);
        }
    }

    private static async Task SendLoopAsync(
        WebSocket socket,
        ChannelReader<SignalMessage> reader,
        CancellationToken cancellationToken)
    {
        await foreach (SignalMessage message in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                message,
                LobbyJsonContext.Default.SignalMessage);
            await socket
                .SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task ReceiveLoopAsync(
        WebSocket socket,
        Room room,
        int slot,
        ChannelWriter<SignalMessage> writer,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await socket
                .ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                .ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            if (message.Length + result.Count > MaxMessageBytes)
            {
                _ = writer.TryWrite(
                    new SignalMessage
                    {
                        Type = "error",
                        Message = "The signaling message is too large.",
                    });
                return;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            string json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);
            SignalMessage? incoming;
            try
            {
                incoming = JsonSerializer.Deserialize(json, LobbyJsonContext.Default.SignalMessage);
            }
            catch (JsonException)
            {
                _ = writer.TryWrite(
                    new SignalMessage { Type = "error", Message = "The signaling JSON is invalid." });
                continue;
            }

            if (incoming is null || string.IsNullOrWhiteSpace(incoming.Type))
            {
                continue;
            }

            switch (incoming.Type)
            {
                case "transport":
                    if (incoming.Mode is { } mode)
                    {
                        _ = room.SetTransport(slot, mode, DateTimeOffset.UtcNow);
                    }

                    break;
                case "ready":
                    room.SetReady(slot, true);
                    break;
                case "not-ready":
                    room.SetReady(slot, false);
                    break;
                case "ping":
                    _ = writer.TryWrite(new SignalMessage { Type = "pong" });
                    break;
                case "leave":
                    return;
                default:
                    _ = writer.TryWrite(
                        new SignalMessage
                        {
                            Type = "error",
                            Message = $"Unknown signaling message type '{incoming.Type}'.",
                        });
                    break;
            }
        }
    }

    private static SignalMessage? CreateSignal(RoomEventArgs roomEvent, Room room)
    {
        RoomStatus snapshot = room.Snapshot(DateTimeOffset.UtcNow);
        return roomEvent.Type switch
        {
            RoomEventType.Joined => new SignalMessage
            {
                Type = "peer-joined",
                RoomId = room.JoinCode,
                Slot = roomEvent.Slot,
                Room = snapshot,
                Players = snapshot.Players,
            },
            RoomEventType.Left => new SignalMessage
            {
                Type = "peer-left",
                RoomId = room.JoinCode,
                Slot = roomEvent.Slot,
                Room = snapshot,
                Players = snapshot.Players,
            },
            RoomEventType.TransportChanged => new SignalMessage
            {
                Type = "transport",
                RoomId = room.JoinCode,
                Slot = roomEvent.Slot,
                Mode = roomEvent.Mode,
                Room = snapshot,
                Players = snapshot.Players,
            },
            RoomEventType.ReadyChanged => new SignalMessage
            {
                Type = "ready",
                RoomId = room.JoinCode,
                Slot = roomEvent.Slot,
                Mode = roomEvent.Mode,
                Room = snapshot,
                Players = snapshot.Players,
            },
            RoomEventType.Closed => new SignalMessage
            {
                Type = "closed",
                RoomId = room.JoinCode,
                Room = snapshot,
                Players = snapshot.Players,
            },
            _ => null,
        };
    }

    private static async Task CloseAsync(WebSocket socket, ILogger logger)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await socket
                .CloseAsync(WebSocketCloseStatus.NormalClosure, "Lobby signaling closed.", timeout.Token)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or WebSocketException
            or ObjectDisposedException)
        {
            LobbyLog.SignalCloseFailed(logger, exception);
        }
    }
}
