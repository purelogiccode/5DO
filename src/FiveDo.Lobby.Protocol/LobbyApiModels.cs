using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace FiveDo.Lobby.Protocol;

/// <summary>A host:port pair returned by the lobby.</summary>
/// <param name="Host">Public host name or IPv4 address.</param>
/// <param name="Port">Port number, or zero when the endpoint is disabled.</param>
public sealed record LobbyEndpoint(string Host, int Port);

/// <summary>A request to create a room.</summary>
public sealed record CreateRoomRequest
{
    /// <summary>Gets or sets the emulator tag, for example <c>5do</c> or <c>xgrid</c>.</summary>
    public string Emulator { get; init; } = LobbyProtocol.Emulator5Do;

    /// <summary>Gets or sets the emulator-facing protocol version.</summary>
    public int ProtocolVersion { get; init; } = LobbyProtocol.Version;

    /// <summary>Gets or sets an opaque content identity, such as a disc or title id.</summary>
    public string ContentId { get; init; } = string.Empty;

    /// <summary>Gets or sets an optional content fingerprint.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Gets or sets the host's display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets or sets the requested player count. The prototype supports two.</summary>
    public int MaxPlayers { get; init; } = LobbyProtocol.PlayerSlots;
}

/// <summary>A request to join a room by its join code.</summary>
public sealed record JoinRoomRequest
{
    /// <summary>Gets or sets the emulator tag.</summary>
    public string Emulator { get; init; } = LobbyProtocol.Emulator5Do;

    /// <summary>Gets or sets the emulator-facing protocol version.</summary>
    public int ProtocolVersion { get; init; } = LobbyProtocol.Version;

    /// <summary>Gets or sets an opaque content identity.</summary>
    public string ContentId { get; init; } = string.Empty;

    /// <summary>Gets or sets an optional content fingerprint.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Gets or sets the joining player's display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets or sets a previous slot token for a reconnect.</summary>
    public string? SlotToken { get; init; }
}

/// <summary>The connection information shared by both peers in a room.</summary>
public sealed record RoomConnectionInfo
{
    /// <summary>Gets the opaque UDP room identifier as 16 lowercase hexadecimal digits.</summary>
    public required string UdpRoomId { get; init; }

    /// <summary>Gets the human join code.</summary>
    public required string JoinCode { get; init; }

    /// <summary>Gets the emulator tag.</summary>
    public required string Emulator { get; init; }

    /// <summary>Gets the emulator-facing protocol version.</summary>
    public int ProtocolVersion { get; init; }

    /// <summary>Gets the opaque content identity.</summary>
    public required string ContentId { get; init; }

    /// <summary>Gets the optional content fingerprint.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Gets the maximum player count.</summary>
    public int MaxPlayers { get; init; }

    /// <summary>Gets the HTTP/WebSocket host.</summary>
    public required LobbyEndpoint Signal { get; init; }

    /// <summary>Gets the WebSocket signaling path.</summary>
    public required string SignalPath { get; init; }

    /// <summary>Gets the TCP control relay endpoint.</summary>
    public required LobbyEndpoint ControlRelay { get; init; }

    /// <summary>Gets the UDP rendezvous endpoint.</summary>
    public required LobbyEndpoint UdpRendezvous { get; init; }

    /// <summary>Gets the UDP relay endpoint.</summary>
    public required LobbyEndpoint UdpRelay { get; init; }

    /// <summary>Gets when the room expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>The response to a room creation request.</summary>
public sealed record CreateRoomResponse
{
    /// <summary>Gets the room connection information.</summary>
    public required RoomConnectionInfo Room { get; init; }

    /// <summary>Gets the secret host token. Never share it with the joining player.</summary>
    public required string HostToken { get; init; }
}

/// <summary>The response to a room join request.</summary>
public sealed record JoinRoomResponse
{
    /// <summary>Gets the room connection information.</summary>
    public required RoomConnectionInfo Room { get; init; }

    /// <summary>Gets the joining slot's secret token.</summary>
    public required string SlotToken { get; init; }

    /// <summary>Gets the assigned zero-based player slot.</summary>
    public int Slot { get; init; }
}

/// <summary>Public information about one player slot.</summary>
/// <param name="Slot">Zero-based slot index.</param>
/// <param name="DisplayName">Optional display name.</param>
/// <param name="Connected">Whether the slot has registered a live endpoint or signaling connection.</param>
/// <param name="Ready">Whether the player reported readiness.</param>
/// <param name="Transport">The last reported path: <c>direct</c>, <c>relay</c>, or <c>auto</c>.</param>
public sealed record RoomPlayerStatus(
    int Slot,
    string? DisplayName,
    bool Connected,
    bool Ready,
    string? Transport);

/// <summary>Public room status, safe to return without a token.</summary>
public sealed record RoomStatus
{
    /// <summary>Gets the UDP room identifier.</summary>
    public required string UdpRoomId { get; init; }

    /// <summary>Gets the human join code.</summary>
    public required string JoinCode { get; init; }

    /// <summary>Gets the emulator tag.</summary>
    public required string Emulator { get; init; }

    /// <summary>Gets the emulator-facing protocol version.</summary>
    public int ProtocolVersion { get; init; }

    /// <summary>Gets the opaque content identity.</summary>
    public required string ContentId { get; init; }

    /// <summary>Gets the optional content fingerprint.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Gets the maximum player count.</summary>
    public int MaxPlayers { get; init; }

    /// <summary>Gets the current player states.</summary>
    public RoomPlayerStatus[] Players { get; init; } = [];

    /// <summary>Gets when the room was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the room expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Gets whether the room is closed.</summary>
    public bool Closed { get; init; }
}

/// <summary>A structured lobby error.</summary>
/// <param name="Code">Stable machine-readable error code.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record LobbyError(string Code, string Message);

/// <summary>A signaling message exchanged over the lobby WebSocket.</summary>
public sealed record SignalMessage
{
    /// <summary>Gets or sets the message type.</summary>
    public required string Type { get; init; }

    /// <summary>Gets or sets the room identifier.</summary>
    public string? RoomId { get; init; }

    /// <summary>Gets or sets the sender or subject slot.</summary>
    public int? Slot { get; init; }

    /// <summary>Gets or sets the peer slot.</summary>
    public int? PeerSlot { get; init; }

    /// <summary>Gets or sets the transport path: <c>direct</c>, <c>relay</c>, or <c>auto</c>.</summary>
    public string? Mode { get; init; }

    /// <summary>Gets or sets a human-readable detail.</summary>
    public string? Message { get; init; }

    /// <summary>Gets or sets the room snapshot.</summary>
    public RoomStatus? Room { get; init; }

    /// <summary>Gets or sets the player list.</summary>
    public RoomPlayerStatus[]? Players { get; init; }
}

/// <summary>A lightweight health response.</summary>
public sealed record LobbyHealth(
    string Status,
    int Rooms,
    int MaxRooms,
    int UdpPort,
    double UptimeSeconds);

/// <summary>An error raised by the lobby HTTP API.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Status and error code are consumed by the lobby server and API clients.")]
public sealed class LobbyApiException : Exception
{
    /// <summary>Initializes a generic API exception.</summary>
    public LobbyApiException()
        : this(HttpStatusCode.InternalServerError, "lobby_error", "The lobby request failed.")
    {
    }

    /// <summary>Initializes an API exception with a message.</summary>
    /// <param name="message">Human-readable detail.</param>
    public LobbyApiException(string message)
        : this(HttpStatusCode.InternalServerError, "lobby_error", message)
    {
    }

    /// <summary>Initializes an API exception with a message and inner exception.</summary>
    /// <param name="message">Human-readable detail.</param>
    /// <param name="innerException">Underlying failure.</param>
    public LobbyApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = HttpStatusCode.InternalServerError;
        Code = "lobby_error";
    }

    /// <summary>Initializes a new API exception.</summary>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="code">Stable error code.</param>
    /// <param name="message">Human-readable detail.</param>
    public LobbyApiException(HttpStatusCode statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>Gets the HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Gets the stable error code.</summary>
    public string Code { get; }
}
