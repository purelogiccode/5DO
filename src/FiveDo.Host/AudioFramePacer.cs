using System;
using System.Diagnostics;

namespace FiveDo.Host;

/// <summary>
/// Derives the core frame period from the number of DSP samples produced by each frame.
/// </summary>
/// <remarks>
/// <para>
/// The 3DO audio clock is the stable timebase: a frame that produced <c>n</c> samples at
/// 44.1 kHz represents <c>n / 44100</c> seconds. Pacing by that value keeps the host audio
/// ring at a steady fill level even when the emulated CPU or DSP cadence differs slightly
/// from the nominal 60 Hz. The approach follows 4DO's worker loop.
/// </para>
/// <para>
/// A short rolling average absorbs single-frame jitter. Pathological deltas (zero samples,
/// a pause, or a state load) reset the window and fall back to 60 Hz; the result is clamped
/// so a malformed sample count cannot stall or accelerate the emulator.
/// </para>
/// </remarks>
public sealed class AudioFramePacer
{
    private const int WindowSize = 30;
    private const int SampleRate = 44_100;
    private const double MinimumFramesPerSecond = 50.0;
    private const double MaximumFramesPerSecond = 70.0;

    private readonly double[] _samplesPerFrame = new double[WindowSize];
    private readonly double _fallbackPeriodTicks;
    private int _count;
    private int _next;
    private long _lastSampleCount;
    private double _sum;
    private bool _hasSampleCount;

    /// <summary>Initializes a new instance of the <see cref="AudioFramePacer"/> class.</summary>
    /// <param name="fallbackFramesPerSecond">Rate used before the first audio measurement.</param>
    public AudioFramePacer(double fallbackFramesPerSecond = 60.0)
    {
        if (fallbackFramesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fallbackFramesPerSecond),
                fallbackFramesPerSecond,
                "The fallback frame rate must be positive.");
        }

        _fallbackPeriodTicks = Stopwatch.Frequency / fallbackFramesPerSecond;
        FramePeriodTicks = _fallbackPeriodTicks;
    }

    /// <summary>Gets the current target frame period in <see cref="Stopwatch"/> ticks.</summary>
    public double FramePeriodTicks { get; private set; }

    /// <summary>Gets the current target frames per second, or 0 before the first measurement.</summary>
    public double FramesPerSecond => FramePeriodTicks > 0
        ? Stopwatch.Frequency / FramePeriodTicks
        : 0;

    /// <summary>Gets the rolling average samples per frame.</summary>
    public double SamplesPerFrame { get; private set; }

    /// <summary>Records the samples produced by one completed frame.</summary>
    /// <param name="totalSamples">Cumulative samples produced by the machine.</param>
    public void FrameCompleted(long totalSamples)
    {
        if (!_hasSampleCount)
        {
            _lastSampleCount = totalSamples;
            _hasSampleCount = true;
            return;
        }

        var delta = totalSamples - _lastSampleCount;
        _lastSampleCount = totalSamples;
        if (delta <= 0 || delta > SampleRate)
        {
            ResetWindow();
            return;
        }

        if (_count == WindowSize)
        {
            _sum -= _samplesPerFrame[_next];
        }
        else
        {
            _count++;
        }

        _samplesPerFrame[_next] = delta;
        _sum += delta;
        _next = (_next + 1) % WindowSize;
        SamplesPerFrame = _sum / _count;

        var framesPerSecond = SampleRate / SamplesPerFrame;
        if (framesPerSecond < MinimumFramesPerSecond || framesPerSecond > MaximumFramesPerSecond)
        {
            FramePeriodTicks = _fallbackPeriodTicks;
            return;
        }

        FramePeriodTicks = Stopwatch.Frequency * (SamplesPerFrame / SampleRate);
    }

    /// <summary>Discards the measurement window and resynchronizes after a pause or reset.</summary>
    /// <param name="totalSamples">Cumulative samples produced by the machine.</param>
    public void Resync(long totalSamples)
    {
        _lastSampleCount = totalSamples;
        _hasSampleCount = true;
        ResetWindow();
    }

    private void ResetWindow()
    {
        _count = 0;
        _next = 0;
        _sum = 0;
        SamplesPerFrame = 0;
        FramePeriodTicks = _fallbackPeriodTicks;
    }
}
