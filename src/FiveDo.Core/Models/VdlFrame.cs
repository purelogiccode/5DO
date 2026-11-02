using System;

namespace FiveDo.Core;

/// <summary>
/// A reusable frame of parsed VDLP lines.
/// </summary>
/// <remarks>
/// FreeDO: <c>struct VDLFrame</c> in <c>freedocore.h</c>. The frame owns 960 line objects
/// (240 visible lines times four) so the future high-resolution mode can fill mirrored
/// lines without reallocating. Normal NTSC output uses lines 0..239.
/// </remarks>
public sealed class VdlFrame
{
    /// <summary>Maximum number of lines stored in a frame.</summary>
    public const int MaxLines = EmulatorConstants.VdlFrameMaxLines;

    private readonly VdlLine[] _lines;

    /// <summary>Initializes a new instance of the <see cref="VdlFrame"/> class.</summary>
    public VdlFrame()
    {
        _lines = new VdlLine[MaxLines];
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i] = new VdlLine();
        }

        SourceWidth = EmulatorConstants.VideoWidth;
        SourceHeight = EmulatorConstants.VideoHeight;
    }

    /// <summary>Gets or sets the width of the visible source image. FreeDO: <c>srcw</c>.</summary>
    public int SourceWidth { get; set; }

    /// <summary>Gets or sets the height of the visible source image. FreeDO: <c>srch</c>.</summary>
    public int SourceHeight { get; set; }

    /// <summary>Gets all frame lines.</summary>
    public Span<VdlLine> Lines => _lines;

    /// <summary>Gets the line at <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based line index.</param>
    public VdlLine this[int index] => _lines[index];

    /// <summary>
    /// Copies a rectangular crop from another frame into this frame.
    /// </summary>
    /// <param name="source">Source frame.</param>
    /// <param name="left">Left pixel column to keep.</param>
    /// <param name="top">First visible line to keep.</param>
    /// <param name="width">Number of pixel columns to keep.</param>
    /// <param name="height">Number of visible lines to keep.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the crop is negative, wider than the source, or larger than the frame store.
    /// </exception>
    /// <remarks>
    /// The destination keeps each source line's palette, background, output-control word,
    /// and DMA flag, so it can be presented or assembled exactly like a full frame.
    /// </remarks>
    public void CopyCroppedFrom(VdlFrame source, int left, int top, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(left);
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (left + width > source.SourceWidth || top + height > source.SourceHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"Crop ({left},{top},{width}x{height}) exceeds the {source.SourceWidth}x{source.SourceHeight} source."));
        }

        if (width > VdlLine.MaxPixels || height > MaxLines)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Crop exceeds the frame store.");
        }

        SourceWidth = width;
        SourceHeight = height;
        for (var line = 0; line < height; line++)
        {
            VdlLine sourceLine = source[top + line];
            VdlLine destinationLine = _lines[line];
            destinationLine.Width = width;
            destinationLine.OutControl = sourceLine.OutControl;
            destinationLine.ClutDma = sourceLine.ClutDma;
            destinationLine.Background = sourceLine.Background;
            sourceLine.ClutB.CopyTo(destinationLine.ClutB);
            sourceLine.ClutG.CopyTo(destinationLine.ClutG);
            sourceLine.ClutR.CopyTo(destinationLine.ClutR);
            sourceLine.PixelBuffer.Slice(left, width).CopyTo(destinationLine.PixelBuffer);
        }
    }

    /// <summary>Clears every line and restores the default source dimensions.</summary>
    public void Clear()
    {
        foreach (VdlLine line in _lines)
        {
            line.Clear();
        }

        SourceWidth = EmulatorConstants.VideoWidth;
        SourceHeight = EmulatorConstants.VideoHeight;
    }
}
