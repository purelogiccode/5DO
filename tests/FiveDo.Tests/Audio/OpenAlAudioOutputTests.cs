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

        PushTone(output);

        Thread.Sleep(200);
        Assert.False(output.DeviceFailed);
        Assert.True(output.SamplesPushed > 40_000, $"Only {output.SamplesPushed} samples were accepted.");
        Assert.True(output.SamplesDropped < 5_000, $"{output.SamplesDropped} samples were dropped.");
        Assert.True(
            WaitForQueuedBuffers(output),
            "The OpenAL source has no queued buffers.");
    }

    [Fact]
    public void ReplacingOutput_WhileAnotherIsOpen_KeepsTheReplacementAlive()
    {
        // Opening an audio device is opt-in so headless CI does not depend on a sound card.
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_AUDIO_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        OpenAlAudioOutput? previous = OpenAlAudioOutput.TryCreate(100, deviceName: null, out _);
        Assert.NotNull(previous);
        OpenAlAudioOutput? replacement = OpenAlAudioOutput.TryCreate(100, deviceName: null, out _);
        Assert.NotNull(replacement);
        using (replacement)
        using (previous)
        {
            // Let the replacement fill thread establish its context before the old output
            // is retired, mirroring a live switch in the settings dialog.
            Thread.Sleep(100);
            previous.Dispose();

            PushTone(replacement);

            Thread.Sleep(200);
            Assert.False(replacement.DeviceFailed);
            Assert.Equal(0, replacement.AlErrors);
            Assert.True(replacement.SamplesPushed > 40_000, $"Only {replacement.SamplesPushed} samples were accepted.");
            Assert.True(replacement.SamplesDropped < 5_000, $"{replacement.SamplesDropped} samples were dropped.");
            Assert.True(
                WaitForQueuedBuffers(replacement),
                "The replacement OpenAL source has no queued buffers.");
        }
    }

    [Fact]
    public void FullyQueuedTone_DoesNotUnderrun()
    {
        // Opening an audio device is opt-in so headless CI does not depend on a sound card.
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_AUDIO_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using OpenAlAudioOutput? output = OpenAlAudioOutput.TryCreate(500, deviceName: null, out _);
        Assert.NotNull(output);

        // The Settings test tone queues its whole second up front so Windows sleep
        // granularity cannot drain the ring and insert silent filler blocks.
        const int frames = 44_100;
        for (var i = 0; i < frames; i++)
        {
            var phase = i * 2.0 * Math.PI * 440.0 / 44_100.0;
            var value = (short)(Math.Sin(phase) * 6000.0);
            output.PushSample(new AudioSample(value, value).Packed);
        }

        Thread.Sleep(500);
        Assert.False(output.DeviceFailed);
        Assert.Equal(0, output.AlErrors);
        Assert.Equal(0, output.UnderrunBuffers);
        Assert.Equal(0, output.SamplesDropped);
        Assert.True(output.QueuedBuffers > 0, "The OpenAL source has no queued buffers.");
    }

    private static bool WaitForQueuedBuffers(OpenAlAudioOutput output)
    {
        for (var attempt = 0; attempt < 250; attempt++)
        {
            if (output.QueuedBuffers > 0)
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return output.QueuedBuffers > 0;
    }

    private static void PushTone(OpenAlAudioOutput output)
    {
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
    }
}
