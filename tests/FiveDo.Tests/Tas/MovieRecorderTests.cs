using System;
using System.Collections.Generic;
using FiveDo.Core;
using FiveDo.Input;
using FiveDo.Tas;

namespace FiveDo.Tests.Tas;

public sealed class MovieRecorderTests
{
    [Fact]
    public void Recorder_CapturesPacketsAndBuildsMovie()
    {
        var recorder = new MovieRecorder();
        recorder.Record([0x00, 0x48]);
        recorder.Record([0x80, 0x80, 0x01]);
        recorder.Record([]);

        Assert.Equal(3, recorder.FrameCount);
        Movie movie = recorder.Build(new MovieHeader { GameName = "Recorder" });

        Assert.Equal(3, movie.FrameCount);
        Assert.Equal(3, movie.Header.FrameCount);
        Assert.False(movie.HasState);
        Assert.Equal(new byte[] { 0x00, 0x48 }, movie.Frames[0]);
        Assert.Equal(new byte[] { 0x80, 0x80, 0x01 }, movie.Frames[1]);
        Assert.Empty(movie.Frames[2]);
    }

    [Fact]
    public void Recorder_RejectsOversizedPackets()
    {
        var recorder = new MovieRecorder();
        var packet = new byte[MovieFormat.MaxPacketLength + 1];

        Assert.Throws<ArgumentOutOfRangeException>(() => recorder.Record(packet));
        Assert.Equal(0, recorder.FrameCount);
    }

    [Fact]
    public void RecordingSource_WrapsLiveSourceAndRecordsEveryFrame()
    {
        var inner = new SequencePbusSource(
            new byte[] { 0x00, 0x48, 0x01, 0x80 },
            new byte[] { 0x00, 0x48, 0x02, 0x80 });
        var recorder = new MovieRecorder();
        var source = new MovieRecordingPbusSource(inner, recorder);
        var destination = new byte[PbusEncoder.PacketSize];

        Assert.Equal(4, source.GetPbusData(destination));
        Assert.Equal(4, source.GetPbusData(destination));
        Assert.Equal(2, recorder.FrameCount);
        Assert.Equal(new byte[] { 0x00, 0x48, 0x01, 0x80 }, recorder.Frames[0]);
        Assert.Equal(new byte[] { 0x00, 0x48, 0x02, 0x80 }, recorder.Frames[1]);
    }

    [Fact]
    public void PlaybackSource_ReturnsFramesInOrderAndFinishes()
    {
        var movie = new Movie(
            new MovieHeader { GameName = "Playback" },
            [[0x00, 0x48, 0x01, 0x80], [0x00, 0x48, 0x02, 0x80]]);
        var source = new MoviePbusSource(movie);
        var destination = new byte[PbusEncoder.PacketSize];

        Assert.Equal(4, source.GetPbusData(destination));
        Assert.Equal(new byte[] { 0x00, 0x48, 0x01, 0x80 }, destination[..4]);
        Assert.Equal(1, source.Position);
        Assert.False(source.IsFinished);

        Assert.Equal(4, source.GetPbusData(destination));
        Assert.Equal(new byte[] { 0x00, 0x48, 0x02, 0x80 }, destination[..4]);
        Assert.Equal(2, source.Position);
        Assert.True(source.IsFinished);

        Assert.Equal(0, source.GetPbusData(destination));
        Assert.Equal(2, source.Position);
    }

    [Fact]
    public void PlaybackSource_SeekAndStepBackSelectFramesWithoutAdvancing()
    {
        var movie = new Movie(
            new MovieHeader(),
            [[0x01], [0x02], [0x03]]);
        var source = new MoviePbusSource(movie);

        source.Seek(2);
        Assert.Equal(2, source.Position);
        Assert.Equal(new byte[] { 0x03 }, source.CurrentPacket);

        Assert.True(source.StepBack());
        Assert.Equal(1, source.Position);
        Assert.Equal(new byte[] { 0x02 }, source.CurrentPacket);

        Assert.True(source.StepBack());
        Assert.Equal(0, source.Position);
        Assert.Equal(new byte[] { 0x01 }, source.CurrentPacket);

        Assert.False(source.StepBack());

        source.Seek(3);
        Assert.True(source.IsFinished);
        Assert.Empty(source.CurrentPacket);
    }

    [Fact]
    public void PlaybackSource_DecodesInputForDisplay()
    {
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode([new ControllerState(ControllerButton.A)], packet);
        var movie = new Movie(new MovieHeader(), [packet]);
        var source = new MoviePbusSource(movie);
        var destination = new byte[PbusEncoder.PacketSize];

        source.GetPbusData(destination);

        Assert.True(source.CurrentControllers[0].IsDown(ControllerButton.A));
        Assert.False(source.CurrentControllers[0].IsDown(ControllerButton.B));
        Assert.Equal("A", PbusDecoder.FormatButtons(source.CurrentControllers[0]));
    }

    [Fact]
    public void PlaybackSource_RejectsFramesOutsideTheMovie()
    {
        var movie = new Movie(new MovieHeader(), [[0x01]]);
        var source = new MoviePbusSource(movie);

        Assert.Throws<ArgumentOutOfRangeException>(() => source.Seek(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Seek(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoviePbusSource(movie, 2));
    }

    [Fact]
    public void PlaybackSource_DoesNotAllocateInSteadyState()
    {
        var frames = new byte[120][];
        for (var i = 0; i < frames.Length; i++)
        {
            frames[i] = new byte[PbusEncoder.PacketSize];
        }

        var source = new MoviePbusSource(new Movie(new MovieHeader(), frames));
        var destination = new byte[PbusEncoder.PacketSize];
        source.GetPbusData(destination);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            source.GetPbusData(destination);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
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
            if (_packets.Count == 0)
            {
                destination.Clear();
                return 0;
            }

            byte[] packet = _packets.Dequeue();
            packet.CopyTo(destination);
            return packet.Length;
        }
    }
}
