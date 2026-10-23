using System;

namespace ThreeDo.Core;

/// <summary>
/// Thrown when a save state is truncated, corrupt, or has an unsupported version.
/// </summary>
public sealed class InvalidStateException : EmulationException
{
    /// <summary>Initializes a new instance of the <see cref="InvalidStateException"/> class.</summary>
    public InvalidStateException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidStateException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public InvalidStateException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidStateException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public InvalidStateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
