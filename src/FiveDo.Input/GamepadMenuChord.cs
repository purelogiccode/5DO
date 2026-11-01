namespace FiveDo.Input;

/// <summary>
/// Recognises the controller chord that opens the desktop quick menu.
/// </summary>
/// <remarks>
/// The chord is Select+Start so a game's own Start/Pause button keeps working. The
/// platform backend applies edge detection; this helper only defines the chord.
/// </remarks>
public static class GamepadMenuChord
{
    /// <summary>Gets the buttons that must be held together.</summary>
    public static GamepadButtons Buttons => GamepadButtons.Select | GamepadButtons.Start;

    /// <summary>Returns whether a raw sample holds the quick-menu chord.</summary>
    /// <param name="state">Raw platform sample.</param>
    /// <returns><see langword="true"/> when both chord buttons are pressed.</returns>
    public static bool IsPressed(GamepadState state)
    {
        return (state.Buttons & Buttons) == Buttons;
    }
}
