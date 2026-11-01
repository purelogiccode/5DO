using System.Runtime.InteropServices;
using FiveDo.Input;

namespace FiveDo.Host;


/// <summary>Status snapshot published by <see cref="CoreRunner"/> at a low rate.</summary>
/// <param name="Frames">Number of frames executed since the runner started.</param>
/// <param name="ProgramCounter">Current ARM program counter.</param>
/// <param name="Cycles">ARM cycles consumed by the most recent frame.</param>
/// <param name="FramesPerSecond">Measured emulated frame rate over the status interval.</param>
/// <param name="TargetFramesPerSecond">Audio-derived target frame rate, or 0 before measurement.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct CoreStatus(
    long Frames,
    uint ProgramCounter,
    int Cycles,
    double FramesPerSecond,
    double TargetFramesPerSecond)
{
    /// <summary>Gets the measured presented frame rate over the status interval.</summary>
    public double PresentedFramesPerSecond { get; init; }

    /// <summary>Gets the active frame-skip policy.</summary>
    public FrameSkipMode FrameSkipMode { get; init; }

    /// <summary>Gets the effective auto frame-skip cadence (1 = every frame).</summary>
    public int FrameSkipCadence { get; init; } = 1;

    /// <summary>Gets the player-1 controller state from the most recent frame.</summary>
    public ControllerState Player1 { get; init; }

    /// <summary>Gets the number of frames whose presentation was skipped.</summary>
    public long SkippedFrames { get; init; }

    /// <summary>Gets a value indicating whether the virtual disc tray is open.</summary>
    public bool TrayOpen { get; init; }

    /// <summary>Gets a value indicating whether the drive reports a disc.</summary>
    public bool DiscPresent { get; init; }
}
