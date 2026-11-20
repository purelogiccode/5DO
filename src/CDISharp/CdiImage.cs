using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CDISharp;

/// <summary>
/// Reads DiscJuggler <c>.cdi</c> optical disc images.
/// </summary>
/// <remarks>
/// A CDI file stores the raw sector stream first and appends a descriptor at the end.
/// The last four bytes give the descriptor length; the descriptor lists sessions and
/// tracks, each with its mode, sector layout, start address, and length. The reader
/// exposes contiguous logical sector addresses: track 1 starts at LBA 0 and each later
/// track follows the previous one. The 150-sector lead-in pregap is skipped when it is
/// present in the data area.
/// </remarks>
public sealed class CdiImage : IDisposable
{
    /// <summary>The 3DO logical user-data sector size.</summary>
    public const int UserDataSectorSize = 2048;

    /// <summary>The raw CD sector size.</summary>
    public const int RawSectorSize = 2352;

    /// <summary>The number of lead-in sectors skipped at the start of a CDI data area.</summary>
    public const int LeadInSectors = 150;

    private const int MaxSessions = 99;
    private const int MaxTracks = 99;
    private const int SessionBlockSize = 15;
    private const int MaxDescriptorBytes = 4 * 1024 * 1024;
    private const int MaxSectorBytes = 2449;
    private const int MaxCdTextBlocks = 4096;

    private readonly FileStream _stream;
    private readonly CdiTrack[] _tracks;
    private readonly CdiSession[] _sessions;
    private readonly byte[] _sector = new byte[MaxSectorBytes];
    private bool _disposed;

    private CdiImage(FileStream stream, string path)
    {
        _stream = stream;
        long fileLength = stream.Length;
        byte[] descriptor = ReadDescriptor(stream, fileLength, path);
        var rawTracks = ParseDescriptor(descriptor, path);
        _tracks = BuildTracks(rawTracks, fileLength - descriptor.Length, path);
        _sessions = BuildSessions(_tracks);
        TotalSectorCount = _tracks.Sum(static track => track.SectorCount);
    }

    /// <summary>Gets the tracks in disc order.</summary>
    public IReadOnlyList<CdiTrack> Tracks => _tracks;

    /// <summary>Gets the sessions in disc order.</summary>
    public IReadOnlyList<CdiSession> Sessions => _sessions;

    /// <summary>Gets the total number of logical sectors across every track.</summary>
    public int TotalSectorCount { get; }

    /// <summary>Opens a DiscJuggler image.</summary>
    /// <param name="path">Path to the <c>.cdi</c> file.</param>
    /// <returns>An open CDI image. The caller owns and must dispose it.</returns>
    /// <exception cref="InvalidDataException">The descriptor or track geometry is malformed.</exception>
    public static CdiImage Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            return new CdiImage(stream, path);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Checks whether a file has a valid DiscJuggler descriptor signature.</summary>
    /// <param name="path">Path to inspect.</param>
    /// <returns><see langword="true"/> when the trailing descriptor looks like a CDI file.</returns>
    public static bool HasSignature(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            long length = stream.Length;
            if (length < SessionBlockSize + 1 + 4)
            {
                return false;
            }

            Span<byte> lengthBytes = stackalloc byte[4];
            stream.Position = length - 4;
            stream.ReadExactly(lengthBytes);
            int descriptorLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if (descriptorLength <= 0 || descriptorLength > length || descriptorLength > MaxDescriptorBytes)
            {
                return false;
            }

            var descriptor = new byte[descriptorLength];
            stream.Position = length - descriptorLength;
            stream.ReadExactly(descriptor);

            return descriptor.Length >= SessionBlockSize + 1
                && descriptor[0] is > 0 and <= MaxSessions
                && descriptor[1 + 1] is > 0 and <= MaxTracks
                && LooksLikeSessionBlock(descriptor.AsSpan(1, SessionBlockSize));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Reads one 2048-byte user-data sector.</summary>
    /// <param name="lba">Contiguous logical sector address.</param>
    /// <param name="destination">Destination span of at least 2048 bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lba"/> is outside the image.</exception>
    /// <exception cref="InvalidOperationException">The address belongs to an audio track.</exception>
    public void ReadSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CdiTrack track = FindTrack(lba);
        if (track.IsAudio)
        {
            throw new InvalidOperationException($"Sector {lba} belongs to an audio track and has no 2048-byte user data.");
        }

        if (destination.Length < UserDataSectorSize)
        {
            throw new ArgumentException($"Destination must hold at least {UserDataSectorSize} bytes.", nameof(destination));
        }

        int available = ReadTrackSector(lba, _sector);
        int copy = Math.Min(UserDataSectorSize, available - track.PayloadOffset);
        if (copy < 0)
        {
            throw new InvalidDataException($"CDI track {track.Number} at LBA {lba} is too short for user data.");
        }

        _sector.AsSpan(track.PayloadOffset, copy).CopyTo(destination);
        if (copy < UserDataSectorSize)
        {
            destination[copy..UserDataSectorSize].Clear();
        }
    }

    /// <summary>Reads one native track sector.</summary>
    /// <param name="lba">Contiguous logical sector address.</param>
    /// <param name="destination">Destination span of at least the track's <see cref="CdiTrack.SectorSize"/> bytes.</param>
    /// <returns>The number of bytes written: 2048, 2336, or 2352.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lba"/> is outside the image.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too small.</exception>
    public int ReadTrackSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CdiTrack track = FindTrack(lba);
        if (destination.Length < track.SectorSize)
        {
            throw new ArgumentException(
                $"Destination must hold {track.SectorSize} bytes for track {track.Number}.",
                nameof(destination));
        }

        long offset = track.FileOffset + ((long)(lba - track.StartSector) * track.BytesPerSector);
        if (offset < 0 || offset + track.SectorSize > _stream.Length)
        {
            throw new InvalidDataException($"Sector {lba} is outside CDI track {track.Number}.");
        }

        _stream.Position = offset;
        Span<byte> target = destination[..track.SectorSize];
        _stream.ReadExactly(target);
        return track.SectorSize;
    }

    /// <summary>Disposes the image file handle.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }

    private static byte[] ReadDescriptor(FileStream stream, long fileLength, string path)
    {
        if (fileLength < 4)
        {
            throw new InvalidDataException($"CDI image '{path}' is too small to contain a descriptor.");
        }

        Span<byte> lengthBytes = stackalloc byte[4];
        stream.Position = fileLength - 4;
        stream.ReadExactly(lengthBytes);
        int descriptorLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (descriptorLength <= 0 || descriptorLength > fileLength || descriptorLength > MaxDescriptorBytes)
        {
            throw new InvalidDataException(
                $"CDI image '{path}' has an invalid descriptor length of {descriptorLength} bytes.");
        }

        var descriptor = new byte[descriptorLength];
        stream.Position = fileLength - descriptorLength;
        stream.ReadExactly(descriptor);
        return descriptor;
    }

    private static List<RawTrack> ParseDescriptor(byte[] descriptor, string path)
    {
        if (descriptor.Length < SessionBlockSize + 1)
        {
            throw new InvalidDataException($"CDI image '{path}' has a truncated descriptor.");
        }

        int maxSessions = descriptor[0];
        if (maxSessions is 0 or > MaxSessions)
        {
            throw new InvalidDataException($"CDI image '{path}' has an invalid session count of {maxSessions}.");
        }

        var tracks = new List<RawTrack>();
        int position = 1;
        int sessionNumber = 0;

        for (var session = 0; session < maxSessions; session++)
        {
            Require(descriptor, position, SessionBlockSize, "session header");
            if (!LooksLikeSessionBlock(descriptor.AsSpan(position, SessionBlockSize)))
            {
                if (!TryFindNextSession(descriptor, ref position))
                {
                    break;
                }
            }

            int trackCount = descriptor[position + 1];
            if (trackCount == 0)
            {
                break;
            }

            if (trackCount > MaxTracks)
            {
                throw new InvalidDataException(
                    $"CDI image '{path}' declares {trackCount} tracks in one session; the maximum is {MaxTracks}.");
            }

            sessionNumber++;
            position += SessionBlockSize;

            for (var track = 0; track < trackCount; track++)
            {
                RawTrack raw = ParseTrack(descriptor, ref position, sessionNumber, tracks.Count + 1, path);
                tracks.Add(raw);
            }
        }

        if (tracks.Count == 0)
        {
            throw new InvalidDataException($"CDI image '{path}' contains no tracks.");
        }

        return tracks;
    }

    private static RawTrack ParseTrack(
        byte[] descriptor,
        ref int position,
        int sessionNumber,
        int trackNumber,
        string path)
    {
        Require(descriptor, position, 16, "track header");
        position += 16;

        Require(descriptor, position, 1, "track filename length");
        int filenameLength = descriptor[position++];
        Require(descriptor, position, filenameLength, "track filename");
        position += filenameLength;

        Require(descriptor, position, 29, "track header");
        position += 29;

        Require(descriptor, position, 2, "track medium type");
        position += 2;

        Require(descriptor, position, 2, "track index count");
        int indexCount = BinaryPrimitives.ReadUInt16LittleEndian(descriptor.AsSpan(position, 2));
        position += 2;
        if (indexCount > 99)
        {
            throw new InvalidDataException($"CDI track {trackNumber} has an invalid index count of {indexCount}.");
        }

        Require(descriptor, position, indexCount * 4, "track index lengths");
        int pregapSectors = indexCount > 0
            ? BinaryPrimitives.ReadInt32LittleEndian(descriptor.AsSpan(position, 4))
            : 0;
        position += indexCount * 4;

        Require(descriptor, position, 4, "track CD-Text count");
        uint cdTextBlocks = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(position, 4));
        position += 4;
        if (cdTextBlocks > MaxCdTextBlocks)
        {
            throw new InvalidDataException(
                $"CDI track {trackNumber} has an invalid CD-Text block count of {cdTextBlocks}.");
        }

        string? title = null;
        for (uint block = 0; block < cdTextBlocks; block++)
        {
            for (var field = 0; field < 18; field++)
            {
                Require(descriptor, position, 1, "track CD-Text length");
                int textLength = descriptor[position++];
                if (textLength == 0)
                {
                    continue;
                }

                Require(descriptor, position, textLength, "track CD-Text");
                if (field == 10)
                {
                    title = Encoding.Latin1.GetString(descriptor, position, textLength);
                }

                position += textLength;
            }
        }

        Require(descriptor, position, 2, "track header");
        position += 2;

        Require(descriptor, position, 4, "track mode");
        uint trackModeValue = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(position, 4));
        position += 4;

        Require(descriptor, position, 4, "track header");
        position += 4;

        Require(descriptor, position, 4, "track session");
        position += 4;

        Require(descriptor, position, 4, "track number");
        position += 4;

        Require(descriptor, position, 4, "track start");
        uint sourceStart = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(position, 4));
        position += 4;

        Require(descriptor, position, 4, "track length");
        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(position, 4));
        position += 4;

        Require(descriptor, position, 16, "track header");
        position += 16;

        Require(descriptor, position, 4, "track read mode");
        uint readModeValue = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(position, 4));
        position += 4;

        Require(descriptor, position, 4, "track control flags");
        uint control = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(position, 4));
        position += 4;

        Require(descriptor, position, 9, "track header");
        position += 9;

        Require(descriptor, position, 12, "track ISRC");
        string isrc = ReadFixedString(descriptor.AsSpan(position, 12));
        position += 12;

        Require(descriptor, position, 4, "track ISRC valid flag");
        position += 4;

        Require(descriptor, position, 87, "track header");
        position += 87;

        Require(descriptor, position, 1, "track session type");
        position++;

        Require(descriptor, position, 5, "track header");
        position += 5;

        Require(descriptor, position, 1, "track follow flag");
        position++;

        Require(descriptor, position, 2, "track header");
        position += 2;

        Require(descriptor, position, 4, "track end address");
        position += 4;

        CdiTrackMode mode = trackModeValue switch
        {
            0 => CdiTrackMode.Audio,
            1 => CdiTrackMode.Mode1,
            2 => CdiTrackMode.Mode2,
            _ => throw new InvalidDataException(
                $"CDI track {trackNumber} in '{path}' has an unknown mode of {trackModeValue}."),
        };

        CdiReadMode readMode = readModeValue switch
        {
            0 => CdiReadMode.Mode1,
            1 => CdiReadMode.Mode2,
            2 => CdiReadMode.Raw,
            3 => CdiReadMode.RawPq,
            4 => CdiReadMode.RawPw,
            _ => throw new InvalidDataException(
                $"CDI track {trackNumber} in '{path}' has an unknown read mode of {readModeValue}."),
        };

        (int sectorSize, int bytesPerSector, int payloadOffset) = ResolveLayout(mode, readMode, trackNumber, path);

        if (sourceStart > int.MaxValue || declaredLength > int.MaxValue)
        {
            throw new InvalidDataException($"CDI track {trackNumber} in '{path}' is larger than 2 GiB.");
        }

        int startSector = (int)sourceStart;
        int sectorCount = (int)declaredLength;
        if (startSector == 0)
        {
            if (sectorCount <= LeadInSectors)
            {
                throw new InvalidDataException(
                    $"CDI track {trackNumber} in '{path}' is shorter than the {LeadInSectors}-sector lead-in.");
            }

            sectorCount -= LeadInSectors;
        }
        else if (startSector >= LeadInSectors)
        {
            startSector -= LeadInSectors;
        }
        else
        {
            startSector = 0;
        }

        return new RawTrack
        {
            Number = trackNumber,
            Session = sessionNumber,
            Mode = mode,
            ReadMode = readMode,
            SourceStartSector = startSector,
            SectorCount = sectorCount,
            SectorSize = sectorSize,
            BytesPerSector = bytesPerSector,
            PayloadOffset = payloadOffset,
            PregapSectors = Math.Max(0, pregapSectors),
            Title = title,
            Isrc = isrc,
            Control = (int)control,
        };
    }

    private static CdiTrack[] BuildTracks(List<RawTrack> rawTracks, long dataLength, string path)
    {
        if (dataLength < 0)
        {
            throw new InvalidDataException($"CDI image '{path}' has no data area.");
        }

        long requiredWithLeadIn = (long)LeadInSectors * rawTracks[0].BytesPerSector;
        long requiredWithoutLeadIn = 0;
        foreach (RawTrack track in rawTracks)
        {
            long bytes = (long)track.SectorCount * track.BytesPerSector;
            requiredWithLeadIn += bytes;
            requiredWithoutLeadIn += bytes;
        }

        bool includeLeadIn = dataLength >= requiredWithLeadIn;
        if (!includeLeadIn && dataLength < requiredWithoutLeadIn)
        {
            throw new InvalidDataException(
                $"CDI image '{path}' declares more track data than the file contains "
                + $"({requiredWithoutLeadIn:N0} bytes required, {dataLength:N0} available).");
        }

        var result = new CdiTrack[rawTracks.Count];
        long currentOffset = includeLeadIn ? (long)LeadInSectors * rawTracks[0].BytesPerSector : 0;
        var startSector = 0;
        for (var index = 0; index < rawTracks.Count; index++)
        {
            RawTrack raw = rawTracks[index];
            long length = (long)raw.SectorCount * raw.BytesPerSector;
            if (currentOffset + length > dataLength)
            {
                throw new InvalidDataException(
                    $"CDI track {raw.Number} in '{path}' extends past the image data area.");
            }

            result[index] = raw.ToTrack(startSector, currentOffset);
            currentOffset += length;
            startSector += raw.SectorCount;
        }

        return result;
    }

    private static CdiSession[] BuildSessions(CdiTrack[] tracks)
    {
        var sessions = new List<CdiSession>();
        var index = 0;
        while (index < tracks.Length)
        {
            int session = tracks[index].Session;
            int firstTrack = tracks[index].Number;
            var count = 0;
            while (index < tracks.Length && tracks[index].Session == session)
            {
                count++;
                index++;
            }

            sessions.Add(new CdiSession(session, firstTrack, count));
        }

        return [.. sessions];
    }

    private static (int SectorSize, int BytesPerSector, int PayloadOffset) ResolveLayout(
        CdiTrackMode mode,
        CdiReadMode readMode,
        int trackNumber,
        string path)
    {
        if (readMode == CdiReadMode.Mode1 && mode != CdiTrackMode.Mode1)
        {
            throw new InvalidDataException(
                $"CDI track {trackNumber} in '{path}' uses cooked Mode 1 sectors for a {mode} track.");
        }

        if (readMode == CdiReadMode.Mode2 && mode != CdiTrackMode.Mode2)
        {
            throw new InvalidDataException(
                $"CDI track {trackNumber} in '{path}' uses 2336-byte Mode 2 sectors for a {mode} track.");
        }

        if (mode == CdiTrackMode.Audio && readMode is not (CdiReadMode.Raw or CdiReadMode.RawPq or CdiReadMode.RawPw))
        {
            throw new InvalidDataException(
                $"CDI audio track {trackNumber} in '{path}' uses a data read mode.");
        }

        return readMode switch
        {
            CdiReadMode.Mode1 => (2048, 2048, 0),
            CdiReadMode.Mode2 => (2336, 2336, 8),
            CdiReadMode.Raw => (RawSectorSize, RawSectorSize, mode switch
            {
                CdiTrackMode.Audio => 0,
                CdiTrackMode.Mode1 => 16,
                _ => 24,
            }),
            CdiReadMode.RawPq => (RawSectorSize, RawSectorSize + 16, mode switch
            {
                CdiTrackMode.Audio => 0,
                CdiTrackMode.Mode1 => 16,
                _ => 24,
            }),
            CdiReadMode.RawPw => (RawSectorSize, RawSectorSize + 96, mode switch
            {
                CdiTrackMode.Audio => 0,
                CdiTrackMode.Mode1 => 16,
                _ => 24,
            }),
            _ => throw new InvalidDataException($"CDI track {trackNumber} in '{path}' has an unsupported layout."),
        };
    }

    private static bool LooksLikeSessionBlock(ReadOnlySpan<byte> block)
    {
        if (block.Length < SessionBlockSize)
        {
            return false;
        }

        return block[0] == 0
            && block[2] == 0
            && block[3] == 0
            && block[4] == 0
            && block[5] == 0
            && block[6] == 0
            && block[7] == 0
            && block[8] == 0
            && block[9] == 1
            && block[10] == 0
            && block[11] == 0
            && block[12] == 0
            && block[13] == 0xFF
            && block[14] == 0xFF;
    }

    private static bool TryFindNextSession(byte[] descriptor, ref int position)
    {
        while (position + SessionBlockSize <= descriptor.Length)
        {
            if (LooksLikeSessionBlock(descriptor.AsSpan(position, SessionBlockSize))
                && descriptor[position + 1] <= MaxTracks)
            {
                return true;
            }

            position++;
        }

        return false;
    }

    private static void Require(byte[] descriptor, int position, int count, string field)
    {
        if (count < 0 || position < 0 || position > descriptor.Length - count)
        {
            throw new InvalidDataException($"CDI descriptor is truncated while reading {field}.");
        }
    }

    private static string ReadFixedString(ReadOnlySpan<byte> bytes)
    {
        int length = bytes.IndexOf((byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return length == 0 ? string.Empty : Encoding.ASCII.GetString(bytes[..length]).Trim();
    }

    private CdiTrack FindTrack(int lba)
    {
        if (lba < 0 || lba >= TotalSectorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lba),
                lba,
                $"Sector {lba} is outside the CDI image ({TotalSectorCount} sectors).");
        }

        foreach (CdiTrack track in _tracks)
        {
            if (track.Contains(lba))
            {
                return track;
            }
        }

        throw new InvalidDataException($"Sector {lba} is not covered by any CDI track.");
    }

    private sealed class RawTrack
    {
        public required int Number { get; init; }

        public required int Session { get; init; }

        public required CdiTrackMode Mode { get; init; }

        public required CdiReadMode ReadMode { get; init; }

        public required int SourceStartSector { get; init; }

        public required int SectorCount { get; init; }

        public required int SectorSize { get; init; }

        public required int BytesPerSector { get; init; }

        public required int PayloadOffset { get; init; }

        public required int PregapSectors { get; init; }

        public required string? Title { get; init; }

        public required string? Isrc { get; init; }

        public required int Control { get; init; }

        public CdiTrack ToTrack(int startSector, long fileOffset) => new(
            Number,
            Session,
            Mode,
            ReadMode,
            startSector,
            SectorCount,
            SectorSize,
            BytesPerSector,
            PayloadOffset,
            fileOffset,
            SourceStartSector,
            PregapSectors,
            Title,
            string.IsNullOrEmpty(Isrc) ? null : Isrc,
            Control);
    }
}
