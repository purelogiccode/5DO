using System.IO;
using System.Text;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class GifRecorderTests
{
    [Fact]
    public void Recorder_WritesHeaderTrailerAndEverySecondFrame()
    {
        var machine = new FiveDoMachine();
        using var stream = new MemoryStream();
        using var recorder = new GifRecorder(stream, sourceFramesPerSecond: 60, targetFramesPerSecond: 30);
        machine.FrameSink = recorder;

        machine.RunFrames(6);
        recorder.Complete();

        Assert.Equal(3, recorder.CapturedFrames);
        Assert.Equal(320, recorder.Width);
        Assert.Equal(240, recorder.Height);
        Assert.Equal(30, recorder.CaptureFramesPerSecond);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 32);
        Assert.Equal("GIF89a", Encoding.ASCII.GetString(bytes, 0, 6));
        Assert.Equal(0x3B, bytes[^1]);
        Assert.Equal((byte)(320 & 0xFF), bytes[6]);
        Assert.Equal((byte)1, bytes[7]);
        Assert.Equal((byte)(240 & 0xFF), bytes[8]);
    }

    [Fact]
    public void Recorder_ClampsTheTargetRateAndSupportsAFrameLimit()
    {
        using var stream = new MemoryStream();
        using var recorder = new GifRecorder(stream, sourceFramesPerSecond: 50, targetFramesPerSecond: 200, maxFrames: 2);

        Assert.Equal(50, recorder.CaptureFramesPerSecond);

        var machine1 = new FiveDoMachine();
        machine1.FrameSink = recorder;
        machine1.RunFrames(5);

        Assert.Equal(2, recorder.CapturedFrames);
    }

    [Fact]
    public void Recorder_RescalesFramesWhoseSizeChanges()
    {
        using var stream = new MemoryStream();
        using var recorder = new GifRecorder(stream, sourceFramesPerSecond: 50, targetFramesPerSecond: 50);
        var first = new VdlFrame { SourceWidth = 320, SourceHeight = 240 };
        var second = new VdlFrame { SourceWidth = 384, SourceHeight = 288 };

        recorder.OnFrame(first);
        Assert.Equal(1, recorder.CapturedFrames);
        recorder.OnFrame(second);
        Assert.Equal(2, recorder.CapturedFrames);
        Assert.Equal(320, recorder.Width);
        Assert.Equal(240, recorder.Height);
        recorder.Complete();

        var bytes = stream.ToArray();
        Assert.Equal("GIF89a", Encoding.ASCII.GetString(bytes, 0, 6));
        Assert.Equal((byte)(320 & 0xFF), bytes[6]);
        Assert.Equal((byte)(240 & 0xFF), bytes[8]);
    }

    [Fact]
    public void Recorder_HandlesAWovenMachineFrame()
    {
        var machine = new FiveDoMachine { Deinterlace = DeinterlaceMode.Weave };
        using var stream = new MemoryStream();
        using var recorder = new GifRecorder(stream, sourceFramesPerSecond: 60, targetFramesPerSecond: 30);
        machine.FrameSink = recorder;

        machine.RunFrames(6);
        recorder.Complete();

        Assert.True(recorder.CapturedFrames >= 2);
        Assert.Equal(320, recorder.Width);
        Assert.Equal(480, recorder.Height);
    }

    [Fact]
    public void Recorder_CompleteWithoutFramesWritesAnEmptyGif()
    {
        using var stream = new MemoryStream();
        using var recorder = new GifRecorder(stream, sourceFramesPerSecond: 60);

        recorder.Complete();
        var bytes = stream.ToArray();

        Assert.Equal("GIF89a", Encoding.ASCII.GetString(bytes, 0, 6));
        Assert.Equal(0x3B, bytes[^1]);
        Assert.Equal(0, recorder.CapturedFrames);
    }

}
