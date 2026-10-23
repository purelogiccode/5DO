using System;

namespace ThreeDo.Storage;

/// <summary>
/// Thrown when a disc image cannot be opened or read.
/// </summary>
public sealed class DiscImageException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DiscImageException"/> class.</summary>
    /// <param name="message">Error message.</param>
    public DiscImageException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiscImageException"/> class.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Underlying exception.</param>
    public DiscImageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
