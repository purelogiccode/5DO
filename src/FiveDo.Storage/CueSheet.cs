using System;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// Opens a CUE sheet and returns a multi-track disc image.
/// </summary>
/// <remarks>
/// Use <see cref="CueDiscImage"/> directly when the track table or native audio sectors
/// are needed. This facade keeps the historical <see cref="CueSheet.Open"/> call site
/// returning <see cref="IDiscSource"/>.
/// </remarks>
public static class CueSheet
{
    /// <summary>Opens a CUE sheet.</summary>
    /// <param name="cuePath">Path to the CUE file.</param>
    /// <returns>A disc source over every track in the sheet.</returns>
    /// <exception cref="DiscImageException">Thrown when the sheet is malformed or has no data track.</exception>
    public static IDiscSource Open(string cuePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cuePath);
        return CueDiscImage.Open(cuePath);
    }
}
