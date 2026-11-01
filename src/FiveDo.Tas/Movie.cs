using System;
using System.Collections.Generic;
using FiveDo.Input;

namespace FiveDo.Tas;

/// <summary>
/// An immutable movie: metadata, one PBUS packet per frame, and an optional starting state.
/// </summary>
public sealed class Movie
{
    private readonly byte[][] _frames;

    /// <summary>Initializes a new instance of the <see cref="Movie"/> class.</summary>
    /// <param name="header">Movie metadata.</param>
    /// <param name="frames">Per-frame PBUS packets.</param>
    /// <param name="state">Optional starting save state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="header"/> or <paramref name="frames"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a packet is too large.</exception>
    public Movie(MovieHeader header, IReadOnlyList<byte[]> frames, byte[]? state = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(frames);
        Header = header;
        _frames = new byte[frames.Count][];
        for (var i = 0; i < frames.Count; i++)
        {
            byte[] packet = frames[i] ?? throw new ArgumentException("A movie frame cannot be null.", nameof(frames));
            MovieFormat.ValidatePacketLength(packet.Length);
            _frames[i] = [.. packet];
        }

        if (state is not null && state.Length > MovieFormat.MaxStateLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state.Length,
                $"The embedded state must be at most {MovieFormat.MaxStateLength} bytes.");
        }

        State = state is null ? null : [.. state];
    }

    /// <summary>Gets the movie metadata.</summary>
    public MovieHeader Header { get; }

    /// <summary>Gets the per-frame PBUS packets.</summary>
    public IReadOnlyList<byte[]> Frames => _frames;

    /// <summary>Gets the number of recorded frames.</summary>
    public int FrameCount => _frames.Length;

    /// <summary>Gets the optional starting save state.</summary>
    public byte[]? State { get; }

    /// <summary>Gets a value indicating whether a starting state is embedded.</summary>
    public bool HasState => State is not null;

    /// <summary>Returns a copy of one packet, or an empty array when the index is outside the movie.</summary>
    /// <param name="frame">Zero-based frame index.</param>
    /// <returns>The packet bytes.</returns>
    public byte[] GetFrame(int frame)
    {
        return frame >= 0 && frame < _frames.Length ? [.. _frames[frame]] : [];
    }

    /// <summary>Decodes the controller states for one frame.</summary>
    /// <param name="frame">Zero-based frame index.</param>
    /// <returns>Six controller states; disconnected slots are empty.</returns>
    public ControllerState[] GetControllers(int frame)
    {
        return frame >= 0 && frame < _frames.Length
            ? PbusDecoder.Decode(_frames[frame])
            : new ControllerState[PbusEncoder.MaxControllers];
    }

    /// <summary>Returns a copy with a different header and the same frames and state.</summary>
    /// <param name="header">Replacement metadata.</param>
    /// <returns>A new movie.</returns>
    public Movie WithHeader(MovieHeader header)
    {
        return new Movie(header, _frames, State);
    }
}
