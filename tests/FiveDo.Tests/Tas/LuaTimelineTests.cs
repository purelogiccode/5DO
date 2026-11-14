using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Scripting;
using FiveDo.Tas;
using FiveDo.Video;

namespace FiveDo.Tests.Tas;

public sealed class LuaTimelineTests
{
    [Fact]
    public void Compile_EmitsFrameHoldPressAndReleaseEntries()
    {
        IReadOnlyList<InputScriptEntry> entries = LuaInputScript.Compile(
            """
            frame(0, "A")
            frame(5, "B")
            frame(10, "None")
            hold(20, 24, "Down")
            press(40, "P", 2)
            release(50)
            """);

        Assert.Equal(0, entries[0].Frame);
        Assert.True(entries[0].State.IsDown(ControllerButton.A));
        Assert.Equal(5, entries[1].Frame);
        Assert.True(entries[1].State.IsDown(ControllerButton.B));
        Assert.Equal(10, entries[2].Frame);
        Assert.False(entries[2].State.IsPressed);
        Assert.Equal(20, entries[3].Frame);
        Assert.True(entries[3].State.IsDown(ControllerButton.Down));
        Assert.Equal(25, entries[4].Frame);
        Assert.False(entries[4].State.IsPressed);
        Assert.Equal(40, entries[5].Frame);
        Assert.True(entries[5].State.IsDown(ControllerButton.P));
        Assert.Equal(42, entries[6].Frame);
        Assert.False(entries[6].State.IsPressed);
        Assert.Equal(7, entries.Count);
    }

    [Fact]
    public void Compile_SupportsLuaLoopsAndLocals()
    {
        IReadOnlyList<InputScriptEntry> entries = LuaInputScript.Compile(
            """
            local buttons = { "A", "B", "C" }
            local frameNumber = 10
            for i = 1, 3 do
              frame(frameNumber, buttons[i])
              frameNumber = frameNumber + 10
            end
            """);

        Assert.Equal(3, entries.Count);
        Assert.Equal(10, entries[0].Frame);
        Assert.True(entries[0].State.IsDown(ControllerButton.A));
        Assert.Equal(20, entries[1].Frame);
        Assert.True(entries[1].State.IsDown(ControllerButton.B));
        Assert.Equal(30, entries[2].Frame);
        Assert.True(entries[2].State.IsDown(ControllerButton.C));
    }

    [Fact]
    public void Compile_RejectsUnknownButtonsAndNegativeFrames()
    {
        Assert.Throws<FormatException>(() => LuaInputScript.Compile("frame(1, 'Nope')"));
        Assert.Throws<FormatException>(() => LuaInputScript.Compile("frame(-1, 'A')"));
        Assert.Throws<FormatException>(() => LuaInputScript.Compile("os.time()"));
    }

    [Fact]
    public void LuaAuthoredMovie_ReplaysDeterministically()
    {
        IReadOnlyList<InputScriptEntry> entries = LuaInputScript.Compile(
            """
            frame(0, "A")
            frame(2, "B")
            frame(4, "None")
            """);
        Movie movie = Record(entries, 8);

        FiveDoMachine replay = new();
        var playback = new MoviePbusSource(movie);
        replay.PbusSource = playback;
        replay.RunFrames(movie.FrameCount);

        Assert.Equal(movie.Header.FinalFrameHash, FrameHasher.Hash(replay.Frame));
        Assert.Equal(movie.Header.FinalProgramCounter, replay.Cpu.ProgramCounter);
        Assert.Equal(movie.Header.FinalStateHash, HashState(replay));
        Assert.Equal(movie.FrameCount, playback.Position);
    }

    [Fact]
    public void Timeline_RoundTripsMovieInputAndScript()
    {
        IReadOnlyList<InputScriptEntry> entries =
        [
            new(0, new ControllerState(ControllerButton.A)),
            new(2, new ControllerState(ControllerButton.B | ControllerButton.Down)),
            new(5, ControllerState.Empty),
        ];
        Movie movie = Record(entries, 8);
        InputTimeline timeline = InputTimeline.FromMovie(movie);

        Assert.Equal(3, timeline.Entries.Count);
        Assert.True(timeline.StateAt(0).IsDown(ControllerButton.A));
        Assert.True(timeline.StateAt(2).IsDown(ControllerButton.Down));
        Assert.False(timeline.StateAt(5).IsPressed);

        InputTimeline edited = timeline
            .WithFrame(3, new ControllerState(ControllerButton.Up))
            .WithButton(3, ControllerButton.Down, pressed: true)
            .Trim(8);
        Movie editedMovie = edited.ApplyTo(movie);
        Assert.True(editedMovie.GetControllers(3)[0].IsDown(ControllerButton.Up));
        Assert.True(editedMovie.GetControllers(3)[0].IsDown(ControllerButton.Down));
        Assert.True(editedMovie.GetControllers(4)[0].IsDown(ControllerButton.Down));
        Assert.False(editedMovie.GetControllers(5)[0].IsPressed);

        var path = Path.Combine(Path.GetTempPath(), $"fivedo-timeline-{Guid.NewGuid():N}.txt");
        try
        {
            edited.Save(path);
            InputTimeline loaded = InputTimeline.Load(path, 8);
            Assert.Equal(edited.Entries.Count, loaded.Entries.Count);
            Assert.True(loaded.StateAt(3).IsDown(ControllerButton.Up));
            Movie reapplied = loaded.ApplyTo(movie);
            for (var frame = 0; frame < movie.FrameCount; frame++)
            {
                Assert.Equal(editedMovie.GetControllers(frame)[0], reapplied.GetControllers(frame)[0]);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Movie Record(IReadOnlyList<InputScriptEntry> entries, int frames)
    {
        FiveDoMachine machine = new();
        var recorder = new MovieRecorder();
        machine.PbusSource = new MovieRecordingPbusSource(
            new AdvancingSource(new ScriptedPbusSource(entries)),
            recorder);
        machine.RunFrames(frames);
        var header = new MovieHeader
        {
            GameName = "Lua synthetic",
            FrameCount = recorder.FrameCount,
            FinalFrameHash = FrameHasher.Hash(machine.Frame),
            FinalProgramCounter = machine.Cpu.ProgramCounter,
            FinalStateHash = HashState(machine),
        };
        return recorder.Build(header);
    }

    private static uint HashState(FiveDoMachine machine)
    {
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        return MovieHash.Fnv1a(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }

    private sealed class AdvancingSource(ScriptedPbusSource inner) : IPbusSource
    {
        private long _frame;

        public int GetPbusData(Span<byte> destination)
        {
            inner.AdvanceTo(_frame++);
            return inner.GetPbusData(destination);
        }
    }
}
