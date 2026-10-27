using System.Diagnostics;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class AudioFramePacerTests
{
    [Fact]
    public void BeforeTheFirstFrame_UsesTheFallbackPeriod()
    {
        var pacer = new AudioFramePacer(60.0);

        Assert.Equal(60.0, pacer.FramesPerSecond, precision: 6);
        Assert.Equal(0, pacer.SamplesPerFrame);
    }

    [Fact]
    public void SevenHundredThirtyFiveSamplesPerFrame_KeepsSixtyHertz()
    {
        var pacer = new AudioFramePacer();
        var samples = 0L;

        for (var i = 0; i < 30; i++)
        {
            samples += 735;
            pacer.FrameCompleted(samples);
        }

        Assert.Equal(735.0, pacer.SamplesPerFrame, precision: 6);
        Assert.Equal(60.0, pacer.FramesPerSecond, precision: 6);
        Assert.InRange(pacer.FramePeriodTicks, (Stopwatch.Frequency / 60.0) - 1, (Stopwatch.Frequency / 60.0) + 1);
    }

    [Fact]
    public void SevenHundredFortySamplesPerFrame_SlowsToTheAudioClock()
    {
        var pacer = new AudioFramePacer();
        var samples = 0L;

        for (var i = 0; i < 30; i++)
        {
            samples += 740;
            pacer.FrameCompleted(samples);
        }

        Assert.Equal(740.0, pacer.SamplesPerFrame, precision: 6);
        Assert.Equal(44_100.0 / 740.0, pacer.FramesPerSecond, precision: 6);
    }

    [Fact]
    public void RollingAverageAbsorbsSingleFrameJitter()
    {
        var pacer = new AudioFramePacer();
        var samples = 0L;

        for (var i = 0; i < 29; i++)
        {
            samples += 735;
            pacer.FrameCompleted(samples);
        }

        samples += 736;
        pacer.FrameCompleted(samples);

        Assert.InRange(pacer.SamplesPerFrame, 735.0, 735.1);
    }

    [Fact]
    public void ZeroSampleDelta_ResetsToTheFallback()
    {
        var pacer = new AudioFramePacer();
        var samples = 0L;
        for (var i = 0; i < 10; i++)
        {
            samples += 735;
            pacer.FrameCompleted(samples);
        }

        pacer.FrameCompleted(samples);

        Assert.Equal(0, pacer.SamplesPerFrame);
        Assert.Equal(60.0, pacer.FramesPerSecond, precision: 6);
    }

    [Fact]
    public void NegativeSampleDelta_FromReset_ResyncsOnTheNextFrame()
    {
        var pacer = new AudioFramePacer();
        pacer.FrameCompleted(1000);
        pacer.FrameCompleted(1735);
        Assert.Equal(735.0, pacer.SamplesPerFrame, precision: 6);

        // A machine reset resets the cumulative counter to zero.
        pacer.FrameCompleted(735);
        Assert.Equal(0, pacer.SamplesPerFrame);

        pacer.FrameCompleted(1470);
        Assert.Equal(735.0, pacer.SamplesPerFrame, precision: 6);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(2000)]
    public void ImplausibleSampleCounts_FallBackToSixtyHertz(int samplesPerFrame)
    {
        var pacer = new AudioFramePacer();
        var samples = 0L;

        for (var i = 0; i < 30; i++)
        {
            samples += samplesPerFrame;
            pacer.FrameCompleted(samples);
        }

        Assert.Equal(60.0, pacer.FramesPerSecond, precision: 6);
    }

    [Fact]
    public void Resync_DiscardsTheMeasurementWindow()
    {
        var pacer = new AudioFramePacer();
        var samples = 0L;
        for (var i = 0; i < 30; i++)
        {
            samples += 740;
            pacer.FrameCompleted(samples);
        }

        pacer.Resync(samples);

        Assert.Equal(0, pacer.SamplesPerFrame);
        Assert.Equal(60.0, pacer.FramesPerSecond, precision: 6);
    }
}
