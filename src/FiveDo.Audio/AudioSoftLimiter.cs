using System;

namespace FiveDo.Audio;

/// <summary>
/// A soft peak limiter for host audio output.
/// </summary>
/// <remarks>
/// <para>
/// The 3DO DSP output reaches full scale in some game mixes (Road Rash's race mix clips
/// in the emulated samples themselves). Sending those samples to a host resampler at full
/// scale makes the resampler overshoot above 1.0 and hard-clip, which is heard as pops.
/// </para>
/// <para>
/// Content below the threshold is passed through untouched, so quiet material keeps its
/// exact waveform. Only the excess above the threshold is compressed into the ceiling,
/// which preserves loudness while removing the hard clip.
/// </para>
/// </remarks>
public static class AudioSoftLimiter
{
    /// <summary>Level above which peaks are compressed (0-1).</summary>
    public const float Threshold = 0.8f;

    /// <summary>Maximum output level after compression (0-1).</summary>
    public const float Ceiling = 0.9f;

    /// <summary>
    /// Limits one sample. Values below <see cref="Threshold"/> are returned unchanged.
    /// </summary>
    /// <param name="value">Sample in the range -1..1.</param>
    /// <returns>
    /// The limited sample, guaranteed to stay within -<see cref="Ceiling"/>..<see cref="Ceiling"/>.
    /// </returns>
    public static float Limit(float value)
    {
        var magnitude = MathF.Abs(value);
        if (magnitude <= Threshold)
        {
            return value;
        }

        // Compress the excess into the remaining headroom with a smooth knee.
        var excess = magnitude - Threshold;
        var range = Ceiling - Threshold;
        var compressed = Threshold + (MathF.Tanh(excess / range) * range);
        return value < 0f ? -compressed : compressed;
    }
}
