namespace FiveDo.Host;

/// <summary>
/// Tracks the focus-loss pause transition for the desktop shell.
/// </summary>
/// <remarks>
/// The shell releases input on every deactivation. Pausing is optional; when it
/// happens, the policy remembers that the pause was automatic so a later activation
/// can resume the run without overriding a manual pause.
/// </remarks>
public sealed class FocusPausePolicy
{
    /// <summary>Gets a value indicating whether the current pause was caused by focus loss.</summary>
    public bool AutoPaused { get; private set; }

    /// <summary>Decides whether a deactivation should pause emulation.</summary>
    /// <param name="pauseOnFocusLoss">Whether the user enabled focus-loss pausing.</param>
    /// <param name="running">Whether emulation is currently running.</param>
    /// <returns><see langword="true"/> when the caller should pause.</returns>
    public bool OnDeactivated(bool pauseOnFocusLoss, bool running)
    {
        if (!pauseOnFocusLoss || !running)
        {
            return false;
        }

        AutoPaused = true;
        return true;
    }

    /// <summary>Decides whether an activation should resume emulation.</summary>
    /// <param name="running">Whether emulation is currently running.</param>
    /// <returns><see langword="true"/> when the caller should resume.</returns>
    public bool OnActivated(bool running)
    {
        if (!AutoPaused)
        {
            return false;
        }

        AutoPaused = false;
        return !running;
    }

    /// <summary>Clears the automatic-pause state after a manual start, stop, or pause.</summary>
    public void Clear()
    {
        AutoPaused = false;
    }
}
