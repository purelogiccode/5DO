using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Fuzz;

/// <summary>Classifies expected parser failures and unexpected defects.</summary>
internal static class FuzzFailureClassifier
{
    /// <summary>
    /// Gets a value indicating whether an exception is a documented, typed rejection of
    /// malformed input rather than an unexpected defect.
    /// </summary>
    /// <param name="exception">Exception raised by the target.</param>
    /// <returns><see langword="true"/> for typed input rejections.</returns>
    internal static bool IsTypedFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is InvalidStateException
            or DiscImageException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or FormatException
            or System.Security.SecurityException;
    }
}
