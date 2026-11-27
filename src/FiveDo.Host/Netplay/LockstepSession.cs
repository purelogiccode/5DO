using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using FiveDo.Input;

namespace FiveDo.Host.Netplay;

/// <summary>
/// Options shared by both peers of a direct-IP lockstep session.
/// </summary>
public sealed record NetplayOptions
{
    /// <summary>Gets the local role. The host owns player 1; the join owns player 2.</summary>
    public NetplayRole Role { get; init; }

    /// <summary>Gets the number of emulated frames local input is sampled ahead.</summary>
    /// <remarks>Zero still works on loopback; LAN and internet sessions should use 2-6.</remarks>
    public byte InputDelayFrames { get; init; } = 2;

    /// <summary>Gets the connected player count.</summary>
    public byte PlayerCount { get; init; } = 2;

    /// <summary>Gets the session identifier. The host generates one when this is zero.</summary>
    public ulong SessionId { get; init; }

    /// <summary>Gets the BIOS/disc/tuning fingerprint both peers must share.</summary>
    public ulong ContentHash { get; init; }

    /// <summary>Gets the first emulated frame in the session.</summary>
    public long FirstFrame { get; init; }

    /// <summary>
    /// Gets whether cumulative audio hashes are part of desync detection. Frame hashes
    /// are always compared. Desktop sessions disable audio comparison when the live
    /// audio sink cannot guarantee a frame-aligned sample baseline.
    /// </summary>
    public bool CompareAudioHashes { get; init; } = true;

    /// <summary>Gets how long a peer waits for a required datagram before giving up.</summary>
    public TimeSpan StepTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Validates the option ranges.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is out of range.</exception>
    [SuppressMessage(
        "Meziantou.Analyzer",
        "MA0015",
        Justification = "Validates record properties, which are not method parameters.")]
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(FirstFrame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(InputDelayFrames, (byte)120);
        ArgumentOutOfRangeException.ThrowIfLessThan(PlayerCount, (byte)2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PlayerCount, (byte)2);
        if (StepTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(StepTimeout), "The step timeout must be positive.");
        }
    }
}

/// <summary>
/// The lifecycle state of a lockstep session.
/// </summary>
public enum NetplaySessionState
{
    /// <summary>The session has been constructed but not started.</summary>
    Created = 0,

    /// <summary>The peers are exchanging the opening handshake.</summary>
    Handshaking = 1,

    /// <summary>The peers are exchanging inputs and hashes.</summary>
    Running = 2,

    /// <summary>The requested frame count completed normally.</summary>
    Completed = 3,

    /// <summary>The frame or audio hashes diverged.</summary>
    Desynchronized = 4,

    /// <summary>A required datagram did not arrive before the deadline.</summary>
    TimedOut = 5,

    /// <summary>The handshake or a packet was invalid, or the remote stopped.</summary>
    Failed = 6,
}

/// <summary>
/// Details of the first frame or audio divergence detected by a session.
/// </summary>
/// <param name="Frame">Frame where the hashes differ.</param>
/// <param name="LocalFrameHash">Local frame hash.</param>
/// <param name="RemoteFrameHash">Remote frame hash.</param>
/// <param name="LocalAudioHash">Local cumulative audio hash.</param>
/// <param name="RemoteAudioHash">Remote cumulative audio hash.</param>
/// <param name="LocalAudioSamples">Local cumulative sample count.</param>
/// <param name="RemoteAudioSamples">Remote cumulative sample count.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayDesync(
    long Frame,
    uint LocalFrameHash,
    uint RemoteFrameHash,
    uint LocalAudioHash,
    uint RemoteAudioHash,
    long LocalAudioSamples,
    long RemoteAudioSamples);

/// <summary>
/// One peer of a delay-based lockstep session. The caller supplies local input for a
/// future frame, receives the pair of inputs to apply to the current frame, and publishes
/// frame/audio hashes for desync detection.
/// </summary>
/// <remarks>
/// <para>
/// The session samples local input <see cref="NetplayOptions.InputDelayFrames"/> frames
/// ahead and sends the newest eight samples in every packet. The redundancy makes a lost
/// datagram recoverable without retransmission; a longer stall triggers an explicit
/// resend and eventually a timeout. Both peers apply the same player inputs to the same
/// frame number, so the deterministic machine produces the same frame and audio hashes.
/// </para>
/// <para>
/// A session is not thread-safe. Each peer must be driven from one thread.
/// </para>
/// </remarks>
public sealed class LockstepSession : IDisposable
{
    private readonly INetplayTransport _transport;
    private readonly NetplayOptions _options;
    private readonly Func<long, ControllerState> _localInput;
    private readonly Dictionary<long, ControllerButton> _localInputs = [];
    private readonly Dictionary<long, ControllerButton> _remoteInputs = [];
    private readonly Dictionary<long, NetplayHash> _localHashes = [];
    private readonly Dictionary<long, NetplayHash> _remoteHashes = [];
    private readonly byte[] _lastInputPacket = new byte[NetplayCodec.MaxPacketSize];
    private int _lastInputPacketLength;
    private readonly byte[] _receiveBuffer = new byte[NetplayCodec.MaxPacketSize];
    private readonly byte[] _sendBuffer = new byte[NetplayCodec.MaxPacketSize];
    private readonly NetplayInputEntry[] _sendHistory = new NetplayInputEntry[NetplayCodec.InputRedundancy];
    private readonly NetplayInputEntry[] _receiveHistory = new NetplayInputEntry[NetplayCodec.InputRedundancy];
    private readonly Stopwatch _handshakeClock = new();
    private readonly Stopwatch _waitClock = new();
    private readonly Stopwatch _resendClock = new();
    private bool _lastInputPacketValid;
    private bool _stopRequested;
    private NetplayByeReason _stopReason = NetplayByeReason.PeerStopped;
    private long _nextLocalFrame;
    private long _lastHandshakeSend;
    private NetplayHello? _pendingHandshake;
    private NetplayHello? _pendingAcknowledgement;
    private bool _disposed;

    /// <summary>Initializes a new lockstep session.</summary>
    /// <param name="transport">Datagram transport for this peer.</param>
    /// <param name="options">Session options. Both peers must use the same values.</param>
    /// <param name="localInput">
    /// Samples the local controller state for a frame number. The session asks for a
    /// future frame <see cref="NetplayOptions.InputDelayFrames"/> ahead of the applied one.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public LockstepSession(
        INetplayTransport transport,
        NetplayOptions options,
        Func<long, ControllerState> localInput)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(localInput);
        options.Validate();
        _transport = transport;
        _options = options;
        _localInput = localInput;
        _nextLocalFrame = options.FirstFrame;
        _lastHandshakeSend = -1000;
        SessionId = options.SessionId;
        if (options.Role == NetplayRole.Host && SessionId == 0)
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            SessionId = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }
    }

    /// <summary>Gets the session identifier. The join learns it from the acknowledgement.</summary>
    public ulong SessionId { get; private set; }

    /// <summary>Gets the local player index. The host is player 1; the join is player 2.</summary>
    public byte LocalPlayerIndex => _options.Role == NetplayRole.Host ? (byte)0 : (byte)1;

    /// <summary>Gets the current session state.</summary>
    public NetplaySessionState State { get; private set; }

    /// <summary>Gets the next frame the caller will execute.</summary>
    public long Frame { get; private set; }

    /// <summary>Gets the local input applied to the frame returned by the last successful step.</summary>
    public ControllerState LocalInput { get; private set; }

    /// <summary>Gets the remote input applied to the frame returned by the last successful step.</summary>
    public ControllerState RemoteInput { get; private set; }

    /// <summary>Gets the first detected divergence, when the session desynchronized.</summary>
    public NetplayDesync? Desync { get; private set; }

    /// <summary>Gets the failure or timeout explanation, when the session did not complete.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>Gets the negotiated session options.</summary>
    public NetplayOptions Options => _options;

    /// <summary>Gets the session counters.</summary>
    public NetplayMetrics Metrics { get; } = new();

    /// <summary>Gets the number of frames executed since the last successful start.</summary>
    public long FramesExecuted => Frame - _options.FirstFrame;

    /// <summary>Runs the opening handshake and primes the delayed input queue.</summary>
    /// <returns><see langword="true"/> when both peers are running.</returns>
    public bool Start()
    {
        ThrowIfDisposed();
        if (State != NetplaySessionState.Created)
        {
            throw new InvalidOperationException("The session has already been started.");
        }

        State = NetplaySessionState.Handshaking;
        _handshakeClock.Restart();
        var started = _options.Role == NetplayRole.Host ? StartHost() : StartJoin();
        if (!started)
        {
            return false;
        }

        _handshakeClock.Stop();
        return BeginRunning();
    }

    /// <summary>Starts a session whose handshake and state sync completed on the control channel.</summary>
    /// <remarks>
    /// The caller must already have validated the content hash and loaded the transferred
    /// machine state. Use <see cref="Start"/> for the standalone UDP handshake.
    /// </remarks>
    /// <returns><see langword="true"/> when the input queue was primed.</returns>
    public bool StartNegotiated()
    {
        ThrowIfDisposed();
        if (State != NetplaySessionState.Created)
        {
            throw new InvalidOperationException("The session has already been started.");
        }

        State = NetplaySessionState.Running;
        return BeginRunning();
    }

    /// <summary>Requests a thread-safe stop from another thread.</summary>
    /// <remarks>
    /// The session thread observes the request, sends a close packet, and returns from
    /// <see cref="Step"/>. This method does not touch the transport or session buffers.
    /// </remarks>
    public void RequestStop(NetplayByeReason reason = NetplayByeReason.PeerStopped)
    {
        _stopReason = reason;
        Volatile.Write(ref _stopRequested, true);
    }

    private bool BeginRunning()
    {
        State = NetplaySessionState.Running;
        Frame = _options.FirstFrame;
        while (_nextLocalFrame < _options.FirstFrame + _options.InputDelayFrames)
        {
            _localInputs[_nextLocalFrame] = _localInput(_nextLocalFrame).Buttons;
            _nextLocalFrame++;
        }

        SendInputPacket();
        _resendClock.Restart();
        return true;
    }

    /// <summary>Advances one frame after collecting both peers' input.</summary>
    /// <remarks>
    /// The caller runs the machine immediately after a <see langword="true"/> result,
    /// then calls <see cref="PublishHash"/> with that frame's hashes.
    /// </remarks>
    /// <returns><see langword="true"/> when the frame can be executed.</returns>
    public bool Step()
    {
        ThrowIfDisposed();
        if (Volatile.Read(ref _stopRequested))
        {
            Fail(NetplaySessionState.Failed, _stopReason, "The local peer requested a stop.");
            return false;
        }

        if (State == NetplaySessionState.Completed && !_remoteInputs.ContainsKey(Frame))
        {
            return false;
        }

        if (State != NetplaySessionState.Running && State != NetplaySessionState.Completed)
        {
            return false;
        }

        var frame = Frame;
        while (_nextLocalFrame <= frame + _options.InputDelayFrames)
        {
            _localInputs[_nextLocalFrame] = _localInput(_nextLocalFrame).Buttons;
            _nextLocalFrame++;
        }

        SendInputPacket();
        _waitClock.Restart();
        _resendClock.Restart();
        var spin = new SpinWait();
        while (!_remoteInputs.ContainsKey(frame))
        {
            Pump();
            if (_remoteInputs.ContainsKey(frame))
            {
                break;
            }

            if (Volatile.Read(ref _stopRequested))
            {
                RecordWait();
                Fail(NetplaySessionState.Failed, _stopReason, "The local peer requested a stop.");
                return false;
            }

            if (State != NetplaySessionState.Running)
            {
                RecordWait();
                return false;
            }

            if (_waitClock.Elapsed > _options.StepTimeout)
            {
                RecordWait();
                Fail(NetplaySessionState.TimedOut, NetplayByeReason.Timeout, $"Timed out waiting for frame {frame} input.");
                return false;
            }

            if (_resendClock.ElapsedMilliseconds >= 50)
            {
                if (ResendInputPacket())
                {
                    Metrics.RecordResend();
                }

                SendInputRequest(frame);
                _resendClock.Restart();
            }

            spin.SpinOnce();
        }

        RecordWait();
        LocalInput = new ControllerState(_localInputs[frame]);
        RemoteInput = new ControllerState(_remoteInputs[frame]);
        Frame = frame + 1;
        Prune(frame);
        return true;
    }

    /// <summary>Publishes the hashes produced by the frame that was just executed.</summary>
    /// <param name="frame">Executed frame number.</param>
    /// <param name="frameHash">Deterministic frame hash.</param>
    /// <param name="audioHash">Cumulative PCM hash.</param>
    /// <param name="audioSamples">Cumulative stereo sample count.</param>
    public void PublishHash(long frame, uint frameHash, uint audioHash, long audioSamples)
    {
        ThrowIfDisposed();
        if (State != NetplaySessionState.Running)
        {
            return;
        }

        var hash = new NetplayHash(frame, frameHash, audioHash, audioSamples);
        _localHashes[frame] = hash;
        var length = NetplayCodec.WriteHash(_sendBuffer, hash);
        Send(_sendBuffer.AsSpan(0, length));
        if (_remoteHashes.TryGetValue(frame, out NetplayHash remote))
        {
            CompareHashes(hash, remote);
        }
    }

    /// <summary>Closes a running session normally.</summary>
    public void Complete()
    {
        if (State != NetplaySessionState.Running)
        {
            return;
        }

        SendBye(NetplayByeReason.Normal);
        var grace = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(
            Math.Min(2000, _options.StepTimeout.TotalMilliseconds));
        while (State == NetplaySessionState.Running && grace.Elapsed < timeout)
        {
            Pump();
            if (State != NetplaySessionState.Running)
            {
                break;
            }

            Thread.Sleep(1);
        }

        if (State == NetplaySessionState.Running)
        {
            State = NetplaySessionState.Completed;
        }
    }

    /// <summary>Stops the session and notifies the peer.</summary>
    /// <param name="reason">Close reason.</param>
    public void Stop(NetplayByeReason reason)
    {
        if (State is NetplaySessionState.Running or NetplaySessionState.Handshaking)
        {
            SendBye(reason);
            State = NetplaySessionState.Failed;
            FailureMessage ??= $"The local peer stopped the session ({reason}).";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (State is NetplaySessionState.Running or NetplaySessionState.Handshaking)
        {
            SendBye(NetplayByeReason.PeerStopped);
            State = NetplaySessionState.Failed;
        }

        _disposed = true;
        _transport.Dispose();
    }

    private bool StartHost()
    {
        while (State == NetplaySessionState.Handshaking
            && _handshakeClock.Elapsed <= _options.StepTimeout)
        {
            Pump();
            if (_pendingHandshake is { } hello)
            {
                _pendingHandshake = null;
                if (hello.Role != NetplayRole.Join
                    || hello.PlayerCount != _options.PlayerCount
                    || hello.InputDelay != _options.InputDelayFrames
                    || hello.FirstFrame != _options.FirstFrame
                    || hello.ContentHash != _options.ContentHash
                    || (hello.SessionId != 0 && hello.SessionId != SessionId))
                {
                    Fail(
                        NetplaySessionState.Failed,
                        NetplayByeReason.ContentMismatch,
                        "The joining peer's BIOS, disc, tuning, or protocol settings do not match.");
                    return false;
                }

                SendHelloAck();
                return true;
            }

            Thread.Sleep(1);
        }

        if (State != NetplaySessionState.Handshaking)
        {
            return false;
        }

        Fail(
            NetplaySessionState.TimedOut,
            NetplayByeReason.Timeout,
            "Timed out waiting for a joining peer.");
        return false;
    }

    private void SendHelloAck()
    {
        var acknowledgement = new NetplayHello(
            SessionId,
            _options.ContentHash,
            _options.FirstFrame,
            NetplayRole.Host,
            _options.InputDelayFrames,
            _options.PlayerCount);
        var length = NetplayCodec.WriteHello(
            _sendBuffer,
            NetplayMessageType.HelloAck,
            acknowledgement);
        Send(_sendBuffer.AsSpan(0, length));
    }

    private bool StartJoin()
    {
        while (State == NetplaySessionState.Handshaking
            && _handshakeClock.Elapsed <= _options.StepTimeout)
        {
            if (_handshakeClock.ElapsedMilliseconds - _lastHandshakeSend >= 100)
            {
                _lastHandshakeSend = _handshakeClock.ElapsedMilliseconds;
                var hello = new NetplayHello(
                    0,
                    _options.ContentHash,
                    _options.FirstFrame,
                    NetplayRole.Join,
                    _options.InputDelayFrames,
                    _options.PlayerCount);
                var length = NetplayCodec.WriteHello(_sendBuffer, NetplayMessageType.Hello, hello);
                Send(_sendBuffer.AsSpan(0, length));
            }

            Pump();
            if (_pendingAcknowledgement is { } acknowledgement)
            {
                _pendingAcknowledgement = null;
                if (acknowledgement.Role != NetplayRole.Host
                    || acknowledgement.PlayerCount != _options.PlayerCount
                    || acknowledgement.InputDelay != _options.InputDelayFrames
                    || acknowledgement.FirstFrame != _options.FirstFrame
                    || acknowledgement.ContentHash != _options.ContentHash)
                {
                    Fail(
                        NetplaySessionState.Failed,
                        NetplayByeReason.ContentMismatch,
                        "The hosting peer's BIOS, disc, tuning, or protocol settings do not match.");
                    return false;
                }

                if (_options.SessionId != 0 && acknowledgement.SessionId != _options.SessionId)
                {
                    Fail(
                        NetplaySessionState.Failed,
                        NetplayByeReason.ContentMismatch,
                        "The hosting peer's session identifier does not match.");
                    return false;
                }

                SessionId = acknowledgement.SessionId;
                return true;
            }

            Thread.Sleep(1);
        }

        if (State != NetplaySessionState.Handshaking)
        {
            return false;
        }

        Fail(
            NetplaySessionState.TimedOut,
            NetplayByeReason.Timeout,
            "Timed out waiting for the hosting peer.");
        return false;
    }

    private void Pump()
    {
        for (var processed = 0; processed < 64; processed++)
        {
            if (!_transport.TryReceive(_receiveBuffer, out var length))
            {
                return;
            }

            Metrics.RecordReceived(length);
            ReadOnlySpan<byte> packet = _receiveBuffer.AsSpan(0, length);
            if (!NetplayCodec.TryPeekType(packet, out NetplayMessageType type))
            {
                // Stray traffic on the port is ignored; only valid 5DO datagrams are considered.
                continue;
            }

            switch (type)
            {
                case NetplayMessageType.Hello:
                    if (!NetplayCodec.TryReadHello(packet, type, out NetplayHello hello))
                    {
                        Fail(NetplaySessionState.Failed, NetplayByeReason.ProtocolError, "Malformed hello packet.");
                        return;
                    }

                    _pendingHandshake = hello;
                    if (State == NetplaySessionState.Running)
                    {
                        SendHelloAck();
                    }

                    break;
                case NetplayMessageType.HelloAck:
                    if (!NetplayCodec.TryReadHello(packet, type, out NetplayHello ack))
                    {
                        Fail(NetplaySessionState.Failed, NetplayByeReason.ProtocolError, "Malformed hello acknowledgement.");
                        return;
                    }

                    _pendingAcknowledgement = ack;
                    break;
                case NetplayMessageType.Input:
                    if (!NetplayCodec.TryReadInput(
                            packet,
                            out var player,
                            _receiveHistory,
                            out var count))
                    {
                        Fail(
                            NetplaySessionState.Failed,
                            NetplayByeReason.ProtocolError,
                            $"Malformed input packet ({length} bytes).");
                        return;
                    }

                    AcceptInput(player, _receiveHistory.AsSpan(0, count));
                    break;
                case NetplayMessageType.Hash:
                    if (!NetplayCodec.TryReadHash(packet, out NetplayHash hash))
                    {
                        Fail(NetplaySessionState.Failed, NetplayByeReason.ProtocolError, "Malformed hash packet.");
                        return;
                    }

                    AcceptHash(hash);
                    break;
                case NetplayMessageType.InputRequest:
                    if (!NetplayCodec.TryReadInputRequest(packet, out var requestedFrame))
                    {
                        Fail(
                            NetplaySessionState.Failed,
                            NetplayByeReason.ProtocolError,
                            "Malformed input request.");
                        return;
                    }

                    RespondToInputRequest(requestedFrame);
                    break;
                case NetplayMessageType.Bye:
                    if (!NetplayCodec.TryReadBye(packet, out NetplayByeReason reason))
                    {
                        Fail(NetplaySessionState.Failed, NetplayByeReason.ProtocolError, "Malformed close packet.");
                        return;
                    }

                    if (reason == NetplayByeReason.Normal)
                    {
                        State = NetplaySessionState.Completed;
                        return;
                    }

                    Fail(
                        NetplaySessionState.Failed,
                        reason,
                        $"The remote peer stopped the session ({reason}).");
                    return;
            }
        }
    }

    private void AcceptInput(byte player, ReadOnlySpan<NetplayInputEntry> entries)
    {
        var remotePlayer = LocalPlayerIndex == 0 ? (byte)1 : (byte)0;
        if (player != remotePlayer)
        {
            return;
        }

        foreach (NetplayInputEntry entry in entries)
        {
            if (entry.Frame < Frame)
            {
                Metrics.RecordRedundantInput();
                continue;
            }

            if (entry.Frame > Frame + _options.InputDelayFrames + NetplayCodec.InputRedundancy)
            {
                continue;
            }

            if (!_remoteInputs.TryAdd(entry.Frame, entry.Buttons))
            {
                Metrics.RecordDuplicateInput();
            }
        }
    }

    private void AcceptHash(in NetplayHash hash)
    {
        if (hash.Frame < _options.FirstFrame || hash.Frame > Frame + 16)
        {
            return;
        }

        _remoteHashes[hash.Frame] = hash;
        if (_localHashes.TryGetValue(hash.Frame, out NetplayHash local))
        {
            CompareHashes(local, hash);
        }
    }

    private void CompareHashes(in NetplayHash local, in NetplayHash remote)
    {
        if (State != NetplaySessionState.Running)
        {
            return;
        }

        var framesMatch = local.FrameHash == remote.FrameHash;
        var audioMatches = !_options.CompareAudioHashes
            || (local.AudioHash == remote.AudioHash
                && local.AudioSamples == remote.AudioSamples);
        if (framesMatch && audioMatches)
        {
            return;
        }

        Desync = new NetplayDesync(
            local.Frame,
            local.FrameHash,
            remote.FrameHash,
            local.AudioHash,
            remote.AudioHash,
            local.AudioSamples,
            remote.AudioSamples);
        SendBye(NetplayByeReason.Desync);
        State = NetplaySessionState.Desynchronized;
        FailureMessage =
            $"Frame {local.Frame} diverged (frame {local.FrameHash:X8} vs {remote.FrameHash:X8}, "
            + $"audio {local.AudioHash:X8}/{local.AudioSamples} vs {remote.AudioHash:X8}/{remote.AudioSamples}).";
    }

    private void SendInputPacket() => SendInputHistory(_nextLocalFrame - 1);

    private void SendInputHistory(long newest)
    {
        var oldest = Math.Max(_options.FirstFrame, newest - NetplayCodec.InputRedundancy + 1);
        var count = 0;
        for (var frame = oldest; frame <= newest; frame++)
        {
            if (_localInputs.TryGetValue(frame, out ControllerButton buttons))
            {
                _sendHistory[count++] = new NetplayInputEntry(frame, buttons);
            }
        }

        if (count == 0)
        {
            return;
        }

        var length = NetplayCodec.WriteInput(
            _sendBuffer,
            LocalPlayerIndex,
            _sendHistory.AsSpan(0, count));
        _sendBuffer.AsSpan(0, length).CopyTo(_lastInputPacket);
        _lastInputPacketLength = length;
        _lastInputPacketValid = true;
        Send(_sendBuffer.AsSpan(0, length));
    }

    private void SendInputRequest(long frame)
    {
        var length = NetplayCodec.WriteInputRequest(_sendBuffer, frame);
        Send(_sendBuffer.AsSpan(0, length));
    }

    private void RespondToInputRequest(long frame)
    {
        if (_localInputs.ContainsKey(frame))
        {
            var newest = Math.Min(
                _nextLocalFrame - 1,
                frame + NetplayCodec.InputRedundancy - 1);
            SendInputHistory(newest);
        }
    }

    private bool ResendInputPacket()
    {
        if (!_lastInputPacketValid)
        {
            return false;
        }

        Send(_lastInputPacket.AsSpan(0, _lastInputPacketLength));
        return true;
    }

    private void Send(ReadOnlySpan<byte> packet)
    {
        if (!_transport.HasRemote)
        {
            return;
        }

        _transport.Send(packet);
        Metrics.RecordSent(packet.Length);
    }

    private void SendBye(NetplayByeReason reason)
    {
        if (!_transport.HasRemote)
        {
            return;
        }

        var length = NetplayCodec.WriteBye(_sendBuffer, reason);
        try
        {
            Send(_sendBuffer.AsSpan(0, length));
        }
        catch (InvalidOperationException)
        {
            // The transport lost its endpoint while shutting down; nothing left to notify.
        }
    }

    private void RecordWait()
    {
        Metrics.RecordWait(_waitClock.Elapsed.TotalMilliseconds);
        _waitClock.Stop();
    }

    private void Prune(long frame)
    {
        RemoveBefore(_localInputs, frame - NetplayCodec.InputRedundancy - 2);
        RemoveBefore(_remoteInputs, frame - 2);
        RemoveBefore(_localHashes, frame - 600);
        RemoveBefore(_remoteHashes, frame - 600);
    }

    private static void RemoveBefore<T>(Dictionary<long, T> values, long frame)
    {
        if (values.Count < 128)
        {
            return;
        }

        List<long>? expired = null;
        foreach (long key in values.Keys)
        {
            if (key < frame)
            {
                (expired ??= []).Add(key);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (long key in expired)
        {
            values.Remove(key);
        }
    }

    private void Fail(NetplaySessionState state, NetplayByeReason reason, string message)
    {
        if (State is NetplaySessionState.Completed or NetplaySessionState.Desynchronized)
        {
            return;
        }

        if (State is NetplaySessionState.Running or NetplaySessionState.Handshaking)
        {
            SendBye(reason);
        }

        State = state;
        FailureMessage = message;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
