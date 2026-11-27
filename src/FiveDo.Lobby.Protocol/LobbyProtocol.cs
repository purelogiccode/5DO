using System;

namespace FiveDo.Lobby.Protocol;

/// <summary>
/// Shared constants for the emulator-agnostic lobby control plane.
/// </summary>
/// <remarks>
/// The lobby never interprets emulator state, frame data, or input packets. It only
/// registers rooms, exchanges public UDP endpoints, punches NAT mappings, and relays
/// opaque datagrams between two player slots.
/// </remarks>
public static class LobbyProtocol
{
    /// <summary>The control-plane protocol version written by this build.</summary>
    public const byte Version = 1;

    /// <summary>The default emulator tag used by 5DO sessions.</summary>
    public const string Emulator5Do = "5do";

    /// <summary>The default emulator tag used by XGrid sessions.</summary>
    public const string EmulatorXGrid = "xgrid";

    /// <summary>The HTTP path for WebSocket signaling.</summary>
    public const string SignalPath = "/v1/signal";

    /// <summary>The number of player slots supported by this prototype.</summary>
    public const int PlayerSlots = 2;

    /// <summary>The largest datagram accepted by the lobby UDP endpoint.</summary>
    public const int MaxDatagramSize = 64 * 1024;

    /// <summary>The magic value at the start of every lobby UDP datagram.</summary>
    public static ReadOnlySpan<byte> DatagramMagic => "5DOL"u8;

    /// <summary>The magic value at the start of every TCP relay connect header.</summary>
    public static ReadOnlySpan<byte> RelayConnectMagic => "5DLC"u8;

    /// <summary>The magic value at the start of every TCP relay paired preamble.</summary>
    public static ReadOnlySpan<byte> RelayPairedMagic => "5DLP"u8;

    /// <summary>Gets the display label for a player slot.</summary>
    /// <param name="slot">Zero-based slot index.</param>
    /// <returns>A stable label.</returns>
    public static string SlotName(int slot)
    {
        return slot switch
        {
            0 => "host",
            1 => "join",
            _ => $"slot{slot}",
        };
    }
}
