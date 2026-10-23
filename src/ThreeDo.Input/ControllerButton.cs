using System;

namespace ThreeDo.Input;

/// <summary>
/// Buttons on a 3DO gamepad. The flags are internal to the input layer; the PBUS encoder
/// maps them to the console's two button bytes.
/// </summary>
[Flags]
public enum ControllerButton
{
    /// <summary>No buttons.</summary>
    None = 0,

    /// <summary>The A button.</summary>
    A = 1 << 0,

    /// <summary>The B button.</summary>
    B = 1 << 1,

    /// <summary>The C button.</summary>
    C = 1 << 2,

    /// <summary>The P (play/pause) button.</summary>
    P = 1 << 3,

    /// <summary>The X (stop) button.</summary>
    X = 1 << 4,

    /// <summary>The left shoulder button.</summary>
    L = 1 << 5,

    /// <summary>The right shoulder button.</summary>
    R = 1 << 6,

    /// <summary>D-pad up.</summary>
    Up = 1 << 7,

    /// <summary>D-pad down.</summary>
    Down = 1 << 8,

    /// <summary>D-pad left.</summary>
    Left = 1 << 9,

    /// <summary>D-pad right.</summary>
    Right = 1 << 10,
}
