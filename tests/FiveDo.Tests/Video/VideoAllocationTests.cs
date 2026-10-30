using System;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Tests.Video;

[CollectionDefinition("Allocation", DisableParallelization = true)]
public sealed class AllocationGroup;

[Collection("Allocation")]
public sealed class VideoAllocationTests
{
    [Fact]
    public void Uploader_DoesNotAllocateInSteadyState()
    {
        VdlFrame frame = MakeFrame();
        var uploader = new VdlFrameUploader();
        for (var i = 0; i < 200; i++)
        {
            uploader.Update(frame);
        }

        // Discard one measured window so tiered JIT/OSR and monitor setup settle
        // before the steady-state assertion.
        for (var i = 0; i < 60; i++)
        {
            uploader.Update(frame);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            uploader.Update(frame);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Mailbox_DoesNotAllocateInSteadyState()
    {
        VdlFrame frame = MakeFrame();
        var mailbox = new VdlFrameMailbox();
        var destination = new VdlFrame();
        Action<VdlFrame> reader = _ => { };
        for (var i = 0; i < 200; i++)
        {
            mailbox.Write(frame);
            mailbox.Read(destination, reader);
        }

        for (var i = 0; i < 60; i++)
        {
            mailbox.Write(frame);
            mailbox.Read(destination, reader);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            mailbox.Write(frame);
            mailbox.Read(destination, reader);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Hqx_DoesNotAllocateInSteadyState()
    {
        var source = new FrameBuffer(320, 240);
        var destination = new FrameBuffer(640, 480);
        var filter = new HqxFilter();
        for (var i = 0; i < 200; i++)
        {
            filter.Expand(source, destination);
        }

        for (var i = 0; i < 60; i++)
        {
            filter.Expand(source, destination);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            filter.Expand(source, destination);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static VdlFrame MakeFrame()
    {
        var frame = new VdlFrame { SourceWidth = 320, SourceHeight = 240 };
        for (var y = 0; y < 240; y++)
        {
            VdlLine line = frame[y];
            line.Width = 320;
            line.Background = (uint)y;
            line.OutControl = (uint)y << 16;
            for (var x = 0; x < 320; x++)
            {
                line.Pixels[x] = (ushort)(x + y);
            }
        }

        return frame;
    }
}
