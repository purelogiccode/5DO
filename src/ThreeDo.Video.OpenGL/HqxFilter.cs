using System;
using ThreeDo.Core;

namespace ThreeDo.Video.OpenGL;

/// <summary>
/// A CPU implementation of the HQx 2x pixel-art filter, used as an optional
/// presentation filter before the final sharp-bilinear scale.
/// </summary>
/// <remarks>
/// <para>
/// The pattern switch is derived from Maxim Stepin's hq2x (Copyright (C) 2003
/// Maxim Stepin, maxst@hiend3d.com) and Cameron Zemek's C port (Copyright (C)
/// 2010 Cameron Zemek, grom@zeminvaders.net), both distributed under the GNU
/// Lesser General Public License 2.1 or later. The interpolation and edge
/// handling are implemented here; the generated switch is data-like code.
/// </para>
/// <para>
/// The filter consumes the CPU-expanded BGRA frame, so palette expansion remains
/// pixel-identical to <see cref="FrameAssembler"/>. It is opt-in and runs only
/// when <see cref="VideoFilter.Hqx"/> is selected.
/// </para>
/// </remarks>
public sealed partial class HqxFilter
{
    private uint[] _source = [];
    private uint[] _destination = [];
    private readonly uint[] _window = new uint[10];

    /// <summary>Gets the integer scale factor of this filter.</summary>
    public const int Scale = 2;

    /// <summary>Expands a BGRA frame by 2x.</summary>
    /// <param name="source">Source frame.</param>
    /// <param name="destination">Destination frame, which must be twice the source size.</param>
    /// <exception cref="ArgumentNullException">Thrown when a frame is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the destination size is not exactly 2x.</exception>
    public void Expand(FrameBuffer source, FrameBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Width != source.Width * Scale || destination.Height != source.Height * Scale)
        {
            throw new ArgumentException(
                $"The HQx destination must be {source.Width * Scale}x{source.Height * Scale}.",
                nameof(destination));
        }

        var width = source.Width;
        var height = source.Height;
        var pixelCount = checked(width * height);
        if (_source.Length < pixelCount)
        {
            _source = new uint[pixelCount];
            _destination = new uint[checked(pixelCount * Scale * Scale)];
        }

        ToArgb(source.PixelSpan, pixelCount, _source);
        var sourcePixels = _source;
        var destinationPixels = _destination;
        var w = _window;
        for (var y = 0; y < height; y++)
        {
            var previous = y > 0 ? -width : 0;
            var next = y < height - 1 ? width : 0;
            for (var x = 0; x < width; x++)
            {
                w[2] = sourcePixels[(y * width) + previous + x];
                w[5] = sourcePixels[(y * width) + x];
                w[8] = sourcePixels[(y * width) + next + x];

                if (x > 0)
                {
                    w[1] = sourcePixels[(y * width) + previous + x - 1];
                    w[4] = sourcePixels[(y * width) + x - 1];
                    w[7] = sourcePixels[(y * width) + next + x - 1];
                }
                else
                {
                    w[1] = w[2];
                    w[4] = w[5];
                    w[7] = w[8];
                }

                if (x < width - 1)
                {
                    w[3] = sourcePixels[(y * width) + previous + x + 1];
                    w[6] = sourcePixels[(y * width) + x + 1];
                    w[9] = sourcePixels[(y * width) + next + x + 1];
                }
                else
                {
                    w[3] = w[2];
                    w[6] = w[5];
                    w[9] = w[8];
                }

                var pattern = ComputePattern(w);
                ApplyPattern(pattern, w, out var p00, out var p01, out var p10, out var p11);
                var destinationOffset = ((y * Scale) * width * Scale) + (x * Scale);
                destinationPixels[destinationOffset] = p00;
                destinationPixels[destinationOffset + 1] = p01;
                destinationPixels[destinationOffset + width * Scale] = p10;
                destinationPixels[destinationOffset + width * Scale + 1] = p11;
            }
        }

        ToBgra(destinationPixels, destination.PixelSpan, pixelCount * Scale * Scale);
    }

    private static int ComputePattern(uint[] w)
    {
        var (centerY, centerU, centerV) = ToYuv(w[5]);
        var pattern = 0;
        var flag = 1;
        for (var i = 1; i <= 9; i++)
        {
            if (i == 5)
            {
                continue;
            }

            if (w[i] != w[5])
            {
                var (y, u, v) = ToYuv(w[i]);
                if (Math.Abs(centerY - y) > 48 || Math.Abs(centerU - u) > 7 || Math.Abs(centerV - v) > 6)
                {
                    pattern |= flag;
                }
            }

            flag <<= 1;
        }

        return pattern;
    }

    private static bool Diff(uint c1, uint c2)
    {
        var (y1, u1, v1) = ToYuv(c1);
        var (y2, u2, v2) = ToYuv(c2);
        return Math.Abs(y1 - y2) > 48 || Math.Abs(u1 - u2) > 7 || Math.Abs(v1 - v2) > 6;
    }

    private static uint Interp1(uint c1, uint c2) => Interpolate2(c1, 3, c2, 1, 2);

    private static uint Interp2(uint c1, uint c2, uint c3) => Interpolate3(c1, 2, c2, 1, c3, 1, 2);

    private static uint Interp3(uint c1, uint c2) => Interpolate2(c1, 7, c2, 1, 3);

    private static uint Interp4(uint c1, uint c2, uint c3) => Interpolate3(c1, 2, c2, 7, c3, 7, 4);

    private static uint Interp5(uint c1, uint c2) => Interpolate2(c1, 1, c2, 1, 1);

    private static uint Interp6(uint c1, uint c2, uint c3) => Interpolate3(c1, 5, c2, 2, c3, 1, 3);

    private static uint Interp7(uint c1, uint c2, uint c3) => Interpolate3(c1, 6, c2, 1, c3, 1, 3);

    private static uint Interp8(uint c1, uint c2) => Interpolate2(c1, 5, c2, 3, 3);

    private static uint Interp9(uint c1, uint c2, uint c3) => Interpolate3(c1, 2, c2, 3, c3, 3, 3);

    private static uint Interp10(uint c1, uint c2, uint c3) => Interpolate3(c1, 14, c2, 1, c3, 1, 4);

    private static uint Interpolate2(uint c1, uint w1, uint c2, uint w2, int shift)
    {
        if (c1 == c2)
        {
            return c1;
        }

        unchecked
        {
            return ((((((c1 & 0xFF000000u) >> 24) * w1) + (((c2 & 0xFF000000u) >> 24) * w2)) << (24 - shift)) & 0xFF000000u)
                + (((((c1 & 0x0000FF00u) * w1) + ((c2 & 0x0000FF00u) * w2)) >> shift) & 0x0000FF00u)
                + (((((c1 & 0x00FF00FFu) * w1) + ((c2 & 0x00FF00FFu) * w2)) >> shift) & 0x00FF00FFu);
        }
    }

    private static uint Interpolate3(uint c1, uint w1, uint c2, uint w2, uint c3, uint w3, int shift)
    {
        unchecked
        {
            return ((((((c1 & 0xFF000000u) >> 24) * w1) + (((c2 & 0xFF000000u) >> 24) * w2) + (((c3 & 0xFF000000u) >> 24) * w3)) << (24 - shift)) & 0xFF000000u)
                + (((((c1 & 0x0000FF00u) * w1) + ((c2 & 0x0000FF00u) * w2) + ((c3 & 0x0000FF00u) * w3)) >> shift) & 0x0000FF00u)
                + (((((c1 & 0x00FF00FFu) * w1) + ((c2 & 0x00FF00FFu) * w2) + ((c3 & 0x00FF00FFu) * w3)) >> shift) & 0x00FF00FFu);
        }
    }

    private static (int Y, int U, int V) ToYuv(uint color)
    {
        var r = (int)((color >> 16) & 0xFF);
        var g = (int)((color >> 8) & 0xFF);
        var b = (int)(color & 0xFF);
        var y = (int)((0.299 * r) + (0.587 * g) + (0.114 * b));
        var u = ((int)((-0.169 * r) - (0.331 * g) + (0.5 * b)) + 128) & 0xFF;
        var v = ((int)((0.5 * r) - (0.419 * g) - (0.081 * b)) + 128) & 0xFF;
        return (y, u, v);
    }

    private static void ToArgb(ReadOnlySpan<byte> source, int pixelCount, uint[] destination)
    {
        var offset = 0;
        for (var i = 0; i < pixelCount; i++)
        {
            destination[i] = 0xFF000000u
                | ((uint)source[offset + 2] << 16)
                | ((uint)source[offset + 1] << 8)
                | source[offset];
            offset += 4;
        }
    }

    private static void ToBgra(ReadOnlySpan<uint> source, Span<byte> destination, int pixelCount)
    {
        var offset = 0;
        for (var i = 0; i < pixelCount; i++)
        {
            var color = source[i];
            destination[offset] = (byte)color;
            destination[offset + 1] = (byte)(color >> 8);
            destination[offset + 2] = (byte)(color >> 16);
            destination[offset + 3] = 0xFF;
            offset += 4;
        }
    }
}
