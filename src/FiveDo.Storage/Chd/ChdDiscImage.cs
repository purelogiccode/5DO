using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CHDSharp;
using CHDSharp.Models;
using FiveDo.Core;

namespace FiveDo.Storage.Chd;

/// <summary>
/// A read-only 3DO disc image backed by a MAME CHD file through the pure C# CHDSharp library.
/// </summary>
/// <remarks>
/// 3DO software is distributed on CD-ROM. CHDSharp exposes CD CHDs as 2352-byte raw sectors
/// (plus optional subcode); this adapter exposes both the normalized 2048-byte user-data
/// stream and the native track sectors, including Red Book audio tracks.
/// <para>
/// Flat CHDs created from a cooked ISO with <c>chdman createdvd</c> (or
/// <c>createhd</c> with 2048-byte units) have no CD track table. Those are exposed as a
/// single 2048-byte data track, which is sufficient for data-only 3DO discs.
/// </para>
/// </remarks>
public sealed class ChdDiscImage : ITrackedDiscSource
{
    /// <summary>The 3DO user-data sector size.</summary>
    public const int UserDataSectorSize = 2048;

    private const int RawSectorSize = 2352;

    private readonly ChdFile _chd;
    private readonly byte[] _rawSector = new byte[RawSectorSize];
    private readonly bool _flat;
    private bool _disposed;

    private ChdDiscImage(ChdFile chd, IReadOnlyList<DiscTrack> tracks, bool flat)
    {
        _chd = chd;
        Tracks = tracks;
        _flat = flat;
        TotalSectorCount = tracks.Sum(static track => track.SectorCount);
    }

    /// <summary>Gets the tracks in disc order.</summary>
    public IReadOnlyList<DiscTrack> Tracks { get; }

    /// <summary>Gets the total number of logical sectors across all tracks.</summary>
    public int TotalSectorCount { get; }

    /// <summary>Gets the number of logical sectors across all tracks.</summary>
    public int SectorCount => TotalSectorCount;

    /// <summary>Gets the user-data sector size (2048 bytes).</summary>
    public int SectorSize => UserDataSectorSize;

    /// <summary>Opens a CHD disc image.</summary>
    /// <param name="path">Path to the CHD file.</param>
    /// <param name="parentPath">Optional parent CHD path for a differential child image.</param>
    /// <param name="readAheadHunks">
    /// Number of hunks to pre-decompress in the background. Zero disables read-ahead.
    /// </param>
    /// <returns>An open <see cref="ChdDiscImage"/>.</returns>
    /// <exception cref="InvalidDataException">
    /// The file is not a supported 3DO CHD image (CD track table or a flat 2048-byte-sector image).
    /// </exception>
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
                return OpenFlat(chd, path, readAheadHunks);
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
            if (dataTrack.Frames <= 0)
            {
                throw new InvalidDataException($"CHD data track has no sectors: '{path}'.");
            }

            var discTracks = new List<DiscTrack>(tracks.Count);
            int startLba = 0;
            foreach (ChdTrackInfo track in tracks)
            {
                if (track.Frames <= 0)
                {
                    continue;
                }

                bool audio = track.TrackType == ChdTrackType.Audio;
                int sectorSize = audio ? RawSectorSize : track.DataSize;
                var discTrack = new DiscTrack(
                    track.TrackNumber,
                    audio ? DiscTrackKind.Audio : DiscTrackKind.Data,
                    startLba,
                    track.Frames,
                    sectorSize,
                    GetPayloadOffset(track, dataOffset, sectorSize));
                discTracks.Add(discTrack);
                startLba += track.Frames;
            }

            if (readAheadHunks > 0)
            {
                chd.ConfigureReadAhead(readAheadHunks);
            }

            return new ChdDiscImage(chd, discTracks, flat: false);
        }
        catch
        {
            chd.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a flat CHD whose decompressed image is a cooked 2048-byte-sector ISO stream.
    /// </summary>
    private static ChdDiscImage OpenFlat(ChdFile chd, string path, int readAheadHunks)
    {
        if (chd.UnitBytes != UserDataSectorSize)
        {
            throw new InvalidDataException(
                $"CHD is not a CD image and its {chd.UnitBytes}-byte units are not 3DO 2048-byte sectors: '{path}'. "
                + "Re-create it with chdman createcd, or createdvd for a cooked 2048-byte ISO.");
        }

        if (chd.TotalBytes % UserDataSectorSize != 0)
        {
            throw new InvalidDataException(
                $"Flat CHD image '{path}' is not a whole number of 2048-byte 3DO sectors.");
        }

        int sectorCount = checked((int)(chd.TotalBytes / UserDataSectorSize));
        if (sectorCount < 2)
        {
            throw new InvalidDataException($"Flat CHD image '{path}' is too small to be a 3DO disc.");
        }

        if (readAheadHunks > 0)
        {
            chd.ConfigureReadAhead(readAheadHunks);
        }

        var track = new DiscTrack(1, DiscTrackKind.Data, 0, sectorCount, UserDataSectorSize);
        return new ChdDiscImage(chd, [track], flat: true);
    }

    /// <summary>Reads one 2048-byte user-data sector from a data track.</summary>
    /// <param name="lba">Logical block address, zero-based.</param>
    /// <param name="destination">Destination span of at least 2048 bytes.</param>
    public void ReadSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(lba);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(lba, TotalSectorCount);
        if (destination.Length < UserDataSectorSize)
        {
            throw new ArgumentException("Destination must hold one 2048-byte 3DO sector.", nameof(destination));
        }

        if (_flat)
        {
            ReadFlatSector(lba, destination);
            return;
        }

        DiscTrack? track = LocateTrack(lba);
        if (track is null || track.IsAudio)
        {
            throw new InvalidDataException($"LBA {lba} is not a data track sector.");
        }

        ReadRawSector(lba);
        _rawSector.AsSpan(track.PayloadOffset, UserDataSectorSize).CopyTo(destination);
    }

    /// <summary>Reads one native track sector (2048, 2336, or 2352 bytes).</summary>
    /// <param name="lba">Logical block address.</param>
    /// <param name="destination">Destination span.</param>
    /// <returns>The number of bytes written.</returns>
    public int ReadTrackSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(lba);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(lba, TotalSectorCount);
        DiscTrack track = LocateTrack(lba)
            ?? throw new InvalidDataException($"LBA {lba} is outside the CHD track table.");
        if (destination.Length < track.SectorSize)
        {
            throw new ArgumentException(
                $"Destination must hold {track.SectorSize} bytes for track {track.Number}.",
                nameof(destination));
        }

        if (_flat)
        {
            ReadFlatSector(lba, destination);
            return track.SectorSize;
        }

        ReadRawSector(lba);
        if (track.IsAudio)
        {
            // CHD stores Red Book audio byte-swapped (cdrom_file::track_info::swap).
            for (var i = 0; i < RawSectorSize; i += 2)
            {
                (_rawSector[i], _rawSector[i + 1]) = (_rawSector[i + 1], _rawSector[i]);
            }
        }

        if (track.SectorSize == RawSectorSize)
        {
            _rawSector.AsSpan(0, RawSectorSize).CopyTo(destination);
            return RawSectorSize;
        }

        int payloadOffset = track.SectorSize == UserDataSectorSize ? 0 : RawSectorSize - track.SectorSize;
        _rawSector.AsSpan(payloadOffset, track.SectorSize).CopyTo(destination);
        return track.SectorSize;
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

    private DiscTrack? LocateTrack(int lba)
    {
        foreach (DiscTrack track in Tracks)
        {
            if (track.Contains(lba))
            {
                return track;
            }
        }

        return null;
    }

    private void ReadRawSector(int lba)
    {
        ChdError error = _chd.ReadSector((uint)lba, _rawSector, CancellationToken.None);
        if (error != ChdError.Chderrnone)
        {
            throw new IOException($"CHD sector read failed ({error.GetMessage()}) at LBA {lba}.");
        }
    }

    private void ReadFlatSector(int lba, Span<byte> destination)
    {
        ChdError error = _chd.Read(
            (ulong)lba * UserDataSectorSize,
            _rawSector,
            0,
            UserDataSectorSize,
            CancellationToken.None);
        if (error != ChdError.Chderrnone)
        {
            throw new IOException($"CHD sector read failed ({error.GetMessage()}) at LBA {lba}.");
        }

        _rawSector.AsSpan(0, UserDataSectorSize).CopyTo(destination);
    }

    private static int GetPayloadOffset(ChdTrackInfo track, int rawPayloadOffset, int sectorSize)
    {
        if (track.TrackType == ChdTrackType.Audio)
        {
            return 0;
        }

        return sectorSize switch
        {
            RawSectorSize => rawPayloadOffset,
            UserDataSectorSize => 0,
            // Native 2336-byte Mode 2 sectors begin after the 16-byte sync/header.
            _ => Math.Max(0, rawPayloadOffset - 16),
        };
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
