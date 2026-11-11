using System;
using System.Runtime.InteropServices;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// Scales a native-resolution CPU frame into a larger output frame.
/// </summary>
/// <remarks>
/// The software presenter uses this when the user selects a fixed output resolution. The
/// filtered GPU presenters perform the same aspect and integer-scale policy in their
/// scale pass; this implementation keeps the software path visually consistent.
/// </remarks>
internal static class FrameScaler
{
    /// <summary>Scales a source frame into a destination frame.</summary>
    /// <param name="source">Native-resolution source frame.</param>
    /// <param name="destination">Output-resolution destination frame.</param>
    /// <param name="settings">Aspect and filter settings.</param>
    /// <param name="pixelAspect">
    /// Height of one source row relative to a square output pixel. Defaults to 1; woven
    /// 480i/576i frames pass 0.5.
    /// </param>
    public static void Scale(
        FrameBuffer source,
        FrameBuffer destination,
        RendererSettings settings,
        double pixelAspect = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        Span<byte> destinationPixels = destination.PixelSpan;
        MemoryMarshal.Cast<byte, uint>(destinationPixels).Fill(0xFF000000);

        ViewportRect rect = ViewportCalculator.Compute(
            source.Width,
            source.Height,
            destination.Width,
            destination.Height,
            settings,
            pixelAspect);
        if (destination.Width == source.Width
            && destination.Height == source.Height
            && rect == new ViewportRect(0, 0, source.Width, source.Height))
        {
            source.PixelSpan.CopyTo(destinationPixels);
            return;
        }

        if (settings.Filter == VideoFilter.Bicubic)
        {
            ScaleBicubic(source, destination, rect);
        }
        else if (settings.Filter is VideoFilter.Accurate or VideoFilter.Nearest)
        {
            ScaleNearest(source, destination, rect);
        }
        else
        {
            ScaleBilinear(source, destination, rect);
        }
    }

    private static void ScaleBicubic(FrameBuffer source, FrameBuffer destination, ViewportRect rect)
    {
        ReadOnlySpan<byte> sourcePixels = source.PixelSpan;
        Span<byte> destinationPixels = destination.PixelSpan;
        Span<float> wy = stackalloc float[4];
        Span<float> wx = stackalloc float[4];
        for (var y = 0; y < rect.Height; y++)
        {
            var sourceY = (((y + 0.5) * source.Height) / rect.Height) - 0.5;
            var baseY = (int)Math.Floor(sourceY);
            var fy = sourceY - baseY;
            CubicWeights((float)fy, wy);
            var destinationRow = (rect.Y + y) * destination.Stride;
            for (var x = 0; x < rect.Width; x++)
            {
                var sourceX = (((x + 0.5) * source.Width) / rect.Width) - 0.5;
                var baseX = (int)Math.Floor(sourceX);
                var fx = sourceX - baseX;
                CubicWeights((float)fx, wx);

                var red = 0f;
                var green = 0f;
                var blue = 0f;
                for (var tapY = 0; tapY < 4; tapY++)
                {
                    var sampleY = Math.Clamp(baseY + tapY - 1, 0, source.Height - 1);
                    var row = sampleY * source.Stride;
                    var weightY = wy[tapY];
                    for (var tapX = 0; tapX < 4; tapX++)
                    {
                        var sampleX = Math.Clamp(baseX + tapX - 1, 0, source.Width - 1);
                        var offset = row + (sampleX * 4);
                        var weight = weightY * wx[tapX];
                        blue += sourcePixels[offset] * weight;
                        green += sourcePixels[offset + 1] * weight;
                        red += sourcePixels[offset + 2] * weight;
                    }
                }

                var destinationOffset = destinationRow + ((rect.X + x) * 4);
                destinationPixels[destinationOffset] = ToByte(blue);
                destinationPixels[destinationOffset + 1] = ToByte(green);
                destinationPixels[destinationOffset + 2] = ToByte(red);
                destinationPixels[destinationOffset + 3] = 0xff;
            }
        }
    }

    private static void CubicWeights(float fraction, Span<float> weights)
    {
        // Catmull-Rom (a = -0.5) weights for source taps -1, 0, 1, 2.
        weights[0] = (((-0.5f * fraction) + 1f) * fraction - 0.5f) * fraction;
        weights[1] = (((1.5f * fraction) - 2.5f) * fraction * fraction) + 1f;
        weights[2] = (((-1.5f * fraction) + 2f) * fraction + 0.5f) * fraction;
        weights[3] = (((0.5f * fraction) - 0.5f) * fraction) * fraction;
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }

    private static void ScaleNearest(FrameBuffer source, FrameBuffer destination, ViewportRect rect)
    {
        ReadOnlySpan<byte> sourcePixels = source.PixelSpan;
        Span<byte> destinationPixels = destination.PixelSpan;
        for (var y = 0; y < rect.Height; y++)
        {
            var sourceY = Math.Min(source.Height - 1, y * source.Height / rect.Height);
            var sourceRow = sourceY * source.Stride;
            var destinationRow = (rect.Y + y) * destination.Stride;
            for (var x = 0; x < rect.Width; x++)
            {
                var sourceX = Math.Min(source.Width - 1, x * source.Width / rect.Width);
                var sourceOffset = sourceRow + (sourceX * 4);
                var destinationOffset = destinationRow + ((rect.X + x) * 4);
                sourcePixels.Slice(sourceOffset, 4).CopyTo(destinationPixels[destinationOffset..]);
            }
        }
    }

    private static void ScaleBilinear(FrameBuffer source, FrameBuffer destination, ViewportRect rect)
    {
        ReadOnlySpan<byte> sourcePixels = source.PixelSpan;
        Span<byte> destinationPixels = destination.PixelSpan;
        for (var y = 0; y < rect.Height; y++)
        {
            var sourceY = (((y + 0.5) * source.Height) / rect.Height) - 0.5;
            var y0 = (int)Math.Floor(sourceY);
            var fy = (float)(sourceY - y0);
            if (y0 < 0)
            {
                y0 = 0;
                fy = 0;
            }

            var y1 = Math.Min(y0 + 1, source.Height - 1);
            var row0 = y0 * source.Stride;
            var row1 = y1 * source.Stride;
            var destinationRow = (rect.Y + y) * destination.Stride;
            for (var x = 0; x < rect.Width; x++)
            {
                var sourceX = (((x + 0.5) * source.Width) / rect.Width) - 0.5;
                var x0 = (int)Math.Floor(sourceX);
                var fx = (float)(sourceX - x0);
                if (x0 < 0)
                {
                    x0 = 0;
                    fx = 0;
                }

                var x1 = Math.Min(x0 + 1, source.Width - 1);
                var offset00 = row0 + (x0 * 4);
                var offset10 = row0 + (x1 * 4);
                var offset01 = row1 + (x0 * 4);
                var offset11 = row1 + (x1 * 4);
                var destinationOffset = destinationRow + ((rect.X + x) * 4);
                for (var channel = 0; channel < 3; channel++)
                {
                    var top = Lerp(sourcePixels[offset00 + channel], sourcePixels[offset10 + channel], fx);
                    var bottom = Lerp(sourcePixels[offset01 + channel], sourcePixels[offset11 + channel], fx);
                    destinationPixels[destinationOffset + channel] = (byte)MathF.Round(Lerp(top, bottom, fy));
                }

                destinationPixels[destinationOffset + 3] = 0xff;
            }
        }
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + ((to - from) * amount);
    }
}
