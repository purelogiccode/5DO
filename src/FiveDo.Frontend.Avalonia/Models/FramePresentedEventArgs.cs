using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using FiveDo.Core;
using FiveDo.Video;
using FiveDo.Video.OpenGL;
using Silk.NET.OpenGL;

namespace FiveDo.Frontend.Avalonia.Controls;


/// <summary>Event data for <see cref="VideoSurface.FramePresented"/>.</summary>
public sealed class FramePresentedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="FramePresentedEventArgs"/> class.</summary>
    /// <param name="presentMilliseconds">CPU present time in milliseconds.</param>
    /// <param name="gpuMilliseconds">Latest GPU frame time, or <see langword="null"/> when unavailable.</param>
    public FramePresentedEventArgs(double presentMilliseconds, double? gpuMilliseconds)
    {
        PresentMilliseconds = presentMilliseconds;
        GpuMilliseconds = gpuMilliseconds;
    }

    /// <summary>Gets the CPU present time in milliseconds.</summary>
    public double PresentMilliseconds { get; }

    /// <summary>Gets the latest GPU frame time, or <see langword="null"/> when unavailable.</summary>
    public double? GpuMilliseconds { get; }
}
