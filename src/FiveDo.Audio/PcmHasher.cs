using System;
using System.Buffers.Binary;

namespace FiveDo.Audio;

/// <summary>
/// Accumulates a deterministic FNV-1a hash over packed stereo PCM samples.
/// </summary>
/// <remarks>
/// A packed sample stores the left channel in the low 16 bits and the right channel
/// in the high 16 bits. Samples are hashed as four little-endian bytes so the result
/// is independent of host endianness and matches the differential harness.
/// </remarks>
public sealed class PcmHasher
{
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;

    private uint _hash = FnvOffsetBasis;
    private long _sampleCount;

    /// <summary>Gets the current FNV-1a hash.</summary>
    public uint Hash => _hash;

    /// <summary>Gets the number of samples hashed.</summary>
    public long SampleCount => _sampleCount;

    /// <summary>Adds one packed stereo sample.</summary>
    /// <param name="packedSample">Packed sample. See <see cref="FiveDo.Core.AudioSample"/>.</param>
    public void Add(uint packedSample)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, packedSample);
        foreach (var value in bytes)
        {
            _hash = unchecked((_hash ^ value) * FnvPrime);
        }

        _sampleCount++;
    }

    /// <summary>Adds an interleaved stereo S16 batch.</summary>
    /// <param name="samples">Interleaved left/right pairs.</param>
    public void Add(ReadOnlySpan<short> samples)
    {
        if ((samples.Length & 1) != 0)
        {
            throw new ArgumentException("Stereo sample data must contain whole frames.", nameof(samples));
        }

        for (var i = 0; i < samples.Length; i += 2)
        {
            var packed = (uint)(ushort)samples[i] | ((uint)(ushort)samples[i + 1] << 16);
            Add(packed);
        }
    }

    /// <summary>Resets the accumulator to its initial state.</summary>
    public void Reset()
    {
        _hash = FnvOffsetBasis;
        _sampleCount = 0;
    }
}
