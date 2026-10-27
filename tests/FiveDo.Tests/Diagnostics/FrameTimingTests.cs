using System;
using FiveDo.Diagnostics;

namespace FiveDo.Tests.Diagnostics;

public sealed class FrameTimingTests
{
    [Fact]
    public void Summarize_ComputesNearestRankPercentiles()
    {
        var samples = new double[100];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = i + 1;
        }

        FrameTimingSummary summary = FrameTiming.Summarize(samples);

        Assert.Equal(50.5, summary.MeanMs, 6);
        Assert.Equal(50, summary.P50Ms);
        Assert.Equal(95, summary.P95Ms);
        Assert.Equal(99, summary.P99Ms);
        Assert.Equal(100, summary.MaxMs);
    }

    [Fact]
    public void Summarize_DoesNotRequireSortedInput()
    {
        var samples = new[] { 4.0, 1.0, 3.0, 2.0 };

        FrameTimingSummary summary = FrameTiming.Summarize(samples);

        Assert.Equal(2.5, summary.MeanMs, 6);
        Assert.Equal(2, summary.P50Ms);
        Assert.Equal(4, summary.P95Ms);
        Assert.Equal(4, summary.P99Ms);
    }

    [Fact]
    public void PercentileSorted_HandlesBoundaries()
    {
        var samples = new[] { 1.0, 2.0, 3.0 };

        Assert.Equal(1, FrameTiming.PercentileSorted(samples, 0));
        Assert.Equal(3, FrameTiming.PercentileSorted(samples, 1));
        Assert.Equal(2, FrameTiming.PercentileSorted(samples, 0.5));
    }

    [Fact]
    public void Summarize_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => FrameTiming.Summarize(ReadOnlySpan<double>.Empty));
    }

    [Fact]
    public void PercentileSorted_RejectsInvalidPercentile()
    {
        var samples = new[] { 1.0, 2.0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => FrameTiming.PercentileSorted(samples, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameTiming.PercentileSorted(samples, 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameTiming.PercentileSorted(samples, double.NaN));
    }
}
