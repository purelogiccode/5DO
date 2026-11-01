using System;
using System.Collections.Generic;
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
