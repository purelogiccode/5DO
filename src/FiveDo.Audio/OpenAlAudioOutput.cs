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
/// audio thread keeps up to three queued OpenAL buffers filled from that ring. The OpenAL context
/// is made current on the audio thread because OpenAL's current-context state is thread-local.
/// </para>
/// <para>
/// Construction returns <see langword="null"/> from <see cref="TryCreate(int, out string?)"/> when no
/// device/context is available, so headless runs and CI fall back to silence. A lost device or
/// driver error is swallowed after it stops the fill loop so emulation can continue.
/// </para>
/// </remarks>
public sealed unsafe class OpenAlAudioOutput : IAudioSink, IDisposable
{
    private const int SampleRate = EmulatorConstants.SoundClock;
    private const int BufferFrames = 512;
    private const int BufferCount = 3;
    private const int PrefillBuffers = 2;

    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly uint _source;
    private readonly uint[] _buffers = new uint[BufferCount];
    private readonly uint[] _unqueue = new uint[BufferCount];
    private readonly short[] _samples = new short[BufferFrames * 2];
    private readonly float[] _floatFrames = new float[BufferFrames * 2];
    private readonly AudioRingBuffer _ring;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _stop = new();
    private volatile bool _disposed;
    private volatile float _volume = 1f;
    private long _samplesPushed;
    private long _samplesDropped;
    private long _underrunBuffers;
    private long _queuedBuffers;
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

        _al.SetSourceProperty(_source, SourceFloat.Gain, 1f);
        _thread = new Thread(FillLoop)
        {
            IsBackground = true,
            Name = "3DO audio",
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

    /// <summary>Gets the number of stereo frames waiting in the emulation ring.</summary>
    public int BufferedFrames => _ring.FramesAvailable;

    /// <summary>Gets the approximate output latency from the ring and queued buffers.</summary>
    public double LatencyMilliseconds =>
        ((QueuedBuffers * BufferFrames) + BufferedFrames) * 1000.0 / SampleRate;

    /// <summary>Gets a value indicating whether the audio thread stopped after a device error.</summary>
    public bool DeviceFailed => _deviceFailed;

    /// <summary>Gets the output sample rate.</summary>
    public const int OutputSampleRate = SampleRate;

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

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        if (_disposed)
        {
            return;
        }

        var sample = AudioSample.FromPacked(packedSample);
        _floatFrames[0] = sample.Left / 32768f;
        _floatFrames[1] = sample.Right / 32768f;
        if (_ring.Write(_floatFrames.AsSpan(0, 2)) == 1)
        {
            Interlocked.Increment(ref _samplesPushed);
        }
        else
        {
            Interlocked.Increment(ref _samplesDropped);
        }
    }

    private void FillLoop()
    {
        var queued = 0;
        var started = false;
        try
        {
            if (!_alc.MakeContextCurrent(_context))
            {
                _deviceFailed = true;
                return;
            }

            while (!_stop.IsCancellationRequested)
            {
                _al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out var processed);
                if (processed > 0)
                {
                    fixed (uint* names = _unqueue)
                    {
                        _al.SourceUnqueueBuffers(_source, processed, names);
                    }

                    queued = Math.Max(0, queued - processed);
                    Interlocked.Exchange(ref _queuedBuffers, queued);
                }

                while (queued < BufferCount && !_stop.IsCancellationRequested)
                {
                    var frames = 0;
                    if (!started && _ring.FramesAvailable >= BufferFrames)
                    {
                        frames = _ring.Read(_floatFrames);
                    }
                    else if (started)
                    {
                        frames = _ring.Read(_floatFrames);
                    }

                    if (frames == 0)
                    {
                        if (!started)
                        {
                            break;
                        }

                        // The ring ran dry. Pad a whole buffer with silence so OpenAL keeps a
                        // stable queue; OpenAL rejects a zero-length buffer and a short queue
                        // would cause a hard stutter.
                        Array.Clear(_floatFrames);
                        frames = BufferFrames;
                        Interlocked.Increment(ref _underrunBuffers);
                    }
                    else if (frames < BufferFrames)
                    {
                        Array.Clear(_floatFrames, frames * 2, (BufferFrames - frames) * 2);
                        frames = BufferFrames;
                        Interlocked.Increment(ref _underrunBuffers);
                    }

                    var gain = _volume;
                    for (var i = 0; i < frames * 2; i++)
                    {
                        _samples[i] = (short)Math.Clamp(
                            (int)(_floatFrames[i] * gain * 32767f),
                            short.MinValue,
                            short.MaxValue);
                    }

                    var buffer = _buffers[queued];
                    _al.BufferData(buffer, BufferFormat.Stereo16, _samples, SampleRate);
                    _al.SourceQueueBuffers(_source, [buffer]);
                    queued++;
                    Interlocked.Exchange(ref _queuedBuffers, queued);

                    if (!started && queued >= PrefillBuffers)
                    {
                        _al.SourcePlay(_source);
                        started = true;
                    }
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
