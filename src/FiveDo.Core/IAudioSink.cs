namespace FiveDo.Core;

/// <summary>
/// Receives packed stereo samples from the DSP.
/// </summary>
public interface IAudioSink
{
    /// <summary>Pushes one packed 32-bit stereo sample.</summary>
    /// <param name="packedSample">Packed sample. See <see cref="AudioSample"/>.</param>
    void PushSample(uint packedSample);

    /// <summary>
    /// Drops queued audio after a machine reset or state load.
    /// </summary>
    /// <remarks>
    /// Live backends should clear their ring/queue so the next timeline does not replay
    /// samples from the old one. Offline sinks that intentionally capture continuously
    /// may leave the default no-op implementation.
    /// </remarks>
    void Reset()
    {
    }
}
