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

/// <summary>
/// Complete input configuration: up to six player profiles plus console hotkeys.
/// </summary>
public sealed class InputProfile
{
    /// <summary>Gets player profiles. The array always has six entries.</summary>
    public PlayerProfile[] Players { get; set; } = CreateDefaultPlayers();

    /// <summary>Gets console hotkeys (action name to key name).</summary>
    public IDictionary<string, string> Hotkeys { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SaveState"] = "F5",
        ["LoadState"] = "F8",
        ["Screenshot"] = "F12",
        ["Fullscreen"] = "F11",
        ["Pause"] = "Escape",
        ["Reset"] = "F10",
    };

    /// <summary>Creates the default input profile.</summary>
    /// <returns>A profile with six players and default hotkeys.</returns>
    public static InputProfile CreateDefault() => new();

    /// <summary>Creates a deep copy of this profile.</summary>
    /// <returns>A copy that can be edited without changing the original.</returns>
    public InputProfile Clone()
    {
        var players = new PlayerProfile[Players.Length];
        for (var i = 0; i < players.Length; i++)
        {
            players[i] = Players[i].Clone();
        }

        return new InputProfile
        {
            Players = players,
            Hotkeys = new Dictionary<string, string>(Hotkeys, StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>Gets all binding conflicts across players and hotkeys.</summary>
    /// <returns>Conflict descriptions; empty when the profile is consistent.</returns>
    public IReadOnlyList<string> FindConflicts()
    {
        var conflicts = new List<string>();
        for (var i = 0; i < Players.Length; i++)
        {
            foreach (var conflict in Players[i].FindConflicts())
            {
                conflicts.Add($"Player {i + 1}: {conflict}");
            }
        }

        var hotkeyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (action, key) in Hotkeys)
        {
            if (hotkeyNames.TryGetValue(key, out var existing))
            {
                conflicts.Add($"Hotkey '{key}' is bound to both {existing} and {action}.");
            }
            else
            {
                hotkeyNames[key] = action;
            }
        }

        return conflicts;
    }

    private static PlayerProfile[] CreateDefaultPlayers()
    {
        var players = new PlayerProfile[6];
        for (var i = 0; i < players.Length; i++)
        {
            players[i] = PlayerProfile.CreateDefault(i + 1);
        }

        return players;
    }
}

/// <summary>
/// Loads and saves <see cref="InputProfile"/> as JSON.
/// </summary>
public static class InputProfileStore
{
    /// <summary>Loads a profile file, creating and saving defaults when it does not exist.</summary>
    /// <param name="path">Profile path.</param>
    /// <returns>The loaded or default profile.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file exists but is not a valid profile.</exception>
    public static InputProfile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            var defaults = InputProfile.CreateDefault();
            Save(path, defaults);
            return defaults;
        }

        try
        {
            InputProfile? profile = JsonSerializer.Deserialize(File.ReadAllText(path), InputProfileJsonContext.Default.InputProfile);
            return profile ?? throw new InvalidDataException($"Input profile '{path}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Input profile '{path}' is not valid JSON.", exception);
        }
    }

    /// <summary>Saves a profile atomically.</summary>
    /// <param name="path">Profile path.</param>
    /// <param name="profile">Profile to save.</param>
    public static void Save(string path, InputProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(profile);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(profile, InputProfileJsonContext.Default.InputProfile));
        File.Move(temporary, path, overwrite: true);
    }
}
