using System;
using ThreeDo.Core;

namespace ThreeDo.Video;

/// <summary>
/// Packs a completed <see cref="VdlFrame"/> into GPU-friendly textures:
/// raw 16-bit indexed pixels, a 32-entry RGB palette per line, and per-line control words.
/// </summary>
/// <remarks>
/// The buffers are reused across frames, so steady-state presentation performs no managed
/// allocations. Unlike <see cref="FrameAssembler"/>, this class does not expand the palette;
/// the OpenGL fragment shader consumes the raw values.
/// </remarks>
public sealed class VdlFrameUploader
{
    /// <summary>Maximum supported width.</summary>
    public const int MaxWidth = EmulatorConstants.VdlLineMaxPixels;

    /// <summary>Maximum supported height, including the high-resolution mode.</summary>
    public const int MaxHeight = EmulatorConstants.HighResolutionHeight;

    /// <summary>Number of CLUT entries per line.</summary>
    public const int ClutEntries = VdlLine.ClutSize;

    private readonly ushort[] _pixels = new ushort[MaxWidth * MaxHeight];
    private readonly byte[] _clut = new byte[ClutEntries * 3 * MaxHeight];
    private readonly uint[] _lineParams = new uint[2 * MaxHeight];

    /// <summary>Gets the packed indexed pixels, row-major.</summary>
    public ReadOnlySpan<ushort> Pixels => _pixels.AsSpan(0, Width * Height);

    /// <summary>Gets the per-line RGB palette: 32 entries x 3 bytes x height.</summary>
    public ReadOnlySpan<byte> Clut => _clut.AsSpan(0, Width == 0 ? 0 : ClutEntries * 3 * Height);

    /// <summary>Gets the per-line control pairs: background, out-control.</summary>
    public ReadOnlySpan<uint> LineParams => _lineParams.AsSpan(0, Width == 0 ? 0 : 2 * Height);

    /// <summary>Gets the packed width.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the packed height.</summary>
    public int Height { get; private set; }

    /// <summary>Packs a completed frame.</summary>
    /// <param name="frame">Completed VDL frame.</param>
    /// <exception cref="ArgumentNullException">Thrown when the frame is null.</exception>
    public void Update(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var height = Math.Clamp(frame.SourceHeight, 0, MaxHeight);
        var width = Math.Clamp(frame.SourceWidth, 0, MaxWidth);
        Width = width;
        Height = height;

        for (var y = 0; y < height; y++)
        {
            var line = frame[y];
            var lineWidth = Math.Min(line.Width, width);
            var pixelDestination = y * width;
            var source = line.Pixels;
            source[..lineWidth].CopyTo(_pixels.AsSpan(pixelDestination, lineWidth));
            if (lineWidth < width)
            {
                _pixels.AsSpan(pixelDestination + lineWidth, width - lineWidth).Clear();
            }

            var clutDestination = y * ClutEntries * 3;
            for (var i = 0; i < ClutEntries; i++)
            {
                _clut[clutDestination + (i * 3)] = line.ClutR[i];
                _clut[clutDestination + (i * 3) + 1] = line.ClutG[i];
                _clut[clutDestination + (i * 3) + 2] = line.ClutB[i];
            }

            _lineParams[y * 2] = line.Background;
            _lineParams[(y * 2) + 1] = line.OutControl;
        }
    }
}
