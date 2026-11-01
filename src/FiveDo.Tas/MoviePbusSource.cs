using System;
using FiveDo.Core;
using FiveDo.Input;

namespace FiveDo.Tas;

/// <summary>
/// Plays a movie's PBUS packets back to the emulated machine, one frame per call.
/// </summary>
/// <remarks>
/// Playback is sequential and deterministic. <see cref="Seek"/> and
/// <see cref="StepBack"/> support frame-by-frame TAS editing without touching the
/// emulated machine; the host must load the matching state before seeking backwards.
/// </remarks>
public sealed class MoviePbusSource : IPbusSource
{
    private static readonly byte[] EmptyPacket = [];
    private readonly Movie _movie;
    private readonly ControllerState[] _controllers = new ControllerState[PbusEncoder.MaxControllers];
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="MoviePbusSource"/> class.</summary>
    /// <param name="movie">Movie to play.</param>
    /// <param name="startFrame">Zero-based frame to play first.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="movie"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the start frame is outside the movie.</exception>
    public MoviePbusSource(Movie movie, int startFrame = 0)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentOutOfRangeException.ThrowIfNegative(startFrame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startFrame, movie.FrameCount);
        _movie = movie;
        _position = startFrame;
        if (startFrame < movie.FrameCount)
        {
            CurrentPacket = movie.GetFrame(startFrame);
            PbusDecoder.Decode(CurrentPacket, _controllers);
        }
        else
        {
            CurrentPacket = EmptyPacket;
        }

        CurrentControllers = _controllers;
    }

    /// <summary>Gets the movie being played.</summary>
    public Movie Movie => _movie;

    /// <summary>Gets the number of frames in the movie.</summary>
    public int FrameCount => _movie.FrameCount;

    /// <summary>Gets the next frame index that will be played.</summary>
    public int Position => _position;

    /// <summary>Gets a value indicating whether every frame has been played.</summary>
    public bool IsFinished => _position >= _movie.FrameCount;

    /// <summary>Gets the packet for the most recently selected frame.</summary>
    public byte[] CurrentPacket { get; private set; }

    /// <summary>Gets the decoded controllers for the most recently selected frame.</summary>
    public ControllerState[] CurrentControllers { get; private set; }

    /// <summary>Moves to a frame without playing intermediate packets.</summary>
    /// <param name="frame">Zero-based frame index, 0 through <see cref="FrameCount"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame is outside the movie.</exception>
    public void Seek(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, _movie.FrameCount);
        _position = frame;
        if (frame < _movie.FrameCount)
        {
            CurrentPacket = _movie.GetFrame(frame);
            PbusDecoder.Decode(CurrentPacket, _controllers);
        }
        else
        {
            CurrentPacket = EmptyPacket;
            Array.Clear(_controllers);
        }
    }

    /// <summary>Moves back one frame when possible.</summary>
    /// <returns><see langword="true"/> when the position changed.</returns>
    public bool StepBack()
    {
        if (_position == 0)
        {
            return false;
        }

        Seek(_position - 1);
        return true;
    }

    /// <inheritdoc />
    public int GetPbusData(Span<byte> destination)
    {
        if (_position >= _movie.FrameCount)
        {
            destination.Clear();
            return 0;
        }

        byte[] packet = _movie.Frames[_position];
        _position++;
        CurrentPacket = packet;
        PbusDecoder.Decode(packet, _controllers);
        var length = Math.Min(packet.Length, destination.Length);
        packet.AsSpan(0, length).CopyTo(destination);
        return length;
    }
}
