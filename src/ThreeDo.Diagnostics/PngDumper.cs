using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using ThreeDo.Core;

namespace ThreeDo.Diagnostics;

/// <summary>
/// Writes frame buffers to PNG for headless diagnostics and screenshots.
/// </summary>
public static class PngDumper
{
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>Writes a BGRA/RGBA frame as an 8-bit RGB PNG.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="path">Destination path.</param>
    public static void Write(FrameBuffer frame, string path)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var raw = new byte[((frame.Width * 3) + 1) * frame.Height];
        var pixels = frame.PixelSpan;
        var destination = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            raw[destination++] = 0;
            var line = y * frame.Stride;
            for (var x = 0; x < frame.Width; x++)
            {
                var offset = line + (x * 4);
                var b = pixels[offset];
                var g = pixels[offset + 1];
                var r = pixels[offset + 2];
                raw[destination++] = frame.Format == PixelFormat.Bgra8888 ? r : b;
                raw[destination++] = g;
                raw[destination++] = frame.Format == PixelFormat.Bgra8888 ? b : r;
            }
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var output = File.Create(path);
        output.Write([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, frame.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), frame.Height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = 0xffffffffu;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        BinaryPrimitives.WriteUInt32BigEndian(length, crc ^ 0xffffffffu);
        output.Write(length);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
