using System;
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
/// audio thread keeps <see cref="BufferCount"/> queued OpenAL buffers filled from that ring. The
/// OpenAL context is made current on the audio thread because OpenAL's current-context state is
/// thread-local.
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
    private const int BufferFrames = 512;
    private const int BufferCount = 8;

    // Playback starts once this many frames are buffered. The core emits a burst of
    // samples once per video frame while the device drains steadily, and the frontend's
    // UI/render threads can delay the core or fill thread; a deep lead keeps the queue
    // fed through that jitter so the device never plays a gap.
    private const int PrefillFrames = 3072;

    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly uint _source;
    private readonly uint[] _buffers = new uint[BufferCount];
    private readonly int[] _bufferFrameCounts = new int[BufferCount];
    private readonly uint[] _unqueue = new uint[BufferCount];
    private readonly AudioBufferPool _freeBuffers;
    private readonly short[] _samples = new short[BufferFrames * 2];
    private readonly float[] _floatFrames = new float[BufferFrames * 2];
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

    private OpenAlAudioOutput(
        ALContext alc,
        AL al,
        Device* device,
        Context* context,
        AudioRingBuffer ring)
    {
        _alc = alc;
        _al = al;
        _device = device;
        _context = context;
        _ring = ring;

        if (!_alc.MakeContextCurrent(_context))
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
            }
        }

        _al.SetSourceProperty(_source, SourceFloat.Gain, 1f);
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
    public static OpenAlAudioOutput? TryCreate(int capacityFrames = 4096) =>
        TryCreate(capacityFrames, out _);

    /// <summary>Tries to open the default OpenAL device and reports why it failed.</summary>
    /// <param name="capacityFrames">Ring-buffer capacity in stereo frames.</param>
    /// <param name="error">Receives a diagnostic message when the result is null.</param>
    /// <returns>A running output, or <see langword="null"/> when OpenAL is unavailable.</returns>
    public static OpenAlAudioOutput? TryCreate(int capacityFrames, out string? error)
    {
        error = null;
        ALContext? alc = null;
        AL? al = null;
        Device* device = null;
        Context* context = null;
        try
        {
            alc = ALContext.GetApi();
            al = AL.GetApi();
            device = alc.OpenDevice(string.Empty);
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

            if (!alc.MakeContextCurrent(context))
            {
                error = "The OpenAL context could not be made current.";
                return null;
            }

            var output = new OpenAlAudioOutput(alc, al, device, context, new AudioRingBuffer(capacityFrames));
            alc = null;
            al = null;
            device = null;
            context = null;
            return output;
        }
        catch (Exception exception)
        {
            error = exception.Message;
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

    /// <summary>Drops buffered and queued samples after a machine reset or state load.</summary>
    public void Reset() => _flushRequested = true;

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        if (_disposed)
        {
            return;
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
            if (!_alc.MakeContextCurrent(_context))
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
                while (queued < BufferCount && !_stop.IsCancellationRequested)
                {
                    // The emulation emits a burst of samples once per video frame while the
                    // device drains steadily, so build a lead in the ring before the first
                    // block is queued. Without it the ring empties between bursts and the
                    // device plays a gap.
                    if (!started && _ring.FramesAvailable < PrefillFrames)
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
                        frames = BufferFrames;
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
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            multimediaToken.Dispose();
            _alc.MakeContextCurrent(null);
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

        if (!_alc.MakeContextCurrent(_context))
        {
            _al.Dispose();
            _alc.Dispose();
            return;
        }

        _al.SourceStop(_source);
        _al.DeleteSource(_source);
        _al.DeleteBuffers(_buffers);
        _alc.MakeContextCurrent(null);
        _alc.DestroyContext(_context);
        _alc.CloseDevice(_device);
        _al.Dispose();
        _alc.Dispose();
    }
}
