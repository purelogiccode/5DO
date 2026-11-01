using FiveDo.Audio;

namespace FiveDo.Tests.Audio;

public sealed class AudioBufferConfigurationTests
{
    [Theory]
    [InlineData(1, 20)]
    [InlineData(20, 20)]
    [InlineData(100, 100)]
    [InlineData(500, 500)]
    [InlineData(10_000, 500)]
    public void ComputeBufferConfiguration_ClampsTheRequestedLatency(int requested, int expected)
    {
        AudioBufferConfiguration clamped = OpenAlAudioOutput.ComputeBufferConfiguration(20);
        AudioBufferConfiguration actual = OpenAlAudioOutput.ComputeBufferConfiguration(requested);

        Assert.Equal(256, actual.BufferFrames);
        Assert.True(actual.BufferCount >= 4);
        Assert.True(actual.PrefillFrames >= 256);
        Assert.True(actual.RingCapacityFrames >= actual.BufferCount * actual.BufferFrames);
        if (expected == 20)
        {
            Assert.Equal(clamped.BufferCount, actual.BufferCount);
            Assert.Equal(clamped.RingCapacityFrames, actual.RingCapacityFrames);
        }
    }

    [Fact]
    public void ComputeBufferConfiguration_GrowsWithLatency()
    {
        AudioBufferConfiguration low = OpenAlAudioOutput.ComputeBufferConfiguration(20);
        AudioBufferConfiguration medium = OpenAlAudioOutput.ComputeBufferConfiguration(100);
        AudioBufferConfiguration high = OpenAlAudioOutput.ComputeBufferConfiguration(500);

        Assert.True(low.BufferCount < medium.BufferCount);
        Assert.True(medium.BufferCount < high.BufferCount);
        Assert.True(low.RingCapacityFrames < medium.RingCapacityFrames);
        Assert.True(medium.RingCapacityFrames < high.RingCapacityFrames);
        Assert.InRange(high.BufferCount, 4, 32);
    }

    [Fact]
    public void ComputeBufferConfiguration_ProducesWholeMillisecondsOfQueue()
    {
        AudioBufferConfiguration configuration = OpenAlAudioOutput.ComputeBufferConfiguration(100);
        double queuedMilliseconds =
            configuration.BufferCount * configuration.BufferFrames * 1000.0
            / OpenAlAudioOutput.OutputSampleRate;

        Assert.InRange(queuedMilliseconds, 80.0, 120.0);
    }
}
