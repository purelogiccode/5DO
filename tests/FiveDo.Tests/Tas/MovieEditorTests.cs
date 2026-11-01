using System;
using System.Collections.Generic;
using FiveDo.Input;
using FiveDo.Tas;

namespace FiveDo.Tests.Tas;

public sealed class MovieEditorTests
{
    [Fact]
    public void Trim_KeepsFramesAndClearsCompletionEvidence()
    {
        Movie movie = CreateMovie(5);
        movie = movie.WithHeader(movie.Header with
        {
            FinalFrameHash = 0x1234,
            FinalProgramCounter = 0x5678,
            FinalStateHash = 0x9ABC,
        });

        Movie trimmed = MovieEditor.Trim(movie, 3);

        Assert.Equal(3, trimmed.FrameCount);
        Assert.Equal(3, trimmed.Header.FrameCount);
        Assert.Null(trimmed.Header.FinalFrameHash);
        Assert.Null(trimmed.Header.FinalProgramCounter);
        Assert.Null(trimmed.Header.FinalStateHash);
        Assert.Equal(movie.State, trimmed.State);
        Assert.Equal(new byte[] { 0x00, 0x01 }, trimmed.Frames[0]);
        Assert.Equal(new byte[] { 0x04, 0x05 }, trimmed.Frames[2]);
    }

    [Fact]
    public void Branch_IsATrimAlias()
    {
        Movie movie = CreateMovie(4);

        Movie branch = MovieEditor.Branch(movie, 2);

        Assert.Equal(2, branch.FrameCount);
        Assert.Equal(movie.Header.StartFrame, branch.Header.StartFrame);
    }

    [Fact]
    public void Append_ExtendsTheMovieAndClearsCompletionEvidence()
    {
        Movie movie = CreateMovie(2).WithHeader(CreateMovie(2).Header with
        {
            FinalFrameHash = 0x1111,
            FinalProgramCounter = 0x2222,
            FinalStateHash = 0x3333,
        });

        Movie extended = MovieEditor.Append(movie, [[0xAA], [0xBB, 0xCC]]);

        Assert.Equal(4, extended.FrameCount);
        Assert.Equal(4, extended.Header.FrameCount);
        Assert.Null(extended.Header.FinalFrameHash);
        Assert.Null(extended.Header.FinalProgramCounter);
        Assert.Null(extended.Header.FinalStateHash);
        Assert.Equal(new byte[] { 0xAA }, extended.Frames[2]);
        Assert.Equal(new byte[] { 0xBB, 0xCC }, extended.Frames[3]);
    }

    [Fact]
    public void TrimAndBranch_RejectOutOfRangeFrames()
    {
        Movie movie = CreateMovie(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => MovieEditor.Trim(movie, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MovieEditor.Trim(movie, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => MovieEditor.Branch(movie, 3));
    }

    [Fact]
    public void Append_RejectsNullFrames()
    {
        Movie movie = CreateMovie(1);

        Assert.Throws<ArgumentException>(() => MovieEditor.Append(movie, [null!]));
    }

    [Fact]
    public void EmptyBranch_IsValid()
    {
        Movie branch = MovieEditor.Branch(CreateMovie(2), 0);

        Assert.Empty(branch.Frames);
        Assert.Equal(0, branch.Header.FrameCount);
        Assert.True(branch.HasState);
    }

    [Fact]
    public void SetButton_ChangesOnlyTheSelectedPlayerAndFrame()
    {
        Movie movie = CreateControllerMovie(3);
        movie = movie.WithHeader(movie.Header with { FinalFrameHash = 0x1234 });

        Movie edited = MovieEditor.SetButton(movie, 1, 0, ControllerButton.A, pressed: true);
        edited = MovieEditor.SetButton(edited, 1, 1, ControllerButton.Left, pressed: true);
        edited = MovieEditor.SetButton(edited, 2, 0, ControllerButton.P, pressed: true);

        Assert.Equal(3, edited.FrameCount);
        Assert.True(edited.GetControllers(1)[0].IsDown(ControllerButton.A));
        Assert.False(edited.GetControllers(0)[0].IsDown(ControllerButton.A));
        Assert.True(edited.GetControllers(1)[1].IsDown(ControllerButton.Left));
        Assert.True(edited.GetControllers(2)[0].IsDown(ControllerButton.P));
        Assert.Null(edited.Header.FinalFrameHash);

        Movie released = MovieEditor.SetButton(edited, 1, 0, ControllerButton.A, pressed: false);
        Assert.False(released.GetControllers(1)[0].IsDown(ControllerButton.A));
    }

    [Fact]
    public void InsertAndRemoveFrame_KeepOrderAndHeaderCount()
    {
        Movie movie = CreateControllerMovie(3);
        ControllerState[] inserted = [new ControllerState(ControllerButton.B), .. new ControllerState[5]];

        Movie extended = MovieEditor.InsertFrame(movie, 1, inserted);
        Assert.Equal(4, extended.FrameCount);
        Assert.Equal(4, extended.Header.FrameCount);
        Assert.True(extended.GetControllers(1)[0].IsDown(ControllerButton.B));
        Assert.False(extended.GetControllers(2)[0].IsDown(ControllerButton.B));

        Movie removed = MovieEditor.RemoveFrame(extended, 1);
        Assert.Equal(3, removed.FrameCount);
        Assert.False(removed.GetControllers(1)[0].IsDown(ControllerButton.B));
    }

    [Fact]
    public void SetFrame_ReplacesAllSixControllers()
    {
        Movie movie = CreateControllerMovie(2);
        ControllerState[] controllers =
        [
            new(ControllerButton.A | ControllerButton.Right),
            new(ControllerButton.C),
            .. new ControllerState[4],
        ];

        Movie edited = MovieEditor.SetFrame(movie, 0, controllers);

        Assert.True(edited.GetControllers(0)[0].IsDown(ControllerButton.A));
        Assert.True(edited.GetControllers(0)[0].IsDown(ControllerButton.Right));
        Assert.True(edited.GetControllers(0)[1].IsDown(ControllerButton.C));
        Assert.False(edited.GetControllers(1)[0].IsPressed);
    }

    [Fact]
    public void EditOperations_RejectOutOfRangeFrames()
    {
        Movie movie = CreateControllerMovie(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => MovieEditor.SetFrame(movie, 2, new ControllerState[6]));
        Assert.Throws<ArgumentOutOfRangeException>(() => MovieEditor.RemoveFrame(movie, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MovieEditor.InsertFrame(movie, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => MovieEditor.SetButton(movie, 0, 6, ControllerButton.A, true));
    }

    private static Movie CreateControllerMovie(int frameCount)
    {
        var frames = new List<byte[]>();
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode(new ControllerState[PbusEncoder.MaxControllers], packet);
        for (var i = 0; i < frameCount; i++)
        {
            frames.Add(packet);
        }

        return new Movie(new MovieHeader { FrameCount = frameCount }, frames);
    }

    private static Movie CreateMovie(int frames)
    {
        var list = new List<byte[]>(frames);
        for (var i = 0; i < frames; i++)
        {
            list.Add([(byte)(i * 2), (byte)((i * 2) + 1)]);
        }

        return new Movie(
            new MovieHeader { GameName = "Editor", StartFrame = 100 },
            list,
            [0xDE, 0xAD, 0xBE, 0xEF]);
    }
}
