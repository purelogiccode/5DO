using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// The 3DO volume header in sector 0 of a disc image. FreeDO/4DO: <c>CoreVolumeHeader</c>.
/// </summary>
/// <remarks>
/// All multibyte fields are big-endian. <see cref="BlockCount"/> at byte offset 80 is the
/// authoritative disc size for the CD-ROM device and game database.
/// </remarks>
public readonly struct VolumeHeader
{
    /// <summary>Size of the volume header record.</summary>
    public const int Size = 132;

    /// <summary>Byte offset of the big-endian block count.</summary>
    public const int BlockCountOffset = 80;

    private VolumeHeader(
        byte recordType,
        byte flags,
        string comment,
        string label,
        uint id,
        uint blockSize,
        uint blockCount,
        uint rootDirectoryId,
        uint rootDirectoryBlocks,
        uint rootDirectoryBlockSize,
        uint lastRootDirectoryCopy)
    {
        RecordType = recordType;
        Flags = flags;
        Comment = comment;
        Label = label;
        Id = id;
        BlockSize = blockSize;
        BlockCount = blockCount;
        RootDirectoryId = rootDirectoryId;
        RootDirectoryBlocks = rootDirectoryBlocks;
        RootDirectoryBlockSize = rootDirectoryBlockSize;
        LastRootDirectoryCopy = lastRootDirectoryCopy;
    }

    /// <summary>Gets the record type (1 for a formatted volume).</summary>
    public byte RecordType { get; }

    /// <summary>Gets the volume flags.</summary>
    public byte Flags { get; }

    /// <summary>Gets the comment string.</summary>
    public string Comment { get; }

    /// <summary>Gets the volume label.</summary>
    public string Label { get; }

    /// <summary>Gets the volume identifier.</summary>
    public uint Id { get; }

    /// <summary>Gets the block size in bytes.</summary>
    public uint BlockSize { get; }

    /// <summary>Gets the number of blocks in the volume; this is the disc's sector count.</summary>
    public uint BlockCount { get; }

    /// <summary>Gets the root directory identifier.</summary>
    public uint RootDirectoryId { get; }

    /// <summary>Gets the root directory length in blocks.</summary>
    public uint RootDirectoryBlocks { get; }

    /// <summary>Gets the root directory block size.</summary>
    public uint RootDirectoryBlockSize { get; }

    /// <summary>Gets the last root directory copy index.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Part of the parsed on-disc volume header exposed to storage consumers.")]
    public uint LastRootDirectoryCopy { get; }

    /// <summary>Parses a volume header from the first 132 bytes of sector 0.</summary>
    /// <param name="sector">Sector 0 bytes.</param>
    /// <returns>The parsed header.</returns>
    /// <exception cref="ArgumentException">Thrown when the span is too small.</exception>
    public static VolumeHeader Parse(ReadOnlySpan<byte> sector)
    {
        if (sector.Length < Size)
        {
            throw new ArgumentException($"A volume header needs at least {Size} bytes.", nameof(sector));
        }

        return new VolumeHeader(
            sector[0],
            sector[7],
            ReadString(sector.Slice(8, 32)),
            ReadString(sector.Slice(40, 32)),
            BinaryPrimitives.ReadUInt32BigEndian(sector[72..]),
            BinaryPrimitives.ReadUInt32BigEndian(sector[76..]),
            BinaryPrimitives.ReadUInt32BigEndian(sector[80..]),
            BinaryPrimitives.ReadUInt32BigEndian(sector[84..]),
            BinaryPrimitives.ReadUInt32BigEndian(sector[88..]),
            BinaryPrimitives.ReadUInt32BigEndian(sector[92..]),
            BinaryPrimitives.ReadUInt32BigEndian(sector[96..]));
    }

    /// <summary>Reads the volume header from a disc image.</summary>
    /// <param name="disc">Disc source.</param>
    /// <returns>The parsed header, or <see langword="null"/> when the image has no sector 0.</returns>
    public static VolumeHeader? TryRead(IDiscSource disc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        if (disc.SectorCount == 0)
        {
            return null;
        }

        var sector = new byte[2048];
        disc.ReadSector(0, sector);
        return Parse(sector);
    }

    private static string ReadString(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        if (end < 0)
        {
            end = bytes.Length;
        }

        return Encoding.ASCII.GetString(bytes[..end]);
    }
}

/// <summary>
/// Computes the 3DO game identifier used by the 4DO game database.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "MD5 of the first two sectors is the 3DO game identifier used by 4DO.")]
public static class GameId
{
    /// <summary>Computes the lowercase MD5 of the first two disc sectors.</summary>
    /// <param name="disc">Disc source.</param>
    /// <returns>32 hexadecimal characters.</returns>
    public static string Compute(IDiscSource disc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        if (disc.SectorCount < 2)
        {
            throw new DiscImageException("A 3DO disc needs at least two sectors for a game ID.");
        }

        var data = new byte[4096];
        disc.ReadSector(0, data.AsSpan(0, 2048));
        disc.ReadSector(1, data.AsSpan(2048, 2048));
        return Convert.ToHexStringLower(MD5.HashData(data));
    }
}
