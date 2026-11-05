using System;
using System.Collections.Generic;

namespace FiveDo.Input;


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
        ["SlotPrevious"] = "F6",
        ["SlotNext"] = "F7",
        ["FrameAdvance"] = "F9",
        ["Rewind"] = "R",
        ["FastForward"] = "Tab",
        ["QuickMenu"] = "F4",
        ["SwapDisc"] = "F3",
        ["ToggleInputDisplay"] = "F2",
    };

    /// <summary>Creates the default input profile.</summary>
    /// <returns>A profile with six players and default hotkeys.</returns>
    public static InputProfile CreateDefault()
    {
        return new InputProfile();
    }

    /// <summary>Adds hotkeys introduced after an older profile was saved.</summary>
    /// <remarks>
    /// Existing assignments are preserved, so a user's custom key map is never
    /// overwritten. This keeps profiles created before F6/F7/F9 were added working
    /// without requiring a reset-to-defaults action.
    /// </remarks>
    public void AddMissingDefaultHotkeys()
    {
        // Older profiles can deserialize a null Hotkeys dictionary.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (Hotkeys is null)
        {
            Hotkeys = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        InputProfile defaults = CreateDefault();
        foreach ((var action, var key) in defaults.Hotkeys)
        {
            Hotkeys.TryAdd(action, key);
        }
    }

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
        var players = new PlayerProfile[PbusEncoder.MaxPorts];
        for (var i = 0; i < players.Length; i++)
        {
            players[i] = PlayerProfile.CreateDefault(i + 1);
        }

        return players;
    }
}
