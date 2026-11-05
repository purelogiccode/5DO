using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using FiveDo.Core;
using FiveDo.Machine;

namespace FiveDo.Host.Rewind;

/// <summary>
/// Keeps a deterministic rewind history for a running machine.
/// </summary>
/// <remarks>
/// <para>
/// The buffer stores compressed save states at a fixed keyframe interval and a copy of the
/// PBUS packet for every frame. Rewinding to an arbitrary frame loads the nearest earlier
/// keyframe, replays the recorded packets, and restores the original input, audio, and frame
/// sinks. The guest therefore sees exactly the same input stream it saw the first time.
/// </para>
/// <para>
/// History is cleared automatically when the machine's frame counter jumps, which happens
/// after a reset, a save-state load, or a disc change. The core loop captures history on the
/// emulation thread; UI reads and rewind requests take the same lock and are safe while the
/// core is paused.
/// </para>
/// </remarks>
public sealed class RewindBuffer : IDisposable
{
    private static readonly StateSaveOptions FastCaptureOptions = new()
    {
        // Rewind snapshots are transient and captured on the emulation thread. Fastest
        // Brotli cuts the capture cost by roughly six times while keeping the history
        // within the memory budget.
        CompressionLevel = CompressionLevel.Fastest,
        // The snapshot is consumed by the same process immediately, so the per-section
        // CRC pass (several megabytes) is not worth its cost on the emulation thread.
        ChecksumsEnabled = false,
        InitialCapacity = 1024 * 1024,
    };

    private readonly FiveDoMachine _machine;
    private readonly RewindOptions _options;
    private readonly List<Keyframe> _keyframes = new();
    private readonly Stack<byte[]> _statePool = new();
    private readonly MemoryStream _captureStream = new(1024 * 1024);
    private readonly StateWriter _stateWriter = new(1024 * 1024);
    private readonly byte[] _inputs;
    private readonly int _inputCapacity;
    private readonly int _inputPacketSize;
    private readonly Lock _gate = new();
    private long _newestFrame = -1;
    private long _lastCapturedFrame = -1;
    private long _lastKeyframeFrame = -1;
    private long _memoryUsage;

    /// <summary>Initializes a new instance of the <see cref="RewindBuffer"/> class.</summary>
    /// <param name="machine">Machine whose state is captured.</param>
    /// <param name="options">Optional buffer configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="machine"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the options are invalid.</exception>
    public RewindBuffer(FiveDoMachine machine, RewindOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
        _options = options ?? RewindOptions.Default;
        _options.Validate();
        _inputPacketSize = machine.Madam.PbusQueue.Length;
        _inputCapacity = checked((_options.Seconds * 60) + _options.KeyframeInterval + 2);
        _inputs = new byte[checked(_inputCapacity * _inputPacketSize)];
    }

    /// <summary>Gets a value indicating whether history recording is enabled.</summary>
    public bool Enabled => _options.Enabled;

    /// <summary>Gets the configured history length.</summary>
    public TimeSpan Window => TimeSpan.FromSeconds(_options.Seconds);

    /// <summary>Gets a value indicating whether at least one frame can be rewound.</summary>
    public bool CanRewind
    {
        get
        {
            lock (_gate)
            {
                return _options.Enabled
                    && _keyframes.Count > 0
                    && _machine.FramesExecuted == _newestFrame
                    && _newestFrame > _keyframes[0].Frame;
            }
        }
    }

    /// <summary>Gets the oldest frame still held by the buffer, or -1 when empty.</summary>
    public long OldestFrame
    {
        get
        {
            lock (_gate)
            {
                return _keyframes.Count == 0 ? -1 : _keyframes[0].Frame;
            }
        }
    }

    /// <summary>Gets the newest frame held by the buffer, or -1 when empty.</summary>
    public long NewestFrame
    {
        get
        {
            lock (_gate)
            {
                return _newestFrame;
            }
        }
    }

    /// <summary>Gets the approximate number of bytes held by compressed keyframes.</summary>
    public long MemoryUsage
    {
        get
        {
            lock (_gate)
            {
                return _memoryUsage;
            }
        }
    }

    /// <summary>Gets the number of retained keyframes.</summary>
    public int KeyframeCount
    {
        get
        {
            lock (_gate)
            {
                return _keyframes.Count;
            }
        }
    }

    /// <summary>Clears all history and records the current machine state as the first keyframe.</summary>
    public void ResetHistory()
    {
        lock (_gate)
        {
            ClearCore();
            if (!_options.Enabled)
            {
                return;
            }

            var frame = _machine.FramesExecuted;
            AddKeyframe(frame);
            CaptureInput(frame);
            _newestFrame = frame;
            _lastCapturedFrame = frame;
        }
    }

    /// <summary>Clears all history.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            ClearCore();
        }
    }

    /// <summary>
    /// Records the state and input for a completed frame.
    /// </summary>
    /// <param name="frame">The machine's frame counter after the frame completed.</param>
    public void Capture(long frame)
    {
        lock (_gate)
        {
            if (!_options.Enabled)
            {
                return;
            }

            if (_lastCapturedFrame >= 0 && frame != _lastCapturedFrame + 1)
            {
                // A reset or state load changed the timeline. Keep the new timeline only.
                ClearCore();
            }

            if (_keyframes.Count == 0)
            {
                AddKeyframe(frame);
            }

            CaptureInput(frame);
            _newestFrame = frame;
            _lastCapturedFrame = frame;

            if (frame - _lastKeyframeFrame >= _options.KeyframeInterval)
            {
                AddKeyframe(frame);
            }

            TrimCore();
        }
    }

    /// <summary>
    /// Rewinds the machine to a frame, replaying recorded input after the nearest keyframe.
    /// </summary>
    /// <param name="targetFrame">Target frame. The value is clamped to the retained window.</param>
    /// <returns><see langword="true"/> when the machine was restored; otherwise <see langword="false"/>.</returns>
    public bool RewindTo(long targetFrame)
    {
        lock (_gate)
        {
            return RewindToCore(targetFrame);
        }
    }

    /// <summary>Rewinds the machine by a number of frames.</summary>
    /// <param name="frames">Frame count. Values below one rewind to the oldest retained frame.</param>
    /// <returns><see langword="true"/> when the machine was restored; otherwise <see langword="false"/>.</returns>
    public bool Rewind(int frames)
    {
        lock (_gate)
        {
            return RewindToCore(_machine.FramesExecuted - Math.Max(0, frames));
        }
    }

    private bool RewindToCore(long targetFrame)
    {
        if (!_options.Enabled || _keyframes.Count == 0)
        {
            return false;
        }

        if (_machine.FramesExecuted != _newestFrame)
        {
            // A state load or reset happened outside the capture path. Do not rewind
            // into a different timeline.
            ClearCore();
            return false;
        }

        var clamped = Math.Clamp(targetFrame, _keyframes[0].Frame, _newestFrame);
        if (clamped != _machine.FramesExecuted)
        {
            Keyframe keyframe = FindKeyframe(clamped);
            if (keyframe.Frame == clamped)
            {
                LoadKeyframe(keyframe);
            }
            else
            {
                Replay(keyframe, clamped);
            }
        }

        TrimAfter(clamped);
        return true;
    }

    private void Replay(Keyframe keyframe, long targetFrame)
    {
        IAudioSink? audioSink = _machine.AudioSink;
        IFrameSink? frameSink = _machine.FrameSink;
        IPbusSource? pbusSource = _machine.PbusSource;
        var replaySource = new ReplayPbusSource(_inputs, _inputCapacity, _inputPacketSize, keyframe.Frame + 1);

        _machine.AudioSink = null;
        _machine.FrameSink = null;
        _machine.PbusSource = replaySource;
        try
        {
            LoadKeyframe(keyframe);
            while (_machine.FramesExecuted < targetFrame)
            {
                _machine.RunFrame(false);
            }
        }
        finally
        {
            _machine.PbusSource = pbusSource;
            _machine.FrameSink = frameSink;
            _machine.AudioSink = audioSink;
        }
    }

    private void LoadKeyframe(Keyframe keyframe)
    {
        using var stream = new MemoryStream(keyframe.State, 0, keyframe.Length, writable: false);
        _machine.LoadState(stream);
    }

    private void CaptureInput(long frame)
    {
        var index = (int)(frame % _inputCapacity);
        _machine.Madam.PbusQueue.CopyTo(
            _inputs.AsSpan(index * _inputPacketSize, _inputPacketSize));
    }

    private void AddKeyframe(long frame)
    {
        _captureStream.SetLength(0);
        _captureStream.Position = 0;
        _machine.SaveState(_captureStream, FastCaptureOptions, reusableWriter: _stateWriter);
        var length = checked((int)_captureStream.Length);
        byte[] state = RentStateBuffer(length);
        _captureStream.GetBuffer().AsSpan(0, length).CopyTo(state);
        _keyframes.Add(new Keyframe(frame, state, length));
        _memoryUsage += length;
        _lastKeyframeFrame = frame;
    }

    private byte[] RentStateBuffer(int length)
    {
        while (_statePool.Count > 0)
        {
            byte[] buffer = _statePool.Pop();
            if (buffer.Length >= length)
            {
                return buffer;
            }
        }

        return GC.AllocateUninitializedArray<byte>(length);
    }

    private void ReturnStateBuffer(byte[] buffer)
    {
        _statePool.Push(buffer);
    }

    private void TrimCore()
    {
        var oldestAllowed = _newestFrame - (_options.Seconds * 60L);
        while (_keyframes.Count > 1 && _keyframes[0].Frame < oldestAllowed)
        {
            RemoveFirst();
        }

        while (_keyframes.Count > 1 && _memoryUsage > _options.MemoryLimitBytes)
        {
            RemoveFirst();
        }
    }

    private void TrimAfter(long targetFrame)
    {
        while (_keyframes.Count > 0 && _keyframes[^1].Frame > targetFrame)
        {
            Keyframe removed = _keyframes[^1];
            _keyframes.RemoveAt(_keyframes.Count - 1);
            _memoryUsage -= removed.Length;
            ReturnStateBuffer(removed.State);
        }

        _newestFrame = targetFrame;
        _lastCapturedFrame = targetFrame;
        _lastKeyframeFrame = _keyframes.Count > 0 ? _keyframes[^1].Frame : -1;
    }

    private void RemoveFirst()
    {
        Keyframe removed = _keyframes[0];
        _keyframes.RemoveAt(0);
        _memoryUsage -= removed.Length;
        ReturnStateBuffer(removed.State);
    }

    private Keyframe FindKeyframe(long targetFrame)
    {
        var low = 0;
        var high = _keyframes.Count - 1;
        Keyframe result = _keyframes[0];
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            Keyframe candidate = _keyframes[middle];
            if (candidate.Frame <= targetFrame)
            {
                result = candidate;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            ClearCore();
            _captureStream.Dispose();
        }
    }

    private void ClearCore()
    {
        foreach (Keyframe keyframe in _keyframes)
        {
            ReturnStateBuffer(keyframe.State);
        }

        _keyframes.Clear();
        _memoryUsage = 0;
        _newestFrame = -1;
        _lastCapturedFrame = -1;
        _lastKeyframeFrame = -1;
    }

    private sealed class Keyframe
    {
        internal Keyframe(long frame, byte[] state, int length)
        {
            Frame = frame;
            State = state;
            Length = length;
        }

        internal long Frame { get; }

        internal byte[] State { get; }

        internal int Length { get; }
    }

    private sealed class ReplayPbusSource : IPbusSource
    {
        private readonly byte[] _inputs;
        private readonly int _capacity;
        private readonly int _packetSize;
        private long _frame;

        internal ReplayPbusSource(byte[] inputs, int capacity, int packetSize, long firstFrame)
        {
            _inputs = inputs;
            _capacity = capacity;
            _packetSize = packetSize;
            _frame = firstFrame;
        }

        public int GetPbusData(Span<byte> destination)
        {
            var index = (int)(_frame % _capacity);
            var length = Math.Min(_packetSize, destination.Length);
            _inputs.AsSpan(index * _packetSize, length).CopyTo(destination);
            _frame++;
            return length;
        }
    }
}
