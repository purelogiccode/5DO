using System;
using System.Globalization;
using FiveDo.Lobby.Protocol;

namespace FiveDo.Host.Netplay.Lobby;

/// <summary>
/// Describes a lobby room and the local slot used by a netplay session. The record keeps
/// the control-relay token, the UDP rendezvous/relay endpoint data, and the host token
/// needed to close the room when the session ends.
/// </summary>
public sealed record LobbyNetplaySession
{
    /// <summary>Gets the lobby base URI.</summary>
    public required Uri LobbyUri { get; init; }

    /// <summary>Gets the human join code.</summary>
    public required string JoinCode { get; init; }

    /// <summary>Gets the room connection information returned by the lobby.</summary>
    public required RoomConnectionInfo Room { get; init; }

    /// <summary>Gets the opaque UDP room identifier.</summary>
    public required ulong RoomId { get; init; }

    /// <summary>Gets the control-relay and UDP registration token for the local slot.</summary>
    public required Guid Token { get; init; }

    /// <summary>Gets the local player slot (host is zero).</summary>
    public required byte Slot { get; init; }

    /// <summary>Gets a value indicating whether the local peer hosts the room.</summary>
    public required bool IsHost { get; init; }

    /// <summary>Gets the host bearer token, or <see langword="null"/> for the joining peer.</summary>
    public string? HostToken { get; init; }

    /// <summary>Gets the requested UDP transport mode.</summary>
    public LobbyTransportMode Mode { get; init; } = LobbyTransportMode.Auto;

    /// <summary>Gets how long direct punching is attempted before relay fallback.</summary>
    public TimeSpan DirectTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Creates the session descriptor for a room created by this peer.</summary>
    /// <param name="lobbyUri">Lobby base URI.</param>
    /// <param name="response">Room creation response.</param>
    /// <param name="mode">Requested UDP transport mode.</param>
    /// <param name="directTimeout">Direct-punch timeout before relay fallback.</param>
    /// <returns>The session descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    /// <exception cref="FormatException">Thrown when the room id or token is malformed.</exception>
    public static LobbyNetplaySession ForHost(
        Uri lobbyUri,
        CreateRoomResponse response,
        LobbyTransportMode mode,
        TimeSpan directTimeout)
    {
        ArgumentNullException.ThrowIfNull(lobbyUri);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.Room, nameof(response));
        return new LobbyNetplaySession
        {
            LobbyUri = lobbyUri,
            JoinCode = response.Room.JoinCode,
            Room = response.Room,
            RoomId = ParseRoomId(response.Room.UdpRoomId),
            Token = ParseToken(response.HostToken),
            Slot = 0,
            IsHost = true,
            HostToken = response.HostToken,
            Mode = mode,
            DirectTimeout = directTimeout,
        };
    }

    /// <summary>Creates the session descriptor for a room joined by this peer.</summary>
    /// <param name="lobbyUri">Lobby base URI.</param>
    /// <param name="response">Room join response.</param>
    /// <param name="mode">Requested UDP transport mode.</param>
    /// <param name="directTimeout">Direct-punch timeout before relay fallback.</param>
    /// <returns>The session descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    /// <exception cref="FormatException">Thrown when the room id or token is malformed.</exception>
    public static LobbyNetplaySession ForJoin(
        Uri lobbyUri,
        JoinRoomResponse response,
        LobbyTransportMode mode,
        TimeSpan directTimeout)
    {
        ArgumentNullException.ThrowIfNull(lobbyUri);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.Room, nameof(response));
        return new LobbyNetplaySession
        {
            LobbyUri = lobbyUri,
            JoinCode = response.Room.JoinCode,
            Room = response.Room,
            RoomId = ParseRoomId(response.Room.UdpRoomId),
            Token = ParseToken(response.SlotToken),
            Slot = checked((byte)response.Slot),
            IsHost = false,
            Mode = mode,
            DirectTimeout = directTimeout,
        };
    }

    private static ulong ParseRoomId(string value)
    {
        return ulong.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static Guid ParseToken(string value)
    {
        return Guid.ParseExact(value, "N");
    }
}
