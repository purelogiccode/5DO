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

    [Fact]
    public void Parse_ReadsMousePointerAndPadButtons()
    {
        IReadOnlyList<InputScriptEntry> entries =
            InputScript.Parse("10 A @mouse 40 -20 left,right\n");

        InputScriptEntry entry = Assert.Single(entries);
        Assert.Equal(10, entry.Frame);
        Assert.Equal(ControllerButton.A, entry.State.Buttons);
        Assert.Equal(InputScriptPointerKind.Mouse, entry.PointerState.Kind);
        Assert.Equal(40, entry.PointerState.Mouse.DeltaX);
        Assert.Equal(-20, entry.PointerState.Mouse.DeltaY);
        Assert.True(entry.PointerState.Mouse.Left);
        Assert.False(entry.PointerState.Mouse.Middle);
        Assert.True(entry.PointerState.Mouse.Right);
    }

    [Fact]
    public void Parse_ReadsLightgunArcadeAndTrackballPointers()
    {
        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse(
            """
            0 @lightgun 160 120 trigger,option,offscreen
            10 @arcadegun 12 34 trigger,service,coins,start,holster
            20 @orbatak 3 -2 start1,start2,coin1,coin2,service
            """);

        Assert.Equal(3, entries.Count);
        Assert.Equal(InputScriptPointerKind.Lightgun, entries[0].PointerState.Kind);
        Assert.Equal(160, entries[0].PointerState.Lightgun.X);
        Assert.Equal(120, entries[0].PointerState.Lightgun.Y);
        Assert.True(entries[0].PointerState.Lightgun.Trigger);
        Assert.True(entries[0].PointerState.Lightgun.Option);
        Assert.True(entries[0].PointerState.Lightgun.OffScreen);

        Assert.Equal(InputScriptPointerKind.ArcadeLightgun, entries[1].PointerState.Kind);
        Assert.True(entries[1].PointerState.ArcadeLightgun.Trigger);
        Assert.True(entries[1].PointerState.ArcadeLightgun.Service);
        Assert.True(entries[1].PointerState.ArcadeLightgun.Coins);
        Assert.True(entries[1].PointerState.ArcadeLightgun.Start);
        Assert.True(entries[1].PointerState.ArcadeLightgun.Holster);

        Assert.Equal(InputScriptPointerKind.Trackball, entries[2].PointerState.Kind);
        Assert.Equal(3, entries[2].PointerState.Trackball.DeltaX);
        Assert.Equal(-2, entries[2].PointerState.Trackball.DeltaY);
        Assert.True(entries[2].PointerState.TrackballButtons.StartP1);
        Assert.True(entries[2].PointerState.TrackballButtons.StartP2);
        Assert.True(entries[2].PointerState.TrackballButtons.CoinP1);
        Assert.True(entries[2].PointerState.TrackballButtons.CoinP2);
        Assert.True(entries[2].PointerState.TrackballButtons.Service);
    }

    [Fact]
    public void Parse_AcceptsNoneForPointerButtons()
    {
        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse("0 @mouse 0 0 None\n");
        InputScriptEntry entry = Assert.Single(entries);
        Assert.Equal(InputScriptPointerKind.Mouse, entry.PointerState.Kind);
        Assert.False(entry.PointerState.Mouse.Left);
        Assert.False(entry.PointerState.Mouse.Right);
    }

    [Fact]
    public void Parse_RejectsMalformedPointers()
    {
        Assert.Throws<FormatException>(() => InputScript.Parse("0 @mouse 1\n"));
        Assert.Throws<FormatException>(() => InputScript.Parse("0 @mouse 1 2 turbo\n"));
        Assert.Throws<FormatException>(() => InputScript.Parse("0 @lightgun a 2\n"));
        Assert.Throws<FormatException>(() => InputScript.Parse("0 @unknown 1 2\n"));
    }

    [Fact]
    public void ScriptedSource_AppliesPointerStateAndClearsMotionAfterSample()
    {
        var source = new ScriptedPbusSource(
            InputScript.Parse("0 @mouse 40 -20 left\n20 @mouse 0 0\n"));
        source.Source.SetDevice(0, InputDeviceType.Mouse);
        source.Source.SetControllerCount(1);

        source.AdvanceTo(0);
        Assert.Equal(InputScriptPointerKind.Mouse, source.CurrentPointerState.Kind);
        Assert.Equal(40, source.Source.Devices.GetMouse(0).DeltaX);
        Assert.Equal(-20, source.Source.Devices.GetMouse(0).DeltaY);
        Assert.True(source.Source.Devices.GetMouse(0).Left);

        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        _ = source.GetPbusData(packet);
        Assert.Equal(0, source.Source.Devices.GetMouse(0).DeltaX);
        Assert.Equal(0, source.Source.Devices.GetMouse(0).DeltaY);
        Assert.True(source.Source.Devices.GetMouse(0).Left);

        source.AdvanceTo(20);
        Assert.False(source.Source.Devices.GetMouse(0).Left);
    }

    [Fact]
    public void ScriptedSource_OnlyAppliesPointerToMatchingPorts()
    {
        var source = new ScriptedPbusSource(InputScript.Parse("0 @lightgun 160 120 trigger\n"));
        source.Source.SetDevice(0, InputDeviceType.Joypad);
        source.Source.SetDevice(1, InputDeviceType.Lightgun);
        source.Source.SetControllerCount(2);

        source.AdvanceTo(0);
        Assert.Equal(LightgunState.Empty, source.Source.Devices.GetLightgun(0));
        Assert.Equal(160, source.Source.Devices.GetLightgun(1).X);
        Assert.Equal(120, source.Source.Devices.GetLightgun(1).Y);
        Assert.True(source.Source.Devices.GetLightgun(1).Trigger);
    }

    [Fact]
    public void Offset_PreservesPointerState()
    {
        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse("0 @mouse 5 -6 left\n");
        IReadOnlyList<InputScriptEntry> shifted = InputScript.Offset(entries, 91_000);

        Assert.Equal(91_000, shifted[0].Frame);
        Assert.Equal(InputScriptPointerKind.Mouse, shifted[0].PointerState.Kind);
        Assert.Equal(5, shifted[0].PointerState.Mouse.DeltaX);
        Assert.Equal(-6, shifted[0].PointerState.Mouse.DeltaY);
        Assert.True(shifted[0].PointerState.Mouse.Left);
    }
}
