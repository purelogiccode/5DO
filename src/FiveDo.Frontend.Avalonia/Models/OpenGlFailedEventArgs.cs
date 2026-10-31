using System;

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
