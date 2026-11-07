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
    /// <summary>Output-control bit that enables direct 5-bit color when the pixel high bit is set.</summary>
    public const uint FixedClutEnable = 0x02000000;
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

            for (var x = 0; x < width; x++)
            {
                ExpandPixel(sourceLine, sourcePixels[x], out var r, out var g, out var b);
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

    /// <summary>
    /// Expands one indexed VDL pixel to 8-bit RGB using the line's palette and control words.
    /// </summary>
    /// <param name="line">Line that supplies the palette, background, and output control.</param>
    /// <param name="pixel">Indexed or direct-color pixel.</param>
    /// <param name="red">Expanded red component.</param>
    /// <param name="green">Expanded green component.</param>
    /// <param name="blue">Expanded blue component.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="line"/> is null.</exception>
    /// <remarks>
    /// This is the single palette rule shared by the CPU assembler, the interlace detector,
    /// and the blend path. It matches the GPU palette shader exactly: pixel zero uses the
    /// line background, the fixed 5-bit CLUT is used when output-control bit 25 and pixel
    /// bit 15 are set, and the per-line CLUT is used otherwise.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void ExpandPixel(VdlLine line, ushort pixel, out byte red, out byte green, out byte blue)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (pixel == 0)
        {
            var background = line.Background;
            red = (byte)((background >> 16) & 0xff);
            green = (byte)((background >> 8) & 0xff);
            blue = (byte)(background & 0xff);
            return;
        }

        if ((line.OutControl & FixedClutEnable) != 0 && (pixel & 0x8000) != 0)
        {
            blue = FixedClutB[pixel & 0x1f];
            green = FixedClutG[(pixel >> 5) & 0x1f];
            red = FixedClutR[(pixel >> 10) & 0x1f];
            return;
        }

        blue = line.ClutB[pixel & 0x1f];
        green = line.ClutG[(pixel >> 5) & 0x1f];
        red = line.ClutR[(pixel >> 10) & 0x1f];
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
