using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CHDSharp.Encoder;
using CHDSharp.Encoder.Models;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// Opens a CUE sheet with one or more data/audio tracks and exposes the native sectors.
/// </summary>
/// <remarks>
/// The CUE parser comes from the CHDSharp library's MAME-compatible <c>CueParser</c>,
/// which resolves multi-file, INDEX 00/01, PREGAP, POSTGAP, WAVE, and multisession
/// layouts. This adapter maps the parsed track table onto the 3DO logical block
/// addressing model: LBA 0 is the first track's INDEX 01, and every following
/// track starts after the preceding track's frame count.
/// </remarks>
public sealed class CueDiscImage : ITrackedDiscSource
{
    /// <summary>Raw CD sector size.</summary>
    public const int RawSectorSize = 2352;

    private const int Mode1PayloadOffset = 16;
    private const int Mode2PayloadOffset = 24;
    private const int Mode2SubheaderSize = 8;

    private readonly List<CueTrack> _tracks;
    private readonly Dictionary<string, FileStream> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] _rawSector = new byte[RawSectorSize];
    private bool _disposed;

    private CueDiscImage(CdToc toc)
    {
        _tracks = BuildTracks(toc);
        if (_tracks.Count == 0)
        {
            throw new DiscImageException("CUE sheet has no usable tracks.");
        }

        if (!_tracks.Any(static track => !track.Track.IsAudio))
        {
            throw new DiscImageException("CUE sheet has no data track.");
        }

        Tracks = _tracks.Select(static track => track.Track).ToArray();
        TotalSectorCount = _tracks.Sum(static track => track.Track.SectorCount);
    }

    /// <summary>Gets the tracks in disc order.</summary>
    public IReadOnlyList<DiscTrack> Tracks { get; }

    /// <summary>Gets the total number of logical sectors across all tracks.</summary>
    public int TotalSectorCount { get; }

    /// <summary>Gets the number of logical sectors on the disc.</summary>
    public int SectorCount => TotalSectorCount;

    /// <summary>Gets the normalized user-data sector size (2048 bytes).</summary>
    public int SectorSize => IsoImage.SectorBytes;

    /// <summary>Opens a CUE sheet.</summary>
    /// <param name="cuePath">Path to the CUE file.</param>
    /// <returns>An open multi-track disc image.</returns>
    /// <exception cref="DiscImageException">The CUE sheet is malformed or has no data track.</exception>
    public static CueDiscImage Open(string cuePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cuePath);
        try
        {
            return new CueDiscImage(CueParser.Parse(cuePath));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            throw new DiscImageException($"CUE sheet '{cuePath}' is invalid: {exception.Message}", exception);
        }
    }

    /// <summary>Reads one 2048-byte user-data sector from a data track.</summary>
    /// <param name="lba">Logical block address.</param>
    /// <param name="destination">Destination span of at least 2048 bytes.</param>
    /// <exception cref="DiscImageException">
    /// The address is outside the disc, belongs to an audio track, or the source is too short.
    /// </exception>
    public void ReadSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CueTrack track = FindTrack(lba);
        if (track.Track.IsAudio)
        {
            throw new DiscImageException($"Sector {lba} belongs to an audio track and has no 2048-byte user data.");
        }

        int available = ReadTrackSector(lba, _rawSector);
        int payloadOffset = track.Track.PayloadOffset;
        int copy = Math.Min(IsoImage.SectorBytes, Math.Min(destination.Length, available - payloadOffset));
        if (copy < 0)
        {
            throw new DiscImageException($"CUE track {track.Track.Number} at LBA {lba} is too short.");
        }

        _rawSector.AsSpan(payloadOffset, copy).CopyTo(destination);
    }

    /// <summary>Reads one native track sector (2048, 2336, or 2352 bytes).</summary>
    /// <param name="lba">Logical block address.</param>
    /// <param name="destination">Destination span.</param>
    /// <returns>The number of bytes written.</returns>
    public int ReadTrackSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CueTrack track = FindTrack(lba);
        int sectorBytes = track.Track.SectorSize;
        if (destination.Length < sectorBytes)
        {
            throw new ArgumentException(
                $"Destination must hold {sectorBytes} bytes for track {track.Track.Number}.",
                nameof(destination));
        }

        FileStream stream = GetStream(track.Info.FileName);
        long offset = track.Info.FileOffset
            + ((long)(lba - track.Track.StartLba) * (sectorBytes + track.Info.SubSize));
        if (offset < 0 || offset + sectorBytes > stream.Length)
        {
            throw new DiscImageException($"Sector {lba} is outside CUE track {track.Track.Number}.");
        }

        stream.Position = offset;
        Span<byte> target = destination[..sectorBytes];
        stream.ReadExactly(target);
        return sectorBytes;
    }

    /// <summary>Disposes the underlying track files.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (FileStream stream in _files.Values)
        {
            stream.Dispose();
        }

        _files.Clear();
    }

    private static List<CueTrack> BuildTracks(CdToc toc)
    {
        var result = new List<CueTrack>(toc.Tracks.Count);
        var startLba = 0;
        foreach (CdTrack info in toc.Tracks)
        {
            if (info.Frames <= 0 || string.IsNullOrEmpty(info.FileName))
            {
                continue;
            }

            int sectorSize = info.TrackType == CdTrackType.Audio ? RawSectorSize : info.DataSize;
            if (sectorSize <= 0)
            {
                continue;
            }

            var discTrack = new DiscTrack(
                info.Number,
                info.TrackType == CdTrackType.Audio ? DiscTrackKind.Audio : DiscTrackKind.Data,
                startLba,
                info.Frames,
                sectorSize,
                GetPayloadOffset(info));
            result.Add(new CueTrack(discTrack, info));
            startLba += info.Frames;
        }

        return result;
    }

    private static int GetPayloadOffset(CdTrack info)
    {
        return info.TrackType switch
        {
            CdTrackType.Mode1Raw => Mode1PayloadOffset,
            CdTrackType.Mode2Raw or CdTrackType.Mode2FormMix => Mode2PayloadOffset,
            CdTrackType.Mode2 => Mode2SubheaderSize,
            _ => 0,
        };
    }

    private CueTrack FindTrack(int lba)
    {
        foreach (CueTrack track in _tracks)
        {
            if (track.Track.Contains(lba))
            {
                return track;
            }
        }

        throw new DiscImageException($"Sector {lba} is outside the CUE image ({TotalSectorCount} sectors).");
    }

    private FileStream GetStream(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new DiscImageException("CUE track has no source file.");
        }

        if (_files.TryGetValue(path, out FileStream? stream))
        {
            return stream;
        }

        stream = File.OpenRead(path);
        _files[path] = stream;
        return stream;
    }

    private sealed record CueTrack(DiscTrack Track, CdTrack Info);
}
