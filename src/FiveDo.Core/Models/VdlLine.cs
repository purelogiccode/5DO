using System;

namespace FiveDo.Core;

/// <summary>
/// One parsed VDLP scanline: raw indexed pixels plus the palette and control words
/// needed to expand them.
/// </summary>
/// <remarks>
/// FreeDO: <c>struct VDLLine</c> in <c>freedocore.h</c>. The pixel capacity is 1280 so
/// wider VDLP modes (384/512/640/1024) can be represented without changing this type.
/// </remarks>
public sealed class VdlLine
{
    /// <summary>Maximum number of pixels stored in a line.</summary>
    public const int MaxPixels = EmulatorConstants.VdlLineMaxPixels;

    /// <summary>Number of palette entries per color component.</summary>
    public const int ClutSize = 32;

    private readonly ushort[] _pixels = new ushort[MaxPixels];
    private readonly byte[] _clutB = new byte[ClutSize];
    private readonly byte[] _clutG = new byte[ClutSize];
    private readonly byte[] _clutR = new byte[ClutSize];
    private int _width = EmulatorConstants.VideoWidth;

    /// <summary>Gets or sets the number of valid pixels in <see cref="Pixels"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the width is outside 0..<see cref="MaxPixels"/>.</exception>
    public int Width
    {
        get => _width;
        set
        {
            if ((uint)value > MaxPixels)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The VDL line width is out of range.");
            }

            _width = value;
        }
    }

    /// <summary>Gets the valid indexed pixels for this line.</summary>
    public Span<ushort> Pixels => _pixels.AsSpan(0, _width);

    /// <summary>Gets the full pixel buffer, including pixels beyond <see cref="Width"/>.</summary>
    public Span<ushort> PixelBuffer => _pixels;

    /// <summary>Gets the blue palette component (32 entries).</summary>
    public Span<byte> ClutB => _clutB;

    /// <summary>Gets the green palette component (32 entries).</summary>
    public Span<byte> ClutG => _clutG;

    /// <summary>Gets the red palette component (32 entries).</summary>
    public Span<byte> ClutR => _clutR;

    /// <summary>Gets or sets the VDLP output-control word. FreeDO: <c>xOUTCONTROLL</c>.</summary>
    public uint OutControl { get; set; }

    /// <summary>Gets or sets the raw CLUT DMA word. FreeDO: <c>xCLUTDMA</c>.</summary>
    public uint ClutDma { get; set; }

    /// <summary>Gets or sets the background color word. FreeDO: <c>xBACKGROUND</c>.</summary>
    public uint Background { get; set; }

    /// <summary>Clears the pixels, palette, and control words.</summary>
    public void Clear()
    {
        _pixels.AsSpan().Clear();
        _clutB.AsSpan().Clear();
        _clutG.AsSpan().Clear();
        _clutR.AsSpan().Clear();
        OutControl = 0;
        ClutDma = 0;
        Background = 0;
        _width = EmulatorConstants.VideoWidth;
    }
}
