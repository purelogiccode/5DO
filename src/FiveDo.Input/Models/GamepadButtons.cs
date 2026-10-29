using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FiveDo.Input;


/// <summary>
/// Physical gamepad inputs reported by a platform backend. The flags are intentionally
/// vendor-neutral: XInput, evdev, and future backends translate their native buttons into
/// this set, and <see cref="GamepadMapper"/> applies the user's per-player bindings.
/// </summary>
[Flags]
public enum GamepadButtons
{
    /// <summary>No physical buttons.</summary>
    None = 0,

    /// <summary>The bottom face button (XInput A, evdev BTN_SOUTH).</summary>
    South = 1 << 0,

    /// <summary>The right face button (XInput B, evdev BTN_EAST).</summary>
    East = 1 << 1,

    /// <summary>The top face button (XInput Y, evdev BTN_NORTH).</summary>
    North = 1 << 2,

    /// <summary>The left face button (XInput X, evdev BTN_WEST).</summary>
    West = 1 << 3,

    /// <summary>The left shoulder button.</summary>
    LeftShoulder = 1 << 4,

    /// <summary>The right shoulder button.</summary>
    RightShoulder = 1 << 5,

    /// <summary>The left analog trigger, reported when it passes the trigger threshold.</summary>
    LeftTrigger = 1 << 6,

    /// <summary>The right analog trigger, reported when it passes the trigger threshold.</summary>
    RightTrigger = 1 << 7,

    /// <summary>D-pad up.</summary>
    DpadUp = 1 << 8,

    /// <summary>D-pad down.</summary>
    DpadDown = 1 << 9,

    /// <summary>D-pad left.</summary>
    DpadLeft = 1 << 10,

    /// <summary>D-pad right.</summary>
    DpadRight = 1 << 11,

    /// <summary>The Start button.</summary>
    Start = 1 << 12,

    /// <summary>The Select/Back button.</summary>
    Select = 1 << 13,

    /// <summary>The left stick click.</summary>
    LeftStick = 1 << 14,

    /// <summary>The right stick click.</summary>
    RightStick = 1 << 15,
}
