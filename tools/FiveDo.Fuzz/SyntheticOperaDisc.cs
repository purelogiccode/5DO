using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace FiveDo.Fuzz;

/// <summary>Builds small valid Opera file-system images for deep parser fuzzing.</summary>
internal static class SyntheticOperaDisc
{
    private const int BlockSize = 2048;
    private const uint RootBlock = 1;

    /// <summary>Builds a valid image with a root file and a nested directory.</summary>
    /// <param name="blockCount">Image size in blocks.</param>
    /// <returns>The image bytes.</returns>
    internal static byte[] Build(int blockCount = 8)
    {
        var image = new byte[blockCount * BlockSize];
        WriteVolumeHeader(image, (uint)blockCount);
        WriteDirectory(
            image,
            RootBlock,
            [
                new Entry("HELLO.TXT", IsDirectory: false, Lba: 2, LengthBytes: 5),
                new Entry("DIR", IsDirectory: true, Lba: 3, LengthBytes: BlockSize),
            ]);
        WriteDirectory(
            image,
            3,
            [new Entry("NESTED.BIN", IsDirectory: false, Lba: 4, LengthBytes: 4)]);
        "hello"u8.CopyTo(image.AsSpan(2 * BlockSize));
        "nest"u8.CopyTo(image.AsSpan(4 * BlockSize));
        return image;
    }

    private static void WriteVolumeHeader(byte[] image, uint blockCount)
    {
        var header = image.AsSpan(0, BlockSize);
        header[0] = 1;
        "ZZZZZ"u8.CopyTo(header[1..]);
        header[6] = 1;
        "Fuzz disc"u8.CopyTo(header[8..]);
        "FUZZDISC"u8.CopyTo(header[40..]);
        BinaryPrimitives.WriteUInt32BigEndian(header[72..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(header[76..], BlockSize);
        BinaryPrimitives.WriteUInt32BigEndian(header[80..], blockCount);
        BinaryPrimitives.WriteUInt32BigEndian(header[84..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(header[88..], 1);
        BinaryPrimitives.WriteUInt32BigEndian(header[92..], BlockSize);
        BinaryPrimitives.WriteUInt32BigEndian(header[96..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(header[100..], RootBlock);
    }

    private static void WriteDirectory(byte[] image, uint firstBlock, IReadOnlyList<Entry> entries)
    {
        var block = image.AsSpan((int)(firstBlock * BlockSize), BlockSize);
        var used = 20 + (entries.Count * 72);

        BinaryPrimitives.WriteInt32BigEndian(block, -1);
        BinaryPrimitives.WriteInt32BigEndian(block[4..], -1);
        BinaryPrimitives.WriteUInt32BigEndian(block[8..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(block[12..], (uint)used);
        BinaryPrimitives.WriteUInt32BigEndian(block[16..], 20);

        var offset = 20;
        for (var index = 0; index < entries.Count; index++)
        {
            Entry entry = entries[index];
            var lastInBlock = index == entries.Count - 1;
            var flags = entry.IsDirectory ? 7u : 2u;
            flags |= lastInBlock ? 0xC0000000u : 0u;
            var bytes = block[offset..];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, flags);
            BinaryPrimitives.WriteUInt32BigEndian(bytes[4..], 0);
            (entry.IsDirectory ? "*dir"u8 : "    "u8).CopyTo(bytes[8..]);
            BinaryPrimitives.WriteUInt32BigEndian(bytes[12..], BlockSize);
            BinaryPrimitives.WriteUInt32BigEndian(bytes[16..], entry.LengthBytes);
            BinaryPrimitives.WriteUInt32BigEndian(
                bytes[20..],
                (entry.LengthBytes + BlockSize - 1) / BlockSize);
            BinaryPrimitives.WriteUInt32BigEndian(bytes[24..], 1);
            BinaryPrimitives.WriteUInt32BigEndian(bytes[28..], 0);
            Encoding.ASCII.GetBytes(entry.Name).CopyTo(bytes[32..]);
            BinaryPrimitives.WriteUInt32BigEndian(bytes[64..], 0);
            BinaryPrimitives.WriteUInt32BigEndian(bytes[68..], entry.Lba);
            offset += 72;
        }
    }

    private sealed record Entry(string Name, bool IsDirectory, uint Lba, uint LengthBytes);
}
