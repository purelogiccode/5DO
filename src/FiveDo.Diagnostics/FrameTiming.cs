using System;

namespace FiveDo.Diagnostics;

/// <summary>Summary statistics for a sequence of per-frame timings in milliseconds.</summary>
/// <param name="MeanMs">Arithmetic mean.</param>
/// <param name="P50Ms">Median (50th percentile).</param>
/// <param name="P95Ms">95th percentile.</param>
/// <param name="P99Ms">99th percentile.</param>
/// <param name="MaxMs">Largest sample.</param>
public readonly record struct FrameTimingSummary(
    double MeanMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxMs);

/// <summary>
/// Calculates stable per-frame timing percentiles for benchmarks and diagnostics.
/// </summary>
/// <remarks>
/// Percentiles use the nearest-rank definition: the smallest sample whose rank is at
/// least <c>ceil(percentile * count)</c>. This is easy to explain, does not interpolate
/// between samples, and reports an actual measured frame time.
/// </remarks>
public static class FrameTiming
{
    /// <summary>Summarizes a set of per-frame timings.</summary>
    /// <param name="samplesMs">Per-frame timings in milliseconds. The span is not modified.</param>
    /// <returns>Mean, p50, p95, p99, and maximum.</returns>
    /// <exception cref="ArgumentException">The sample set is empty.</exception>
    public static FrameTimingSummary Summarize(ReadOnlySpan<double> samplesMs)
    {
        if (samplesMs.IsEmpty)
        {
            throw new ArgumentException("At least one timing sample is required.", nameof(samplesMs));
        }

        var sorted = samplesMs.ToArray();
        Array.Sort(sorted);

        var total = 0.0;
        foreach (var sample in sorted)
        {
            total += sample;
        }

        return new FrameTimingSummary(
            total / sorted.Length,
            PercentileSorted(sorted, 0.50),
            PercentileSorted(sorted, 0.95),
            PercentileSorted(sorted, 0.99),
            sorted[^1]);
    }

    /// <summary>Returns a percentile from an already sorted sample array.</summary>
    /// <param name="sortedSamples">Samples in ascending order.</param>
    /// <param name="percentile">Percentile in the range 0-1.</param>
    /// <returns>The nearest-rank sample.</returns>
    /// <exception cref="ArgumentException">The sample set is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The percentile is outside 0-1.</exception>
    public static double PercentileSorted(ReadOnlySpan<double> sortedSamples, double percentile)
    {
        if (sortedSamples.IsEmpty)
        {
            throw new ArgumentException("At least one timing sample is required.", nameof(sortedSamples));
        }

        if (!double.IsFinite(percentile) || percentile < 0 || percentile > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Percentile must be between 0 and 1.");
        }

        var rank = (int)Math.Ceiling(percentile * sortedSamples.Length);
        if (rank < 1)
        {
            rank = 1;
        }

        return sortedSamples[rank - 1];
    }
}
