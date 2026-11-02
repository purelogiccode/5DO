using System;

namespace FiveDo.Frontend.Avalonia.Controls;

/// <summary>Event data for <see cref="Direct3DVideoSurface.Failed"/>.</summary>
public sealed class Direct3D11FailedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="exception">Failure reported by the Direct3D surface.</param>
    public Direct3D11FailedEventArgs(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }

    /// <summary>Gets the failure.</summary>
    public Exception Exception { get; }
}
