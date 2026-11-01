using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Tas;
using FiveDo.Video;

namespace FiveDo.Tests.Tas;

public sealed class MovieMachineTests
{
    [Fact]
    public void RecordedMovie_ReplaysToTheSameStateAndVerifies()
    {
        FiveDoMachine source = new();
        source.PbusSource = new SequencePbusSource(
            Packet(ControllerButton.A),
            Packet(ControllerButton.B),
            Packet(ControllerButton.Up | ControllerButton.A),
            Packet(ControllerButton.None));
        source.RunFrames(5);
        using var startState = new MemoryStream();
        source.SaveState(startState, "test-disc");
        byte[] startBytes = startState.ToArray();

        var recorder = new MovieRecorder();
        source.PbusSource = new MovieRecordingPbusSource(
            new SequencePbusSource(
                Packet(ControllerButton.A),
                Packet(ControllerButton.Right),
                Packet(ControllerButton.None)),
            recorder);
        source.RunFrames(3);
        uint finalFrameHash = FrameHasher.Hash(source.Frame);
        uint finalPc = source.Cpu.ProgramCounter;
        uint finalStateHash = HashState(source);

        var header = new MovieHeader
        {
            GameName = "Synthetic",
            StartFrame = 5,
            FrameCount = recorder.FrameCount,
            FinalFrameHash = finalFrameHash,
            FinalProgramCounter = finalPc,
            FinalStateHash = finalStateHash,
        };
        var movie = recorder.Build(header, startBytes);

        FiveDoMachine replay = new();
        using (var state = new MemoryStream(startBytes))
        {
            replay.LoadState(state);
        }

        var playback = new MoviePbusSource(movie);
        replay.PbusSource = playback;
        replay.RunFrames(movie.FrameCount);

        Assert.Equal(finalFrameHash, FrameHasher.Hash(replay.Frame));
        Assert.Equal(finalPc, replay.Cpu.ProgramCounter);
        Assert.Equal(finalStateHash, HashState(replay));
        MovieVerification verification = MovieVerifier.Verify(
            movie.Header,
            playback.Position,
            FrameHasher.Hash(replay.Frame),
            replay.Cpu.ProgramCounter,
            HashState(replay));
        Assert.True(verification.Ok);
        Assert.Equal(3, movie.FrameCount);
    }

    [Fact]
    public void TamperedMovieFrame_FailsStateVerification()
    {
        FiveDoMachine source = new();
        source.PbusSource = new SequencePbusSource(Packet(ControllerButton.A));
        var recorder = new MovieRecorder();
        source.PbusSource = new MovieRecordingPbusSource(
            new SequencePbusSource(Packet(ControllerButton.A)),
            recorder);
        source.RunFrames(2);

        var header = new MovieHeader
        {
            FrameCount = recorder.FrameCount,
            FinalFrameHash = FrameHasher.Hash(source.Frame),
            FinalProgramCounter = source.Cpu.ProgramCounter,
            FinalStateHash = HashState(source),
        };
        var movie = recorder.Build(header);
        movie.Frames[1][2] = 0x7F;

        FiveDoMachine replay = new();
        var playback = new MoviePbusSource(movie);
        replay.PbusSource = playback;
        replay.RunFrames(movie.FrameCount);

        MovieVerification verification = MovieVerifier.Verify(
            movie.Header,
            playback.Position,
            FrameHasher.Hash(replay.Frame),
            replay.Cpu.ProgramCounter,
            HashState(replay));
        Assert.False(verification.Ok);
    }

    [Fact]
    public void MovieFile_RoundtripReplaysToTheSameState()
    {
        FiveDoMachine source = new();
        var recorder = new MovieRecorder();
        source.PbusSource = new MovieRecordingPbusSource(
            new SequencePbusSource(Packet(ControllerButton.A), Packet(ControllerButton.B)),
            recorder);
        source.RunFrames(4);

        var movie = recorder.Build(new MovieHeader
        {
            FrameCount = recorder.FrameCount,
            FinalFrameHash = FrameHasher.Hash(source.Frame),
            FinalProgramCounter = source.Cpu.ProgramCounter,
            FinalStateHash = HashState(source),
        });
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            MovieFile.Save(path, movie);
            Movie loaded = MovieFile.Load(path);

            FiveDoMachine replay = new();
            replay.PbusSource = new MoviePbusSource(loaded);
            replay.RunFrames(loaded.FrameCount);

            Assert.True(MovieVerifier.Verify(
                loaded.Header,
                loaded.FrameCount,
                FrameHasher.Hash(replay.Frame),
                replay.Cpu.ProgramCounter,
                HashState(replay)).Ok);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] Packet(ControllerButton buttons)
    {
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode([new ControllerState(buttons)], packet);
        return packet;
    }

    private static uint HashState(FiveDoMachine machine)
    {
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        return MovieHash.Fnv1a(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }

    private sealed class SequencePbusSource : IPbusSource
    {
        private readonly Queue<byte[]> _packets;

        internal SequencePbusSource(params byte[][] packets)
        {
            _packets = new Queue<byte[]>(packets);
        }

        public int GetPbusData(Span<byte> destination)
        {
            byte[] packet = _packets.Count > 0 ? _packets.Dequeue() : new byte[PbusEncoder.PacketSize];
            packet.CopyTo(destination);
            return packet.Length;
        }
    }
}
