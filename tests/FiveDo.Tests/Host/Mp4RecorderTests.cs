using System;
using System.IO;
using System.Text;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class Mp4RecorderTests
{
    [Fact]
    public void Mp4Recorder_WritesVideoAndAudio()
    {
        if (Mp4Recorder.FindFfmpeg() is null)
        {
            return;
        }

        var path = Path.Combine(
            Path.GetTempPath(),
            "5do-mp4-test-" + Guid.NewGuid().ToString("N") + ".mp4");
        try
        {
            using var recorder = Mp4Recorder.Start(path, 59.94, 60_000, 1_001);
            VdlFrame frame = MakeFrame();
            for (var i = 0; i < 10; i++)
            {
                recorder.OnFrame(frame);
            }

            for (var i = 0; i < 8_000; i++)
            {
                recorder.AudioSink.PushSample(0);
            }

            recorder.Complete();

            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 2_000);
            Assert.Equal("ftyp", Encoding.ASCII.GetString(bytes, 4, 4));
            Assert.Equal(10, recorder.CapturedFrames);
            Assert.Equal(320, recorder.Width);
            Assert.Equal(240, recorder.Height);
            Assert.Null(recorder.Failure);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Mp4Recorder_ReportsMissingFrames()
    {
        if (Mp4Recorder.FindFfmpeg() is null)
        {
            return;
        }

        using var recorder = Mp4Recorder.Start(
            Path.Combine(Path.GetTempPath(), "5do-mp4-empty.mp4"),
            59.94,
            60_000,
            1_001);

        Assert.Throws<InvalidOperationException>(() => recorder.Complete());
    }

    private static VdlFrame MakeFrame()
    {
        var frame = new VdlFrame { SourceWidth = 320, SourceHeight = 240 };
        for (var y = 0; y < 240; y++)
        {
            VdlLine line = frame[y];
            line.Width = 320;
            for (var i = 0; i < VdlLine.ClutSize; i++)
            {
                line.ClutR[i] = (byte)(i * 7);
                line.ClutG[i] = (byte)(i * 5);
                line.ClutB[i] = (byte)(i * 3);
            }

            for (var x = 0; x < 320; x++)
            {
                line.PixelBuffer[x] = (ushort)(((x * 3) & 0x1f) << 10
                    | ((y * 5) & 0x1f) << 5
                    | ((x + y) & 0x1f));
            }
        }

        return frame;
    }
}
