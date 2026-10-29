using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace FiveDo.Tests;

/// <summary>Builds small synthetic 3DO Opera file-system images for storage tests.</summary>
internal static class SyntheticOperaDisc
{
    internal const int BlockSize = 2048;
    internal const uint RootBlock = 1;

    internal sealed record EntrySpec(
        string Name,
        bool IsDirectory,
        uint Lba,
        uint LengthBytes,
        uint Burst = 1,
        uint Gap = 0,
        uint Type = 0,
        uint[]? Copies = null);

    internal static byte[] Build(
        IReadOnlyList<EntrySpec> rootEntries,
        uint blockCount = 16,
        string label = "TESTDISC",
        IReadOnlyDictionary<uint, IReadOnlyList<EntrySpec>>? directories = null)
    {
        var image = new byte[blockCount * BlockSize];
        WriteVolumeHeader(image, blockCount, label);
        WriteDirectory(image, RootBlock, rootEntries);
        if (directories is not null)
        {
            foreach ((uint lba, IReadOnlyList<EntrySpec> entries) in directories)
            {
                WriteDirectory(image, lba, entries);
            }
        }

        return image;
    }

    internal static void WriteFile(byte[] image, uint lba, ReadOnlySpan<byte> data)
    {
        data.CopyTo(image.AsSpan((int)(lba * BlockSize)));
    }

    private static void WriteVolumeHeader(byte[] image, uint blockCount, string label)
    {
        var header = image.AsSpan(0, BlockSize);
        header[0] = 1;
        "ZZZZZ"u8.CopyTo(header[1..]);
        header[6] = 1;
        "Synthetic disc"u8.CopyTo(header[8..]);
        Encoding.ASCII.GetBytes(label).CopyTo(header[40..]);
        BinaryPrimitives.WriteUInt32BigEndian(header[72..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(header[76..], BlockSize);
        BinaryPrimitives.WriteUInt32BigEndian(header[80..], blockCount);
        BinaryPrimitives.WriteUInt32BigEndian(header[84..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(header[88..], 1);
        BinaryPrimitives.WriteUInt32BigEndian(header[92..], BlockSize);
        BinaryPrimitives.WriteUInt32BigEndian(header[96..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(header[100..], RootBlock);
    }

    private static void WriteDirectory(byte[] image, uint firstBlock, IReadOnlyList<EntrySpec> entries)
    {
        const int maxEntriesPerBlock = (BlockSize - 20) / 72;
        var blockCount = Math.Max(1, (entries.Count + maxEntriesPerBlock - 1) / maxEntriesPerBlock);
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var block = image.AsSpan((int)((firstBlock + (uint)blockIndex) * BlockSize), BlockSize);
            var start = blockIndex * maxEntriesPerBlock;
            var count = Math.Min(maxEntriesPerBlock, entries.Count - start);
            var used = 20;
            for (var i = 0; i < count; i++)
            {
                used += 72 + (4 * ((entries[start + i].Copies?.Length ?? 1) - 1));
            }

            BinaryPrimitives.WriteInt32BigEndian(block, blockIndex + 1 < blockCount ? blockIndex + 1 : -1);
            BinaryPrimitives.WriteInt32BigEndian(block[4..], blockIndex > 0 ? blockIndex - 1 : -1);
            BinaryPrimitives.WriteUInt32BigEndian(block[8..], 0);
            BinaryPrimitives.WriteUInt32BigEndian(block[12..], (uint)used);
            BinaryPrimitives.WriteUInt32BigEndian(block[16..], 20);

            var offset = 20;
            for (var i = 0; i < count; i++)
            {
                var lastInBlock = i == count - 1;
                var lastInDirectory = blockIndex == blockCount - 1 && lastInBlock;
                EntrySpec entry = entries[start + i];
                WriteEntry(block, offset, entry, lastInBlock, lastInDirectory);
                offset += 72 + (4 * ((entry.Copies?.Length ?? 1) - 1));
            }
        }
    }

    private static void WriteEntry(
        Span<byte> block,
        int offset,
        EntrySpec entry,
        bool lastInBlock,
        bool lastInDirectory)
    {
        var type = entry.Type != 0 ? entry.Type : entry.IsDirectory ? 7u : 2u;
        var flags = type;
        if (lastInDirectory)
        {
            flags |= 0xC0000000;
        }
        else if (lastInBlock)
        {
            flags |= 0x40000000;
        }

        var bytes = block[offset..];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, flags);
        BinaryPrimitives.WriteUInt32BigEndian(bytes[4..], 0);
        (entry.IsDirectory ? "*dir"u8 : "    "u8).CopyTo(bytes[8..]);
        BinaryPrimitives.WriteUInt32BigEndian(bytes[12..], BlockSize);
        BinaryPrimitives.WriteUInt32BigEndian(bytes[16..], entry.LengthBytes);
        BinaryPrimitives.WriteUInt32BigEndian(
            bytes[20..],
            (entry.LengthBytes + BlockSize - 1) / BlockSize);
        BinaryPrimitives.WriteUInt32BigEndian(bytes[24..], entry.Burst);
        BinaryPrimitives.WriteUInt32BigEndian(bytes[28..], entry.Gap);
        Encoding.ASCII.GetBytes(entry.Name).CopyTo(bytes[32..]);

        var copies = entry.Copies ?? [entry.Lba];
        BinaryPrimitives.WriteUInt32BigEndian(bytes[64..], (uint)(copies.Length - 1));
        for (var i = 0; i < copies.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes[(68 + (i * 4))..], copies[i]);
        }
    }
}
