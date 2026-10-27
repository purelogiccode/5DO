using System;
using System.Buffers.Binary;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Diagnostics;

/// <summary>
/// Writes frame buffers to uncompressed BMP files for headless diagnostics.
/// </summary>
public static class FrameDumper
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;

    /// <summary>Writes a 32-bit BGRA/RGBA frame as a bottom-up BMP.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="path">Destination path.</param>
    public static void WriteBmp(FrameBuffer frame, string path)
    {
        ArgumentNullException.ThrowIfNull(frame);
        WriteBmp(frame.PixelSpan, frame.Width, frame.Height, frame.Stride, path);
    }

    /// <summary>Writes a tightly packed 32-bit BGRA frame as a bottom-up BMP.</summary>
    /// <param name="bgra">Pixels in BGRA order, row-major, with no padding.</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="path">Destination path.</param>
    /// <exception cref="ArgumentException">The pixel span is smaller than the frame.</exception>
    public static void WriteBmp(ReadOnlySpan<byte> bgra, int width, int height, string path)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        WriteBmp(bgra, width, height, checked(width * 4), path);
    }

    private static void WriteBmp(ReadOnlySpan<byte> pixels, int width, int height, int stride, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var rowBytes = checked(width * 4);
        var imageBytes = checked(rowBytes * height);
        if (pixels.Length < imageBytes)
        {
            throw new ArgumentException($"The pixel span needs {imageBytes} bytes.", nameof(pixels));
        }

        var fileSize = FileHeaderSize + InfoHeaderSize + imageBytes;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(path);
        Span<byte> header = stackalloc byte[FileHeaderSize + InfoHeaderSize];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(header[2..], fileSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[10..], FileHeaderSize + InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[14..], InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[18..], width);
        BinaryPrimitives.WriteInt32LittleEndian(header[22..], height);
        BinaryPrimitives.WriteInt16LittleEndian(header[26..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[28..], 32);
        BinaryPrimitives.WriteInt32LittleEndian(header[34..], imageBytes);
        BinaryPrimitives.WriteInt32LittleEndian(header[38..], 2835);
        BinaryPrimitives.WriteInt32LittleEndian(header[42..], 2835);
        stream.Write(header);

        var row = new byte[rowBytes];
        for (var y = height - 1; y >= 0; y--)
        {
            var source = y * stride;
            for (var x = 0; x < width; x++)
            {
                var offset = source + (x * 4);
                var destination = x * 4;
                row[destination] = pixels[offset];
                row[destination + 1] = pixels[offset + 1];
                row[destination + 2] = pixels[offset + 2];
                row[destination + 3] = 0xff;
            }

            stream.Write(row);
        }
    }
}
