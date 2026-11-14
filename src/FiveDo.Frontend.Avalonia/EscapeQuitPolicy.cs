namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Tracks the two-step Escape shortcut used by the desktop shell: the first press pauses
/// emulation and arms the shortcut, and the next press closes the application.
/// </summary>
/// <remarks>
/// The policy is disarmed whenever emulation resumes, stops, or the window loses focus
/// so a later Escape always pauses again instead of closing the shell by surprise.
/// </remarks>
internal sealed class EscapeQuitPolicy
{
    private bool _armed;

    /// <summary>Gets a value indicating whether the next Escape closes the application.</summary>
    public bool IsArmed => _armed;

    /// <summary>Clears the armed state so the next Escape pauses emulation again.</summary>
    public void Disarm()
    {
        _armed = false;
    }

    /// <summary>Applies an Escape press to the two-step pause/quit sequence.</summary>
    /// <param name="running">Whether emulation is currently running.</param>
    /// <returns>
    /// <see langword="true"/> when the press should close the application; otherwise
    /// <see langword="false"/> when it should pause or leave emulation paused.
    /// </returns>
    public bool ShouldQuit(bool running)
    {
        if (!running && _armed)
        {
            _armed = false;
            return true;
        }

        _armed = true;
        return false;
    }
}
