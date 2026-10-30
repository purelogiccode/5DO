using FiveDo.Audio;

namespace FiveDo.Tests.Audio;

public sealed class AudioSoftLimiterTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(-0.1f)]
    [InlineData(AudioSoftLimiter.Threshold)]
    [InlineData(-AudioSoftLimiter.Threshold)]
    public void Limit_PassesQuietSamplesThroughUntouched(float value)
    {
        Assert.Equal(value, AudioSoftLimiter.Limit(value));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    [InlineData(2f)]
    [InlineData(-2f)]
    public void Limit_KeepsPeaksBelowTheCeiling(float value)
    {
        var limited = AudioSoftLimiter.Limit(value);

        Assert.InRange(limited, -AudioSoftLimiter.Ceiling, AudioSoftLimiter.Ceiling);
    }

    [Fact]
    public void Limit_CompressesPeaksWithoutChangingSign()
    {
        Assert.True(AudioSoftLimiter.Limit(1f) > AudioSoftLimiter.Threshold);
        Assert.True(AudioSoftLimiter.Limit(-1f) < -AudioSoftLimiter.Threshold);
        Assert.True(AudioSoftLimiter.Limit(1f) < 1f);
    }

    [Fact]
    public void Limit_MonotonicAcrossTheCompressionRange()
    {
        var previous = 0f;
        for (var value = 0f; value <= 1f; value += 0.05f)
        {
            var limited = AudioSoftLimiter.Limit(value);
            Assert.True(limited >= previous, $"Limit is not monotonic at {value}: {limited} < {previous}.");
            previous = limited;
        }
    }
}
