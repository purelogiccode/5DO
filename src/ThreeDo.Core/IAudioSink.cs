namespace ThreeDo.Core;

/// <summary>
/// Receives packed stereo samples from the DSP.
/// </summary>
public interface IAudioSink
{
    /// <summary>Pushes one packed 32-bit stereo sample.</summary>
    /// <param name="packedSample">Packed sample. See <see cref="AudioSample"/>.</param>
    void PushSample(uint packedSample);
}
