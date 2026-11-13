using System;
using System.Collections.Generic;

namespace FiveDo.Input;

/// <summary>Visual state of one 3DO pad button in the input-bindings preview.</summary>
public enum ControllerPreviewState
{
    /// <summary>No binding and not pressed.</summary>
    Normal,

    /// <summary>A keyboard or gamepad control is mapped to the button.</summary>
    Mapped,

    /// <summary>The binding row for the button is selected.</summary>
    Selected,

    /// <summary>The mapped keyboard key or gamepad control is held down.</summary>
    Pressed,
}

/// <summary>
/// Computes the state of each 3DO pad button for the input-bindings controller preview.
/// </summary>
/// <remarks>
/// The preview uses button flags so keyboard and gamepad presses share one code path.
/// State precedence is pressed, selected, mapped, normal: a held button is always visible
/// even when another row is selected.
/// </remarks>
public static class ControllerPreview
{
    /// <summary>Gets the visual state for one button.</summary>
    /// <param name="button">The single button to test.</param>
    /// <param name="mapped">Buttons with at least one keyboard or gamepad binding.</param>
    /// <param name="selected">Buttons whose binding row is selected.</param>
    /// <param name="pressed">Buttons whose mapped control is currently held.</param>
    /// <returns>The visual state.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="button"/> is <see cref="ControllerButton.None"/> or a
    /// combination of several buttons.
    /// </exception>
    public static ControllerPreviewState GetState(
        ControllerButton button,
        ControllerButton mapped,
        ControllerButton selected,
        ControllerButton pressed)
    {
        if (button == ControllerButton.None || (button & (button - 1)) != ControllerButton.None)
        {
            throw new ArgumentOutOfRangeException(nameof(button), button, "Select exactly one controller button.");
        }

        if ((pressed & button) != ControllerButton.None)
        {
            return ControllerPreviewState.Pressed;
        }

        if ((selected & button) != ControllerButton.None)
        {
            return ControllerPreviewState.Selected;
        }

        return (mapped & button) != ControllerButton.None ? ControllerPreviewState.Mapped : ControllerPreviewState.Normal;
    }

    /// <summary>Returns every button that has a keyboard or gamepad binding.</summary>
    /// <param name="profile">Player profile to inspect.</param>
    /// <returns>The mapped button flags, or <see cref="ControllerButton.None"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    public static ControllerButton GetMappedButtons(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ControllerButton mapped = ControllerButton.None;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (profile.Keyboard is not null)
        {
            foreach (ControllerButton button in profile.Keyboard.Values)
            {
                mapped |= button;
            }
        }

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (profile.Gamepad is not null)
        {
            foreach (ControllerButton button in profile.Gamepad.Values)
            {
                mapped |= button;
            }
        }

        return mapped;
    }

    /// <summary>Finds the first keyboard key bound to a button.</summary>
    /// <param name="profile">Player profile to inspect.</param>
    /// <param name="button">Button to find.</param>
    /// <returns>The key name, or <see langword="null"/> when the button is unbound.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    public static string? FindKeyboardKey(PlayerProfile profile, ControllerButton button)
    {
        ArgumentNullException.ThrowIfNull(profile);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (button == ControllerButton.None || profile.Keyboard is null)
        {
            return null;
        }

        foreach ((var key, ControllerButton value) in profile.Keyboard)
        {
            if (value == button && !string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>Finds the physical gamepad controls bound to a button.</summary>
    /// <param name="profile">Player profile to inspect.</param>
    /// <param name="button">Button to find.</param>
    /// <returns>The binding names in ordinal order; empty when the button is unbound.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    public static IReadOnlyList<string> FindGamepadNames(PlayerProfile profile, ControllerButton button)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var names = new List<string>();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (button == ControllerButton.None || profile.Gamepad is null)
        {
            return names;
        }

        foreach ((var name, ControllerButton value) in profile.Gamepad)
        {
            if (value == button && !string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>Tests whether a key name is bound to a button for one player.</summary>
    /// <param name="profile">Player profile to inspect.</param>
    /// <param name="key">Key name from the input event.</param>
    /// <returns>The bound button, or <see cref="ControllerButton.None"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    public static ControllerButton FindKeyboardButton(PlayerProfile profile, string? key)
    {
        ArgumentNullException.ThrowIfNull(profile);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (string.IsNullOrWhiteSpace(key) || profile.Keyboard is null)
        {
            return ControllerButton.None;
        }

        foreach ((var name, ControllerButton value) in profile.Keyboard)
        {
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return ControllerButton.None;
    }
}
