using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using FiveDo.Core;
using Silk.NET.OpenAL;

namespace FiveDo.Audio;

/// <summary>
/// Live stereo audio output through OpenAL Soft.
/// </summary>
/// <remarks>
/// <para>
/// The emulation thread calls <see cref="PushSample"/> into a lock-free ring buffer. A dedicated
/// audio thread keeps <see cref="BufferCount"/> queued OpenAL buffers filled from that ring.
/// OpenAL's basic current-context state is process-wide, so the output makes its context
/// current per thread through <c>ALC_EXT_thread_local_context</c> when available. This keeps
/// concurrent outputs (live device switching or the Settings test tone) from resetting each
/// other's context.
/// </para>
/// <para>
/// Construction returns <see langword="null"/> from <see cref="TryCreate(int, out string?)"/> when no
/// device/context is available, so headless runs and CI fall back to silence. A lost device or
/// driver error is swallowed after it stops the fill loop so emulation can continue.
/// </para>
/// <para>
/// Set <c>FIVEDO_AUDIO_TAP</c> to a path prefix to record the exact queued PCM and a per-buffer
/// timing log; see <see cref="AudioTap"/>.
/// </para>
/// </remarks>
public sealed unsafe class OpenAlAudioOutput : IAudioSink, IDisposable
{
    private const int SampleRate = EmulatorConstants.SoundClock;
    private const int DefaultBufferMilliseconds = 100;

    private readonly int _bufferFrames;
    private readonly int _bufferCount;
    private readonly int _prefillFrames;
    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly delegate* unmanaged<Context*, byte> _setThreadContext;
    private readonly uint _source;
    private readonly uint[] _buffers;
    private readonly int[] _bufferFrameCounts;
    private readonly uint[] _unqueue;
    private readonly AudioBufferPool _freeBuffers;
    private readonly short[] _samples;
    private readonly float[] _floatFrames;
    private readonly float[] _pushFrame = new float[2];
    private readonly AudioRingBuffer _ring;
    private readonly AudioTap? _tap;
    private readonly string? _tapPath;
    private readonly string? _tapError;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _stop = new();
    private volatile bool _disposed;
    private volatile bool _flushRequested;
    private volatile float _volume = 1f;
    private long _samplesPushed;
    private long _samplesDropped;
    private long _underrunBuffers;
    private long _queuedBuffers;
    private long _queuedFrames;
    private long _alErrors;
    private volatile string? _lastAlError;
    private int _peakBits;
    private volatile bool _deviceFailed;
    private readonly int _syncOffsetFrames;
    private int _silenceFramesRemaining;
    private int _dropFramesRemaining;

    private OpenAlAudioOutput(
        ALContext alc,
        AL al,
        Device* device,
        Context* context,
        delegate* unmanaged<Context*, byte> setThreadContext,
        AudioRingBuffer ring,
        AudioBufferConfiguration configuration)
    {
        _alc = alc;
        _al = al;
        _device = device;
        _context = context;
        _setThreadContext = setThreadContext;
        _ring = ring;
        _bufferFrames = configuration.BufferFrames;
        _bufferCount = configuration.BufferCount;
        _prefillFrames = configuration.PrefillFrames;
        _syncOffsetFrames = configuration.SyncOffsetFrames;
        _silenceFramesRemaining = Math.Max(0, _syncOffsetFrames);
        _dropFramesRemaining = Math.Max(0, -_syncOffsetFrames);
        _buffers = new uint[_bufferCount];
        _bufferFrameCounts = new int[_bufferCount];
        _unqueue = new uint[_bufferCount];
        _samples = new short[_bufferFrames * 2];
        _floatFrames = new float[_bufferFrames * 2];

        if (!MakeContextCurrent())
        {
            throw new InvalidOperationException("The OpenAL context could not be made current.");
        }

        _source = _al.GenSource();
        for (var i = 0; i < _buffers.Length; i++)
        {
            _buffers[i] = _al.GenBuffer();
        }

        _freeBuffers = new AudioBufferPool(_buffers);

        var tapPrefix = Environment.GetEnvironmentVariable(AudioTap.EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(tapPrefix))
        {
            _tapPath = tapPrefix;
            try
            {
                _tap = new AudioTap(tapPrefix);
            }
            catch (Exception exception)
            {
                _tapError = exception.Message;
                Serilog.Log.Warning(exception, "Audio tap could not be created; continuing without it.");
            }
        }

        _al.SetSourceProperty(_source, SourceFloat.Gain, 1f);
        ClearContext();
        _thread = new Thread(FillLoop)
        {
            IsBackground = true,
            Name = "3DO audio",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
    }

    /// <summary>Gets or sets the output gain (0-1). Safe to change while audio is running.</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Gets the number of samples accepted since creation.</summary>
    public long SamplesPushed => Interlocked.Read(ref _samplesPushed);

    /// <summary>Gets the number of samples dropped because the ring was full.</summary>
    public long SamplesDropped => Interlocked.Read(ref _samplesDropped);

    /// <summary>Gets the number of buffers that had to be padded because the ring ran dry.</summary>
    public long UnderrunBuffers => Interlocked.Read(ref _underrunBuffers);

    /// <summary>Gets the number of buffers currently queued on the OpenAL source.</summary>
    public long QueuedBuffers => Interlocked.Read(ref _queuedBuffers);

    /// <summary>Gets the number of frames currently queued on the OpenAL source.</summary>
    public long QueuedFrames => Interlocked.Read(ref _queuedFrames);

    /// <summary>Gets the number of OpenAL errors observed while queueing buffers.</summary>
    public long AlErrors => Interlocked.Read(ref _alErrors);

    /// <summary>Gets the most recent OpenAL error, or <see langword="null"/> when none occurred.</summary>
    public string? LastAlError => _lastAlError;

    /// <summary>Gets a value indicating whether the PCM tap is recording.</summary>
    public bool TapEnabled => _tap is not null;

    /// <summary>Gets the tap path prefix, or <see langword="null"/> when the tap was not requested.</summary>
    public string? TapPath => _tapPath;

    /// <summary>Gets the tap creation error, or <see langword="null"/> when the tap was created or not requested.</summary>
    public string? TapError => _tapError;

    /// <summary>Gets the number of stereo frames waiting in the emulation ring.</summary>
    public int BufferedFrames => _ring.FramesAvailable;

    /// <summary>Gets the approximate output latency from the ring and queued buffers.</summary>
    public double LatencyMilliseconds =>
        (QueuedFrames + BufferedFrames) * 1000.0 / SampleRate;

    /// <summary>Gets a value indicating whether the audio thread stopped after a device error.</summary>
    public bool DeviceFailed => _deviceFailed;

    /// <summary>Gets the output sample rate.</summary>
    public const int OutputSampleRate = SampleRate;

    /// <summary>Gets the OpenAL buffer size used for silence padding and queue chunks.</summary>
    public int BufferFrames => _bufferFrames;

    /// <summary>Gets the number of OpenAL buffers queued by the fill loop.</summary>
    public int BufferCount => _bufferCount;

    /// <summary>Gets the ring frames required before playback starts.</summary>
    public int PrefillFrames => _prefillFrames;

    /// <summary>Gets the configured audio sync offset in milliseconds.</summary>
    public int SyncOffsetMilliseconds => _syncOffsetFrames * 1000 / SampleRate;

    /// <summary>
    /// Computes the queue shape for a requested latency in milliseconds.
    /// </summary>
    /// <param name="milliseconds">Requested target latency, clamped to 20-500 ms.</param>
    /// <param name="syncMilliseconds">
    /// Audio sync offset, clamped to -250 to 250 ms. Positive values delay audio by
    /// inserting silence; negative values advance it by dropping initial samples.
    /// </param>
    /// <returns>Buffer frames, buffer count, prefill frames, ring capacity, and sync offset.</returns>
    public static AudioBufferConfiguration ComputeBufferConfiguration(
        int milliseconds,
        int syncMilliseconds = 0)
    {
        int clamped = Math.Clamp(milliseconds, 20, 500);
        int targetFrames = (int)((long)SampleRate * clamped / 1000);
        const int bufferFrames = 256;
        int bufferCount = Math.Clamp(
            (targetFrames + bufferFrames - 1) / bufferFrames,
            4,
            32);
        int prefillFrames = Math.Max(bufferFrames, bufferCount * bufferFrames / 2);
        int syncFrames = (int)((long)SampleRate
            * Math.Clamp(syncMilliseconds, -250, 250) / 1000);
        int ringCapacity = Math.Max(targetFrames * 2, bufferCount * bufferFrames * 2);
        ringCapacity += Math.Abs(syncFrames);
        return new AudioBufferConfiguration(
            bufferFrames,
            bufferCount,
            prefillFrames,
            ringCapacity,
            syncFrames);
    }

    /// <summary>Takes the peak absolute level of the audio queued since the previous call.</summary>
    /// <returns>A value from 0 to 1, or zero when nothing was queued.</returns>
    public float TakePeakLevel()
    {
        var bits = Interlocked.Exchange(ref _peakBits, 0);
        return BitConverter.Int32BitsToSingle(bits);
    }

    /// <summary>Tries to open the default OpenAL device.</summary>
    /// <param name="capacityFrames">Ring-buffer capacity in stereo frames.</param>
    /// <returns>A running output, or <see langword="null"/> when OpenAL is unavailable.</returns>
    public static OpenAlAudioOutput? TryCreate(int capacityFrames = 4096)
    {
        return TryCreate(capacityFrames, out _);
    }

    /// <summary>Tries to open the default OpenAL device and reports why it failed.</summary>
    /// <param name="capacityFrames">Ring-buffer capacity in stereo frames.</param>
    /// <param name="error">Receives a diagnostic message when the result is null.</param>
    /// <returns>A running output, or <see langword="null"/> when OpenAL is unavailable.</returns>
    public static OpenAlAudioOutput? TryCreate(int capacityFrames, out string? error)
    {
        AudioBufferConfiguration configuration = ComputeBufferConfiguration(DefaultBufferMilliseconds);
        return TryCreate(
            Math.Max(capacityFrames, configuration.RingCapacityFrames),
            deviceName: null,
            configuration,
            out error);
    }

    /// <summary>Tries to open an OpenAL device with a requested latency.</summary>
    /// <param name="bufferMilliseconds">Target buffer latency, 20-500 ms.</param>
    /// <param name="deviceName">Device name, or <see langword="null"/> for the default device.</param>
    /// <param name="error">Receives a diagnostic message when the result is null.</param>
    /// <returns>A running output, or <see langword="null"/> when OpenAL is unavailable.</returns>
    public static OpenAlAudioOutput? TryCreate(
        int bufferMilliseconds,
        string? deviceName,
        out string? error)
    {
        return TryCreate(bufferMilliseconds, deviceName, syncMilliseconds: 0, out error);
    }

    /// <summary>Tries to open an OpenAL device with a requested latency and audio sync offset.</summary>
    /// <param name="bufferMilliseconds">Target buffer latency, 20-500 ms.</param>
    /// <param name="deviceName">Device name, or <see langword="null"/> for the default device.</param>
    /// <param name="syncMilliseconds">Audio sync offset, -250 to 250 ms.</param>
    /// <param name="error">Receives a diagnostic message when the result is null.</param>
    /// <returns>A running output, or <see langword="null"/> when OpenAL is unavailable.</returns>
    public static OpenAlAudioOutput? TryCreate(
        int bufferMilliseconds,
        string? deviceName,
        int syncMilliseconds,
        out string? error)
    {
        AudioBufferConfiguration configuration = ComputeBufferConfiguration(
            bufferMilliseconds,
            syncMilliseconds);
        return TryCreate(configuration.RingCapacityFrames, deviceName, configuration, out error);
    }

    private static OpenAlAudioOutput? TryCreate(
        int capacityFrames,
        string? deviceName,
        AudioBufferConfiguration configuration,
        out string? error)
    {
        error = null;
        ALContext? alc = null;
        AL? al = null;
        Device* device = null;
        Context* context = null;
        delegate* unmanaged<Context*, byte> setThreadContext;
        try
        {
            alc = ALContext.GetApi();
            al = AL.GetApi();
            device = alc.OpenDevice(string.IsNullOrWhiteSpace(deviceName) ? string.Empty : deviceName);
            if (device is null)
            {
                error = "No OpenAL playback device was found.";
                return null;
            }

            context = alc.CreateContext(device, null);
            if (context is null)
            {
                error = "The OpenAL context could not be created.";
                return null;
            }

            // OpenAL's current context is process-wide; concurrent outputs (device switching
            // or the Settings test tone) would otherwise fight over it. OpenAL Soft exposes
            // the thread-local context extension, which keeps each fill thread independent.
            setThreadContext = GetSetThreadContext(alc, device);
            var output = new OpenAlAudioOutput(
                alc,
                al,
                device,
                context,
                setThreadContext,
                new AudioRingBuffer(capacityFrames),
                configuration);
            alc = null;
            al = null;
            device = null;
            context = null;
            return output;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Serilog.Log.Warning(exception, "OpenAL audio output could not be created.");
            return null;
        }
        finally
        {
            if (context is not null)
            {
                alc?.DestroyContext(context);
            }

            if (device is not null)
            {
                alc?.CloseDevice(device);
            }

            al?.Dispose();
            alc?.Dispose();
        }
    }

    private static delegate* unmanaged<Context*, byte> GetSetThreadContext(
        ALContext alc,
        Device* device)
    {
        if (!alc.IsExtensionPresent(device, "ALC_EXT_thread_local_context"))
        {
            return null;
        }

        void* address = alc.GetProcAddress(device, "alcSetThreadContext");
        return address is null
            ? null
            : (delegate* unmanaged<Context*, byte>)address;
    }

    private bool MakeContextCurrent()
    {
        return _setThreadContext is not null
            ? _setThreadContext(_context) != 0
            : _alc.MakeContextCurrent(_context);
    }

    private void ClearContext()
    {
        if (_setThreadContext is not null)
        {
            _setThreadContext(null);
        }
        else
        {
            _alc.MakeContextCurrent(null);
        }
    }

    /// <summary>Enumerates the OpenAL playback devices visible to the process.</summary>
    /// <returns>Device names; the list may contain only the default device when enumeration is unsupported.</returns>
    public static IReadOnlyList<string> EnumerateDevices()
    {
        var devices = new List<string>();
        ALContext? alc = null;
        try
        {
            alc = ALContext.GetApi();
            if (alc.IsExtensionPresent(null, "ALC_ENUMERATE_ALL_EXT"))
            {
                void* address = alc.GetProcAddress(null, "alcGetString");
                if (address is not null)
                {
                    var getString = (delegate* unmanaged<Device*, int, byte*>)address;
                    byte* cursor = getString(null, 0x1013);
                    while (cursor is not null && *cursor != 0)
                    {
                        var start = cursor;
                        while (*cursor != 0)
                        {
                            cursor++;
                        }

                        var name = Marshal.PtrToStringAnsi((IntPtr)start);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            devices.Add(name);
                        }

                        cursor++;
                    }
                }
            }

            if (devices.Count == 0)
            {
                var name = alc.GetContextProperty(null, GetContextString.DeviceSpecifier);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    devices.Add(name);
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Serilog.Log.Debug(exception, "OpenAL device enumeration failed.");
        }
        finally
        {
            alc?.Dispose();
        }

        return devices;
    }

    /// <summary>Drops buffered and queued samples after a machine reset or state load.</summary>
    public void Reset()
    {
        _silenceFramesRemaining = Math.Max(0, _syncOffsetFrames);
        _dropFramesRemaining = Math.Max(0, -_syncOffsetFrames);
        _flushRequested = true;
    }

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        if (_disposed)
        {
            return;
        }

        if (_dropFramesRemaining > 0)
        {
            _dropFramesRemaining--;
            return;
        }

        // Positive sync offsets delay the stream by queueing silence ahead of the first
        // real sample. The ring has enough extra capacity from ComputeBufferConfiguration.
        if (_silenceFramesRemaining > 0)
        {
            _pushFrame[0] = 0f;
            _pushFrame[1] = 0f;
            if (_ring.Write(_pushFrame) == 1)
            {
                Interlocked.Increment(ref _samplesPushed);
                _silenceFramesRemaining--;
            }
            else
            {
                Interlocked.Increment(ref _samplesDropped);
            }
        }

        var sample = AudioSample.FromPacked(packedSample);
        _pushFrame[0] = sample.Left / 32768f;
        _pushFrame[1] = sample.Right / 32768f;
        if (_ring.Write(_pushFrame) == 1)
        {
            Interlocked.Increment(ref _samplesPushed);
        }
        else
        {
            Interlocked.Increment(ref _samplesDropped);
        }
    }

    private int BufferSlot(uint name)
    {
        for (var i = 0; i < _buffers.Length; i++)
        {
            if (_buffers[i] == name)
            {
                return i;
            }
        }

        return -1;
    }

    private void FillLoop()
    {
        var queued = 0;
        var queuedFrames = 0L;
        var started = false;
        MultimediaThreadToken multimediaToken = default;
        try
        {
            if (!MakeContextCurrent())
            {
                _deviceFailed = true;
                return;
            }

            // Register as a multimedia thread so Windows schedules it ahead of ordinary
            // UI/render work. Without this the fill loop can miss its wake-up under load
            // and the device queue underruns, which is heard as crackle.
            multimediaToken = MultimediaThread.Begin("Pro Audio");
            _al.GetError();

            while (!_stop.IsCancellationRequested)
            {
                if (_flushRequested)
                {
                    _flushRequested = false;
                    _al.SourceStop(_source);
                    if (queued > 0)
                    {
                        fixed (uint* names = _unqueue)
                        {
                            _al.SourceUnqueueBuffers(_source, queued, names);
                        }

                        queued = 0;
                        queuedFrames = 0;
                        Array.Clear(_bufferFrameCounts);
                        _freeBuffers.Reset(_buffers);
                        Interlocked.Exchange(ref _queuedBuffers, 0);
                        Interlocked.Exchange(ref _queuedFrames, 0);
                    }

                    _ring.Clear();
                    started = false;
                }

                _al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out var processed);
                if (processed > 0)
                {
                    fixed (uint* names = _unqueue)
                    {
                        _al.SourceUnqueueBuffers(_source, processed, names);
                    }

                    for (var i = 0; i < processed; i++)
                    {
                        var name = _unqueue[i];
                        var slot = BufferSlot(name);
                        if (slot >= 0)
                        {
                            queuedFrames -= _bufferFrameCounts[slot];
                            _bufferFrameCounts[slot] = 0;
                        }

                        _freeBuffers.Return(name);
                    }

                    queued = Math.Max(0, queued - processed);
                    Interlocked.Exchange(ref _queuedBuffers, queued);
                    Interlocked.Exchange(ref _queuedFrames, queuedFrames);
                }

                var batchPeak = 0f;
                while (queued < _bufferCount && !_stop.IsCancellationRequested)
                {
                    // The emulation emits a burst of samples once per video frame while the
                    // device drains steadily, so build a lead in the ring before the first
                    // block is queued. Without it the ring empties between bursts and the
                    // device plays a gap.
                    if (!started && _ring.FramesAvailable < _prefillFrames)
                    {
                        break;
                    }

                    // Only ever fill a buffer the source no longer owns. The queued count
                    // is not a buffer index: OpenAL releases the oldest buffers first, so
                    // indexing by it would rewrite audio that is still attached and about
                    // to play, which is heard as clicks and time jumps.
                    if (!_freeBuffers.TryRent(out var buffer))
                    {
                        break;
                    }

                    // Queue exactly what the emulation produced. The DSP emits whole
                    // frames (735 samples at 60 Hz), so padding a short read to a fixed
                    // block size would insert audible silence on nearly every block.
                    // OpenAL accepts variable-size buffers, so concatenate the samples
                    // as they arrive and never synthesise gaps.
                    var frames = _ring.Read(_floatFrames);
                    var underrun = false;
                    if (frames == 0)
                    {
                        if (queued > 0)
                        {
                            // The queue still has buffered audio; wait for more samples
                            // rather than padding silence into the stream.
                            _freeBuffers.Return(buffer);
                            break;
                        }

                        // The queue is empty and the ring is dry: emit one silent block so
                        // the source keeps running instead of underrunning and restarting.
                        frames = _bufferFrames;
                        Array.Clear(_floatFrames);
                        underrun = true;
                        Interlocked.Increment(ref _underrunBuffers);
                    }

                    var gain = _volume;
                    var peak = 0f;
                    for (var i = 0; i < frames * 2; i++)
                    {
                        var limited = AudioSoftLimiter.Limit(_floatFrames[i] * gain);
                        var absolute = MathF.Abs(limited);
                        if (absolute > peak)
                        {
                            peak = absolute;
                        }

                        _samples[i] = (short)Math.Clamp(
                            (int)(limited * 32767f),
                            short.MinValue,
                            short.MaxValue);
                    }

                    if (peak > batchPeak)
                    {
                        batchPeak = peak;
                    }

                    fixed (short* samples = _samples)
                    {
                        _al.BufferData(buffer, BufferFormat.Stereo16, samples, frames * 2 * sizeof(short), SampleRate);
                    }

                    _al.SourceQueueBuffers(_source, [buffer]);
                    var error = _al.GetError();
                    if (error != AudioError.NoError)
                    {
                        Interlocked.Increment(ref _alErrors);
                        _lastAlError = error.ToString();
                    }

                    var slot = BufferSlot(buffer);
                    if (slot >= 0)
                    {
                        _bufferFrameCounts[slot] = frames;
                    }

                    queued++;
                    queuedFrames += frames;
                    Interlocked.Exchange(ref _queuedBuffers, queued);
                    Interlocked.Exchange(ref _queuedFrames, queuedFrames);

                    if (!started)
                    {
                        _al.SourcePlay(_source);
                        started = true;
                    }

                    _tap?.Write(
                        _samples.AsSpan(0, frames * 2),
                        frames,
                        _ring.FramesAvailable,
                        queued,
                        queuedFrames,
                        underrun,
                        peak,
                        error == AudioError.NoError ? null : error.ToString());
                }

                if (batchPeak > 0f)
                {
                    Interlocked.Exchange(ref _peakBits, BitConverter.SingleToInt32Bits(batchPeak));
                }

                if (started)
                {
                    _al.GetSourceProperty(_source, GetSourceInteger.SourceState, out var state);
                    if (state != (int)SourceState.Playing && queued > 0)
                    {
                        _al.SourcePlay(_source);
                    }
                }

                Thread.Sleep(2);
            }
        }
        catch (Exception exception)
        {
            // A lost device or driver failure must not crash emulation.
            _deviceFailed = true;
            Serilog.Log.Error(exception, "OpenAL audio fill loop stopped; the device failed.");
        }
        finally
        {
            multimediaToken.Dispose();
            ClearContext();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Cancel();
        _thread.Join(1000);
        _tap?.Dispose();
        _stop.Dispose();
        if (_deviceFailed)
        {
            _al.Dispose();
            _alc.Dispose();
            return;
        }

        if (!MakeContextCurrent())
        {
            _al.Dispose();
            _alc.Dispose();
            return;
        }

        _al.SourceStop(_source);
        _al.DeleteSource(_source);
        _al.DeleteBuffers(_buffers);
        ClearContext();
        _alc.DestroyContext(_context);
        _alc.CloseDevice(_device);
        _al.Dispose();
        _alc.Dispose();
    }
}

/// <summary>OpenAL queue sizing derived from a requested latency.</summary>
/// <param name="BufferFrames">Frames per queued OpenAL buffer.</param>
/// <param name="BufferCount">Maximum queued buffers.</param>
/// <param name="PrefillFrames">Ring frames buffered before playback starts.</param>
/// <param name="RingCapacityFrames">Suggested ring-buffer capacity.</param>
/// <param name="SyncOffsetFrames">
/// Positive frames of silence inserted before the stream, or negative frames dropped from
/// the head. Zero keeps the historical queue shape.
/// </param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct AudioBufferConfiguration(
    int BufferFrames,
    int BufferCount,
    int PrefillFrames,
    int RingCapacityFrames,
    int SyncOffsetFrames = 0);
