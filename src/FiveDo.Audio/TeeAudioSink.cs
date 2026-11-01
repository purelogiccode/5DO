using FiveDo.Core;

namespace FiveDo.Audio;

/// <summary>
/// Forwards every sample to two sinks, such as live output plus a WAV recorder.
/// </summary>
/// <remarks>
/// The tee does not own either sink. Disposing it is therefore a no-op; the caller stops
/// and disposes the recorder explicitly after restoring the primary sink.
/// </remarks>
public sealed class TeeAudioSink : IAudioSink
{
    private readonly IAudioSink? _first;
    private readonly IAudioSink? _second;

    /// <summary>Initializes a tee sink.</summary>
    /// <param name="first">First destination, or <see langword="null"/>.</param>
    /// <param name="second">Second destination, or <see langword="null"/>.</param>
    public TeeAudioSink(IAudioSink? first, IAudioSink? second)
    {
        _first = first;
        _second = second;
    }

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        _first?.PushSample(packedSample);
        _second?.PushSample(packedSample);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _first?.Reset();
        _second?.Reset();
    }
}
