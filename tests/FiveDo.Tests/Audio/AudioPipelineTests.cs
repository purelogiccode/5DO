using System;
using System.Buffers.Binary;
using System.IO;
using FiveDo.Audio;
using FiveDo.Core;

namespace FiveDo.Tests.Audio;

public sealed class AudioPipelineTests
{
    [Fact]
    public void RingBuffer_PreservesFrameOrderAcrossWrap()
    {
        var ring = new AudioRingBuffer(4);
        var input = new float[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var output = new float[8];

        Assert.Equal(4, ring.Write(input));
        Assert.Equal(4, ring.Read(output));
        Assert.Equal(new float[] { 1, 2, 3, 4, 5, 6, 7, 8 }, output);

        Assert.Equal(4, ring.Write(input));
        Assert.Equal(4, ring.Read(output));
        Assert.Equal(new float[] { 1, 2, 3, 4, 5, 6, 7, 8 }, output);
    }

    [Fact]
    public void RingBuffer_DropsWritesWhenFullAndReturnsZeroWhenEmpty()
    {
        var ring = new AudioRingBuffer(4);
        var input = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        Assert.Equal(4, ring.Write(input));
        Assert.Equal(0, ring.Write(input));
        Assert.Equal(4, ring.FramesAvailable);

        var output = new float[8];
        Assert.Equal(4, ring.Read(output));
        Assert.Equal(0, ring.Read(output));
    }

    [Fact]
    public void RingBuffer_DoesNotAllocateInSteadyState()
    {
        var ring = new AudioRingBuffer(256);
        var input = new float[256];
        var output = new float[256];
        for (var i = 0; i < input.Length; i++)
        {
            input[i] = i;
        }

        for (var i = 0; i < 1000; i++)
        {
            ring.Write(input);
            ring.Read(output);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            ring.Write(input);
            ring.Read(output);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated <= 16_384, $"Ring buffer allocated {allocated} bytes.");
    }

    [Fact]
    public void Pipeline_ConvertsPackedSignedSamplesToNormalizedFloats()
    {
        var pipeline = new AudioPipeline(4);
        pipeline.PushSample(new AudioSample(-32768, 16384).Packed);

        var output = new float[2];
        Assert.Equal(1, pipeline.Read(output));
        Assert.Equal(-1f, output[0]);
        Assert.Equal(0.5f, output[1]);
        Assert.Equal(1, pipeline.SamplesPushed);
        Assert.Equal(0, pipeline.SamplesDropped);
    }

    [Fact]
    public void Pipeline_CountsSamplesDroppedWhenTheRingIsFull()
    {
        var pipeline = new AudioPipeline(1);
        pipeline.PushSample(0);
        pipeline.PushSample(0);

        Assert.Equal(1, pipeline.SamplesPushed);
        Assert.Equal(1, pipeline.SamplesDropped);
    }

    [Fact]
    public void WavSink_WritesHeaderAndPcmSamples()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-audio-{Guid.NewGuid():N}.wav");
        try
        {
            using (var sink = new WavAudioSink(path))
            {
                sink.PushSample(new AudioSample(0x1234, unchecked((short)0xEDCC)).Packed);
                sink.PushSample(new AudioSample(0, 1).Packed);
                Assert.Equal(2, sink.SampleCount);
                Assert.Equal(8, sink.DataBytes);
            }

            var data = File.ReadAllBytes(path);
            Assert.Equal(44 + 8, data.Length);
            Assert.Equal("RIFF"u8.ToArray(), data[..4]);
            Assert.Equal(44 + 8 - 8, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4)));
            Assert.Equal("WAVE"u8.ToArray(), data[8..12]);
            Assert.Equal("fmt "u8.ToArray(), data[12..16]);
            Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(22)));
            Assert.Equal(44100, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(24)));
            Assert.Equal(4, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(32)));
            Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(34)));
            Assert.Equal("data"u8.ToArray(), data[36..40]);
            Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(40)));
            Assert.Equal(0x1234, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(44)));
            Assert.Equal(unchecked((short)0xEDCC), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(46)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NullSink_CountsSamples()
    {
        var sink = new NullAudioSink();
        sink.PushSample(0);
        sink.PushSample(0);
        Assert.Equal(2, sink.SampleCount);
    }
}
