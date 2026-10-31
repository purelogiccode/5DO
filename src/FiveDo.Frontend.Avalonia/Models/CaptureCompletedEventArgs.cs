using System;

namespace FiveDo.Frontend.Avalonia.Controls;

/// <summary>Event data for <see cref="VideoSurface.CaptureCompleted"/>.</summary>
public sealed class CaptureCompletedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="CaptureCompletedEventArgs"/> class.</summary>
    /// <param name="pixels">Captured BGRA pixels.</param>
    /// <param name="width">Capture width in pixels.</param>
    /// <param name="height">Capture height in pixels.</param>
    public CaptureCompletedEventArgs(byte[] pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the captured BGRA pixels.</summary>
    public byte[] Pixels { get; }

    /// <summary>Gets the capture width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the capture height in pixels.</summary>
    public int Height { get; }
}
