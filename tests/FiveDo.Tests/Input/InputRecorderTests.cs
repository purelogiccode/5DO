using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class InputRecorderTests
{
    [Fact]
    public void Record_CoalescesRepeatedStatesAndFormatsEveryButton()
    {
        var recorder = new InputRecorder();
        recorder.Record(0, ControllerState.Empty);
        recorder.Record(1, ControllerState.Empty);
        recorder.Record(10, new ControllerState(ControllerButton.A | ControllerButton.Left));
        recorder.Record(11, new ControllerState(ControllerButton.A | ControllerButton.Left));
        recorder.Record(20, ControllerState.Empty);

        Assert.Equal(3, recorder.EntryCount);
        Assert.Equal(21, recorder.Frames);

        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse(recorder.ToScript());
        Assert.Equal(3, entries.Count);
        Assert.Equal(0, entries[0].Frame);
        Assert.Equal(ControllerButton.None, entries[0].State.Buttons);
        Assert.Equal(10, entries[1].Frame);
        Assert.Equal(ControllerButton.A | ControllerButton.Left, entries[1].State.Buttons);
        Assert.Equal(20, entries[2].Frame);
        Assert.Equal(ControllerButton.None, entries[2].State.Buttons);
    }

    [Fact]
    public void Frames_MeasuresTheRecordedSpanFromANonZeroStart()
    {
        var recorder = new InputRecorder();
        recorder.Record(5000, ControllerState.Empty);
        recorder.Record(5005, new ControllerState(ControllerButton.A));

        Assert.Equal(6, recorder.Frames);
        Assert.Equal(2, recorder.EntryCount);
    }

    [Fact]
    public void Record_RejectsOutOfOrderFrames()
    {
        var recorder = new InputRecorder();
        recorder.Record(10, ControllerState.Empty);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => recorder.Record(9, new ControllerState(ControllerButton.A)));
    }

    [Fact]
    public void FormatButtons_UsesScriptNamesInStableOrder()
    {
        Assert.Equal("None", InputRecorder.FormatButtons(ControllerButton.None));
        Assert.Equal(
            "A,B,C,P,X,L,R,Up,Down,Left,Right",
            InputRecorder.FormatButtons(
                ControllerButton.A
                | ControllerButton.B
                | ControllerButton.C
                | ControllerButton.P
                | ControllerButton.X
                | ControllerButton.L
                | ControllerButton.R
                | ControllerButton.Up
                | ControllerButton.Down
                | ControllerButton.Left
                | ControllerButton.Right));
    }

    [Fact]
    public void Save_WritesAScriptThatRoundtrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-recording-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "input.txt");
        try
        {
            var recorder = new InputRecorder();
            recorder.Record(0, ControllerState.Empty);
            recorder.Record(5, new ControllerState(ControllerButton.A));
            recorder.Save(path);

            IReadOnlyList<InputScriptEntry> entries = InputScript.Load(path);
            Assert.Equal(2, entries.Count);
            Assert.Equal(5, entries[1].Frame);
            Assert.Equal(ControllerButton.A, entries[1].State.Buttons);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void InputManager_RecordsPlayerOneStateAtTheProvidedFrame()
    {
        InputProfile profile = InputProfile.CreateDefault();
        var manager = new InputManager(profile);
        var recorder = new InputRecorder();
        manager.Recorder = recorder;
        manager.FrameProvider = () => 7;

        manager.Keyboard.SetKey("Z", pressed: true);
        Span<byte> packet = stackalloc byte[PbusEncoder.PacketSize];
        manager.GetPbusData(packet);

        Assert.Equal(1, recorder.EntryCount);
        IReadOnlyList<InputScriptEntry> entries = InputScript.Parse(recorder.ToScript());
        Assert.Equal(7, entries[0].Frame);
        Assert.Equal(ControllerButton.A, entries[0].State.Buttons);
    }
}
