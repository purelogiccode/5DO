using System;
using System.Buffers.Binary;
using System.IO;
using ThreeDo.Core;

namespace ThreeDo.Diagnostics;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var rowBytes = frame.Width * 4;
        var imageBytes = rowBytes * frame.Height;
        var fileSize = FileHeaderSize + InfoHeaderSize + imageBytes;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        Span<byte> header = stackalloc byte[FileHeaderSize + InfoHeaderSize];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(header[2..], fileSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[10..], FileHeaderSize + InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[14..], InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[18..], frame.Width);
        BinaryPrimitives.WriteInt32LittleEndian(header[22..], frame.Height);
        BinaryPrimitives.WriteInt16LittleEndian(header[26..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[28..], 32);
        BinaryPrimitives.WriteInt32LittleEndian(header[34..], imageBytes);
        BinaryPrimitives.WriteInt32LittleEndian(header[38..], 2835);
        BinaryPrimitives.WriteInt32LittleEndian(header[42..], 2835);
        stream.Write(header);

        var pixels = frame.PixelSpan;
        var row = new byte[rowBytes];
        for (var y = frame.Height - 1; y >= 0; y--)
        {
            var source = y * frame.Stride;
            for (var x = 0; x < frame.Width; x++)
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
