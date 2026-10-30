using System;
using System.IO;
using FiveDo.Audio;
using FiveDo.Core;

namespace FiveDo.Tests.Audio;

public sealed class AudioAnalyzerTests
{
    [Fact]
    public void Statistics_ComputesPeakRmsClippingAndSilence()
    {
        var stats = new AudioStatistics();
        stats.Add(0, 0);
        stats.Add(short.MaxValue, short.MinValue);
        stats.Add(100, -100);
        stats.Add(1000, 1000);

        Assert.Equal(4, stats.Frames);
        Assert.Equal(4 / (double)EmulatorConstants.SoundClock, stats.DurationSeconds, precision: 12);
        Assert.Equal(32768, stats.Peak);
        Assert.Equal(1, stats.ClippedFrames);
        Assert.Equal(1, stats.SilentFrames);
        Assert.Equal(33_867 / 4.0, stats.DcOffsetLeft, precision: 12);
        Assert.Equal(-31_868 / 4.0, stats.DcOffsetRight, precision: 12);

        var expectedSquares = (32767.0 * 32767) + (32768.0 * 32768)
            + (100.0 * 100) + (100.0 * 100) + (1000.0 * 1000) + (1000.0 * 1000);
        Assert.Equal(Math.Sqrt(expectedSquares / 8.0), stats.Rms, precision: 9);
    }

    [Fact]
    public void WavSink_BuffersSamplesAndRoundtripsThroughTheAnalyzer()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-audio-{Guid.NewGuid():N}.wav");
        try
        {
            using (var sink = new WavAudioSink(path))
            {
                sink.PushSample(new AudioSample(0x1234, unchecked((short)0xEDCC)).Packed);
                sink.PushSample(new AudioSample(0, 1).Packed);
                sink.PushSample(new AudioSample(-2, 2).Packed);
                sink.PushSample(new AudioSample(0, 0).Packed);
                Assert.Equal(4, sink.SampleCount);
                Assert.Equal(16, sink.DataBytes);
                Assert.Equal(4, sink.Statistics.Frames);
                Assert.Equal(0x1234, sink.Statistics.Peak);
            }

            AudioAnalysis analysis = AudioAnalyzer.Analyze(path);
            Assert.Equal(44_100, analysis.File.SampleRate);
            Assert.Equal(2, analysis.File.Channels);
            Assert.Equal(16, analysis.File.BitsPerSample);
            Assert.Equal(4, analysis.File.Frames);
            Assert.Equal(16, analysis.File.DataBytes);
            Assert.Equal(4, analysis.Statistics.Frames);
            Assert.Equal(0x1234, analysis.Statistics.Peak);
            Assert.Equal(0, analysis.Statistics.ClippedFrames);
            Assert.Equal(1, analysis.Statistics.SilentFrames);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyzer_ReadsMonoPcmAndSkipsOddSizedChunks()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-audio-{Guid.NewGuid():N}.wav");
        try
        {
            using (FileStream stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write("RIFF"u8);
                writer.Write(0);
                writer.Write("WAVE"u8);

                writer.Write("JUNK"u8);
                writer.Write(3);
                writer.Write(new byte[] { 1, 2, 3 });
                writer.Write((byte)0);

                writer.Write("fmt "u8);
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(44_100);
                writer.Write(88_200);
                writer.Write((short)2);
                writer.Write((short)16);

                writer.Write("data"u8);
                writer.Write(6);
                writer.Write((short)100);
                writer.Write((short)-200);
                writer.Write((short)300);

                var riffSize = (int)stream.Length - 8;
                stream.Position = 4;
                writer.Write(riffSize);
            }

            AudioAnalysis analysis = AudioAnalyzer.Analyze(path);
            Assert.Equal(1, analysis.File.Channels);
            Assert.Equal(3, analysis.File.Frames);
            Assert.Equal(6, analysis.File.DataBytes);
            Assert.Equal(300, analysis.Statistics.Peak);
            Assert.Equal(3, analysis.Statistics.Frames);
            Assert.Equal(0, analysis.Statistics.SilentFrames);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyzer_RejectsTruncatedDataChunk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-audio-{Guid.NewGuid():N}.wav");
        try
        {
            using (FileStream stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write("RIFF"u8);
                writer.Write(36);
                writer.Write("WAVE"u8);
                writer.Write("fmt "u8);
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)2);
                writer.Write(44_100);
                writer.Write(176_400);
                writer.Write((short)4);
                writer.Write((short)16);
                writer.Write("data"u8);
                writer.Write(100);
            }

            Assert.Throws<InvalidDataException>(() => AudioAnalyzer.Analyze(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WavSink_FlushesBufferedDataOnDispose()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-audio-{Guid.NewGuid():N}.wav");
        try
        {
            const int frames = 40_000;
            using (var sink = new WavAudioSink(path))
            {
                for (var i = 0; i < frames; i++)
                {
                    sink.PushSample(new AudioSample((short)(i & 0xFF), (short)(-(i & 0xFF))).Packed);
                }
            }

            AudioAnalysis analysis = AudioAnalyzer.Analyze(path);
            Assert.Equal(frames, analysis.File.Frames);
            Assert.Equal((long)frames * 4, analysis.File.DataBytes);
            Assert.Equal(255, analysis.Statistics.Peak);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
