using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NRGSharp;

/// <summary>
/// Reads Nero Burning ROM <c>.nrg</c> optical disc images.
/// </summary>
/// <remarks>
/// NRG metadata lives in a chunk chain at the end of the file. Version 1 uses a
/// <c>NERO</c> footer with 32-bit offsets; version 2 uses a <c>NER5</c> footer with
/// 64-bit offsets. DAO chunks (<c>DAOI</c>/<c>DAOX</c>) describe disc-at-once tracks
/// with explicit byte offsets and sector sizes, including raw variants with 16 or 96
/// bytes of subchannel and the 2353/2449-byte layouts that append an error byte.
/// TAO chunks (<c>TINF</c>/<c>ETNF</c>/<c>ETN2</c>) are used as a fallback for older
/// images. The reader exposes contiguous logical sector addresses starting at LBA 0.
/// </remarks>
public sealed class NrgImage : IDisposable
{
    /// <summary>The 3DO logical user-data sector size.</summary>
    public const int UserDataSectorSize = 2048;

    /// <summary>The raw CD sector size.</summary>
    public const int RawSectorSize = 2352;

    private const uint NeroFooter = 0x4E45524F; // "NERO"
    private const uint Ner5Footer = 0x4E455235; // "NER5"
    private const uint DaoV1Chunk = 0x44414F49; // "DAOI"
    private const uint DaoV2Chunk = 0x44414F58; // "DAOX"
    private const uint TaoV0Chunk = 0x54494E46; // "TINF"
    private const uint TaoV1Chunk = 0x45544E46; // "ETNF"
    private const uint TaoV2Chunk = 0x45544E32; // "ETN2"
    private const uint SessionChunk = 0x53494E46; // "SINF"
    private const uint CueV1Chunk = 0x43554553; // "CUES"
    private const uint CueV2Chunk = 0x43554558; // "CUEX"
    private const uint CdTextChunk = 0x43445458; // "CDTX"
    private const uint MediaTypeChunk = 0x4D545950; // "MTYP"
    private const uint DiscInfoChunk = 0x44494E46; // "DINF"
    private const uint TocChunk = 0x544F4354; // "TOCT"
    private const uint RelocationChunk = 0x52454C4F; // "RELO"
    private const uint EndChunk = 0x454E4421; // "END!"

    private const int MaxMetadataBytes = 16 * 1024 * 1024;
    private const int MaxTracks = 99;
    private const int MaxSectorBytes = 2449;

    private readonly FileStream _stream;
    private readonly NrgTrack[] _tracks;
    private readonly byte[] _sector = new byte[MaxSectorBytes];
    private bool _disposed;

    private NrgImage(FileStream stream, string path)
    {
        _stream = stream;
        if (!TryReadFooter(stream, out NrgVersion version, out long firstChunkOffset, out int footerSize))
        {
            throw new InvalidDataException($"NRG image '{path}' has no NERO or NER5 footer.");
        }

        Version = version;
        FirstChunkOffset = firstChunkOffset;
        long footerStart = stream.Length - footerSize;
        long metadataLength = footerStart - firstChunkOffset;
        if (metadataLength <= 0 || metadataLength > MaxMetadataBytes)
        {
            throw new InvalidDataException(
                $"NRG image '{path}' has an invalid metadata length of {metadataLength} bytes.");
        }

        var metadata = new byte[metadataLength];
        stream.Position = firstChunkOffset;
        stream.ReadExactly(metadata);
        _tracks = ParseMetadata(metadata, firstChunkOffset, path, out IReadOnlyList<int> sessionTrackCounts);
        AssignSessions(_tracks, sessionTrackCounts);
        TotalSectorCount = _tracks.Sum(static track => track.SectorCount);
    }

    /// <summary>Gets the NRG footer version.</summary>
    public NrgVersion Version { get; }

    /// <summary>Gets the byte offset of the first metadata chunk.</summary>
    public long FirstChunkOffset { get; }

    /// <summary>Gets the tracks in disc order.</summary>
    public IReadOnlyList<NrgTrack> Tracks => _tracks;

    /// <summary>Gets the total number of logical sectors across every track.</summary>
    public int TotalSectorCount { get; }

    /// <summary>Opens a Nero NRG image.</summary>
    /// <param name="path">Path to the <c>.nrg</c> file.</param>
    /// <returns>An open NRG image. The caller owns and must dispose it.</returns>
    /// <exception cref="InvalidDataException">The footer, chunk chain, or track geometry is malformed.</exception>
    public static NrgImage Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            return new NrgImage(stream, path);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Checks whether a file has a plausible Nero NRG footer and metadata chain.</summary>
    /// <param name="path">Path to inspect.</param>
    /// <returns><see langword="true"/> when the file starts its metadata with a known NRG chunk.</returns>
    public static bool HasSignature(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!TryReadFooter(stream, out _, out long firstChunkOffset, out int footerSize))
            {
                return false;
            }

            long footerStart = stream.Length - footerSize;
            if (firstChunkOffset < 0 || firstChunkOffset + 8 > footerStart)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[8];
            stream.Position = firstChunkOffset;
            stream.ReadExactly(header);
            uint chunkId = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
            return IsKnownChunk(chunkId);
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
        NrgTrack track = FindTrack(lba);
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
            throw new InvalidDataException($"NRG track {track.Number} at LBA {lba} is too short for user data.");
        }

        _sector.AsSpan(track.PayloadOffset, copy).CopyTo(destination);
        if (copy < UserDataSectorSize)
        {
            destination[copy..UserDataSectorSize].Clear();
        }
    }

    /// <summary>Reads one native track sector.</summary>
    /// <param name="lba">Contiguous logical sector address.</param>
    /// <param name="destination">Destination span of at least the track's <see cref="NrgTrack.SectorSize"/> bytes.</param>
    /// <returns>The number of bytes written: 2048, 2324, 2336, or 2352.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lba"/> is outside the image.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too small.</exception>
    public int ReadTrackSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NrgTrack track = FindTrack(lba);
        if (destination.Length < track.SectorSize)
        {
            throw new ArgumentException(
                $"Destination must hold {track.SectorSize} bytes for track {track.Number}.",
                nameof(destination));
        }

        long offset = track.FileOffset + ((long)(lba - track.StartSector) * track.BytesPerSector);
        if (offset < 0 || offset + track.SectorSize > _stream.Length)
        {
            throw new InvalidDataException($"Sector {lba} is outside NRG track {track.Number}.");
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

    private static bool TryReadFooter(
        FileStream stream,
        out NrgVersion version,
        out long firstChunkOffset,
        out int footerSize)
    {
        version = NrgVersion.V1;
        firstChunkOffset = 0;
        footerSize = 0;
        long length = stream.Length;
        if (length < 8)
        {
            return false;
        }

        if (length >= 12)
        {
            Span<byte> footer = stackalloc byte[12];
            stream.Position = length - 12;
            stream.ReadExactly(footer);
            if (BinaryPrimitives.ReadUInt32BigEndian(footer[..4]) == Ner5Footer)
            {
                ulong offset = BinaryPrimitives.ReadUInt64BigEndian(footer[4..]);
                if (offset > long.MaxValue || (long)offset > length - 12)
                {
                    return false;
                }

                version = NrgVersion.V2;
                firstChunkOffset = (long)offset;
                footerSize = 12;
                return true;
            }

            if (BinaryPrimitives.ReadUInt32BigEndian(footer[4..8]) == NeroFooter)
            {
                uint offset = BinaryPrimitives.ReadUInt32BigEndian(footer[8..]);
                if (offset > length - 8)
                {
                    return false;
                }

                version = NrgVersion.V1;
                firstChunkOffset = offset;
                footerSize = 8;
                return true;
            }
        }

        Span<byte> shortFooter = stackalloc byte[8];
        stream.Position = length - 8;
        stream.ReadExactly(shortFooter);
        if (BinaryPrimitives.ReadUInt32BigEndian(shortFooter[..4]) != NeroFooter)
        {
            return false;
        }

        uint shortOffset = BinaryPrimitives.ReadUInt32BigEndian(shortFooter[4..]);
        if (shortOffset > length - 8)
        {
            return false;
        }

        version = NrgVersion.V1;
        firstChunkOffset = shortOffset;
        footerSize = 8;
        return true;
    }

    private static NrgTrack[] ParseMetadata(
        byte[] metadata,
        long dataEnd,
        string path,
        out IReadOnlyList<int> sessionTrackCounts)
    {
        var dao = new List<RawTrack>();
        var tao = new List<RawTrack>();
        var sessions = new List<int>();
        var position = 0;

        while (position + 8 <= metadata.Length)
        {
            uint chunkId = BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(position, 4));
            uint lengthValue = BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(position + 4, 4));
            position += 8;

            if (lengthValue > int.MaxValue)
            {
                throw new InvalidDataException($"NRG chunk at offset {position - 8} is too large.");
            }

            int chunkLength = (int)lengthValue;
            if (chunkLength > metadata.Length - position)
            {
                throw new InvalidDataException($"NRG chunk at offset {position - 8} is truncated.");
            }

            ReadOnlySpan<byte> payload = metadata.AsSpan(position, chunkLength);
            switch (chunkId)
            {
                case DaoV1Chunk:
                case DaoV2Chunk:
                    ParseDao(payload, chunkId == DaoV2Chunk, dataEnd, path, dao);
                    break;
                case TaoV0Chunk:
                case TaoV1Chunk:
                case TaoV2Chunk:
                    ParseTao(payload, chunkId, dataEnd, path, tao);
                    break;
                case SessionChunk:
                    if (chunkLength != 4)
                    {
                        throw new InvalidDataException("NRG SINF chunk has an invalid length.");
                    }

                    sessions.Add(checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload)));
                    break;
            }

            position += chunkLength;
            if (chunkId == EndChunk)
            {
                break;
            }
        }

        List<RawTrack> selected = dao.Count > 0 ? dao : tao;
        if (selected.Count == 0)
        {
            throw new InvalidDataException($"NRG image '{path}' contains no DAO or TAO tracks.");
        }

        if (selected.Count > MaxTracks)
        {
            throw new InvalidDataException($"NRG image '{path}' contains more than {MaxTracks} tracks.");
        }

        sessionTrackCounts = sessions;
        var tracks = new NrgTrack[selected.Count];
        var startSector = 0;
        for (var index = 0; index < selected.Count; index++)
        {
            RawTrack raw = selected[index];
            tracks[index] = raw.ToTrack(startSector);
            startSector += raw.SectorCount;
        }

        return tracks;
    }

    private static void ParseDao(
        ReadOnlySpan<byte> payload,
        bool version2,
        long dataEnd,
        string path,
        List<RawTrack> tracks)
    {
        const int commonLength = 22;
        int entrySize = version2 ? 42 : 30;
        if (payload.Length < commonLength + entrySize)
        {
            throw new InvalidDataException("NRG DAO chunk is truncated.");
        }

        int remaining = payload.Length - commonLength;
        if (remaining % entrySize != 0)
        {
            throw new InvalidDataException("NRG DAO chunk length is not a whole number of track entries.");
        }

        int count = remaining / entrySize;
        for (var index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(commonLength + (index * entrySize), entrySize);
            string isrc = ReadFixedString(entry[..12]);
            int sectorSize = BinaryPrimitives.ReadUInt16BigEndian(entry[12..14]);
            ushort storedMode = BinaryPrimitives.ReadUInt16LittleEndian(entry[14..16]);
            NrgTrackMode mode = NormalizeMode(storedMode, sectorSize, index + 1, path);
            (int nativeSectorSize, int payloadOffset) = ResolveLayout(mode, sectorSize, index + 1, path);

            ulong index0;
            ulong index1;
            ulong end;
            if (version2)
            {
                index0 = BinaryPrimitives.ReadUInt64BigEndian(entry[18..26]);
                index1 = BinaryPrimitives.ReadUInt64BigEndian(entry[26..34]);
                end = BinaryPrimitives.ReadUInt64BigEndian(entry[34..42]);
            }
            else
            {
                index0 = BinaryPrimitives.ReadUInt32BigEndian(entry[18..22]);
                index1 = BinaryPrimitives.ReadUInt32BigEndian(entry[22..26]);
                end = BinaryPrimitives.ReadUInt32BigEndian(entry[26..30]);
            }

            if (end > (ulong)dataEnd || index0 > end || index1 > end)
            {
                throw new InvalidDataException(
                    $"NRG track {index + 1} in '{path}' points outside the image data area.");
            }

            ulong start = index1 >= index0 && index1 < end ? index1 : index0;
            ulong byteLength = end - start;
            if (byteLength == 0 || byteLength % (ulong)sectorSize != 0)
            {
                throw new InvalidDataException(
                    $"NRG track {index + 1} in '{path}' does not contain whole {sectorSize}-byte sectors.");
            }

            ulong sectorCount = byteLength / (ulong)sectorSize;
            if (sectorCount > int.MaxValue || start > long.MaxValue)
            {
                throw new InvalidDataException($"NRG track {index + 1} in '{path}' is larger than 2 GiB.");
            }

            tracks.Add(new RawTrack
            {
                Number = tracks.Count + 1,
                Mode = mode,
                SectorCount = (int)sectorCount,
                SectorSize = nativeSectorSize,
                BytesPerSector = sectorSize,
                PayloadOffset = payloadOffset,
                FileOffset = (long)start,
                Isrc = isrc,
            });
        }
    }

    private static void ParseTao(
        ReadOnlySpan<byte> payload,
        uint chunkId,
        long dataEnd,
        string path,
        List<RawTrack> tracks)
    {
        int entrySize = chunkId switch
        {
            TaoV0Chunk => 12,
            TaoV1Chunk => 20,
            _ => 32,
        };

        if (payload.Length % entrySize != 0)
        {
            throw new InvalidDataException("NRG TAO chunk length is not a whole number of track entries.");
        }

        int count = payload.Length / entrySize;
        for (var index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(index * entrySize, entrySize);
            ulong offset;
            ulong length;
            uint storedMode;
            if (chunkId == TaoV2Chunk)
            {
                offset = BinaryPrimitives.ReadUInt64BigEndian(entry[..8]);
                length = BinaryPrimitives.ReadUInt64BigEndian(entry[8..16]);
                storedMode = BinaryPrimitives.ReadUInt32BigEndian(entry[16..20]);
            }
            else
            {
                offset = BinaryPrimitives.ReadUInt32BigEndian(entry[..4]);
                length = BinaryPrimitives.ReadUInt32BigEndian(entry[4..8]);
                storedMode = BinaryPrimitives.ReadUInt32BigEndian(entry[8..12]);
            }

            if (storedMode > ushort.MaxValue)
            {
                throw new InvalidDataException($"NRG track {index + 1} in '{path}' has an invalid mode.");
            }

            NrgTrackMode mode = NormalizeMode((ushort)storedMode, null, index + 1, path);
            int sectorSize = ModeSectorSize(mode);
            (int nativeSectorSize, int payloadOffset) = ResolveLayout(mode, sectorSize, index + 1, path);
            if (offset > (ulong)dataEnd || length > (ulong)dataEnd - offset || length % (ulong)sectorSize != 0)
            {
                throw new InvalidDataException(
                    $"NRG track {index + 1} in '{path}' points outside the image data area.");
            }

            ulong sectorCount = length / (ulong)sectorSize;
            if (sectorCount > int.MaxValue || offset > long.MaxValue)
            {
                throw new InvalidDataException($"NRG track {index + 1} in '{path}' is larger than 2 GiB.");
            }

            tracks.Add(new RawTrack
            {
                Number = tracks.Count + 1,
                Mode = mode,
                SectorCount = (int)sectorCount,
                SectorSize = nativeSectorSize,
                BytesPerSector = sectorSize,
                PayloadOffset = payloadOffset,
                FileOffset = (long)offset,
                Isrc = null,
            });
        }
    }

    private static NrgTrackMode NormalizeMode(ushort storedMode, int? sectorSize, int trackNumber, string path)
    {
        // Some MagicISO-produced NRG files store cooked mode values for 2352-byte
        // sectors. The sector size disambiguates them as raw Mode 1 or Mode 2.
        if (sectorSize == RawSectorSize)
        {
            storedMode = storedMode switch
            {
                (ushort)NrgTrackMode.Data => (ushort)NrgTrackMode.RawData,
                (ushort)NrgTrackMode.DataMode2Form1 or (ushort)NrgTrackMode.DataMode2Form2 =>
                    (ushort)NrgTrackMode.RawMode2,
                _ => storedMode,
            };
        }

        if (!Enum.IsDefined(typeof(NrgTrackMode), storedMode))
        {
            throw new InvalidDataException(
                $"NRG track {trackNumber} in '{path}' has an unknown mode of 0x{storedMode:X4}.");
        }

        return (NrgTrackMode)storedMode;
    }

    private static int ModeSectorSize(NrgTrackMode mode) => mode switch
    {
        NrgTrackMode.Data or NrgTrackMode.DataMode2Form1 => 2048,
        NrgTrackMode.DataMode2Form2 => 2336,
        NrgTrackMode.RawData or NrgTrackMode.RawMode2 or NrgTrackMode.Audio or NrgTrackMode.AudioAlt => RawSectorSize,
        NrgTrackMode.RawDataSubchannel or NrgTrackMode.AudioSubchannel or NrgTrackMode.RawMode2Subchannel =>
            RawSectorSize + 96,
        _ => RawSectorSize,
    };

    private static (int SectorSize, int PayloadOffset) ResolveLayout(
        NrgTrackMode mode,
        int bytesPerSector,
        int trackNumber,
        string path)
    {
        if (bytesPerSector is not (2048 or 2324 or 2336 or 2352 or 2353 or 2368 or 2448 or 2449))
        {
            throw new InvalidDataException(
                $"NRG track {trackNumber} in '{path}' uses an unsupported sector size of {bytesPerSector} bytes.");
        }

        return mode switch
        {
            NrgTrackMode.Audio or NrgTrackMode.AudioAlt or NrgTrackMode.AudioSubchannel =>
                RequireRawSector(bytesPerSector, trackNumber, path),
            NrgTrackMode.Data => bytesPerSector switch
            {
                2048 or 2324 or 2336 => (2048, 0),
                _ => (RawSectorSize, 16),
            },
            NrgTrackMode.DataMode2Form1 or NrgTrackMode.DataMode2Form2 => bytesPerSector switch
            {
                2048 => (2048, 0),
                2324 => (2324, 0),
                2336 => (2336, 8),
                _ => (RawSectorSize, 24),
            },
            NrgTrackMode.RawData or NrgTrackMode.RawDataSubchannel =>
                (RequireRawSector(bytesPerSector, trackNumber, path).SectorSize, 16),
            NrgTrackMode.RawMode2 or NrgTrackMode.RawMode2Subchannel =>
                (RequireRawSector(bytesPerSector, trackNumber, path).SectorSize, 24),
            _ => throw new InvalidDataException($"NRG track {trackNumber} in '{path}' has an unsupported mode."),
        };
    }

    private static (int SectorSize, int PayloadOffset) RequireRawSector(
        int bytesPerSector,
        int trackNumber,
        string path)
    {
        if (bytesPerSector < RawSectorSize)
        {
            throw new InvalidDataException(
                $"NRG track {trackNumber} in '{path}' stores a raw sector in only {bytesPerSector} bytes.");
        }

        return (RawSectorSize, 0);
    }

    private static void AssignSessions(NrgTrack[] tracks, IReadOnlyList<int> sessionTrackCounts)
    {
        if (sessionTrackCounts.Count == 0)
        {
            for (var index = 0; index < tracks.Length; index++)
            {
                tracks[index].Session = 1;
            }

            return;
        }

        var trackIndex = 0;
        for (var session = 0; session < sessionTrackCounts.Count && trackIndex < tracks.Length; session++)
        {
            int count = Math.Min(sessionTrackCounts[session], tracks.Length - trackIndex);
            for (var offset = 0; offset < count; offset++)
            {
                tracks[trackIndex + offset].Session = session + 1;
            }

            trackIndex += count;
        }

        while (trackIndex < tracks.Length)
        {
            tracks[trackIndex++].Session = sessionTrackCounts.Count;
        }
    }

    private static bool IsKnownChunk(uint chunkId) => chunkId is
        DaoV1Chunk or DaoV2Chunk or TaoV0Chunk or TaoV1Chunk or TaoV2Chunk or SessionChunk
        or CueV1Chunk or CueV2Chunk or CdTextChunk or MediaTypeChunk or DiscInfoChunk
        or TocChunk or RelocationChunk or EndChunk;

    private static string ReadFixedString(ReadOnlySpan<byte> bytes)
    {
        int length = bytes.IndexOf((byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return length == 0 ? string.Empty : Encoding.ASCII.GetString(bytes[..length]).Trim();
    }

    private NrgTrack FindTrack(int lba)
    {
        if (lba < 0 || lba >= TotalSectorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lba),
                lba,
                $"Sector {lba} is outside the NRG image ({TotalSectorCount} sectors).");
        }

        foreach (NrgTrack track in _tracks)
        {
            if (track.Contains(lba))
            {
                return track;
            }
        }

        throw new InvalidDataException($"Sector {lba} is not covered by any NRG track.");
    }

    private sealed class RawTrack
    {
        public required int Number { get; init; }

        public required NrgTrackMode Mode { get; init; }

        public required int SectorCount { get; init; }

        public required int SectorSize { get; init; }

        public required int BytesPerSector { get; init; }

        public required int PayloadOffset { get; init; }

        public required long FileOffset { get; init; }

        public required string? Isrc { get; init; }

        public NrgTrack ToTrack(int startSector) => new(
            Number,
            1,
            Mode,
            startSector,
            SectorCount,
            SectorSize,
            BytesPerSector,
            PayloadOffset,
            FileOffset,
            string.IsNullOrEmpty(Isrc) ? null : Isrc);
    }
}
