using System;
using System.Runtime.InteropServices;
using FiveDo.Audio;
using FiveDo.Core;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Video;

namespace FiveDo.Host.Netplay;

/// <summary>
/// Supplies the combined player packet for the current netplay frame.
/// </summary>
public sealed class NetplayPbusSource : IPbusSource
{
    private readonly PbusSource _inner = new();

    /// <summary>Sets the two connected controllers for the next PBUS read.</summary>
    /// <param name="player1">Player 1 state.</param>
    /// <param name="player2">Player 2 state.</param>
    public void SetControllers(ControllerState player1, ControllerState player2)
    {
        _inner[0] = player1;
        _inner[1] = player2;
    }

    /// <inheritdoc />
    public int GetPbusData(Span<byte> destination) => _inner.GetPbusData(destination);
}

/// <summary>
/// Hashes every PCM sample the machine produces and optionally forwards it to a live sink.
/// </summary>
public sealed class NetplayAudioSink : IAudioSink
{
    private readonly PcmHasher _hasher;
    private readonly IAudioSink? _inner;

    /// <summary>Initializes a new hashing audio sink.</summary>
    /// <param name="inner">Optional live sink that also receives the samples.</param>
    public NetplayAudioSink(IAudioSink? inner = null)
    {
        _inner = inner;
        _hasher = new PcmHasher();
    }

    /// <summary>Gets the cumulative PCM hash.</summary>
    public uint Hash => _hasher.Hash;

    /// <summary>Gets the cumulative stereo sample count.</summary>
    public long SampleCount => _hasher.SampleCount;

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        _hasher.Add(packedSample);
        _inner?.PushSample(packedSample);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _hasher.Reset();
        _inner?.Reset();
    }
}

/// <summary>
/// The result of one netplay frame.
/// </summary>
/// <param name="Advanced">Whether the session produced inputs and ran the machine.</param>
/// <param name="Frame">Executed frame number.</param>
/// <param name="FrameHash">Deterministic frame hash.</param>
/// <param name="AudioHash">Cumulative PCM hash after the frame.</param>
/// <param name="AudioSamples">Cumulative stereo sample count after the frame.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayFrameResult(
    bool Advanced,
    long Frame,
    uint FrameHash,
    uint AudioHash,
    long AudioSamples);

/// <summary>
/// Drives a <see cref="FiveDoMachine"/> from a <see cref="LockstepSession"/> and publishes
/// the frame and audio hashes after every frame.
/// </summary>
public sealed class NetplayMachinePeer : IDisposable
{
    private readonly FiveDoMachine _machine;
    private readonly LockstepSession _session;
    private readonly NetplayPbusSource _pbus = new();
    private readonly NetplayAudioSink _audio;
    private FrameBuffer? _hashBuffer;

    /// <summary>Initializes a new machine peer.</summary>
    /// <param name="machine">Machine to drive.</param>
    /// <param name="session">Lockstep session connected to the other peer.</param>
    /// <param name="audioOutput">Optional live audio sink.</param>
    public NetplayMachinePeer(
        FiveDoMachine machine,
        LockstepSession session,
        IAudioSink? audioOutput = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(session);
        _machine = machine;
        _session = session;
        _audio = new NetplayAudioSink(audioOutput);
        _machine.PbusSource = _pbus;
        _machine.AudioSink = _audio;
    }

    /// <summary>Gets the lockstep session.</summary>
    public LockstepSession Session => _session;

    /// <summary>Gets the hashing audio sink.</summary>
    public NetplayAudioSink Audio => _audio;

    /// <summary>Collects both inputs and executes one emulated frame.</summary>
    /// <returns>The frame result, or an unadvanced result when the session stopped.</returns>
    public NetplayFrameResult RunFrame()
    {
        if (!_session.Step())
        {
            return new NetplayFrameResult(false, _session.Frame, 0, _audio.Hash, _audio.SampleCount);
        }

        var frame = _session.Frame - 1;
        ControllerState player1;
        ControllerState player2;
        if (_session.LocalPlayerIndex == 0)
        {
            player1 = _session.LocalInput;
            player2 = _session.RemoteInput;
        }
        else
        {
            player1 = _session.RemoteInput;
            player2 = _session.LocalInput;
        }

        _pbus.SetControllers(player1, player2);
        _machine.RunFrame();
        var frameHash = HashFrame(_machine.Frame);
        _session.PublishHash(frame, frameHash, _audio.Hash, _audio.SampleCount);
        return new NetplayFrameResult(true, frame, frameHash, _audio.Hash, _audio.SampleCount);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (ReferenceEquals(_machine.PbusSource, _pbus))
        {
            _machine.PbusSource = null;
        }

        if (ReferenceEquals(_machine.AudioSink, _audio))
        {
            _machine.AudioSink = null;
        }
    }

    private uint HashFrame(VdlFrame frame)
    {
        var width = frame.SourceWidth;
        var height = frame.SourceHeight;
        if (_hashBuffer is null || _hashBuffer.Width != width || _hashBuffer.Height != height)
        {
            _hashBuffer = new FrameBuffer(width, height);
        }

        FrameAssembler.Assemble(frame, _hashBuffer, width, height);
        return FrameHasher.HashBgra(_hashBuffer.PixelSpan);
    }
}
