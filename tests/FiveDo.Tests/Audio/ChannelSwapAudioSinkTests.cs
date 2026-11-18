using System.Collections.Generic;
using FiveDo.Audio;
using FiveDo.Core;

namespace FiveDo.Tests.Audio;

public sealed class ChannelSwapAudioSinkTests
{
    [Fact]
    public void PushSample_SwapsLeftAndRight()
    {
        var inner = new RecordingSink();
        var sink = new ChannelSwapAudioSink(inner);
        var sample = new AudioSample(0x1234, 0x5678);

        sink.PushSample(sample.Packed);

        uint forwarded = Assert.Single(inner.Samples);
        AudioSample result = AudioSample.FromPacked(forwarded);
        Assert.Equal(0x5678, unchecked((ushort)result.Left));
        Assert.Equal(0x1234, unchecked((ushort)result.Right));
    }

    [Fact]
    public void PushSample_LeavesMonoSamplesUnchanged()
    {
        var inner = new RecordingSink();
        var sink = new ChannelSwapAudioSink(inner);
        var sample = new AudioSample(-1234, -1234);

        sink.PushSample(sample.Packed);

        Assert.Equal(sample.Packed, Assert.Single(inner.Samples));
    }

    [Fact]
    public void Reset_ForwardsToTheInnerSink()
    {
        var inner = new RecordingSink();
        var sink = new ChannelSwapAudioSink(inner);

        sink.Reset();

        Assert.Equal(1, inner.Resets);
    }

    [Fact]
    public void HashingBeforeTheSwap_KeepsTheAudioHashUnchanged()
    {
        uint[] samples =
        [
            new AudioSample(100, -100).Packed,
            new AudioSample(200, -200).Packed,
            new AudioSample(-32768, 32767).Packed,
        ];

        var raw = new RecordingSink();
        var rawHash = new HashingAudioSink(raw);
        var swapped = new RecordingSink();
        var swappedHash = new HashingAudioSink(new ChannelSwapAudioSink(swapped));

        foreach (uint sample in samples)
        {
            rawHash.PushSample(sample);
            swappedHash.PushSample(sample);
        }

        Assert.Equal(rawHash.Hash, swappedHash.Hash);
        Assert.Equal(rawHash.SampleCount, swappedHash.SampleCount);
        Assert.Equal(samples.Length, swapped.Samples.Count);
        for (var i = 0; i < samples.Length; i++)
        {
            AudioSample expected = AudioSample.FromPacked(samples[i]);
            AudioSample actual = AudioSample.FromPacked(swapped.Samples[i]);
            Assert.Equal(expected.Right, actual.Left);
            Assert.Equal(expected.Left, actual.Right);
        }

        Assert.NotEqual(raw.Samples[0], swapped.Samples[0]);
    }

    private sealed class RecordingSink : IAudioSink
    {
        internal List<uint> Samples { get; } = [];

        internal int Resets { get; private set; }

        public void PushSample(uint packedSample) => Samples.Add(packedSample);

        public void Reset() => Resets++;
    }
}
