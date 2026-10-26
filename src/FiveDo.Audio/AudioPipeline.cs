using System;
using FiveDo.Core;

namespace FiveDo.Audio;

/// <summary>
/// Converts packed DSP samples into normalized float frames in an <see cref="AudioRingBuffer"/>.
/// </summary>
/// <remarks>
/// The host audio callback reads from <see cref="Buffer"/> while the emulation thread calls
/// <see cref="PushSample"/>. When the ring is full the newest sample is dropped, matching the
/// usual emulator behaviour of dropping audio rather than blocking the emulation thread.
/// </remarks>
public sealed class AudioPipeline : IAudioSink
{
    private const float SampleScale = 1f / 32768f;

    private readonly AudioRingBuffer _buffer;
    private readonly float[] _pair = new float[2];
    private long _pushed;
    private long _dropped;

    /// <summary>Initializes a new instance of the <see cref="AudioPipeline"/> class.</summary>
    /// <param name="capacityFrames">Ring-buffer capacity in stereo frames.</param>
    public AudioPipeline(int capacityFrames = 4096)
    {
        _buffer = new AudioRingBuffer(capacityFrames);
    }

    /// <summary>Gets the sample ring buffer shared with the audio callback.</summary>
    public AudioRingBuffer Buffer => _buffer;

    /// <summary>Gets the number of samples accepted since construction.</summary>
    public long SamplesPushed => _pushed;

    /// <summary>Gets the number of samples dropped because the ring was full.</summary>
    public long SamplesDropped => _dropped;

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        var sample = AudioSample.FromPacked(packedSample);
        _pair[0] = sample.Left * SampleScale;
        _pair[1] = sample.Right * SampleScale;

        if (_buffer.Write(_pair) == 1)
        {
            _pushed++;
        }
        else
        {
            _dropped++;
        }
    }

    /// <summary>Reads normalized interleaved stereo frames for the audio callback.</summary>
    /// <param name="destination">Destination span.</param>
    /// <returns>The number of frames read.</returns>
    public int Read(Span<float> destination) => _buffer.Read(destination);

    /// <summary>Resets the counters and empties the ring.</summary>
    public void Reset()
    {
        _buffer.Clear();
        _pushed = 0;
        _dropped = 0;
    }
}
