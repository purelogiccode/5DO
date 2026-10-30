using System;

namespace FiveDo.Host;


/// <summary>Describes a fault raised by the emulation core thread.</summary>
public sealed class CoreFaultedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="CoreFaultedEventArgs"/> class.</summary>
    /// <param name="exception">The exception that stopped the core thread.</param>
    public CoreFaultedEventArgs(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }

    /// <summary>Gets the exception that stopped the core thread.</summary>
    public Exception Exception { get; }
}
