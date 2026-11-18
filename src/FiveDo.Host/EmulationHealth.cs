using System;
using System.Globalization;

namespace FiveDo.Host;

/// <summary>Overall emulation-health level shown by the status-bar badge.</summary>
public enum EmulationHealthState
{
    /// <summary>No game is running.</summary>
    Idle,

    /// <summary>Frame pacing and audio are nominal.</summary>
    Good,

    /// <summary>A counter moved in the wrong direction but play is unaffected.</summary>
    Warning,

    /// <summary>Emulation is well below the target rate or audio is breaking up.</summary>
    Critical,
}

/// <summary>One emulation-health verdict for the status bar.</summary>
/// <param name="State">Health level.</param>
/// <param name="Label">Short badge label (<c>Idle</c>, <c>OK</c>, <c>Watch</c>, <c>Issue</c>).</param>
/// <param name="Details">One-sentence explanation for the tooltip.</param>
public readonly record struct EmulationHealth(EmulationHealthState State, string Label, string Details);

/// <summary>
/// Turns the existing core and audio counters into one health verdict.
/// </summary>
/// <remarks>
/// The badge is presentation-only: it reads counters that are already collected for the
/// FPS overlay and never changes emulation, input, or audio. Frame pacing compares the
/// measured frame rate with the audio-derived target rate; the audio part looks at the
/// increase in underrun/dropped-buffer counters since the previous status tick, so a
/// single historic underrun does not keep the badge amber forever.
/// </remarks>
public static class EmulationHealthEvaluator
{
    /// <summary>Below this fraction of the target rate the badge turns critical.</summary>
    public const double CriticalFrameRatio = 0.75;

    /// <summary>Below this fraction of the target rate the badge turns amber.</summary>
    public const double WarningFrameRatio = 0.95;

    /// <summary>Underruns in one status tick that count as critical.</summary>
    public const long CriticalUnderrunsPerTick = 3;

    /// <summary>Evaluates one status snapshot.</summary>
    /// <param name="running">Whether a game is running.</param>
    /// <param name="status">Latest core status snapshot.</param>
    /// <param name="audioUnderruns">Total OpenAL underrun buffers.</param>
    /// <param name="audioDropped">Total samples dropped because the ring was full.</param>
    /// <param name="previousAudioUnderruns">Underrun total at the previous tick.</param>
    /// <param name="previousAudioDropped">Dropped-sample total at the previous tick.</param>
    /// <returns>The health verdict.</returns>
    public static EmulationHealth Evaluate(
        bool running,
        CoreStatus status,
        long audioUnderruns,
        long audioDropped,
        long previousAudioUnderruns,
        long previousAudioDropped)
    {
        if (!running)
        {
            return new EmulationHealth(EmulationHealthState.Idle, "Idle", "No game is running or it is paused.");
        }

        long underruns = Math.Max(0, audioUnderruns - previousAudioUnderruns);
        long dropped = Math.Max(0, audioDropped - previousAudioDropped);
        double target = status.TargetFramesPerSecond > 1 ? status.TargetFramesPerSecond : 60;
        double fps = status.FramesPerSecond;
        var slow = fps > 0 && fps < target * WarningFrameRatio;
        var verySlow = fps > 0 && fps < target * CriticalFrameRatio;
        var presentedBehind = status.PresentedFramesPerSecond > 0
            && fps > 0
            && status.PresentedFramesPerSecond < fps - 0.5;

        if (underruns >= CriticalUnderrunsPerTick)
        {
            return new EmulationHealth(
                EmulationHealthState.Critical,
                "Issue",
                $"Audio is breaking up: {underruns} underrun buffers and {dropped} dropped samples in the last second.");
        }

        if (verySlow)
        {
            return new EmulationHealth(
                EmulationHealthState.Critical,
                "Issue",
                string.Create(CultureInfo.InvariantCulture,
                    $"Emulation is at {fps:F1} of {target:F0} fps (below {CriticalFrameRatio:P0} of target)."));
        }

        if (underruns > 0 || dropped > 0)
        {
            return new EmulationHealth(
                EmulationHealthState.Warning,
                "Watch",
                $"Audio skipped {underruns} buffer(s) and dropped {dropped} sample(s) in the last second.");
        }

        if (presentedBehind)
        {
            return new EmulationHealth(
                EmulationHealthState.Warning,
                "Watch",
                string.Create(CultureInfo.InvariantCulture,
                    $"The display shows {status.PresentedFramesPerSecond:F1} of {fps:F1} emulated fps."));
        }

        if (slow)
        {
            return new EmulationHealth(
                EmulationHealthState.Warning,
                "Watch",
                string.Create(CultureInfo.InvariantCulture,
                    $"Emulation is at {fps:F1} of {target:F0} fps."));
        }

        if (status.RunAheadAutoDisabled)
        {
            return new EmulationHealth(
                EmulationHealthState.Warning,
                "Watch",
                "Run-ahead disabled itself because frames overran the budget.");
        }

        return new EmulationHealth(
            EmulationHealthState.Good,
            "OK",
            string.Create(CultureInfo.InvariantCulture,
                $"Emulation is at {fps:F1} fps and audio is clean."));
    }
}
