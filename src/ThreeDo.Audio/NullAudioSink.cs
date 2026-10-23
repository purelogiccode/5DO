using System;
using ThreeDo.Core;

namespace ThreeDo.Audio;

/// <summary>
/// An audio sink that discards samples, used by headless runs and tests.
/// </summary>
public sealed class NullAudioSink : IAudioSink
{
    private long _samples;

    /// <summary>Gets the number of samples received.</summary>
    public long SampleCount => _samples;

    /// <inheritdoc />
    public void PushSample(uint packedSample) => _samples++;
}
