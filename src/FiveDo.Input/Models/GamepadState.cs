using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FiveDo.Input;


/// <summary>
/// A raw platform gamepad sample. Axis values are normalized to [-1, 1] with positive Y
/// pointing up, so a backend can pass its native values without knowing the 3DO layout.
/// </summary>
/// <param name="Buttons">Pressed physical buttons.</param>
/// <param name="LeftStickX">Left stick horizontal axis, -1 left to +1 right.</param>
/// <param name="LeftStickY">Left stick vertical axis, -1 down to +1 up.</param>
/// <param name="RightStickX">Right stick horizontal axis, -1 left to +1 right.</param>
/// <param name="RightStickY">Right stick vertical axis, -1 down to +1 up.</param>
/// <param name="LeftTrigger">Left analog trigger, 0 released to 1 fully pressed.</param>
/// <param name="RightTrigger">Right analog trigger, 0 released to 1 fully pressed.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct GamepadState(
    GamepadButtons Buttons,
    float LeftStickX = 0f,
    float LeftStickY = 0f,
    float RightStickX = 0f,
    float RightStickY = 0f,
    float LeftTrigger = 0f,
    float RightTrigger = 0f)
{
    /// <summary>Gets an empty sample with all buttons released and sticks centered.</summary>
    public static GamepadState Empty => default;

    /// <summary>Gets a value indicating whether any physical button is pressed.</summary>
    public bool IsPressed => Buttons != GamepadButtons.None;
}
