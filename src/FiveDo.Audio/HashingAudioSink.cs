using FiveDo.Core;

namespace FiveDo.Audio;

/// <summary>
/// Decorates an optional audio sink and hashes every sample that passes through it.
/// </summary>
/// <remarks>
/// A null inner sink makes this a headless hashing sink: the DSP still runs, samples
/// are hashed, and no device or file is touched. The decorator is used by the CLI's
/// deterministic checkpoint output and by the differential harness.
/// </remarks>
public sealed class HashingAudioSink : IAudioSink
{
    private readonly IAudioSink? _inner;

    /// <summary>Initializes a hashing sink.</summary>
    /// <param name="inner">Optional sink that receives the samples after hashing.</param>
    public HashingAudioSink(IAudioSink? inner = null)
    {
        _inner = inner;
    }

    /// <summary>Gets the hash accumulator.</summary>
    public PcmHasher Hasher { get; } = new();

    /// <summary>Gets the number of samples received.</summary>
    public long SampleCount => Hasher.SampleCount;

    /// <summary>Gets the current sample hash.</summary>
    public uint Hash => Hasher.Hash;

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        Hasher.Add(packedSample);
        _inner?.PushSample(packedSample);
    }

    /// <inheritdoc />
    public void Reset()
    {
        Hasher.Reset();
        _inner?.Reset();
    }
}
