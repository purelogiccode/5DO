using System;

namespace FiveDo.Host;

/// <summary>
/// Presentation frame-skip policy for the dedicated core loop.
/// </summary>
/// <remarks>
/// Frame skipping only suppresses VDLP scanline rendering and host presentation for a
/// frame; the ARM CPU, CEL engine, DSP, timers, and CD-ROM still run every frame. Game
/// speed, audio, and save states therefore remain identical to a full-rate run.
/// </remarks>
public enum FrameSkipMode
{
    /// <summary>Present every emulated frame.</summary>
    Off = 0,

    /// <summary>
    /// Present every frame until the core loop falls behind its audio-derived deadline,
    /// then skip presentation frames until it catches up.
    /// </summary>
    Auto = 1,

    /// <summary>Present one of every two emulated frames (30 fps at a 60 Hz field rate).</summary>
    OneInTwo = 2,

    /// <summary>Present one of every three emulated frames (20 fps at a 60 Hz field rate).</summary>
    OneInThree = 3,

    /// <summary>Present one of every four emulated frames (15 fps at a 60 Hz field rate).</summary>
    OneInFour = 4,

    /// <summary>Present one of every five emulated frames (12 fps at a 60 Hz field rate).</summary>
    OneInFive = 5,
}

/// <summary>Pure helpers for the frame-skip policy.</summary>
public static class FrameSkipPolicy
{
    /// <summary>Gets the number of emulated frames per presented frame.</summary>
    /// <param name="mode">Frame-skip policy.</param>
    /// <returns>1 for <see cref="FrameSkipMode.Off"/> and <see cref="FrameSkipMode.Auto"/>; 2-5 for fixed cadences.</returns>
    public static int Cadence(FrameSkipMode mode)
    {
        return mode switch
        {
            FrameSkipMode.OneInTwo => 2,
            FrameSkipMode.OneInThree => 3,
            FrameSkipMode.OneInFour => 4,
            FrameSkipMode.OneInFive => 5,
            _ => 1,
        };
    }

    /// <summary>Gets a value indicating whether a frame should be presented.</summary>
    /// <param name="mode">Frame-skip policy.</param>
    /// <param name="frameIndex">Monotonic emulated-frame counter.</param>
    /// <param name="autoCadence">Auto-mode cadence chosen by the runner, clamped to 1-5.</param>
    /// <returns><see langword="true"/> when the frame should be rendered and presented.</returns>
    public static bool ShouldPresent(FrameSkipMode mode, long frameIndex, int autoCadence = 1)
    {
        var cadence = mode == FrameSkipMode.Auto
            ? Math.Clamp(autoCadence, 1, 5)
            : Cadence(mode);
        return cadence <= 1 || (frameIndex % cadence) == 0;
    }

    /// <summary>Gets a short display label for the mode.</summary>
    /// <param name="mode">Frame-skip policy.</param>
    /// <param name="autoCadence">Auto-mode cadence chosen by the runner.</param>
    /// <returns>A label such as <c>off</c>, <c>auto x2</c>, or <c>1 of 3</c>.</returns>
    public static string Describe(FrameSkipMode mode, int autoCadence = 1)
    {
        return mode switch
        {
            FrameSkipMode.Auto => autoCadence <= 1 ? "auto" : $"auto x{Math.Clamp(autoCadence, 1, 5)}",
            FrameSkipMode.Off => "off",
            _ => $"1 of {Cadence(mode)}"
        };
    }
}
