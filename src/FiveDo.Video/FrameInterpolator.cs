using System;
using System.Diagnostics;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// Builds an optional intermediate presentation frame from the two most recent completed
/// VDL frames.
/// </summary>
/// <remarks>
/// <para>
/// The 3DO produces 60 fields per second (50 for PAL). Presenting the latest frame more
/// often than that lowers latency but does not add motion. This class keeps the previous
/// and current completed fields and, when asked for a position between them, expands both
/// palettes and averages the result into a reusable VDL frame that uses the fixed 15-bit
/// direct-color path. The existing CPU, OpenGL, Direct3D, and Vulkan palette pipelines
/// already understand that representation, so interpolation needs no renderer change.
/// </para>
/// <para>
/// Blending after two completed fields deliberately lags the newest field by up to one
/// field period. Use it only when the presentation loop runs faster than the field rate;
/// the emulated machine, input sampling, audio clock, and frame hashes are untouched.
/// Interpolated frames carry 5 bits per channel; the endpoint frames are returned
/// unchanged so the native frame keeps its full 8-bit palette.
/// </para>
/// </remarks>
public sealed class FrameInterpolator
{
    /// <summary>Lowest accepted frame interval, in seconds.</summary>
    public const double MinimumIntervalSeconds = 1.0 / 1000.0;

    /// <summary>Highest accepted frame interval, in seconds.</summary>
    public const double MaximumIntervalSeconds = 0.1;

    private VdlFrame _previous = new();
    private VdlFrame _current = new();
    private readonly VdlFrame _blend = new();
    private bool _hasCurrent;
    private bool _hasPair;
    private long _currentTimestamp;
    private double _intervalSeconds;

    /// <summary>Gets a value indicating whether a previous frame is available for blending.</summary>
    public bool HasPair => _hasPair;

    /// <summary>Gets a value indicating whether any frame has been advanced.</summary>
    public bool HasCurrent => _hasCurrent;

    /// <summary>Gets the timestamp of the current frame.</summary>
    public long CurrentTimestamp => _currentTimestamp;

    /// <summary>Gets the estimated interval between the two retained frames, in seconds.</summary>
    public double IntervalSeconds => _intervalSeconds;

    /// <summary>
    /// Advances to a newly completed frame and retains the previous one for blending.
    /// </summary>
    /// <param name="frame">Newly completed frame. It is copied before this method returns.</param>
    /// <param name="timestamp">Frame arrival timestamp from <see cref="Stopwatch.GetTimestamp"/>.</param>
    /// <param name="intervalTicks">
    /// Optional interval between source frame submissions, in <see cref="Stopwatch"/>
    /// ticks. Pass zero to measure the interval from consecutive <paramref name="timestamp"/>
    /// values instead. Successive values are smoothed.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="frame"/> is null.</exception>
    public void Advance(VdlFrame frame, long timestamp, long intervalTicks = 0)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (intervalTicks > 0)
        {
            var seconds = intervalTicks / (double)Stopwatch.Frequency;
            if (_intervalSeconds > 0)
            {
                // Smooth successive submissions so a burst after a stall does not
                // collapse the blend interval for the next few frames.
                seconds = (_intervalSeconds * 0.75) + (seconds * 0.25);
            }

            _intervalSeconds = Math.Clamp(
                seconds,
                MinimumIntervalSeconds,
                MaximumIntervalSeconds);
        }
        else if (_hasCurrent)
        {
            var delta = timestamp - _currentTimestamp;
            if (delta > 0)
            {
                var seconds = delta / (double)Stopwatch.Frequency;
                _intervalSeconds = Math.Clamp(
                    seconds,
                    MinimumIntervalSeconds,
                    MaximumIntervalSeconds);
            }
        }

        if (_hasCurrent)
        {
            // Swap the two retained buffers so the old current frame becomes the previous
            // frame without copying it. The old previous buffer is then reused for the copy.
            (_previous, _current) = (_current, _previous);
            _hasPair = IsCompatible(_previous, frame);
        }
        else
        {
            _hasPair = false;
        }

        _current.CopyFrom(frame);
        _currentTimestamp = timestamp;
        _hasCurrent = true;
    }

    /// <summary>Gets the blend position for a presentation timestamp.</summary>
    /// <param name="timestamp">Presentation timestamp from <see cref="Stopwatch.GetTimestamp"/>.</param>
    /// <returns>
    /// Zero at the current frame's arrival, one after one estimated frame interval, and
    /// clamped to that range. Returns one when no pair is available.
    /// </returns>
    public double PositionAt(long timestamp)
    {
        if (!_hasPair || _intervalSeconds <= 0)
        {
            return 1.0;
        }

        var elapsed = (timestamp - _currentTimestamp) / (double)Stopwatch.Frequency;
        return Math.Clamp(elapsed / _intervalSeconds, 0.0, 1.0);
    }

    /// <summary>Gets the number of blended frames produced since the last reset.</summary>
    public long BlendedFrames { get; private set; }

    /// <summary>Gets a value indicating whether the last <see cref="FrameAt"/> call produced a blend.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Public presenter telemetry consumed by the desktop video controls.")]
    public bool LastFrameBlended { get; private set; }

    /// <summary>
    /// Selects the frame to present at a blend position.
    /// </summary>
    /// <param name="position">
    /// Zero presents the previous frame, one presents the current frame, and values in
    /// between produce a blended frame.
    /// </param>
    /// <returns>
    /// The previous or current frame when an endpoint is selected, a reusable blended
    /// frame for intermediate positions, or <see langword="null"/> before the first
    /// <see cref="Advance"/> call.
    /// </returns>
    /// <remarks>
    /// The returned frame is valid until the next call. It must not be retained or
    /// modified by the caller.
    /// </remarks>
    public VdlFrame? FrameAt(double position)
    {
        LastFrameBlended = false;
        if (!_hasCurrent)
        {
            return null;
        }

        if (!_hasPair)
        {
            return _current;
        }

        if (!double.IsFinite(position))
        {
            return _current;
        }

        if (position <= 0)
        {
            return _previous;
        }

        if (position >= 1)
        {
            return _current;
        }

        Blend(position);
        BlendedFrames++;
        LastFrameBlended = true;
        return _blend;
    }

    /// <summary>Drops the retained frames and timing history.</summary>
    public void Reset()
    {
        _hasCurrent = false;
        _hasPair = false;
        _currentTimestamp = 0;
        _intervalSeconds = 0;
        BlendedFrames = 0;
        LastFrameBlended = false;
    }

    private static bool IsCompatible(VdlFrame previous, VdlFrame current)
    {
        return previous.SourceWidth == current.SourceWidth
            && previous.SourceHeight == current.SourceHeight
            && Math.Abs(previous.PixelAspect - current.PixelAspect) < 1e-9
            && current.SourceWidth > 0
            && current.SourceHeight > 0;
    }

    private void Blend(double position)
    {
        var weight = (int)Math.Round(position * 256.0);
        var inverse = 256 - weight;
        var height = Math.Clamp(_current.SourceHeight, 0, VdlFrame.MaxLines);
        for (var y = 0; y < height; y++)
        {
            VdlLine current = _current[y];
            VdlLine previous = _previous[y];
            VdlLine destination = _blend[y];
            var width = Math.Clamp(current.Width, 0, VdlLine.MaxPixels);
            var shared = Math.Min(width, Math.Clamp(previous.Width, 0, VdlLine.MaxPixels));

            for (var x = 0; x < shared; x++)
            {
                FrameAssembler.ExpandPixel(previous, previous.PixelBuffer[x], out var pr, out var pg, out var pb);
                FrameAssembler.ExpandPixel(current, current.PixelBuffer[x], out var cr, out var cg, out var cb);
                destination.PixelBuffer[x] = FrameAssembler.PackDirectColor(
                    BlendComponent(pr, cr, inverse, weight),
                    BlendComponent(pg, cg, inverse, weight),
                    BlendComponent(pb, cb, inverse, weight));
            }

            // A line that only exists in the current frame keeps the current color. The
            // target line still uses direct color, so it must be encoded the same way.
            for (var x = shared; x < width; x++)
            {
                FrameAssembler.ExpandPixel(current, current.PixelBuffer[x], out var r, out var g, out var b);
                destination.PixelBuffer[x] = FrameAssembler.PackDirectColor(r, g, b);
            }

            destination.Width = width;
            current.ClutB.CopyTo(destination.ClutB);
            current.ClutG.CopyTo(destination.ClutG);
            current.ClutR.CopyTo(destination.ClutR);
            destination.OutControl = current.OutControl | FrameAssembler.FixedClutEnable;
            destination.ClutDma = current.ClutDma;
            destination.Background = current.Background;
        }

        _blend.SourceWidth = _current.SourceWidth;
        _blend.SourceHeight = height;
        _blend.PixelAspect = _current.PixelAspect;
    }

    private static byte BlendComponent(byte previous, byte current, int inverse, int weight)
    {
        return (byte)(((previous * inverse) + (current * weight) + 128) >> 8);
    }
}
