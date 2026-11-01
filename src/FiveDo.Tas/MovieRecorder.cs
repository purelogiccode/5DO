using System;
using System.Collections.Generic;
using FiveDo.Core;

namespace FiveDo.Tas;

/// <summary>
/// Captures the PBUS packet sampled for every emulated frame.
/// </summary>
/// <remarks>
/// The recorder stores packets exactly as the emulated machine saw them. It does not
/// interpret devices, so joypads, lightguns, mice, and future peripherals share one
/// deterministic movie format.
/// </remarks>
public sealed class MovieRecorder
{
    private readonly List<byte[]> _frames = [];

    /// <summary>Gets the number of recorded frames.</summary>
    public int FrameCount => _frames.Count;

    /// <summary>Gets the recorded packets.</summary>
    public IReadOnlyList<byte[]> Frames => _frames;

    /// <summary>Records one frame's PBUS packet.</summary>
    /// <param name="packet">Packet bytes sampled at V1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the packet is too large.</exception>
    public void Record(ReadOnlySpan<byte> packet)
    {
        MovieFormat.ValidatePacketLength(packet.Length);
        _frames.Add(packet.ToArray());
    }

    /// <summary>Removes every recorded frame.</summary>
    public void Clear()
    {
        _frames.Clear();
    }

    /// <summary>Builds a movie from the recorded frames.</summary>
    /// <param name="header">Metadata to store.</param>
    /// <param name="state">Optional starting save state.</param>
    /// <returns>The finished movie.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="header"/> is null.</exception>
    public Movie Build(MovieHeader header, byte[]? state = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        MovieHeader adjusted = header with
        {
            FrameCount = _frames.Count,
            HasState = state is not null,
        };
        return new Movie(adjusted, _frames, state);
    }
}

/// <summary>
/// Wraps another PBUS source and records every packet it returns.
/// </summary>
public sealed class MovieRecordingPbusSource : IPbusSource
{
    private readonly IPbusSource _inner;
    private readonly MovieRecorder _recorder;

    /// <summary>Initializes a new instance of the <see cref="MovieRecordingPbusSource"/> class.</summary>
    /// <param name="inner">Source that supplies live input.</param>
    /// <param name="recorder">Recorder that receives the packets.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public MovieRecordingPbusSource(IPbusSource inner, MovieRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(recorder);
        _inner = inner;
        _recorder = recorder;
    }

    /// <summary>Gets the wrapped live input source.</summary>
    public IPbusSource Inner => _inner;

    /// <summary>Gets the recorder that receives the packets.</summary>
    public MovieRecorder Recorder => _recorder;

    /// <inheritdoc />
    public int GetPbusData(Span<byte> destination)
    {
        var length = _inner.GetPbusData(destination);
        if (length < 0)
        {
            length = 0;
        }
        else if (length > destination.Length)
        {
            length = destination.Length;
        }

        _recorder.Record(destination[..length]);
        return length;
    }
}
