using System;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// The CPU reference implementation of the MMPX 2x pixel-art magnification filter.
/// </summary>
/// <remarks>
/// <para>
/// MMPX is Morgan McGuire and Mara Gagiu's style-preserving pixel-art filter
/// (MIT license). It predicts the four destination pixels for each source pixel from
/// the 3x3 neighbourhood and the two-pixel diamond points, preserving the original
/// palette and single-pixel features. This class is the reference oracle for the GPU
/// rule passes and the opt-in parity tests; the accelerated presenters run the same
/// rules in their fragment shaders.
/// </para>
/// <para>
/// VDL frames are opaque (alpha 255), so the reference luma term
/// <c>(r + g + b + 1) * (256 - alpha)</c> reduces to <c>r + g + b + 1</c>. The luma
/// comparisons keep the reference tie-break and triangle-tip rules active instead of
/// collapsing them to zero.
/// </para>
/// </remarks>
public sealed class MmpxFilter
{
    /// <summary>The fixed MMPX scale factor.</summary>
    public const int Scale = 2;

    private uint[] _source = [];
    private uint[] _destination = [];

    /// <summary>Gets the scale factor used by a video filter, or 1 for non-MMPX filters.</summary>
    /// <param name="filter">Video filter.</param>
    /// <returns>The MMPX scale factor.</returns>
    public static int ScaleFor(VideoFilter filter)
    {
        return filter == VideoFilter.Mmpx ? Scale : 1;
    }

    /// <summary>Expands a BGRA frame by 2x with MMPX.</summary>
    /// <param name="source">Source frame.</param>
    /// <param name="destination">Destination frame, which must be exactly twice the source size.</param>
    /// <exception cref="ArgumentNullException">Thrown when a frame is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the destination size is not 2x the source size.</exception>
    public void Expand(FrameBuffer source, FrameBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Width != source.Width * Scale || destination.Height != source.Height * Scale)
        {
            throw new ArgumentException(
                "The MMPX destination must be exactly 2x the source size.",
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
        var destinationWidth = width * Scale;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = At(sourcePixels, width, height, x - 1, y - 1);
                var b = At(sourcePixels, width, height, x, y - 1);
                var c = At(sourcePixels, width, height, x + 1, y - 1);
                var d = At(sourcePixels, width, height, x - 1, y);
                var e = At(sourcePixels, width, height, x, y);
                var f = At(sourcePixels, width, height, x + 1, y);
                var g = At(sourcePixels, width, height, x - 1, y + 1);
                var h = At(sourcePixels, width, height, x, y + 1);
                var i = At(sourcePixels, width, height, x + 1, y + 1);
                var p = At(sourcePixels, width, height, x, y - 2);
                var s = At(sourcePixels, width, height, x, y + 2);
                var q = At(sourcePixels, width, height, x - 2, y);
                var r = At(sourcePixels, width, height, x + 2, y);

                var j = e;
                var k = e;
                var l = e;
                var m = e;

                if (a != e || b != e || c != e || d != e
                    || f != e || g != e || h != e || i != e)
                {
                    var bl = Luma(b);
                    var dl = Luma(d);
                    var el = Luma(e);
                    var fl = Luma(f);
                    var hl = Luma(h);

                    // 1:1 slope rules.
                    if (d == b && d != h && d != f
                        && (el >= dl || e == a)
                        && AnyEq3(e, a, c, g)
                        && (el < dl || a != d || e != p || e != q))
                    {
                        j = d;
                    }

                    if (b == f && b != d && b != h
                        && (el >= bl || e == c)
                        && AnyEq3(e, a, c, i)
                        && (el < bl || c != b || e != p || e != r))
                    {
                        k = b;
                    }

                    if (h == d && h != f && h != b
                        && (el >= hl || e == g)
                        && AnyEq3(e, a, g, i)
                        && (el < hl || g != h || e != s || e != q))
                    {
                        l = h;
                    }

                    if (f == h && f != b && f != d
                        && (el >= fl || e == i)
                        && AnyEq3(e, c, g, i)
                        && (el < fl || i != h || e != r || e != s))
                    {
                        m = f;
                    }

                    // Intersection rules.
                    if (e != f && AllEq4(e, c, i, d, q) && AllEq2(f, b, h)
                        && f != At(sourcePixels, width, height, x + 3, y))
                    {
                        k = f;
                        m = f;
                    }

                    if (e != d && AllEq4(e, a, g, f, r) && AllEq2(d, b, h)
                        && d != At(sourcePixels, width, height, x - 3, y))
                    {
                        j = d;
                        l = d;
                    }

                    if (e != h && AllEq4(e, g, i, b, p) && AllEq2(h, d, f)
                        && h != At(sourcePixels, width, height, x, y + 3))
                    {
                        l = h;
                        m = h;
                    }

                    if (e != b && AllEq4(e, a, c, h, s) && AllEq2(b, d, f)
                        && b != At(sourcePixels, width, height, x, y - 3))
                    {
                        j = b;
                        k = b;
                    }

                    if (bl < el && AllEq4(e, g, h, i, s) && NoneEq4(e, a, d, c, f))
                    {
                        j = b;
                        k = b;
                    }

                    if (hl < el && AllEq4(e, a, b, c, p) && NoneEq4(e, d, g, i, f))
                    {
                        l = h;
                        m = h;
                    }

                    if (fl < el && AllEq4(e, a, d, g, q) && NoneEq4(e, b, c, i, h))
                    {
                        k = f;
                        m = f;
                    }

                    if (dl < el && AllEq4(e, c, f, i, r) && NoneEq4(e, b, a, g, h))
                    {
                        j = d;
                        l = d;
                    }

                    // 2:1 slope rules.
                    if (h != b)
                    {
                        if (h != a && h != e && h != c)
                        {
                            if (AllEq3(h, g, f, r)
                                && NoneEq2(h, d, At(sourcePixels, width, height, x + 2, y - 1)))
                            {
                                l = m;
                            }

                            if (AllEq3(h, i, d, q)
                                && NoneEq2(h, f, At(sourcePixels, width, height, x - 2, y - 1)))
                            {
                                m = l;
                            }
                        }

                        if (b != i && b != g && b != e)
                        {
                            if (AllEq3(b, a, f, r)
                                && NoneEq2(b, d, At(sourcePixels, width, height, x + 2, y + 1)))
                            {
                                j = k;
                            }

                            if (AllEq3(b, c, d, q)
                                && NoneEq2(b, f, At(sourcePixels, width, height, x - 2, y + 1)))
                            {
                                k = j;
                            }
                        }
                    }

                    if (f != d)
                    {
                        if (d != i && d != e && d != c)
                        {
                            if (AllEq3(d, a, h, s)
                                && NoneEq2(d, b, At(sourcePixels, width, height, x + 1, y + 2)))
                            {
                                j = l;
                            }

                            if (AllEq3(d, g, b, p)
                                && NoneEq2(d, h, At(sourcePixels, width, height, x + 1, y - 2)))
                            {
                                l = j;
                            }
                        }

                        if (f != e && f != a && f != g)
                        {
                            if (AllEq3(f, c, h, s)
                                && NoneEq2(f, b, At(sourcePixels, width, height, x - 1, y + 2)))
                            {
                                k = m;
                            }

                            if (AllEq3(f, i, b, p)
                                && NoneEq2(f, h, At(sourcePixels, width, height, x - 1, y - 2)))
                            {
                                m = k;
                            }
                        }
                    }
                }

                var destinationRow = (y * Scale) * destinationWidth;
                var destinationColumn = x * Scale;
                destinationPixels[destinationRow + destinationColumn] = j;
                destinationPixels[destinationRow + destinationColumn + 1] = k;
                destinationPixels[destinationRow + destinationWidth + destinationColumn] = l;
                destinationPixels[destinationRow + destinationWidth + destinationColumn + 1] = m;
            }
        }

        ToBgra(destinationPixels, destination.PixelSpan, pixelCount * Scale * Scale);
    }

    private static uint At(uint[] pixels, int width, int height, int x, int y)
    {
        x = Math.Clamp(x, 0, width - 1);
        y = Math.Clamp(y, 0, height - 1);
        return pixels[(y * width) + x];
    }

    private static int Luma(uint color)
    {
        // Reference formula for 8-bit unassociated alpha. VDL frames are opaque
        // (alpha 255), so this is r + g + b + 1 for every pixel in practice.
        var alpha = (int)(color >> 24);
        var r = (int)((color >> 16) & 0xFF);
        var g = (int)((color >> 8) & 0xFF);
        var b = (int)(color & 0xFF);
        return (r + g + b + 1) * (256 - alpha);
    }

    private static bool AllEq2(uint b, uint a0, uint a1)
        => b == a0 && b == a1;

    private static bool AllEq3(uint b, uint a0, uint a1, uint a2)
        => b == a0 && b == a1 && b == a2;

    private static bool AllEq4(uint b, uint a0, uint a1, uint a2, uint a3)
        => b == a0 && b == a1 && b == a2 && b == a3;

    private static bool AnyEq3(uint b, uint a0, uint a1, uint a2)
        => b == a0 || b == a1 || b == a2;

    private static bool NoneEq2(uint b, uint a0, uint a1)
        => b != a0 && b != a1;

    private static bool NoneEq4(uint b, uint a0, uint a1, uint a2, uint a3)
        => b != a0 && b != a1 && b != a2 && b != a3;

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
