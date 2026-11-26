using System;

namespace FiveDo.Frontend.Avalonia.Controls;

/// <summary>Event data for <see cref="VulkanVideoSurface.Failed"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Read by the Avalonia failure handler.")]
public sealed class VulkanFailedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="VulkanFailedEventArgs"/> class.</summary>
    /// <param name="exception">The exception that describes the failure.</param>
    public VulkanFailedEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>Gets the exception that describes the failure.</summary>
    public Exception Exception { get; }
}
