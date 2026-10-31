using System;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// CPU palette expansion and frame assembly. FreeDO: <c>frame.cpp: Get_Frame_Bitmap</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the accuracy oracle and the headless/fallback renderer. The OpenGL presenter
/// performs the same palette logic on the GPU in "Accurate" mode.
/// </para>
/// <para>
/// Pixel zero uses the per-line background color. When the line's output-control word has
/// bit 25 set and the pixel has bit 15 set, the fixed grayscale CLUT is used; otherwise the
/// per-line CLUT is used. CLUT entries are already 8-bit. The background color is a 5-bit
/// per-component value and is expanded to 8 bits with the same bit-replication formula used
/// by the fixed CLUT. FreeDO's <c>frame.cpp</c> stores the raw 5-bit background; the
/// expansion here is the documented CPU-oracle behaviour that the GPU shader must match.
/// </para>
/// </remarks>
public sealed class FrameAssembler
{
    private const uint FixedClutEnable = 0x02000000;
    private const int FixedClutSize = 32;

    private static readonly byte[] FixedClutR = CreateFixedClut();
    private static readonly byte[] FixedClutG = CreateFixedClut();
    private static readonly byte[] FixedClutB = CreateFixedClut();

    /// <summary>Converts a completed VDL frame into a CPU frame buffer.</summary>
    /// <param name="source">Source VDL frame.</param>
    /// <param name="destination">Destination frame buffer.</param>
    /// <param name="copyWidth">Number of pixels to copy per line.</param>
    /// <param name="copyHeight">Number of lines to copy.</param>
    /// <exception cref="ArgumentNullException">Thrown when a frame is null.</exception>
    public static void Assemble(VdlFrame source, FrameBuffer destination, int copyWidth, int copyHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var width = Math.Min(Math.Min(copyWidth, destination.Width), EmulatorConstants.VdlLineMaxPixels);
        var height = Math.Min(Math.Min(copyHeight, destination.Height), VdlFrame.MaxLines);
        Span<byte> pixels = destination.PixelSpan;

        for (var line = 0; line < height; line++)
        {
            VdlLine sourceLine = source.Lines[line];
            Span<ushort> sourcePixels = sourceLine.PixelBuffer;
            var lineStart = line * destination.Stride;
            var allowFixedClut = (sourceLine.OutControl & FixedClutEnable) != 0;

            for (var x = 0; x < width; x++)
            {
                var pixel = sourcePixels[x];
                byte b;
                byte g;
                byte r;

                if (pixel == 0)
                {
                    var background = sourceLine.Background;
                    b = Expand5((byte)(background & 0x1f));
                    g = Expand5((byte)((background >> 5) & 0x1f));
                    r = Expand5((byte)((background >> 10) & 0x1f));
                }
                else if (allowFixedClut && (pixel & 0x8000) != 0)
                {
                    b = FixedClutB[pixel & 0x1f];
                    g = FixedClutG[(pixel >> 5) & 0x1f];
                    r = FixedClutR[(pixel >> 10) & 0x1f];
                }
                else
                {
                    b = sourceLine.ClutB[pixel & 0x1f];
                    g = sourceLine.ClutG[(pixel >> 5) & 0x1f];
                    r = sourceLine.ClutR[(pixel >> 10) & 0x1f];
                }

                var offset = lineStart + (x * 4);
                if (destination.Format == PixelFormat.Bgra8888)
                {
                    pixels[offset] = b;
                    pixels[offset + 1] = g;
                    pixels[offset + 2] = r;
                }
                else
                {
                    pixels[offset] = r;
                    pixels[offset + 1] = g;
                    pixels[offset + 2] = b;
                }

                pixels[offset + 3] = 0xff;
            }
        }
    }

    /// <summary>Expands a 5-bit color component to 8 bits.</summary>
    /// <param name="value">5-bit component.</param>
    /// <returns>8-bit component.</returns>
    public static byte Expand5(byte value)
    {
        return (byte)((value << 3) | (value >> 2));
    }

    private static byte[] CreateFixedClut()
    {
        var result = new byte[FixedClutSize];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (byte)(((i & 0x1f) << 3) | ((i >> 2) & 7));
        }

        return result;
    }
}
