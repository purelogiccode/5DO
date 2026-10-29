using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FiveDo.Input;


/// <summary>
/// Keyboard and gamepad bindings for one player.
/// </summary>
public sealed class PlayerProfile
{
    /// <summary>Gets or sets the display name.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Round-tripped through JSON and read by the desktop UI.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets keyboard bindings as key name (e.g. "A", "Left") to button.</summary>
    public IDictionary<string, ControllerButton> Keyboard { get; set; } = new Dictionary<string, ControllerButton>(StringComparer.Ordinal);

    /// <summary>Gets gamepad bindings as button name (e.g. "South") to button.</summary>
    public IDictionary<string, ControllerButton> Gamepad { get; set; } = new Dictionary<string, ControllerButton>(StringComparer.Ordinal);

    /// <summary>Creates the default player profile.</summary>
    /// <param name="player">One-based player number.</param>
    /// <returns>A profile with the default bindings.</returns>
    public static PlayerProfile CreateDefault(int player)
    {
        var profile = new PlayerProfile { Name = $"Player {player}" };
        if (player == 1)
        {
            profile.Keyboard["Up"] = ControllerButton.Up;
            profile.Keyboard["Down"] = ControllerButton.Down;
            profile.Keyboard["Left"] = ControllerButton.Left;
            profile.Keyboard["Right"] = ControllerButton.Right;
            profile.Keyboard["Z"] = ControllerButton.A;
            profile.Keyboard["X"] = ControllerButton.B;
            profile.Keyboard["C"] = ControllerButton.C;
            profile.Keyboard["A"] = ControllerButton.L;
            profile.Keyboard["S"] = ControllerButton.R;
            profile.Keyboard["Enter"] = ControllerButton.P;
            profile.Keyboard["Backspace"] = ControllerButton.X;
        }

        profile.Gamepad["South"] = ControllerButton.A;
        profile.Gamepad["West"] = ControllerButton.B;
        profile.Gamepad["North"] = ControllerButton.C;
        profile.Gamepad["LeftShoulder"] = ControllerButton.L;
        profile.Gamepad["RightShoulder"] = ControllerButton.R;
        profile.Gamepad["LeftTrigger"] = ControllerButton.L;
        profile.Gamepad["RightTrigger"] = ControllerButton.R;
        profile.Gamepad["DpadUp"] = ControllerButton.Up;
        profile.Gamepad["DpadDown"] = ControllerButton.Down;
        profile.Gamepad["DpadLeft"] = ControllerButton.Left;
        profile.Gamepad["DpadRight"] = ControllerButton.Right;
        profile.Gamepad["LeftStickUp"] = ControllerButton.Up;
        profile.Gamepad["LeftStickDown"] = ControllerButton.Down;
        profile.Gamepad["LeftStickLeft"] = ControllerButton.Left;
        profile.Gamepad["LeftStickRight"] = ControllerButton.Right;
        profile.Gamepad["Start"] = ControllerButton.P;
        profile.Gamepad["Select"] = ControllerButton.X;
        return profile;
    }

    /// <summary>Creates a deep copy of this profile.</summary>
    /// <returns>A copy that can be edited without changing the original.</returns>
    public PlayerProfile Clone() => new()
    {
        Name = Name,
        Keyboard = new Dictionary<string, ControllerButton>(Keyboard, StringComparer.OrdinalIgnoreCase),
        Gamepad = new Dictionary<string, ControllerButton>(Gamepad, StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>Finds keys bound to more than one button.</summary>
    /// <returns>Conflict descriptions; empty when the profile is consistent.</returns>
    public IReadOnlyList<string> FindConflicts()
    {
        var conflicts = new List<string>();
        FindConflicts(Keyboard, "keyboard", conflicts);
        FindConflicts(Gamepad, "gamepad", conflicts);
        return conflicts;
    }

    private static void FindConflicts(
        IEnumerable<KeyValuePair<string, ControllerButton>> bindings,
        string kind,
        List<string> conflicts)
    {
        var seen = new Dictionary<string, ControllerButton>(StringComparer.OrdinalIgnoreCase);
        foreach ((var name, ControllerButton button) in bindings)
        {
            if (seen.TryGetValue(name, out ControllerButton existing) && existing != button)
            {
                conflicts.Add($"{kind} input '{name}' is bound to both {existing} and {button}.");
            }
            else
            {
                seen[name] = button;
            }
        }
    }
}
