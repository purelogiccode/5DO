using System;
using System.Collections.Generic;

namespace FiveDo.Tas;

/// <summary>
/// Non-destructive movie edits used by TAS branching and trimming.
/// </summary>
public static class MovieEditor
{
    /// <summary>Keeps the first <paramref name="frameCount"/> frames and clears completion evidence.</summary>
    /// <param name="movie">Source movie.</param>
    /// <param name="frameCount">Number of frames to keep, 0 through the movie length.</param>
    /// <returns>A trimmed movie with the same starting state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="movie"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is outside the movie.</exception>
    public static Movie Trim(Movie movie, int frameCount)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentOutOfRangeException.ThrowIfNegative(frameCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frameCount, movie.FrameCount);
        var frames = new List<byte[]>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            frames.Add(movie.Frames[i]);
        }

        MovieHeader header = movie.Header with
        {
            FrameCount = frameCount,
            FinalFrameHash = null,
            FinalProgramCounter = null,
            FinalStateHash = null,
            HasState = movie.HasState,
        };
        return new Movie(header, frames, movie.State);
    }

    /// <summary>
    /// Creates a branch point: the result contains frames before
    /// <paramref name="frame"/> and can be extended with new input.
    /// </summary>
    /// <param name="movie">Source movie.</param>
    /// <param name="frame">Frame at which the branch starts; frames at or after it are removed.</param>
    /// <returns>A branch movie.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="movie"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame is outside the movie.</exception>
    public static Movie Branch(Movie movie, int frame)
    {
        return Trim(movie, frame);
    }

    /// <summary>Appends frames to an existing movie and clears completion evidence.</summary>
    /// <param name="movie">Base movie.</param>
    /// <param name="frames">Additional per-frame PBUS packets.</param>
    /// <returns>An extended movie.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a packet is too large.</exception>
    public static Movie Append(Movie movie, IReadOnlyList<byte[]> frames)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentNullException.ThrowIfNull(frames);
        var combined = new List<byte[]>(movie.FrameCount + frames.Count);
        for (var i = 0; i < movie.FrameCount; i++)
        {
            combined.Add(movie.Frames[i]);
        }

        for (var i = 0; i < frames.Count; i++)
        {
            byte[] packet = frames[i] ?? throw new ArgumentException(
                "A movie frame cannot be null.",
                nameof(frames));
            MovieFormat.ValidatePacketLength(packet.Length);
            combined.Add(packet);
        }

        MovieHeader header = movie.Header with
        {
            FrameCount = combined.Count,
            FinalFrameHash = null,
            FinalProgramCounter = null,
            FinalStateHash = null,
            HasState = movie.HasState,
        };
        return new Movie(header, combined, movie.State);
    }
}
