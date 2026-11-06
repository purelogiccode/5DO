using System;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// Presentation-side 480i/576i field handling: detection, weaving, and field blending.
/// </summary>
/// <remarks>
/// <para>
/// The 3DO VDLP and the reference cores produce one 240-line (288-line PAL) field per
/// 60 Hz (50 Hz) tick. Interlaced titles use alternate fields for the even and odd rows
/// of a 480-line image, so presenting each field as a full frame (bob) loses half the
/// vertical detail and combs on motion. This processor keeps the previous field and can:
/// </para>
/// <list type="bullet">
/// <item><description>weave the two fields into a 480-line frame with half-height rows;</description></item>
/// <item><description>blend them into one 240-line frame using 15-bit direct color;</description></item>
/// <item><description>auto-detect complementary fields and choose bob or weave.</description></item>
/// </list>
/// <para>
/// Detection is conservative and content-based: it compares the same index in two
/// adjacent fields with the vertical difference inside the current field. Progressive
/// 240p content repeats the same row in both fields, so its temporal difference is near
/// zero; interlaced content puts an adjacent source row in each field, so the temporal
/// and vertical differences are comparable. A verdict must persist for several fields
/// before the mode changes, and explicit <see cref="DeinterlaceMode.Bob"/>,
/// <see cref="DeinterlaceMode.Weave"/>, or <see cref="DeinterlaceMode.Blend"/> overrides
/// detection. The processor never touches guest state or save states.
/// </para>
/// </remarks>
public sealed class InterlaceProcessor
{
    private const double MinimumVerticalDifference = 6.0;
    private const double MinimumTemporalDifference = 6.0;
    private const double InterlaceRatioPercent = 45.0;
    private const double ProgressiveTemporalLimit = 2.0;
    private const int MinimumInformativeSamples = 8;
    private const int InterlacedConfirmationFields = 12;
    private const int ProgressiveConfirmationFields = 30;

    private readonly VdlFrame _previousField = new();
    private readonly VdlFrame _composite = new();

    private DeinterlaceMode _mode = DeinterlaceMode.Auto;
    private bool _hasPrevious;
    private bool _previousSecondField;
    private bool _interlaced;
    private int _interlacedFields;
    private int _progressiveFields;
    private double _detectionFraction;

    /// <summary>Gets or sets the field-combining mode.</summary>
    /// <remarks>Changing the mode discards the retained previous field and detection history.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown mode.</exception>
    public DeinterlaceMode Mode
    {
        get => _mode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown deinterlace mode.");
            }

            if (_mode == value)
            {
                return;
            }

            _mode = value;
            Reset();
        }
    }

    /// <summary>Gets a value indicating whether the detector currently classifies the content as interlaced.</summary>
    public bool IsInterlaced => _interlaced;

    /// <summary>Gets the share of informative samples that looked interlaced in the last detection pass.</summary>
    public double DetectionFraction => _detectionFraction;

    /// <summary>Clears the retained field and all detection history.</summary>
    public void Reset()
    {
        _hasPrevious = false;
        _previousSecondField = false;
        _interlaced = false;
        _interlacedFields = 0;
        _progressiveFields = 0;
        _detectionFraction = 0;
    }

    /// <summary>Processes one completed field for presentation.</summary>
    /// <param name="field">The field produced by the VDLP.</param>
    /// <param name="secondField">Whether this is the second (odd) field of the interlace pair.</param>
    /// <returns>
    /// <paramref name="field"/> unchanged for bob, or a reusable composite frame for weave/blend.
    /// The returned frame is valid until the next call.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="field"/> is null.</exception>
    public VdlFrame Process(VdlFrame field, bool secondField)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (_mode == DeinterlaceMode.Bob)
        {
            _hasPrevious = false;
            return field;
        }

        var paired = _hasPrevious
            && _previousSecondField != secondField
            && _previousField.SourceHeight == field.SourceHeight
            && _previousField.SourceWidth > 0
            && field.SourceWidth > 0;
        var canDoubleHeight = field.SourceHeight * 2 <= VdlFrame.MaxLines;

        if (_mode == DeinterlaceMode.Auto)
        {
            if (paired && canDoubleHeight)
            {
                UpdateDetection(_previousField, field);
            }
            else if (!canDoubleHeight)
            {
                // High-resolution frames are progressive line-doubled output. Do not
                // accumulate interlace evidence from them.
                _interlaced = false;
                _interlacedFields = 0;
                _progressiveFields = 0;
            }
        }

        VdlFrame presented = field;
        if (paired && canDoubleHeight)
        {
            if (_mode == DeinterlaceMode.Weave || (_mode == DeinterlaceMode.Auto && _interlaced))
            {
                presented = ComposeWeave(field, secondField);
            }
            else if (_mode == DeinterlaceMode.Blend)
            {
                presented = ComposeBlend(field);
            }
        }

        _previousField.CopyFrom(field);
        _hasPrevious = true;
        _previousSecondField = secondField;
        return presented;
    }

    private void UpdateDetection(VdlFrame previous, VdlFrame current)
    {
        var height = Math.Min(previous.SourceHeight, current.SourceHeight);
        if (height < 8)
        {
            return;
        }

        var lineStep = Math.Max(1, height / 48);
        var informative = 0;
        var interlaceLike = 0;
        var progressiveLike = 0;
        for (var y = 0; y + 1 < height; y += lineStep)
        {
            VdlLine lineA = previous[y];
            VdlLine lineB = current[y];
            VdlLine lineC = current[y + 1];
            var width = Math.Min(Math.Min(lineA.Width, lineB.Width), lineC.Width);
            if (width <= 0)
            {
                continue;
            }

            var columnStep = Math.Max(1, width / 64);
            for (var x = 0; x < width; x += columnStep)
            {
                FrameAssembler.ExpandPixel(lineA, lineA.PixelBuffer[x], out var ar, out var ag, out var ab);
                FrameAssembler.ExpandPixel(lineB, lineB.PixelBuffer[x], out var br, out var bg, out var bb);
                FrameAssembler.ExpandPixel(lineC, lineC.PixelBuffer[x], out var cr, out var cg, out var cb);
                var temporal = Math.Abs(ar - br) + Math.Abs(ag - bg) + Math.Abs(ab - bb);
                var vertical = Math.Abs(br - cr) + Math.Abs(bg - cg) + Math.Abs(bb - cb);
                if (vertical < MinimumVerticalDifference)
                {
                    continue;
                }

                informative++;
                if (temporal >= MinimumTemporalDifference
                    && temporal * 100.0 >= vertical * InterlaceRatioPercent)
                {
                    interlaceLike++;
                }
                else if (temporal <= ProgressiveTemporalLimit)
                {
                    progressiveLike++;
                }
            }
        }

        if (informative < MinimumInformativeSamples)
        {
            return;
        }

        var decisive = interlaceLike + progressiveLike;
        if (decisive < MinimumInformativeSamples)
        {
            return;
        }

        _detectionFraction = interlaceLike / (double)informative;
        var interlacedPair = interlaceLike >= 8 && interlaceLike >= progressiveLike * 3;
        if (interlacedPair)
        {
            _progressiveFields = 0;
            _interlacedFields++;
            if (!_interlaced && _interlacedFields >= InterlacedConfirmationFields)
            {
                _interlaced = true;
            }
        }
        else
        {
            _interlacedFields = 0;
            _progressiveFields++;
            if (_interlaced && _progressiveFields >= ProgressiveConfirmationFields)
            {
                _interlaced = false;
            }
        }
    }

    private VdlFrame ComposeWeave(VdlFrame field, bool secondField)
    {
        var height = Math.Min(
            Math.Min(field.SourceHeight, _previousField.SourceHeight),
            VdlFrame.MaxLines / 2);
        var width = Math.Clamp(
            Math.Max(field.SourceWidth, _previousField.SourceWidth),
            0,
            VdlLine.MaxPixels);
        var currentOffset = secondField ? 1 : 0;
        var previousOffset = 1 - currentOffset;
        for (var y = 0; y < height; y++)
        {
            CopyLine(field[y], _composite[(y * 2) + currentOffset]);
            CopyLine(_previousField[y], _composite[(y * 2) + previousOffset]);
        }

        _composite.SourceWidth = width;
        _composite.SourceHeight = height * 2;
        _composite.PixelAspect = 0.5;
        return _composite;
    }

    private VdlFrame ComposeBlend(VdlFrame field)
    {
        var height = Math.Min(
            Math.Min(field.SourceHeight, _previousField.SourceHeight),
            VdlFrame.MaxLines);
        for (var y = 0; y < height; y++)
        {
            VdlLine current = field[y];
            VdlLine previous = _previousField[y];
            VdlLine target = _composite[y];
            var width = Math.Clamp(current.Width, 0, VdlLine.MaxPixels);
            for (var x = 0; x < width; x++)
            {
                FrameAssembler.ExpandPixel(current, current.PixelBuffer[x], out var r, out var g, out var b);
                if (x < previous.Width)
                {
                    FrameAssembler.ExpandPixel(previous, previous.PixelBuffer[x], out var pr, out var pg, out var pb);
                    r = (byte)((r + pr + 1) >> 1);
                    g = (byte)((g + pg + 1) >> 1);
                    b = (byte)((b + pb + 1) >> 1);
                }

                target.PixelBuffer[x] = PackDirectColor(r, g, b);
            }

            target.Width = width;
            current.ClutB.CopyTo(target.ClutB);
            current.ClutG.CopyTo(target.ClutG);
            current.ClutR.CopyTo(target.ClutR);
            target.OutControl = current.OutControl | FrameAssembler.FixedClutEnable;
            target.ClutDma = current.ClutDma;
            target.Background = current.Background;
        }

        _composite.SourceWidth = Math.Clamp(field.SourceWidth, 0, VdlLine.MaxPixels);
        _composite.SourceHeight = height;
        _composite.PixelAspect = 1.0;
        return _composite;
    }

    private static void CopyLine(VdlLine source, VdlLine destination)
    {
        var width = Math.Clamp(source.Width, 0, VdlLine.MaxPixels);
        destination.Width = width;
        source.PixelBuffer[..width].CopyTo(destination.PixelBuffer);
        source.ClutB.CopyTo(destination.ClutB);
        source.ClutG.CopyTo(destination.ClutG);
        source.ClutR.CopyTo(destination.ClutR);
        destination.OutControl = source.OutControl;
        destination.ClutDma = source.ClutDma;
        destination.Background = source.Background;
    }

    private static ushort PackDirectColor(byte red, byte green, byte blue)
    {
        var r = Quantize5(red);
        var g = Quantize5(green);
        var b = Quantize5(blue);
        return (ushort)(0x8000 | (r << 10) | (g << 5) | b);
    }

    private static int Quantize5(byte value)
    {
        return Math.Min(31, ((value * 31) + 127) / 255);
    }
}
