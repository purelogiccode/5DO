using System;
using System.Collections.Generic;

namespace ThreeDo.Input;

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

/// <summary>
/// Maps a raw gamepad sample to a 3DO controller state using a player's gamepad bindings.
/// </summary>
/// <remarks>
/// <para>
/// Binding names are the same keys persisted in <see cref="PlayerProfile.Gamepad"/>:
/// <c>South</c>, <c>East</c>, <c>West</c>, <c>North</c>, <c>LeftShoulder</c>,
/// <c>RightShoulder</c>, <c>LeftTrigger</c>, <c>RightTrigger</c>, <c>DpadUp</c>,
/// <c>DpadDown</c>, <c>DpadLeft</c>, <c>DpadRight</c>, <c>Start</c>, <c>Select</c>,
/// <c>LeftStick</c>, <c>RightStick</c>, <c>LeftStickUp</c>, <c>LeftStickDown</c>,
/// <c>LeftStickLeft</c>, <c>LeftStickRight</c>, <c>RightStickUp</c>,
/// <c>RightStickDown</c>, <c>RightStickLeft</c>, and <c>RightStickRight</c>.
/// </para>
/// <para>
/// Profiles written before stick and trigger bindings existed still work: the left stick
/// falls back to the D-pad bindings, and an analog trigger falls back to the matching
/// shoulder binding. Explicit bindings always win.
/// </para>
/// </remarks>
public static class GamepadMapper
{
    /// <summary>Gets the default radial dead zone for the analog sticks.</summary>
    public const float DefaultDeadZone = 0.5f;

    /// <summary>Gets the default threshold for treating an analog trigger as pressed.</summary>
    public const float DefaultTriggerThreshold = 0.5f;

    /// <summary>
    /// Gets the raw input names understood by the mapper, in a stable order suitable for a
    /// binding editor.
    /// </summary>
    public static IReadOnlyList<string> BindingNames { get; } =
    [
        "South",
        "East",
        "West",
        "North",
        "LeftShoulder",
        "RightShoulder",
        "LeftTrigger",
        "RightTrigger",
        "DpadUp",
        "DpadDown",
        "DpadLeft",
        "DpadRight",
        "LeftStickUp",
        "LeftStickDown",
        "LeftStickLeft",
        "LeftStickRight",
        "RightStickUp",
        "RightStickDown",
        "RightStickLeft",
        "RightStickRight",
        "LeftStick",
        "RightStick",
        "Start",
        "Select",
    ];

    /// <summary>Maps one raw sample through a player profile.</summary>
    /// <param name="profile">Player profile containing gamepad bindings.</param>
    /// <param name="state">Raw platform sample.</param>
    /// <param name="deadZone">Stick dead zone in the range 0-1. Defaults to <see cref="DefaultDeadZone"/>.</param>
    /// <param name="triggerThreshold">Trigger press threshold in the range 0-1.</param>
    /// <returns>The mapped 3DO controller state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a threshold is outside 0-1.</exception>
    public static ControllerState Map(
        PlayerProfile profile,
        GamepadState state,
        float deadZone = DefaultDeadZone,
        float triggerThreshold = DefaultTriggerThreshold)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!float.IsFinite(deadZone) || deadZone is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(deadZone), deadZone, "The dead zone must be between 0 and 1.");
        }

        if (!float.IsFinite(triggerThreshold) || triggerThreshold is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(triggerThreshold),
                triggerThreshold,
                "The trigger threshold must be between 0 and 1.");
        }

        var result = ControllerState.Empty;
        if (profile.Gamepad is null)
        {
            return result;
        }

        foreach (var (name, button) in profile.Gamepad)
        {
            if (button == ControllerButton.None)
            {
                continue;
            }

            if (IsPressed(name, state, deadZone, triggerThreshold))
            {
                result = result.With(button);
            }
        }

        ApplyStickFallback(profile, state, deadZone, ref result);
        ApplyTriggerFallback(profile, state, triggerThreshold, ref result);
        return result;
    }

    private static bool IsPressed(string name, GamepadState state, float deadZone, float triggerThreshold)
    {
        if (string.Equals(name, "South", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.South);
        }

        if (string.Equals(name, "East", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.East);
        }

        if (string.Equals(name, "West", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.West);
        }

        if (string.Equals(name, "North", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.North);
        }

        if (string.Equals(name, "LeftShoulder", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.LeftShoulder);
        }

        if (string.Equals(name, "RightShoulder", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.RightShoulder);
        }

        if (string.Equals(name, "LeftTrigger", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.LeftTrigger) || state.LeftTrigger > triggerThreshold;
        }

        if (string.Equals(name, "RightTrigger", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.RightTrigger) || state.RightTrigger > triggerThreshold;
        }

        if (string.Equals(name, "DpadUp", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.DpadUp);
        }

        if (string.Equals(name, "DpadDown", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.DpadDown);
        }

        if (string.Equals(name, "DpadLeft", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.DpadLeft);
        }

        if (string.Equals(name, "DpadRight", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.DpadRight);
        }

        if (string.Equals(name, "Start", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.Start);
        }

        if (string.Equals(name, "Select", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.Select);
        }

        if (string.Equals(name, "LeftStick", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.LeftStick);
        }

        if (string.Equals(name, "RightStick", StringComparison.OrdinalIgnoreCase))
        {
            return Has(state.Buttons, GamepadButtons.RightStick);
        }

        if (string.Equals(name, "LeftStickUp", StringComparison.OrdinalIgnoreCase))
        {
            return state.LeftStickY > deadZone;
        }

        if (string.Equals(name, "LeftStickDown", StringComparison.OrdinalIgnoreCase))
        {
            return state.LeftStickY < -deadZone;
        }

        if (string.Equals(name, "LeftStickLeft", StringComparison.OrdinalIgnoreCase))
        {
            return state.LeftStickX < -deadZone;
        }

        if (string.Equals(name, "LeftStickRight", StringComparison.OrdinalIgnoreCase))
        {
            return state.LeftStickX > deadZone;
        }

        if (string.Equals(name, "RightStickUp", StringComparison.OrdinalIgnoreCase))
        {
            return state.RightStickY > deadZone;
        }

        if (string.Equals(name, "RightStickDown", StringComparison.OrdinalIgnoreCase))
        {
            return state.RightStickY < -deadZone;
        }

        if (string.Equals(name, "RightStickLeft", StringComparison.OrdinalIgnoreCase))
        {
            return state.RightStickX < -deadZone;
        }

        if (string.Equals(name, "RightStickRight", StringComparison.OrdinalIgnoreCase))
        {
            return state.RightStickX > deadZone;
        }

        return false;
    }

    private static void ApplyStickFallback(
        PlayerProfile profile,
        GamepadState state,
        float deadZone,
        ref ControllerState result)
    {
        if (HasBinding(profile, "LeftStickUp")
            || HasBinding(profile, "LeftStickDown")
            || HasBinding(profile, "LeftStickLeft")
            || HasBinding(profile, "LeftStickRight"))
        {
            return;
        }

        if (state.LeftStickY > deadZone)
        {
            AddBinding(profile, "DpadUp", ref result);
        }

        if (state.LeftStickY < -deadZone)
        {
            AddBinding(profile, "DpadDown", ref result);
        }

        if (state.LeftStickX < -deadZone)
        {
            AddBinding(profile, "DpadLeft", ref result);
        }

        if (state.LeftStickX > deadZone)
        {
            AddBinding(profile, "DpadRight", ref result);
        }
    }

    private static void ApplyTriggerFallback(
        PlayerProfile profile,
        GamepadState state,
        float triggerThreshold,
        ref ControllerState result)
    {
        var leftPressed = Has(state.Buttons, GamepadButtons.LeftTrigger) || state.LeftTrigger > triggerThreshold;
        if (!HasBinding(profile, "LeftTrigger") && leftPressed)
        {
            AddBinding(profile, "LeftShoulder", ref result);
        }

        var rightPressed = Has(state.Buttons, GamepadButtons.RightTrigger) || state.RightTrigger > triggerThreshold;
        if (!HasBinding(profile, "RightTrigger") && rightPressed)
        {
            AddBinding(profile, "RightShoulder", ref result);
        }
    }

    private static void AddBinding(PlayerProfile profile, string name, ref ControllerState result)
    {
        if (TryGetBinding(profile, name, out var button) && button != ControllerButton.None)
        {
            result = result.With(button);
        }
    }

    private static bool HasBinding(PlayerProfile profile, string name)
    {
        return TryGetBinding(profile, name, out var button) && button != ControllerButton.None;
    }

    private static bool TryGetBinding(PlayerProfile profile, string name, out ControllerButton button)
    {
        if (profile.Gamepad is null)
        {
            button = ControllerButton.None;
            return false;
        }

        if (profile.Gamepad.TryGetValue(name, out button))
        {
            return true;
        }

        foreach (var (candidate, value) in profile.Gamepad)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
            {
                button = value;
                return true;
            }
        }

        button = ControllerButton.None;
        return false;
    }

    private static bool Has(GamepadButtons value, GamepadButtons flag) => (value & flag) != 0;
}
