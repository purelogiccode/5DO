using System;
using FiveDo.Core;

namespace FiveDo.Audio;

/// <summary>
/// Streaming peak/RMS/clipping statistics for 44.1 kHz stereo audio.
/// </summary>
/// <remarks>
/// The accumulator is allocation-free and safe to call once per sample from the emulation
/// thread. Reading the properties while another thread is adding samples is not supported.
/// </remarks>
public sealed class AudioStatistics
{
    private long _frames;
    private long _clippedFrames;
    private long _silentFrames;
    private int _peak;
    private double _sumLeftSquares;
    private double _sumRightSquares;
    private double _sumLeft;
    private double _sumRight;

    /// <summary>Gets the number of stereo frames observed.</summary>
    public long Frames => _frames;

    /// <summary>Gets the duration in seconds at 44.1 kHz.</summary>
    public double DurationSeconds => _frames / (double)EmulatorConstants.SoundClock;

    /// <summary>Gets the largest absolute sample value (0-32768).</summary>
    public int Peak => _peak;

    /// <summary>Gets the number of frames where either channel reached full scale.</summary>
    public long ClippedFrames => _clippedFrames;

    /// <summary>Gets the number of frames where both channels were exactly zero.</summary>
    public long SilentFrames => _silentFrames;

    /// <summary>Gets the combined left/right RMS amplitude in sample units.</summary>
    public double Rms => _frames == 0
        ? 0
        : Math.Sqrt((_sumLeftSquares + _sumRightSquares) / (2.0 * _frames));

    /// <summary>Gets the left-channel DC offset in sample units.</summary>
    public double DcOffsetLeft => _frames == 0 ? 0 : _sumLeft / _frames;

    /// <summary>Gets the right-channel DC offset in sample units.</summary>
    public double DcOffsetRight => _frames == 0 ? 0 : _sumRight / _frames;

    /// <summary>Adds one stereo frame.</summary>
    /// <param name="left">Left sample.</param>
    /// <param name="right">Right sample.</param>
    public void Add(short left, short right)
    {
        _frames++;
        _sumLeftSquares += left * (double)left;
        _sumRightSquares += right * (double)right;
        _sumLeft += left;
        _sumRight += right;

        var leftPeak = Math.Abs((int)left);
        var rightPeak = Math.Abs((int)right);
        if (leftPeak > _peak)
        {
            _peak = leftPeak;
        }

        if (rightPeak > _peak)
        {
            _peak = rightPeak;
        }

        if (left is short.MinValue or short.MaxValue || right is short.MinValue or short.MaxValue)
        {
            _clippedFrames++;
        }

        if (left == 0 && right == 0)
        {
            _silentFrames++;
        }
    }

    /// <summary>Resets every counter.</summary>
    public void Reset()
    {
        _frames = 0;
        _clippedFrames = 0;
        _silentFrames = 0;
        _peak = 0;
        _sumLeftSquares = 0;
        _sumRightSquares = 0;
        _sumLeft = 0;
        _sumRight = 0;
    }
}
