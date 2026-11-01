using System;
using System.Collections.Generic;
using FiveDo.Input;

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

    /// <summary>Replaces one frame's controller states.</summary>
    /// <param name="movie">Source movie.</param>
    /// <param name="frame">Zero-based frame index.</param>
    /// <param name="controllers">Six controller states, player 1 first.</param>
    /// <returns>A movie with the edited frame and cleared completion evidence.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame is outside the movie.</exception>
    public static Movie SetFrame(Movie movie, int frame, IReadOnlyList<ControllerState> controllers)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentNullException.ThrowIfNull(controllers);
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frame, movie.FrameCount);
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode(NormalizeControllers(controllers), packet);
        var frames = new List<byte[]>(movie.FrameCount);
        for (var i = 0; i < movie.FrameCount; i++)
        {
            frames.Add(i == frame ? packet : movie.Frames[i]);
        }

        return WithFrames(movie, frames);
    }

    /// <summary>Sets or clears one button on one player's frame.</summary>
    /// <param name="movie">Source movie.</param>
    /// <param name="frame">Zero-based frame index.</param>
    /// <param name="player">Zero-based player index, 0-5.</param>
    /// <param name="button">Button to change.</param>
    /// <param name="pressed">Whether the button is pressed.</param>
    /// <returns>A movie with the edited frame.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame or player is invalid.</exception>
    public static Movie SetButton(
        Movie movie,
        int frame,
        int player,
        ControllerButton button,
        bool pressed)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentOutOfRangeException.ThrowIfNegative(player);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(player, PbusEncoder.MaxControllers);
        ControllerState[] controllers = movie.GetControllers(frame);
        controllers[player] = controllers[player].Set(button, pressed);
        return SetFrame(movie, frame, controllers);
    }

    /// <summary>Inserts a frame at an index.</summary>
    /// <param name="movie">Source movie.</param>
    /// <param name="index">Insertion index, 0 through the movie length.</param>
    /// <param name="controllers">Optional controller states; empty when omitted.</param>
    /// <returns>A movie with one more frame.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the movie.</exception>
    public static Movie InsertFrame(
        Movie movie,
        int index,
        IReadOnlyList<ControllerState>? controllers = null)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, movie.FrameCount);
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode(
            controllers is null ? new ControllerState[PbusEncoder.MaxControllers] : NormalizeControllers(controllers),
            packet);
        var frames = new List<byte[]>(movie.FrameCount + 1);
        for (var i = 0; i < index; i++)
        {
            frames.Add(movie.Frames[i]);
        }

        frames.Add(packet);
        for (var i = index; i < movie.FrameCount; i++)
        {
            frames.Add(movie.Frames[i]);
        }

        return WithFrames(movie, frames);
    }

    /// <summary>Removes one frame.</summary>
    /// <param name="movie">Source movie.</param>
    /// <param name="index">Zero-based frame index.</param>
    /// <returns>A movie with one fewer frame.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the movie.</exception>
    public static Movie RemoveFrame(Movie movie, int index)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, movie.FrameCount);
        var frames = new List<byte[]>(movie.FrameCount - 1);
        for (var i = 0; i < movie.FrameCount; i++)
        {
            if (i != index)
            {
                frames.Add(movie.Frames[i]);
            }
        }

        return WithFrames(movie, frames);
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

    private static ControllerState[] NormalizeControllers(IReadOnlyList<ControllerState> controllers)
    {
        var copy = new ControllerState[PbusEncoder.MaxControllers];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = i < controllers.Count ? controllers[i] : ControllerState.Empty;
        }

        return copy;
    }

    private static Movie WithFrames(Movie movie, List<byte[]> frames)
    {
        MovieHeader header = movie.Header with
        {
            FrameCount = frames.Count,
            FinalFrameHash = null,
            FinalProgramCounter = null,
            FinalStateHash = null,
            HasState = movie.HasState,
        };
        return new Movie(header, frames, movie.State);
    }
}
