using System;
using System.Collections.Generic;

namespace FiveDo.Input;

/// <summary>
/// Resolves a keyboard key name against the console hotkeys in an <see cref="InputProfile"/>.
/// </summary>
/// <remarks>
/// The desktop shell handles console hotkeys before it forwards a key to the emulated pad,
/// so F5/F8/F10/F12 never reach a game. Matching is case-insensitive to tolerate platform
/// key-name casing differences.
/// </remarks>
public static class HotkeyRouter
{
    private static readonly Dictionary<string, HotkeyAction> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SaveState"] = HotkeyAction.SaveState,
        ["LoadState"] = HotkeyAction.LoadState,
        ["Screenshot"] = HotkeyAction.Screenshot,
        ["Fullscreen"] = HotkeyAction.Fullscreen,
        ["Pause"] = HotkeyAction.Pause,
        ["Reset"] = HotkeyAction.Reset,
        ["SlotPrevious"] = HotkeyAction.SlotPrevious,
        ["SlotNext"] = HotkeyAction.SlotNext,
        ["FrameAdvance"] = HotkeyAction.FrameAdvance,
        ["Rewind"] = HotkeyAction.Rewind,
        ["FastForward"] = HotkeyAction.FastForward,
        ["QuickMenu"] = HotkeyAction.QuickMenu,
        ["SwapDisc"] = HotkeyAction.SwapDisc,
        ["ToggleInputDisplay"] = HotkeyAction.ToggleInputDisplay,
    };

    /// <summary>Resolves a key name to its bound console action.</summary>
    /// <param name="profile">Input profile that owns the hotkey bindings.</param>
    /// <param name="keyName">Platform key name, for example <c>F5</c> or <c>Escape</c>.</param>
    /// <returns>The bound action, or <see cref="HotkeyAction.None"/> when the key is unbound.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    public static HotkeyAction Resolve(InputProfile profile, string? keyName)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return HotkeyAction.None;
        }

        foreach (var (actionName, boundKey) in profile.Hotkeys)
        {
            if (string.Equals(boundKey, keyName, StringComparison.OrdinalIgnoreCase)
                && Actions.TryGetValue(actionName, out HotkeyAction action))
            {
                return action;
            }
        }

        return HotkeyAction.None;
    }

    /// <summary>Resolves a key name to its bound console action.</summary>
    /// <param name="profile">Input profile that owns the hotkey bindings.</param>
    /// <param name="keyName">Platform key name, for example <c>F5</c> or <c>Escape</c>.</param>
    /// <param name="action">The bound action, or <see cref="HotkeyAction.None"/>.</param>
    /// <returns><see langword="true"/> when the key has a known bound action.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    public static bool TryResolve(InputProfile profile, string? keyName, out HotkeyAction action)
    {
        action = Resolve(profile, keyName);
        return action != HotkeyAction.None;
    }

    /// <summary>Gets the action name stored in <see cref="InputProfile.Hotkeys"/>.</summary>
    /// <param name="action">Action to name.</param>
    /// <returns>The persisted action name, or an empty string for <see cref="HotkeyAction.None"/>.</returns>
    public static string GetActionName(HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.SaveState => "SaveState",
            HotkeyAction.LoadState => "LoadState",
            HotkeyAction.Screenshot => "Screenshot",
            HotkeyAction.Fullscreen => "Fullscreen",
            HotkeyAction.Pause => "Pause",
            HotkeyAction.Reset => "Reset",
            HotkeyAction.SlotPrevious => "SlotPrevious",
            HotkeyAction.SlotNext => "SlotNext",
            HotkeyAction.FrameAdvance => "FrameAdvance",
            HotkeyAction.Rewind => "Rewind",
            HotkeyAction.FastForward => "FastForward",
            HotkeyAction.QuickMenu => "QuickMenu",
            HotkeyAction.SwapDisc => "SwapDisc",
            HotkeyAction.ToggleInputDisplay => "ToggleInputDisplay",
            _ => string.Empty,
        };
    }
}
