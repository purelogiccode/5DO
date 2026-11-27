using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace FiveDo.Lobby;

/// <summary>Source-generated log messages for the lobby service.</summary>
internal static partial class LobbyLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "UDP rendezvous listening on port {Port}.")]
    public static partial void UdpRendezvousListening(ILogger logger, int port);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "UDP rendezvous could not send to {Endpoint}.")]
    public static partial void UdpRendezvousSendFailed(ILogger logger, IPEndPoint endpoint, Exception exception);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Removed {Count} expired lobby rooms.")]
    public static partial void RoomsRemoved(ILogger logger, int count);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error, Message = "Lobby room cleanup failed.")]
    public static partial void RoomCleanupFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Debug, Message = "TCP relay listener {Port} was already stopped.")]
    public static partial void TcpListenerAlreadyStopped(ILogger logger, int port, Exception exception);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Debug, Message = "Rejected a TCP relay connect on room {Room}.")]
    public static partial void TcpConnectRejected(ILogger logger, string room);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Debug, Message = "TCP relay client on room {Room} disconnected before pairing.")]
    public static partial void TcpClientDisconnected(ILogger logger, string room, Exception exception);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Information, Message = "Paired TCP control relay for room {Room}.")]
    public static partial void TcpRelayPaired(ILogger logger, string room);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Debug, Message = "TCP control relay pair for room {Room} closed.")]
    public static partial void TcpRelayPairClosed(ILogger logger, string room, Exception exception);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "UDP relay on port {Port} could not send to {Endpoint}.")]
    public static partial void UdpRelaySendFailed(ILogger logger, int port, IPEndPoint endpoint, Exception exception);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Created room {Room} for {Emulator} content {Content}.")]
    public static partial void RoomCreated(ILogger logger, string room, string emulator, string content);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Information, Message = "Room {Room} joined by slot {Slot}.")]
    public static partial void RoomJoined(ILogger logger, string room, int slot);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Debug, Message = "Lobby signaling socket for room {Room} closed.")]
    public static partial void SignalSocketClosed(ILogger logger, string room, Exception exception);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Debug, Message = "Lobby signaling close handshake failed.")]
    public static partial void SignalCloseFailed(ILogger logger, Exception exception);
}
