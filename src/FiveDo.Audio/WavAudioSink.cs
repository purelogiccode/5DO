using System;
using FiveDo.Core;

namespace FiveDo.Audio;

/// <summary>
/// Writes DSP samples to a 44.1 kHz, 16-bit stereo WAV file.
/// </summary>
/// <remarks>
/// Used by the CLI and tests for offline audio verification. Samples are buffered to avoid
/// one file write per stereo frame. The header sizes are patched when the sink is disposed;
/// callers should dispose it (or use <c>using</c>) before reading the file.
/// </remarks>
public sealed class WavAudioSink : IAudioSink, IDisposable
{
    private readonly Pcm16WavWriter _writer;
    private readonly AudioStatistics _statistics = new();
    private readonly long _maxSamples;
    private long _sampleCount;

    /// <summary>Initializes a new instance of the <see cref="WavAudioSink"/> class.</summary>
    /// <param name="path">Destination WAV path.</param>
    /// <param name="maxSamples">Maximum stereo frames to write, or zero for unlimited.</param>
    public WavAudioSink(string path, long maxSamples = 0)
    {
        _writer = new Pcm16WavWriter(path, EmulatorConstants.SoundClock);
        _maxSamples = maxSamples > 0 ? maxSamples : long.MaxValue;
    }

    /// <summary>Gets the number of stereo frames written.</summary>
    public long SampleCount => _sampleCount;

    /// <summary>Gets the number of bytes of PCM data written.</summary>
    public long DataBytes => _writer.DataBytes;

    /// <summary>Gets streaming peak/RMS/clipping statistics for the written samples.</summary>
    public AudioStatistics Statistics => _statistics;

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        if (_sampleCount >= _maxSamples)
        {
            return;
        }

        var sample = AudioSample.FromPacked(packedSample);
        _writer.WriteSample(sample.Left, sample.Right);
        _sampleCount++;
        _statistics.Add(sample.Left, sample.Right);
    }

    /// <summary>Continues the capture; WAV files intentionally span resets and state loads.</summary>
    public void Reset()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Dispose();
    }
}
