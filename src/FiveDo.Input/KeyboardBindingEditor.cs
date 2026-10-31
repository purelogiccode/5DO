using System;
using System.Collections.Generic;

namespace FiveDo.Input;

/// <summary>
/// Edits one player's keyboard bindings while keeping at most one key per controller button.
/// </summary>
/// <remarks>
/// The editor works on a copy; call <see cref="ApplyTo"/> after the user accepts the changes.
/// Rebinding a key removes it from any other button so the resulting profile cannot contain
/// duplicate key names.
/// </remarks>
public sealed class KeyboardBindingEditor
{
    private static readonly ControllerButton[] Buttons =
    [
        ControllerButton.A,
        ControllerButton.B,
        ControllerButton.C,
        ControllerButton.P,
        ControllerButton.X,
        ControllerButton.L,
        ControllerButton.R,
        ControllerButton.Up,
        ControllerButton.Down,
        ControllerButton.Left,
        ControllerButton.Right,
    ];

    private readonly Dictionary<ControllerButton, string> _bindings = [];

    /// <summary>Initializes a new instance of the <see cref="KeyboardBindingEditor"/> class.</summary>
    /// <param name="profile">Profile to copy. Missing buttons start unbound.</param>
    public KeyboardBindingEditor(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Load(profile);
    }

    /// <summary>Gets the controller buttons in display order.</summary>
    public static IReadOnlyList<ControllerButton> ControllerButtons => Buttons;

    /// <summary>Gets the current key for a button, or <see langword="null"/> when unbound.</summary>
    /// <param name="button">Controller button.</param>
    /// <returns>The key name, or <see langword="null"/>.</returns>
    public string? GetKey(ControllerButton button)
    {
        return _bindings.GetValueOrDefault(button);
    }

    /// <summary>Binds a key to a button, removing the key from any other button.</summary>
    /// <param name="button">Controller button.</param>
    /// <param name="key">Key name, or <see langword="null"/> or empty to clear the binding.</param>
    public void Rebind(ControllerButton button, string? key)
    {
        if (button == ControllerButton.None)
        {
            throw new ArgumentOutOfRangeException(nameof(button), button, "Select a controller button.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            _bindings.Remove(button);
            return;
        }

        foreach (ControllerButton existing in new List<ControllerButton>(_bindings.Keys))
        {
            if (existing != button && string.Equals(_bindings[existing], key, StringComparison.OrdinalIgnoreCase))
            {
                _bindings.Remove(existing);
            }
        }

        _bindings[button] = key;
    }

    /// <summary>Clears one binding.</summary>
    /// <param name="button">Controller button.</param>
    public void Clear(ControllerButton button)
    {
        _bindings.Remove(button);
    }

    /// <summary>Replaces the bindings with the player-1 defaults.</summary>
    public void ResetDefaults()
    {
        Load(PlayerProfile.CreateDefault(1));
    }

    /// <summary>Gets binding conflicts. The editor prevents duplicates, so this is normally empty.</summary>
    /// <returns>Conflict descriptions.</returns>
    public IReadOnlyList<string> FindConflicts()
    {
        var conflicts = new List<string>();
        var keys = new Dictionary<string, ControllerButton>(StringComparer.OrdinalIgnoreCase);
        foreach ((ControllerButton button, var key) in _bindings)
        {
            if (keys.TryGetValue(key, out ControllerButton existing) && existing != button)
            {
                conflicts.Add($"Key '{key}' is bound to both {existing} and {button}.");
            }
            else
            {
                keys[key] = button;
            }
        }

        return conflicts;
    }

    /// <summary>Writes the edited bindings to a profile.</summary>
    /// <param name="profile">Destination profile.</param>
    public void ApplyTo(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Keyboard.Clear();
        foreach ((ControllerButton button, var key) in _bindings)
        {
            profile.Keyboard[key] = button;
        }
    }

    private void Load(PlayerProfile profile)
    {
        _bindings.Clear();
        foreach ((var key, ControllerButton button) in profile.Keyboard)
        {
            if (button != ControllerButton.None && !string.IsNullOrWhiteSpace(key))
            {
                _bindings[button] = key;
            }
        }
    }
}
