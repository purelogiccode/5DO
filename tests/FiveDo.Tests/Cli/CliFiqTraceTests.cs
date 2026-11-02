using System;
using FiveDo.Cli;
using FiveDo.Core;

namespace FiveDo.Tests.Cli;

public sealed class CliFiqTraceTests
{
    [Fact]
    public void RequestLine_CarriesFrameCyclePcAndReasonMasks()
    {
        var line = Program.FormatFiqRequest(
            7,
            0x1234,
            new FiqRequest(123, 0x00000400, 0x00000001));

        Assert.Equal(
            "f=7 cycle=123 kind=request pc=00001234 reason1=00000400 reason2=00000001",
            line);
    }

    [Fact]
    public void EntryLine_CarriesLatencyAndMatchFlag()
    {
        var line = Program.FormatFiqEntry(
            7,
            new FiqEntry(130, 0x00001008, 0x00000400, 0x00000001, 7, true));

        Assert.Equal(
            "f=7 cycle=130 kind=entry pc=00001008 reason1=00000400 reason2=00000001 "
            + "latency=7 matched=1",
            line);
    }

    [Fact]
    public void EntryLine_MarksUnmatchedReentries()
    {
        var line = Program.FormatFiqEntry(
            9,
            new FiqEntry(300, 0x00001008, 0x00000400, 0, 0, false));

        Assert.EndsWith("latency=0 matched=0", line, StringComparison.Ordinal);
    }
}
