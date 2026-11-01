namespace FiveDo.Input;

/// <summary>
/// Console-level actions that can be bound to a keyboard key.
/// </summary>
/// <remarks>
/// The action names stored in <see cref="InputProfile.Hotkeys"/> match these values,
/// so a settings file remains readable when new actions are added.
/// </remarks>
public enum HotkeyAction
{
    /// <summary>No bound action, or an unknown action name.</summary>
    None = 0,

    /// <summary>Save the current state to the selected slot.</summary>
    SaveState,

    /// <summary>Load the selected state slot.</summary>
    LoadState,

    /// <summary>Capture a screenshot.</summary>
    Screenshot,

    /// <summary>Toggle fullscreen presentation.</summary>
    Fullscreen,

    /// <summary>Start or pause emulation.</summary>
    Pause,

    /// <summary>Reset the emulated machine.</summary>
    Reset,

    /// <summary>Select the previous save-state slot.</summary>
    SlotPrevious,

    /// <summary>Select the next save-state slot.</summary>
    SlotNext,

    /// <summary>Advance the paused machine by one frame.</summary>
    FrameAdvance,

    /// <summary>Rewind the emulated machine by a short step.</summary>
    Rewind,

    /// <summary>Hold or toggle fast-forward playback.</summary>
    FastForward,

    /// <summary>Open or close the in-game quick menu.</summary>
    QuickMenu,

    /// <summary>Swap to the next disc in the playlist, or open a disc picker.</summary>
    SwapDisc,

    /// <summary>Toggle the on-screen frame and player-1 input display.</summary>
    ToggleInputDisplay,
}
