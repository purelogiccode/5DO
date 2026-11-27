using System;
using System.Diagnostics.CodeAnalysis;
using FiveDo.Lobby.Protocol;

namespace FiveDo.Host.Netplay.Lobby;

/// <summary>Options for a lobby-assisted netplay UDP transport.</summary>
public sealed record LobbyTransportOptions
{
    /// <summary>Gets the lobby host name or IPv4 address.</summary>
    public required string Host { get; init; }

    /// <summary>Gets the UDP rendezvous port.</summary>
    public required int RendezvousPort { get; init; }

    /// <summary>Gets the UDP relay port, or zero when relaying is disabled.</summary>
    public required int RelayPort { get; init; }

    /// <summary>Gets the opaque room identifier.</summary>
    public required ulong RoomId { get; init; }

    /// <summary>Gets the room slot token.</summary>
    public required Guid Token { get; init; }

    /// <summary>Gets the local player slot.</summary>
    public required byte Slot { get; init; }

    /// <summary>Gets the local UDP bind port. Zero selects an ephemeral port.</summary>
    [SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Configured by the CLI and external clients.")]
    public int LocalPort { get; init; }

    /// <summary>Gets the requested transport mode.</summary>
    public LobbyTransportMode Mode { get; init; } = LobbyTransportMode.Auto;

    /// <summary>Gets how long direct punching is attempted before relay fallback.</summary>
    public TimeSpan DirectTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Gets the rendezvous registration interval.</summary>
    public TimeSpan RegisterInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets the direct punch interval.</summary>
    public TimeSpan PunchInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets the relay registration refresh interval.</summary>
    public TimeSpan RelayHelloInterval { get; init; } = TimeSpan.FromSeconds(2);
}
