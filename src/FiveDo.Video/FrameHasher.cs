using System;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// Computes a deterministic hash of an assembled frame in BGRA8888 byte order.
/// </summary>
/// <remarks>
/// The same algorithm is used by the CLI checkpoint output and the differential
/// harness. Opera's XRGB8888 presenter frames are converted to the same BGRA byte
/// order before hashing, so equal pixels produce equal hashes across engines.
/// The hash is FNV-1a over every byte, alpha included.
/// </remarks>
public static class FrameHasher
{
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;

    /// <summary>Hashes an assembled CPU frame.</summary>
    /// <param name="frame">VDLP frame to assemble.</param>
    /// <returns>The FNV-1a hash of the BGRA8888 pixels.</returns>
    public static uint Hash(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var buffer = new FrameBuffer(frame.SourceWidth, frame.SourceHeight);
        FrameAssembler.Assemble(frame, buffer, buffer.Width, buffer.Height);
        return HashBgra(buffer.PixelSpan);
    }

    /// <summary>Hashes BGRA8888 pixels.</summary>
    /// <param name="pixels">Pixel bytes in BGRA order.</param>
    /// <returns>The FNV-1a hash.</returns>
    public static uint HashBgra(ReadOnlySpan<byte> pixels)
    {
        return HashBytes(pixels);
    }

    /// <summary>Hashes an arbitrary byte sequence with the same FNV-1a contract.</summary>
    /// <param name="bytes">Bytes to hash.</param>
    /// <returns>The FNV-1a hash.</returns>
    public static uint HashBytes(ReadOnlySpan<byte> bytes)
    {
        var hash = FnvOffsetBasis;
        foreach (var value in bytes)
        {
            hash = unchecked((hash ^ value) * FnvPrime);
        }

        return hash;
    }

    /// <summary>Hashes an XRGB8888 presenter frame.</summary>
    /// <param name="pixels">Pixels in native <c>0x00RRGGBB</c> order.</param>
    /// <param name="width">Number of visible pixels per row.</param>
    /// <param name="height">Number of rows.</param>
    /// <param name="stride">Row stride in pixels.</param>
    /// <returns>The FNV-1a hash of the equivalent BGRA8888 pixels.</returns>
    public static uint HashXrgb8888(ReadOnlySpan<uint> pixels, int width, int height, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width);
        if (pixels.Length < ((height - 1) * stride) + width)
        {
            throw new ArgumentException("The pixel span is shorter than the frame.", nameof(pixels));
        }

        var hash = FnvOffsetBasis;
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, width);
            foreach (var pixel in row)
            {
                hash = unchecked((hash ^ (byte)(pixel & 0xff)) * FnvPrime);
                hash = unchecked((hash ^ (byte)((pixel >> 8) & 0xff)) * FnvPrime);
                hash = unchecked((hash ^ (byte)((pixel >> 16) & 0xff)) * FnvPrime);
                hash = unchecked((hash ^ 0xff) * FnvPrime);
            }
        }

        return hash;
    }
}
