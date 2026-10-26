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

    /// <summary>Clears every line and restores the default source dimensions.</summary>
    public void Clear()
    {
        foreach (var line in _lines)
        {
            line.Clear();
        }

        SourceWidth = EmulatorConstants.VideoWidth;
        SourceHeight = EmulatorConstants.VideoHeight;
    }
}
