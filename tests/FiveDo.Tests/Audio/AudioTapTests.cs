using System;
using System.Buffers.Binary;
using System.IO;
using FiveDo.Audio;

namespace FiveDo.Tests.Audio;

public sealed class AudioTapTests
{
    [Fact]
    public void Tap_WritesQueuedPcmAndTimingLog()
    {
        var prefix = Path.Combine(Path.GetTempPath(), $"fivedo-tap-{Guid.NewGuid():N}");
        try
        {
            using (var tap = new AudioTap(prefix))
            {
                short[] samples = [100, -100, 200, -200];
                tap.Write(
                    samples,
                    frames: 2,
                    ringFrames: 10,
                    queuedBuffers: 1,
                    queuedFrames: 2,
                    underrun: false,
                    peak: 0.5f,
                    openAlError: null);
                tap.Write(
                    samples,
                    frames: 2,
                    ringFrames: 0,
                    queuedBuffers: 0,
                    queuedFrames: 0,
                    underrun: true,
                    peak: 0f,
                    openAlError: "InvalidOperation");
                Assert.Equal(2, tap.BuffersWritten);
            }

            var wav = File.ReadAllBytes(prefix + ".wav");
            Assert.Equal(44 + 16, wav.Length);
            Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
            Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
            Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40)));
            Assert.Equal(100, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44)));
            Assert.Equal(-100, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(46)));
            Assert.Equal(200, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(48)));
            Assert.Equal(-200, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(50)));

            var csv = File.ReadAllLines(prefix + ".csv");
            Assert.Equal(3, csv.Length);
            Assert.Equal(
                "elapsedMs,buffer,frames,ringFrames,queuedBuffers,queuedFrames,underrun,peak,openAlError",
                csv[0]);
            Assert.Contains(",2,10,1,2,0,0.5000,", csv[1]);
            Assert.Contains(",2,0,0,0,1,0.0000,InvalidOperation", csv[2]);
        }
        finally
        {
            File.Delete(prefix + ".wav");
            File.Delete(prefix + ".csv");
        }
    }
}
