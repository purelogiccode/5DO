using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class InputScriptTests
{
    [Fact]
    public void Parse_ReadsFramesButtonsCommentsAndHex()
    {
        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse(
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
        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse("10 A\n5 B\n10 C\n");

        Assert.Equal(2, entries.Count);
        Assert.Equal(5, entries[0].Frame);
        Assert.Equal(ControllerButton.B, entries[0].State.Buttons);
        Assert.Equal(10, entries[1].Frame);
        Assert.Equal(ControllerButton.C, entries[1].State.Buttons);
    }

    [Fact]
    public void Parse_AcceptsStartAndStopAliases()
    {
        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse("0 Start,Stop\n");
        Assert.Equal(ControllerButton.P | ControllerButton.X, entries[0].State.Buttons);

        entries = InputScript.Parse("0 Play\n10 Pause\n20 Stop\n");
        Assert.Equal(ControllerButton.P, entries[0].State.Buttons);
        Assert.Equal(ControllerButton.P, entries[1].State.Buttons);
        Assert.Equal(ControllerButton.X, entries[2].State.Buttons);
    }

    [Fact]
    public void Parse_RejectsUnknownButtons()
    {
        FormatException exception = Assert.Throws<FormatException>(() => InputScript.Parse("0 Turbo\n"));
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
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-input-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "0 None\n30 P\n");
            IReadOnlyList<InputScriptEntry> entries = InputScript.Load(path);
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
    public void Offset_ShiftsFramesForStateRelativeScripts()
    {
        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse("0 A\n20 B\n");
        IReadOnlyList<InputScriptEntry> shifted = InputScript.Offset(entries, 91_000);

        Assert.Equal(91_000, shifted[0].Frame);
        Assert.Equal(ControllerButton.A, shifted[0].State.Buttons);
        Assert.Equal(91_020, shifted[1].Frame);
        Assert.Equal(ControllerButton.B, shifted[1].State.Buttons);
    }

    [Fact]
    public void Offset_RejectsNegativeBase()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InputScript.Offset([], -1));
    }

    [Fact]
    public void AdvanceTo_EncodesBAndLeftTogether()
    {
        var source = new ScriptedPbusSource(InputScript.Parse("0 B,Left\n"));
        source.AdvanceTo(0);
        var packet = new byte[PbusEncoder.PacketSize];
        _ = source.GetPbusData(packet);
        Assert.Equal(0x80, packet[2]);
        Assert.Equal(0x82, packet[3]);
    }

    [Fact]
    public void AdvanceTo_RejectsNegativeFrames()
    {
        var source = new ScriptedPbusSource([]);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.AdvanceTo(-1));
    }
}
