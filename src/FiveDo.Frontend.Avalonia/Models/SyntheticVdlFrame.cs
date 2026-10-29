using System;
using FiveDo.Core;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Builds the deterministic synthetic frame shared by the OpenGL and Vulkan probes.
/// </summary>
internal static class SyntheticVdlFrame
{
    /// <summary>Initializes a frame's line metadata and palette.</summary>
    /// <param name="frame">Destination frame.</param>
    internal static void Initialize(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.SourceWidth = EmulatorConstants.VideoWidth;
        frame.SourceHeight = EmulatorConstants.VideoHeight;
        for (var y = 0; y < EmulatorConstants.VideoHeight; y++)
        {
            VdlLine line = frame[y];
            line.Width = EmulatorConstants.VideoWidth;
            line.OutControl = 0;
            line.Background = 0;
            for (var i = 0; i < VdlLine.ClutSize; i++)
            {
                line.ClutR[i] = (byte)(i * 8);
                line.ClutG[i] = (byte)(255 - (i * 8));
                line.ClutB[i] = (byte)(((i * 3) & 0x1f) * 8);
            }
        }
    }

    /// <summary>Fills the frame with a moving 5-bit index pattern.</summary>
    /// <param name="frame">Destination frame.</param>
    /// <param name="phase">Frame phase used to shift the pattern.</param>
    internal static void Fill(VdlFrame frame, int phase)
    {
        ArgumentNullException.ThrowIfNull(frame);
        for (var y = 0; y < EmulatorConstants.VideoHeight; y++)
        {
            Span<ushort> pixels = frame[y].Pixels;
            var row = (y + phase) & 0x1f;
            for (var x = 0; x < EmulatorConstants.VideoWidth; x++)
            {
                var column = (x + phase) & 0x1f;
                var xor = (x ^ y) & 0x1f;
                pixels[x] = (ushort)(column | (row << 5) | (xor << 10));
            }
        }
    }
}
