using System;
using System.Diagnostics;
using System.Threading;

namespace FiveDo.Host;

/// <summary>
/// Rolling software input-to-present latency statistics.
/// </summary>
/// <remarks>
/// The probe timestamps the guest PBUS read and the host frame-submit callback. That is the
/// complete latency 5DO controls; the remaining display-scanout and pixel-response time is
/// outside the process and must be measured with a photodiode or a high-speed camera. The
/// counter is lock-free because the core thread records and the UI thread reads.
/// </remarks>
public sealed class LatencyTelemetry
{
    /// <summary>Gets the number of samples retained by the rolling window.</summary>
    public const int WindowSize = 120;

    private readonly double[] _samples = new double[WindowSize];
    private readonly Lock _gate = new();
    private int _count;
    private int _next;
    private double _sum;
    private double _max;
    private double _last;

    /// <summary>Gets the last measured latency in milliseconds.</summary>
    public double LastMilliseconds
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    /// <summary>Gets the mean latency in milliseconds over the retained window.</summary>
    public double MeanMilliseconds
    {
        get
        {
            lock (_gate)
            {
                return _count == 0 ? 0 : _sum / _count;
            }
        }
    }

    /// <summary>Gets the largest latency in milliseconds over the retained window.</summary>
    public double MaxMilliseconds
    {
        get
        {
            lock (_gate)
            {
                return _max;
            }
        }
    }

    /// <summary>Gets the number of retained samples.</summary>
    public int Samples
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>Records one input-poll-to-frame-submit measurement.</summary>
    /// <param name="inputTimestamp">Input poll timestamp from <see cref="Stopwatch.GetTimestamp"/>.</param>
    /// <param name="presentTimestamp">Frame submit timestamp from <see cref="Stopwatch.GetTimestamp"/>.</param>
    /// <param name="frequency">Timestamp frequency. Defaults to <see cref="Stopwatch.Frequency"/>.</param>
    public void Record(long inputTimestamp, long presentTimestamp, long frequency = 0)
    {
        var rate = frequency > 0 ? frequency : Stopwatch.Frequency;
        var elapsed = presentTimestamp - inputTimestamp;
        if (elapsed < 0 || rate <= 0)
        {
            return;
        }

        var milliseconds = elapsed * 1000.0 / rate;
        lock (_gate)
        {
            _last = milliseconds;
            if (_count == WindowSize)
            {
                _sum -= _samples[_next];
            }
            else
            {
                _count++;
            }

            _samples[_next] = milliseconds;
            _sum += milliseconds;
            _next = (_next + 1) % WindowSize;
            if (milliseconds > _max)
            {
                _max = milliseconds;
            }
        }
    }

    /// <summary>Clears every retained sample.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            Array.Clear(_samples);
            _count = 0;
            _next = 0;
            _sum = 0;
            _max = 0;
            _last = 0;
        }
    }
}
