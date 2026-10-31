using System;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// CPU reference for the GPU palette-expansion shader.
/// </summary>
/// <remarks>
/// This mirrors the fragment shader in <c>FiveDo.Video.OpenGL/Shaders</c> so tests can verify
/// that <see cref="FrameAssembler"/> and the documented GPU algorithm agree pixel-for-pixel.
/// Pixel zero uses the per-line background. When the line's output-control bit 25 is set and
/// the pixel has bit 15 set, the fixed 5-bit grayscale CLUT is used; otherwise the per-line
/// 32-entry CLUT is used.
/// </remarks>
public static class GpuPalette
{
    /// <summary>Output-control bit that enables the fixed CLUT.</summary>
    public const uint FixedClutEnable = 0x02000000;

    /// <summary>Expands one indexed pixel to 8-bit RGB.</summary>
    /// <param name="line">Source line with its CLUT and control words.</param>
    /// <param name="pixel">Raw indexed pixel.</param>
    /// <returns>The expanded RGB components.</returns>
    public static (byte R, byte G, byte B) Expand(VdlLine line, ushort pixel)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (pixel == 0)
        {
            var background = line.Background;
            return (
                Expand5((byte)((background >> 10) & 0x1f)),
                Expand5((byte)((background >> 5) & 0x1f)),
                Expand5((byte)(background & 0x1f)));
        }

        if ((line.OutControl & FixedClutEnable) != 0 && (pixel & 0x8000) != 0)
        {
            return (
                Expand5((byte)((pixel >> 10) & 0x1f)),
                Expand5((byte)((pixel >> 5) & 0x1f)),
                Expand5((byte)(pixel & 0x1f)));
        }

        return (
            line.ClutR[(pixel >> 10) & 0x1f],
            line.ClutG[(pixel >> 5) & 0x1f],
            line.ClutB[pixel & 0x1f]);
    }

    /// <summary>Expands a 5-bit color component to 8 bits, matching the GLSL bit replication.</summary>
    /// <param name="value">5-bit component.</param>
    /// <returns>8-bit component.</returns>
    public static byte Expand5(byte value)
    {
        return (byte)((value << 3) | (value >> 2));
    }
}
