using System;
using System.Threading;
using Silk.NET.OpenAL;
using ThreeDo.Core;

namespace ThreeDo.Audio;

/// <summary>
/// Live stereo audio output through OpenAL Soft.
/// </summary>
/// <remarks>
/// The emulation thread calls <see cref="PushSample"/> into a lock-free ring buffer. A dedicated
/// audio thread keeps four queued OpenAL buffers filled from that ring. Construction returns
/// <see langword="null"/> from <see cref="TryCreate"/> when no device/context is available, so
/// headless runs and CI fall back to silence.
/// </remarks>
public sealed unsafe class OpenAlAudioOutput : IAudioSink, IDisposable
{
    private const int SampleRate = 44100;
    private const int BufferFrames = 1024;
    private const int BufferCount = 4;

    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly uint _source;
    private readonly uint[] _buffers = new uint[BufferCount];
    private readonly short[] _samples = new short[BufferFrames * 2];
    private readonly float[] _floatFrames = new float[BufferFrames * 2];
    private readonly AudioRingBuffer _ring;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _stop = new();
    private volatile bool _disposed;

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
        _source = _al.GenSource();
        for (var i = 0; i < _buffers.Length; i++)
        {
            _buffers[i] = _al.GenBuffer();
        }

        _al.SetSourceProperty(_source, SourceFloat.Gain, 1f);
        _thread = new Thread(FillLoop) { IsBackground = true, Name = "3DO audio" };
        _thread.Start();
    }

    /// <summary>Gets the number of samples accepted since creation.</summary>
    public long SamplesPushed { get; private set; }

    /// <summary>Gets the number of samples dropped because the ring was full.</summary>
    public long SamplesDropped { get; private set; }

    /// <summary>Tries to open the default OpenAL device.</summary>
    /// <param name="capacityFrames">Ring-buffer capacity in stereo frames.</param>
    /// <returns>A running output, or <see langword="null"/> when OpenAL is unavailable.</returns>
    public static OpenAlAudioOutput? TryCreate(int capacityFrames = 8192)
    {
        try
        {
            var alc = ALContext.GetApi();
            var al = AL.GetApi();
            var device = alc.OpenDevice(string.Empty);
            if (device is null)
            {
                al.Dispose();
                alc.Dispose();
                return null;
            }

            var context = alc.CreateContext(device, null);
            if (context is null)
            {
                alc.CloseDevice(device);
                al.Dispose();
                alc.Dispose();
                return null;
            }

            if (!alc.MakeContextCurrent(context))
            {
                alc.DestroyContext(context);
                alc.CloseDevice(device);
                al.Dispose();
                alc.Dispose();
                return null;
            }

            return new OpenAlAudioOutput(alc, al, device, context, new AudioRingBuffer(capacityFrames));
        }
        catch (Exception)
        {
            return null;
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
            SamplesPushed++;
        }
        else
        {
            SamplesDropped++;
        }
    }

    private void FillLoop()
    {
        var queued = 0;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                while (queued < BufferCount)
                {
                    var frames = _ring.Read(_floatFrames);
                    for (var i = 0; i < frames * 2; i++)
                    {
                        _samples[i] = (short)Math.Clamp((int)(_floatFrames[i] * 32767f), short.MinValue, short.MaxValue);
                    }

                    var buffer = _buffers[queued];
                    _al.BufferData(buffer, BufferFormat.Stereo16, _samples, SampleRate);
                    _al.SourceQueueBuffers(_source, [buffer]);
                    queued++;
                }

                _al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out var processed);
                for (var i = 0; i < processed; i++)
                {
                    _al.SourceUnqueueBuffers(_source, [0u]);
                    queued--;
                }

                _al.GetSourceProperty(_source, GetSourceInteger.SourceState, out var state);
                if (state != (int)SourceState.Playing && queued > 0)
                {
                    _al.SourcePlay(_source);
                }

                Thread.Sleep(5);
            }
        }
        catch (Exception)
        {
            // A lost device or driver failure must not crash emulation.
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
        _al.SourceStop(_source);
        _al.DeleteSource(_source);
        _al.DeleteBuffers(_buffers);
        _alc.MakeContextCurrent(null);
        _alc.DestroyContext(_context);
        _alc.CloseDevice(_device);
        _al.Dispose();
        _alc.Dispose();
        _stop.Dispose();
    }
}
