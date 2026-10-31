using System;
using FiveDo.Audio;
using FiveDo.Core;

namespace FiveDo.Tests.DiffHarness;

public sealed class PcmHasherTests
{
    [Fact]
    public void Reset_RestoresOffsetBasis()
    {
        var hasher = new PcmHasher();
        hasher.Add(new short[] { 1, -2, 3, -4 });

        Assert.NotEqual(2166136261u, hasher.Hash);

        hasher.Reset();

        Assert.Equal(2166136261u, hasher.Hash);
        Assert.Equal(0, hasher.SampleCount);
    }

    [Fact]
    public void AddStereo_MatchesPackedSamples()
    {
        var batch = new PcmHasher();
        batch.Add(new short[] { 0x0102, 0x0304, -1, -2 });

        var packed = new PcmHasher();
        packed.Add(0x03040102u);
        packed.Add(0xfffeffffu);

        Assert.Equal(packed.Hash, batch.Hash);
        Assert.Equal(packed.SampleCount, batch.SampleCount);
    }

    [Fact]
    public void AddStereo_RejectsPartialFrame()
    {
        var hasher = new PcmHasher();

        Assert.Throws<ArgumentException>(() => hasher.Add(new short[] { 1 }));
    }

    [Fact]
    public void HashingAudioSink_ForwardsAndHashes()
    {
        var inner = new CountingSink();
        var sink = new HashingAudioSink(inner);

        sink.PushSample(0x12345678u);

        Assert.Equal(1, inner.Count);
        Assert.Equal(1, sink.SampleCount);
        Assert.Equal(0x12345678u, inner.Last);
        Assert.NotEqual(2166136261u, sink.Hash);
    }

    private sealed class CountingSink : IAudioSink
    {
        public int Count { get; private set; }

        public uint Last { get; private set; }

        public void PushSample(uint packedSample)
        {
            Count++;
            Last = packedSample;
        }
    }
}
