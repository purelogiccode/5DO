using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using FiveDo.Core;

namespace FiveDo.Tests.TestUtilities;

/// <summary>
/// Writes a <see cref="FrameBuffer"/> to a PNG file for manual and golden-image inspection.
/// </summary>
/// <remarks>
/// Test-only helper. The production frame dumper arrives with the M8 diagnostics work; this
/// keeps the M4.15 BIOS validation self-contained and free of image-library dependencies.
/// </remarks>
internal static class PngWriter
{
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>Writes a BGRA/RGBA frame buffer as an 8-bit RGB PNG.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="path">Destination file path.</param>
    public static void Write(FrameBuffer frame, string path)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytesPerPixel = 4;
        var raw = new byte[((frame.Width * 3) + 1) * frame.Height];
        var pixels = frame.PixelSpan;
        var destination = 0;

        for (var y = 0; y < frame.Height; y++)
        {
            raw[destination++] = 0;
            var line = y * frame.Stride;
            for (var x = 0; x < frame.Width; x++)
            {
                var offset = line + (x * bytesPerPixel);
                var b = pixels[offset];
                var g = pixels[offset + 1];
                var r = pixels[offset + 2];

                raw[destination++] = frame.Format == PixelFormat.Bgra8888 ? r : b;
                raw[destination++] = g;
                raw[destination++] = frame.Format == PixelFormat.Bgra8888 ? b : r;
            }
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
