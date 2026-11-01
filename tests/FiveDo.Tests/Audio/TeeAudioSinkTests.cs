using System.Collections.Generic;
using FiveDo.Audio;
using FiveDo.Core;

namespace FiveDo.Tests.Audio;

public sealed class TeeAudioSinkTests
{
    [Fact]
    public void PushSample_ForwardsToBothSinks()
    {
        var first = new RecordingSink();
        var second = new RecordingSink();
        var tee = new TeeAudioSink(first, second);

        tee.PushSample(0x12345678);
        tee.PushSample(0x9ABCDEF0);

        Assert.Equal([0x12345678u, 0x9ABCDEF0u], first.Samples);
        Assert.Equal(first.Samples, second.Samples);
    }

    [Fact]
    public void Reset_ForwardsToBothSinks()
    {
        var first = new RecordingSink();
        var second = new RecordingSink();
        var tee = new TeeAudioSink(first, second);

        tee.Reset();

        Assert.Equal(1, first.Resets);
        Assert.Equal(1, second.Resets);
    }

    [Fact]
    public void PushSample_AllowsMissingSinks()
    {
        var tee = new TeeAudioSink(null, null);

        tee.PushSample(1);
        tee.Reset();
    }

    private sealed class RecordingSink : IAudioSink
    {
        internal List<uint> Samples { get; } = [];

        internal int Resets { get; private set; }

        public void PushSample(uint packedSample) => Samples.Add(packedSample);

        public void Reset() => Resets++;
    }
}
