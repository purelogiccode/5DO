using System;
using ThreeDo.Core;
using ThreeDo.Video;

namespace ThreeDo.Tests.VideoRendering;

public sealed class VdlFrameMailboxTests
{
    [Fact]
    public void Mailbox_CopiesFrameUnderLock()
    {
        var frame = new VdlFrame { SourceWidth = 4, SourceHeight = 2 };
        frame[0].Width = 4;
        frame[0].Pixels[0] = 7;
        frame[0].Background = 0x1234;
        frame[0].ClutR[0] = 9;

        var mailbox = new VdlFrameMailbox();
        mailbox.Write(frame);
        frame[0].Pixels[0] = 99;

        VdlFrame? observed = null;
        mailbox.Read(snapshot => observed = snapshot);

        Assert.NotNull(observed);
        Assert.Equal(7, observed[0].Pixels[0]);
        Assert.Equal(0x1234u, observed[0].Background);
        Assert.Equal(9, observed[0].ClutR[0]);
        Assert.Equal(1, mailbox.Sequence);
    }

    [Fact]
    public void Mailbox_ReusesSnapshotAcrossWrites()
    {
        var mailbox = new VdlFrameMailbox();
        var frame = new VdlFrame { SourceWidth = 2, SourceHeight = 1 };
        frame[0].Width = 2;
        frame[0].Pixels[0] = 1;
        mailbox.Write(frame);
        frame[0].Pixels[0] = 2;
        mailbox.Write(frame);

        VdlFrame? observed = null;
        mailbox.Read(snapshot => observed = snapshot);
        Assert.Equal(2, observed![0].Pixels[0]);
        Assert.Equal(2, mailbox.Sequence);
    }
}
