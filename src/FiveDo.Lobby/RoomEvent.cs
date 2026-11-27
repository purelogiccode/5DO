using System;

namespace FiveDo.Lobby;

/// <summary>Kinds of room state changes published to signaling clients.</summary>
public enum RoomEventType
{
    /// <summary>A player joined a slot.</summary>
    Joined = 0,

    /// <summary>A player left or disconnected.</summary>
    Left = 1,

    /// <summary>A UDP rendezvous endpoint changed.</summary>
    EndpointUpdated = 2,

    /// <summary>A UDP relay endpoint changed.</summary>
    RelayEndpointUpdated = 3,

    /// <summary>A player reported a direct or relay path.</summary>
    TransportChanged = 4,

    /// <summary>A player reported readiness.</summary>
    ReadyChanged = 5,

    /// <summary>The room closed.</summary>
    Closed = 6,
}

/// <summary>A room state change.</summary>
public sealed class RoomEventArgs : EventArgs
{
    /// <summary>Initializes a new room event.</summary>
    /// <param name="type">Change kind.</param>
    /// <param name="slot">Affected player slot, or -1 for the room.</param>
    /// <param name="mode">Transport mode or detail, when applicable.</param>
    public RoomEventArgs(RoomEventType type, int slot, string? mode)
    {
        Type = type;
        Slot = slot;
        Mode = mode;
    }

    /// <summary>Gets the change kind.</summary>
    public RoomEventType Type { get; }

    /// <summary>Gets the affected player slot, or -1 for the room.</summary>
    public int Slot { get; }

    /// <summary>Gets the transport mode or detail, when applicable.</summary>
    public string? Mode { get; }
}
