using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CHDSharp;
using CHDSharp.Models;
using ThreeDo.Core;

namespace ThreeDo.Storage.Chd;

/// <summary>
/// A read-only 3DO disc image backed by a MAME CHD file through the pure C# CHDSharp library.
/// </summary>
/// <remarks>
/// 3DO software is distributed on CD-ROM. CHDSharp exposes CD CHDs as 2352-byte raw sectors
/// (plus optional subcode); this adapter extracts the 2048-byte user-data payload according
/// to the first data track's type and data size. It does not expose audio tracks.
/// </remarks>
public sealed class ChdDiscImage : IDiscSource
{
    /// <summary>The 3DO user-data sector size.</summary>
    public const int UserDataSectorSize = 2048;

    private const int RawSectorSize = 2352;

    private readonly ChdFile _chd;
    private readonly byte[] _rawSector = new byte[RawSectorSize];
    private readonly int _dataOffset;
    private bool _disposed;

    private ChdDiscImage(ChdFile chd, int sectorCount, int dataOffset)
    {
        _chd = chd;
        SectorCount = sectorCount;
        _dataOffset = dataOffset;
    }

    /// <summary>Gets the number of 2048-byte user-data sectors.</summary>
    public int SectorCount { get; }

    /// <summary>Gets the user-data sector size (2048 bytes).</summary>
    public int SectorSize => UserDataSectorSize;

    /// <summary>Opens a CHD disc image.</summary>
    /// <param name="path">Path to the CHD file.</param>
    /// <param name="parentPath">Optional parent CHD path for a differential child image.</param>
    /// <param name="readAheadHunks">
    /// Number of hunks to pre-decompress in the background. Zero disables read-ahead.
    /// </param>
    /// <returns>An open <see cref="ChdDiscImage"/>.</returns>
    /// <exception cref="InvalidDataException">The file is not a supported CD CHD image.</exception>
    public static ChdDiscImage Open(string path, string? parentPath = null, int readAheadHunks = 4)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfNegative(readAheadHunks);

        ChdFile? chd;
        ChdError error;
        if (parentPath is null)
        {
            error = ChdFile.Open(path, out chd);
        }
        else
        {
            error = ChdFile.Open(path, parentPath, out chd);
        }

        if (error != ChdError.Chderrnone || chd is null)
        {
            throw new InvalidDataException($"Not a readable CHD image ({error.GetMessage()}): '{path}'.");
        }

        try
        {
            if (!chd.IsCd)
            {
                throw new InvalidDataException($"CHD is not a CD image (DVD/HDD/raw CHDs are not 3DO discs): '{path}'.");
            }

            IReadOnlyList<ChdTrackInfo>? tracks = chd.Tracks;
            if (tracks is null || tracks.Count == 0)
            {
                throw new InvalidDataException($"CHD has no track table: '{path}'.");
            }

            ChdTrackInfo? dataTrack = tracks.FirstOrDefault(track => track.TrackType != ChdTrackType.Audio);
            if (dataTrack is null)
            {
                throw new InvalidDataException($"CHD has no data track: '{path}'.");
            }

            int dataOffset = GetUserDataOffset(dataTrack);
            int sectorCount = dataTrack.Frames;
            if (sectorCount <= 0)
            {
                throw new InvalidDataException($"CHD data track has no sectors: '{path}'.");
            }

            if (readAheadHunks > 0)
            {
                chd.ConfigureReadAhead(readAheadHunks);
            }

            return new ChdDiscImage(chd, sectorCount, dataOffset);
        }
        catch
        {
            chd.Dispose();
            throw;
        }
    }

    /// <summary>Reads one 2048-byte user-data sector.</summary>
    /// <param name="lba">Logical block address, zero-based.</param>
    /// <param name="destination">Destination span of at least 2048 bytes.</param>
    public void ReadSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(lba);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(lba, SectorCount);
        if (destination.Length < UserDataSectorSize)
        {
            throw new ArgumentException("Destination must hold one 2048-byte 3DO sector.", nameof(destination));
        }

        ChdError error = _chd.ReadSector((uint)lba, _rawSector, CancellationToken.None);
        if (error != ChdError.Chderrnone)
        {
            throw new IOException($"CHD sector read failed ({error.GetMessage()}) at LBA {lba}.");
        }

        _rawSector.AsSpan(_dataOffset, UserDataSectorSize).CopyTo(destination);
    }

    /// <summary>Disposes the underlying CHD reader.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _chd.Dispose();
            _disposed = true;
        }
    }

    private static int GetUserDataOffset(ChdTrackInfo track)
    {
        // CHDSharp's ReadSector returns the first 2352 bytes of the 2448-byte frame.
        // Cooked tracks store their user data at offset 0; raw Mode 1 sectors carry a
        // 16-byte sync/header; raw Mode 2 Form 1 sectors carry a 24-byte sync/header/subheader.
        if (track.DataSize == UserDataSectorSize)
        {
            return 0;
        }

        if (track.DataSize != RawSectorSize)
        {
            throw new InvalidDataException(
                $"Unsupported CHD track data size {track.DataSize} ({track.GetTypeString()}); 3DO expects Mode 1 data.");
        }

        return track.TrackType switch
        {
            ChdTrackType.Mode1 or ChdTrackType.Mode1Raw => 16,
            ChdTrackType.Mode2 or ChdTrackType.Mode2Raw or ChdTrackType.Mode2Form1 or ChdTrackType.Mode2FormMix => 24,
            _ => throw new InvalidDataException(
                $"Unsupported CHD track type {track.TrackType} ({track.GetTypeString()}); 3DO expects Mode 1 data."),
        };
    }
}
