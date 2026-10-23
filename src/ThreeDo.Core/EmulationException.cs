using System;

namespace ThreeDo.Core;

/// <summary>
/// Base exception for fatal emulator configuration and emulation errors.
/// </summary>
public class EmulationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="EmulationException"/> class.</summary>
    public EmulationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EmulationException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public EmulationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EmulationException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public EmulationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
