using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// A file or directory in the Opera file system used by 3DO discs.
/// </summary>
public sealed class OperaFileEntry
{
    internal OperaFileEntry(
        string path,
        string name,
        bool isDirectory,
        ulong size,
        uint lba,
        uint flags,
        uint blockSize,
        uint entryLengthBlocks,
        uint burst,
        uint gap,
        IReadOnlyList<uint> copies)
    {
        Path = path;
        Name = name;
        IsDirectory = isDirectory;
        Size = size;
        Lba = lba;
        Flags = flags;
        BlockSize = blockSize;
        EntryLengthBlocks = entryLengthBlocks;
        Burst = burst;
        Gap = gap;
        Copies = copies;
    }

    /// <summary>Gets the path using backslashes and a leading backslash.</summary>
    public string Path { get; }

    /// <summary>Gets the file or directory name.</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether this entry is a directory.</summary>
    public bool IsDirectory { get; }

    /// <summary>Gets the entry length in bytes.</summary>
    public ulong Size { get; }

    /// <summary>Gets the first data block (LBA) of the entry.</summary>
    public uint Lba { get; }

    /// <summary>Gets the raw Opera directory-entry flags.</summary>
    public uint Flags { get; }

    /// <summary>Gets the entry block size in bytes.</summary>
    public uint BlockSize { get; }

    /// <summary>Gets the entry length in blocks.</summary>
    public uint EntryLengthBlocks { get; }

    /// <summary>Gets the number of contiguous data blocks in each burst.</summary>
    public uint Burst { get; }

    /// <summary>Gets the number of blocks skipped between bursts.</summary>
    public uint Gap { get; }

    /// <summary>Gets every recorded copy of the entry; the first copy is the primary one.</summary>
    public IReadOnlyList<uint> Copies { get; }
}

/// <summary>
/// Reads the Opera file system from any <see cref="IDiscSource"/> (ISO, BIN, CUE, or CHD).
/// </summary>
/// <remarks>
/// The parser follows the on-disc volume and directory records described by
/// FreeDO/4DO's <c>CoreVolumeHeader</c>, <c>CoreDirectoryHeader</c>, and
/// <c>CoreDirectoryEntry</c>. Directory blocks are chained through the header's
/// relative <c>nextBlock</c> field, so directories larger than one 2048-byte block
/// are supported.
/// </remarks>
public sealed class OperaFileSystem
{
    private const int DirectoryHeaderSize = 20;
    private const int DirectoryEntrySize = 72;
    private const int MaxCopies = 64;
    private const int MaxDirectoryDepth = 64;
    private const int MaxEntries = 1_000_000;

    private const uint DirectoryEntryTypeMask = 0x0000000F;
    private const uint DirectoryEntryTypeFile = 0x00000002;
    private const uint DirectoryEntryTypeSpecial = 0x00000006;
    private const uint DirectoryEntryTypeFolder = 0x00000007;
    private const uint DirectoryEntryPositionLastInBlock = 0x40000000;
    private const uint DirectoryEntryPositionLastInDirectory = 0x80000000;

    private readonly DiscReader _reader;
    private readonly uint _blockSize;
    private readonly List<OperaFileEntry> _entries = [];
    private readonly HashSet<uint> _directoryBlocks = [];
    private readonly byte[] _entryBuffer = new byte[DirectoryEntrySize + (MaxCopies * 4)];

    private OperaFileSystem(IDiscSource disc, VolumeHeader header)
    {
        _reader = new DiscReader(disc);
        _blockSize = header.BlockSize;
        Label = header.Label;
        Comment = header.Comment;

        var rootBlock = header.FirstRootDirectoryCopy;
        _entries.Add(new OperaFileEntry(
            path: "\\",
            name: "\\",
            isDirectory: true,
            size: 0,
            lba: rootBlock,
            flags: 0,
            blockSize: _blockSize,
            entryLengthBlocks: header.RootDirectoryBlocks,
            burst: 1,
            gap: 0,
            copies: [rootBlock]));

        ParseDirectory(rootBlock, "\\", depth: 0);
        Entries = _entries;
    }

    /// <summary>Gets the volume label.</summary>
    public string Label { get; }

    /// <summary>Gets the volume comment.</summary>
    public string Comment { get; }

    /// <summary>Gets every parsed directory and file, including the root directory.</summary>
    public IReadOnlyList<OperaFileEntry> Entries { get; }

    /// <summary>Mounts and parses the Opera file system on a disc image.</summary>
    /// <param name="disc">An open 3DO disc source. The caller retains ownership.</param>
    /// <returns>The parsed file system.</returns>
    /// <exception cref="InvalidDataException">Thrown when the volume or directory records are malformed.</exception>
    public static OperaFileSystem Mount(IDiscSource disc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        if (disc.SectorCount == 0)
        {
            throw new InvalidDataException("The disc image has no sectors.");
        }

        var headerBytes = new byte[VolumeHeader.Size];
        new DiscReader(disc).ReadAt(0, headerBytes);
        var header = VolumeHeader.Parse(headerBytes);
        if (header.RecordType != 1)
        {
            throw new InvalidDataException(
                $"The disc does not contain a formatted 3DO volume (record type {header.RecordType}).");
        }

        if (header.BlockSize is < 512 or > 65536)
        {
            throw new InvalidDataException(
                $"The 3DO volume header has an invalid block size ({header.BlockSize}).");
        }

        if (header.RootDirectoryBlockSize != 0 && header.RootDirectoryBlockSize != header.BlockSize)
        {
            throw new InvalidDataException(
                $"The 3DO volume header has mismatched block sizes ({header.BlockSize} and {header.RootDirectoryBlockSize}).");
        }

        if (header.FirstRootDirectoryCopy == 0)
        {
            throw new InvalidDataException("The 3DO volume header has no root-directory copy.");
        }

        return new OperaFileSystem(disc, header);
    }

    /// <summary>Reads a file entry from the disc.</summary>
    /// <param name="entry">A file entry returned by <see cref="Entries"/>.</param>
    /// <param name="offset">Zero-based byte offset within the file.</param>
    /// <param name="destination">Destination buffer.</param>
    /// <returns>The number of bytes read, or zero at end of file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="entry"/> is a directory.</exception>
    public int ReadFile(OperaFileEntry entry, long offset, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (entry.IsDirectory)
        {
            throw new InvalidOperationException($"The Opera entry '{entry.Path}' is a directory.");
        }

        if (destination.Length == 0 || offset >= (long)entry.Size)
        {
            return 0;
        }

        var blockSize = entry.BlockSize != 0 ? entry.BlockSize : _blockSize;
        var burst = entry.Burst == 0 ? 1u : entry.Burst;
        var groupSize = (ulong)burst + entry.Gap;
        var remaining = Math.Min((long)destination.Length, (long)entry.Size - offset);
        var total = 0;
        while (remaining > 0)
        {
            var logicalBlock = (ulong)offset / blockSize;
            var inBlock = (int)((ulong)offset % blockSize);
            var group = logicalBlock / burst;
            var withinBurst = logicalBlock % burst;
            var physicalBlock = (ulong)entry.Lba + (group * groupSize) + withinBurst;
            if (physicalBlock > (ulong)long.MaxValue / blockSize)
            {
                throw new InvalidDataException($"The Opera entry '{entry.Path}' has an invalid data block.");
            }

            var physicalByte = (long)((physicalBlock * blockSize) + (uint)inBlock);
            var chunk = (int)Math.Min((ulong)remaining, blockSize - (uint)inBlock);
            _reader.ReadAt(physicalByte, destination.Slice(total, chunk));
            total += chunk;
            offset += chunk;
            remaining -= chunk;
        }

        return total;
    }

    private void ParseDirectory(uint firstBlock, string parentPath, int depth)
    {
        if (depth > MaxDirectoryDepth)
        {
            throw new InvalidDataException("The 3DO directory tree is deeper than the supported limit.");
        }

        if (!_directoryBlocks.Add(firstBlock))
        {
            throw new InvalidDataException($"The 3DO directory at LBA {firstBlock} is referenced more than once.");
        }

        var currentBlock = firstBlock;
        while (true)
        {
            if (!_directoryBlocks.Add(currentBlock) && currentBlock != firstBlock)
            {
                throw new InvalidDataException($"The 3DO directory at LBA {firstBlock} contains a block cycle.");
            }

            var blockByte = (long)currentBlock * _blockSize;
            var headerBytes = new byte[DirectoryHeaderSize];
            _reader.ReadAt(blockByte, headerBytes);
            var nextBlock = BinaryPrimitives.ReadInt32BigEndian(headerBytes);
            var unusedOffset = BinaryPrimitives.ReadUInt32BigEndian(headerBytes.AsSpan(12));
            var directoryOffset = BinaryPrimitives.ReadUInt32BigEndian(headerBytes.AsSpan(16));
            if (directoryOffset < DirectoryHeaderSize || directoryOffset >= _blockSize)
            {
                throw new InvalidDataException(
                    $"The 3DO directory at LBA {currentBlock} has an invalid entry offset ({directoryOffset}).");
            }

            var entriesEnd = unusedOffset > directoryOffset && unusedOffset <= _blockSize
                ? blockByte + unusedOffset
                : blockByte + _blockSize;
            var offset = blockByte + directoryOffset;
            var lastInBlock = false;
            while (offset + DirectoryEntrySize <= entriesEnd)
            {
                var lastInDirectory = ReadDirectoryEntry(
                    offset,
                    parentPath,
                    depth,
                    out lastInBlock,
                    out var entryBytes);
                if (offset + entryBytes > entriesEnd)
                {
                    throw new InvalidDataException(
                        $"The 3DO directory at LBA {currentBlock} has an entry past the end of its block.");
                }

                offset += entryBytes;
                if (lastInDirectory)
                {
                    return;
                }

                if (lastInBlock)
                {
                    break;
                }
            }

            if (!lastInBlock)
            {
                throw new InvalidDataException(
                    $"The 3DO directory at LBA {currentBlock} has no end-of-block marker.");
            }

            if (nextBlock <= 0)
            {
                throw new InvalidDataException(
                    $"The 3DO directory at LBA {currentBlock} has an invalid next block ({nextBlock}).");
            }

            var next = (long)firstBlock + nextBlock;
            if (next <= 0 || next > uint.MaxValue)
            {
                throw new InvalidDataException(
                    $"The 3DO directory at LBA {currentBlock} has an invalid next block ({nextBlock}).");
            }

            currentBlock = (uint)next;
        }
    }

    private bool ReadDirectoryEntry(
        long offset,
        string parentPath,
        int depth,
        out bool lastInBlock,
        out int entryBytes)
    {
        _reader.ReadAt(offset, _entryBuffer.AsSpan(0, DirectoryEntrySize));
        var flags = BinaryPrimitives.ReadUInt32BigEndian(_entryBuffer);
        var blockSize = BinaryPrimitives.ReadUInt32BigEndian(_entryBuffer.AsSpan(12));
        var entryLengthBytes = BinaryPrimitives.ReadUInt32BigEndian(_entryBuffer.AsSpan(16));
        var entryLengthBlocks = BinaryPrimitives.ReadUInt32BigEndian(_entryBuffer.AsSpan(20));
        var burst = BinaryPrimitives.ReadUInt32BigEndian(_entryBuffer.AsSpan(24));
        var gap = BinaryPrimitives.ReadUInt32BigEndian(_entryBuffer.AsSpan(28));
        var lastCopy = BinaryPrimitives.ReadUInt32BigEndian(_entryBuffer.AsSpan(64));
        if (lastCopy > MaxCopies)
        {
            throw new InvalidDataException($"The 3DO directory entry at byte {offset} has too many copies.");
        }

        entryBytes = DirectoryEntrySize + ((int)lastCopy * 4);
        var copies = new uint[lastCopy + 1];
        copies[0] = BinaryPrimitives.ReadUInt32BigEndian(_entryBuffer.AsSpan(68));
        if (lastCopy > 0)
        {
            _reader.ReadAt(offset + DirectoryEntrySize, _entryBuffer.AsSpan(DirectoryEntrySize, (int)lastCopy * 4));
            for (var i = 1; i <= lastCopy; i++)
            {
                copies[i] = BinaryPrimitives.ReadUInt32BigEndian(
                    _entryBuffer.AsSpan(DirectoryEntrySize + ((i - 1) * 4)));
            }
        }

        var name = ReadName(_entryBuffer.AsSpan(32, 32));
        if (name.Length == 0)
        {
            throw new InvalidDataException($"The 3DO directory entry at byte {offset} has no name.");
        }

        if (name.Contains('\\') || name.Contains('/') || name is "." or "..")
        {
            throw new InvalidDataException($"The 3DO directory entry at byte {offset} has an invalid name.");
        }

        lastInBlock = (flags & DirectoryEntryPositionLastInBlock) != 0;
        var lastInDirectory = (flags & DirectoryEntryPositionLastInDirectory) != 0;
        var type = flags & DirectoryEntryTypeMask;
        var path = string.Equals(parentPath, "\\", StringComparison.Ordinal)
            ? "\\" + name
            : parentPath + "\\" + name;
        if (type == DirectoryEntryTypeFolder)
        {
            if (copies[0] == 0)
            {
                throw new InvalidDataException($"The 3DO directory '{path}' has no data block.");
            }

            var size = entryLengthBytes != 0
                ? entryLengthBytes
                : (ulong)entryLengthBlocks * blockSize;
            _entries.Add(new OperaFileEntry(
                path,
                name,
                isDirectory: true,
                size,
                copies[0],
                flags,
                blockSize,
                entryLengthBlocks,
                burst,
                gap,
                copies));
            if (_entries.Count > MaxEntries)
            {
                throw new InvalidDataException("The 3DO directory tree contains too many entries.");
            }

            ParseDirectory(copies[0], path, depth + 1);
        }
        else if (type is DirectoryEntryTypeFile or DirectoryEntryTypeSpecial)
        {
            _entries.Add(new OperaFileEntry(
                path,
                name,
                isDirectory: false,
                entryLengthBytes,
                copies[0],
                flags,
                blockSize,
                entryLengthBlocks,
                burst,
                gap,
                copies));
            if (_entries.Count > MaxEntries)
            {
                throw new InvalidDataException("The 3DO directory tree contains too many entries.");
            }
        }

        return lastInDirectory;
    }

    private static string ReadName(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        if (end >= 0)
        {
            bytes = bytes[..end];
        }

        return Encoding.Latin1.GetString(bytes).TrimEnd();
    }

    private sealed class DiscReader
    {
        private readonly IDiscSource _disc;
        private readonly byte[] _sector;

        internal DiscReader(IDiscSource disc)
        {
            _disc = disc;
            _sector = new byte[disc.SectorSize];
        }

        internal void ReadAt(long byteOffset, Span<byte> destination)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
            while (destination.Length > 0)
            {
                var lba = byteOffset / _disc.SectorSize;
                if (lba < 0 || lba >= _disc.SectorCount)
                {
                    throw new InvalidDataException(
                        $"The disc read at byte {byteOffset} is outside the image.");
                }

                _disc.ReadSector((int)lba, _sector);
                var inSector = (int)(byteOffset % _disc.SectorSize);
                var chunk = Math.Min(destination.Length, _disc.SectorSize - inSector);
                _sector.AsSpan(inSector, chunk).CopyTo(destination);
                destination = destination[chunk..];
                byteOffset += chunk;
            }
        }
    }
}
