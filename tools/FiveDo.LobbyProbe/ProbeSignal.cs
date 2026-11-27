using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;

namespace FiveDo.LobbyProbe;

internal sealed class ProbeSignal : IDisposable
{
    private readonly ClientWebSocket _socket;

    private ProbeSignal(ClientWebSocket socket)
    {
        _socket = socket;
    }

    public static async Task<ProbeSignal> ConnectAsync(
        Uri lobby,
        RoomConnectionInfo room,
        string token,
        CancellationToken cancellationToken)
    {
        var builder = new UriBuilder(
            string.Equals(lobby.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ? "wss" : "ws",
            room.Signal.Host,
            room.Signal.Port,
            room.SignalPath);
        builder.Query = $"room={Uri.EscapeDataString(room.JoinCode)}&token={Uri.EscapeDataString(token)}";
        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(builder.Uri, cancellationToken).ConfigureAwait(false);
            var buffer = new byte[8192];
            WebSocketReceiveResult result = await socket
                .ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                .ConfigureAwait(false);
            string welcome = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"signal: {welcome}");
            await SendAsync(socket, new SignalMessage { Type = "ready" }, cancellationToken).ConfigureAwait(false);
            return new ProbeSignal(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public async Task SendTransportAsync(LobbyTransportMode mode, CancellationToken cancellationToken)
    {
        string value = mode switch
        {
            LobbyTransportMode.Direct => "direct",
            LobbyTransportMode.Relay => "relay",
            _ => "auto",
        };
        await SendAsync(_socket, new SignalMessage { Type = "transport", Mode = value }, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        _socket.Dispose();
    }

    private static Task SendAsync(
        ClientWebSocket socket,
        SignalMessage message,
        CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            message,
            LobbyJsonContext.Default.SignalMessage);
        return socket.SendAsync(
            new ArraySegment<byte>(payload),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }
}
