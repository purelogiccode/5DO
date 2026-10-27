using System;
using System.Threading;
using FiveDo.Audio;
using FiveDo.Core;

namespace FiveDo.Tests.Audio;

public sealed class OpenAlAudioOutputTests
{
    [Fact]
    public void TryCreate_WhenAvailable_PlaysRealTimeStereo()
    {
        // Opening an audio device is opt-in so headless CI does not depend on a sound card.
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_AUDIO_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using OpenAlAudioOutput? output = OpenAlAudioOutput.TryCreate(capacityFrames: 4096);
        Assert.NotNull(output);
        Assert.False(output.DeviceFailed);

        for (var frame = 0; frame < 60; frame++)
        {
            for (var i = 0; i < 735; i++)
            {
                var phase = (frame * 735 + i) * 2.0 * Math.PI * 440.0 / 44_100.0;
                var value = (short)(Math.Sin(phase) * 8000.0);
                output.PushSample(new AudioSample(value, value).Packed);
            }

            Thread.Sleep(16);
        }

        Thread.Sleep(200);
        Assert.False(output.DeviceFailed);
        Assert.True(output.SamplesPushed > 40_000, $"Only {output.SamplesPushed} samples were accepted.");
        Assert.True(output.SamplesDropped < 5_000, $"{output.SamplesDropped} samples were dropped.");
        Assert.True(output.QueuedBuffers > 0, "The OpenAL source has no queued buffers.");
    }
}
