using System;
using System.IO;
using ThreeDo.Input;

namespace ThreeDo.Tests.Input;

public sealed class InputScriptTests
{
    [Fact]
    public void Parse_ReadsFramesButtonsCommentsAndHex()
    {
        var entries = InputScript.Parse(
            """
            # frame  buttons
            0        None
            60       A
            0x78     A, Down
            180      Up+Left|B
            """);

        Assert.Equal(4, entries.Count);
        Assert.Equal(0, entries[0].Frame);
        Assert.Equal(ControllerButton.None, entries[0].State.Buttons);
        Assert.Equal(60, entries[1].Frame);
        Assert.Equal(ControllerButton.A, entries[1].State.Buttons);
        Assert.Equal(120, entries[2].Frame);
        Assert.Equal(ControllerButton.A | ControllerButton.Down, entries[2].State.Buttons);
        Assert.Equal(180, entries[3].Frame);
        Assert.Equal(
            ControllerButton.Up | ControllerButton.Left | ControllerButton.B,
            entries[3].State.Buttons);
    }

    [Fact]
    public void Parse_SortsAndKeepsTheLastDuplicateFrame()
    {
        var entries = InputScript.Parse("10 A\n5 B\n10 C\n");

        Assert.Equal(2, entries.Count);
        Assert.Equal(5, entries[0].Frame);
        Assert.Equal(ControllerButton.B, entries[0].State.Buttons);
        Assert.Equal(10, entries[1].Frame);
        Assert.Equal(ControllerButton.C, entries[1].State.Buttons);
    }

    [Fact]
    public void Parse_RejectsUnknownButtons()
    {
        var exception = Assert.Throws<FormatException>(() => InputScript.Parse("0 Start\n"));
        Assert.Contains("unknown button", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RejectsNegativeAndMalformedFrames()
    {
        Assert.Throws<FormatException>(() => InputScript.Parse("-1 A\n"));
        Assert.Throws<FormatException>(() => InputScript.Parse("frame A\n"));
    }

    [Fact]
    public void Load_ReadsAFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"threedo-input-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "0 None\n30 P\n");
            var entries = InputScript.Load(path);
            Assert.Equal(2, entries.Count);
            Assert.Equal(ControllerButton.P, entries[1].State.Buttons);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AdvanceTo_HoldsTheLastStateAndDrivesPbus()
    {
        var source = new ScriptedPbusSource(InputScript.Parse("10 A\n20 None\n30 Right\n"));

        source.AdvanceTo(0);
        Assert.Equal(ControllerButton.None, source.CurrentState.Buttons);

        source.AdvanceTo(10);
        Assert.Equal(ControllerButton.A, source.CurrentState.Buttons);

        source.AdvanceTo(19);
        Assert.Equal(ControllerButton.A, source.CurrentState.Buttons);

        source.AdvanceTo(20);
        Assert.Equal(ControllerButton.None, source.CurrentState.Buttons);

        source.AdvanceTo(100);
        Assert.Equal(ControllerButton.Right, source.CurrentState.Buttons);

        var packet = new byte[PbusEncoder.PacketSize];
        Assert.Equal(PbusEncoder.PacketSize, source.GetPbusData(packet));
        Assert.Equal(0x84, packet[3]); // Right sets bit 2 of the high button byte; bit 7 is connectivity.
    }

    [Fact]
    public void AdvanceTo_RejectsNegativeFrames()
    {
        var source = new ScriptedPbusSource([]);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.AdvanceTo(-1));
    }
}
