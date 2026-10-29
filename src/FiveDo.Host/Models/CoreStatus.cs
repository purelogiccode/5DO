using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Video;

namespace FiveDo.Host;


/// <summary>Status snapshot published by <see cref="CoreRunner"/> at a low rate.</summary>
/// <param name="Frames">Number of frames executed since the runner started.</param>
/// <param name="ProgramCounter">Current ARM program counter.</param>
/// <param name="Cycles">ARM cycles consumed by the most recent frame.</param>
/// <param name="FramesPerSecond">Measured frame rate over the status interval.</param>
/// <param name="TargetFramesPerSecond">Audio-derived target frame rate, or 0 before measurement.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct CoreStatus(
    long Frames,
    uint ProgramCounter,
    int Cycles,
    double FramesPerSecond,
    double TargetFramesPerSecond);
