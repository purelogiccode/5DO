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


/// <summary>Event data for <see cref="VideoSurface.OpenGlFailed"/>.</summary>
public sealed class OpenGlFailedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="OpenGlFailedEventArgs"/> class.</summary>
    /// <param name="exception">The exception that describes the failure.</param>
    public OpenGlFailedEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>Gets the exception that describes the failure.</summary>
    public Exception Exception { get; }
}
