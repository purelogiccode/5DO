using System;
using FiveDo.Core;

namespace FiveDo.Video;


/// <summary>Renderer configuration shared by the desktop presenter and tests.</summary>
public sealed record RendererSettings
{
    /// <summary>Gets the selected filter.</summary>
    public VideoFilter Filter { get; init; } = VideoFilter.SharpBilinear;

    /// <summary>Gets the aspect-ratio policy.</summary>
    public AspectMode Aspect { get; init; } = AspectMode.FourThree;

    /// <summary>Gets the custom aspect ratio when <see cref="Aspect"/> is <see cref="AspectMode.Custom"/>.</summary>
    public double CustomAspect { get; init; } = 4.0 / 3.0;

    /// <summary>Gets a value indicating whether vsync presentation is requested.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Public renderer setting consumed by presentation backends.")]
    public bool Vsync { get; init; } = true;

    /// <summary>Gets a value indicating whether integer scaling is required.</summary>
    public bool IntegerScale { get; init; } = true;

    /// <summary>Gets the CRT scanline strength (0-1).</summary>
    public float ScanlineStrength { get; init; } = 0.25f;

    /// <summary>Gets the CRT bloom strength (0-1). Zero disables bloom.</summary>
    public float Bloom { get; init; } = 0.35f;

    /// <summary>Gets the brightness threshold used by the CRT bloom pass (0-1).</summary>
    public float BloomThreshold { get; init; } = 0.6f;

    /// <summary>Gets the FSR 1.0 RCAS sharpness (0 = soft, 1 = maximum).</summary>
    public float FsrSharpness { get; init; } = 0.8f;

    /// <summary>Validates the settings.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is out of range.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Meziantou.Analyzer",
        "MA0015",
        Justification = "Validate reports the offending property name; it has no parameters of its own.")]
    public void Validate()
    {
        if (!double.IsFinite(CustomAspect) || CustomAspect <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CustomAspect), CustomAspect, "The custom aspect must be positive.");
        }

        if (ScanlineStrength is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(ScanlineStrength), ScanlineStrength, "The scanline strength must be between 0 and 1.");
        }

        if (Bloom is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(Bloom), Bloom, "The bloom strength must be between 0 and 1.");
        }

        if (BloomThreshold is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(BloomThreshold), BloomThreshold, "The bloom threshold must be between 0 and 1.");
        }

        if (FsrSharpness is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(FsrSharpness), FsrSharpness, "The FSR sharpness must be between 0 and 1.");
        }
    }
}
