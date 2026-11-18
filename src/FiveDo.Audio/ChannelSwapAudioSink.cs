using System;
using FiveDo.Core;

namespace FiveDo.Audio;

/// <summary>
/// Swaps the left and right channels of every sample before forwarding it.
/// </summary>
/// <remarks>
/// <para>
/// Some 3DO titles route effects or music to the "wrong" side on real hardware, and users
/// with a single speaker or an inverted setup need the channels exchanged. The swap is a
/// host presentation option: it runs after the emulated DSP has produced the sample, so
/// frame hashes, state hashes, and the audio hash of the emulated stream are unchanged.
/// </para>
/// <para>
/// Wrap only the live output. WAV recordings and the audio hash should keep the raw
/// emulated channel order.
/// </para>
/// </remarks>
public sealed class ChannelSwapAudioSink : IAudioSink
{
    private readonly IAudioSink _inner;

    /// <summary>Initializes a channel-swapping sink.</summary>
    /// <param name="inner">Sink that receives the swapped samples.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    public ChannelSwapAudioSink(IAudioSink inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Gets the wrapped sink.</summary>
    public IAudioSink Inner => _inner;

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        AudioSample sample = AudioSample.FromPacked(packedSample);
        _inner.PushSample(new AudioSample(sample.Right, sample.Left).Packed);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _inner.Reset();
    }
}
