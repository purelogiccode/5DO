using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CDISharp;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// Adapts a DiscJuggler <c>.cdi</c> image to the 3DO <see cref="ITrackedDiscSource"/> contract.
/// </summary>
/// <remarks>
/// <see cref="CdiImage"/> exposes native 2048/2336/2352-byte sectors. This adapter maps
/// them to the same 2048-byte user-data sectors and track table used by the ISO, BIN,
/// CUE, and CHD readers, so game IDs, TOC metadata, save states, and the CD-ROM device
/// all see an equivalent disc.
/// </remarks>
public sealed class CdiDiscImage : ITrackedDiscSource
{
    private readonly CdiImage _image;
    private readonly DiscTrack[] _tracks;

    private CdiDiscImage(CdiImage image)
    {
        _image = image;
        _tracks = image.Tracks
            .Select(static track => new DiscTrack(
                track.Number,
                track.IsAudio ? DiscTrackKind.Audio : DiscTrackKind.Data,
                track.StartSector,
                track.SectorCount,
                track.SectorSize,
                track.PayloadOffset))
            .ToArray();
    }

    /// <summary>Gets the tracks in disc order.</summary>
    public IReadOnlyList<DiscTrack> Tracks => _tracks;

    /// <summary>Gets the total number of logical sectors across every track.</summary>
    public int TotalSectorCount => _image.TotalSectorCount;

    /// <summary>Gets the number of logical sectors on the disc.</summary>
    public int SectorCount => _image.TotalSectorCount;

    /// <summary>Gets the normalized user-data sector size (2048 bytes).</summary>
    public int SectorSize => CdiImage.UserDataSectorSize;

    /// <summary>Opens a DiscJuggler image.</summary>
    /// <param name="path">Path to the <c>.cdi</c> file.</param>
    /// <returns>An open disc source. The caller owns and must dispose it.</returns>
    /// <exception cref="DiscImageException">The image is malformed or has no data track.</exception>
    public static CdiDiscImage Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            CdiImage image = CdiImage.Open(path);
            if (image.Tracks.All(static track => track.IsAudio))
            {
                image.Dispose();
                throw new DiscImageException($"CDI image '{path}' has no data track.");
            }

            return new CdiDiscImage(image);
        }
        catch (InvalidDataException exception)
        {
            throw new DiscImageException($"CDI image '{path}' is invalid: {exception.Message}", exception);
        }
    }

    /// <summary>Reads one 2048-byte user-data sector.</summary>
    /// <param name="lba">Logical block address, zero-based.</param>
    /// <param name="destination">Destination span of at least 2048 bytes.</param>
    public void ReadSector(int lba, Span<byte> destination)
    {
        try
        {
            _image.ReadSector(lba, destination);
        }
        catch (InvalidOperationException exception)
        {
            throw new DiscImageException(exception.Message, exception);
        }
    }

    /// <summary>Reads one native track sector (2048, 2336, or 2352 bytes).</summary>
    /// <param name="lba">Logical block address, zero-based.</param>
    /// <param name="destination">Destination span.</param>
    /// <returns>The number of bytes written.</returns>
    public int ReadTrackSector(int lba, Span<byte> destination) => _image.ReadTrackSector(lba, destination);

    /// <summary>Disposes the underlying image file handle.</summary>
    public void Dispose() => _image.Dispose();
}
