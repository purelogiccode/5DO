using System;
using ThreeDo.Core;

namespace ThreeDo.Video;

/// <summary>The graphics API behind a renderer.</summary>
public enum RendererApi
{
    /// <summary>The CPU frame assembler fallback.</summary>
    Cpu,

    /// <summary>Desktop OpenGL (WGL/GLX/EGL/macOS core profile).</summary>
    OpenGl,

    /// <summary>OpenGL ES (ANGLE, Raspberry Pi, mobile-class GLES).</summary>
    OpenGlEs,
}

/// <summary>Scaling/filter mode for the required OpenGL presenter.</summary>
public enum VideoFilter
{
    /// <summary>Nearest-neighbor, pixel-perfect, integer scaling; matches the CPU oracle.</summary>
    Accurate,

    /// <summary>Nearest-neighbor with arbitrary scale and letterboxing.</summary>
    Nearest,

    /// <summary>Sharp-bilinear; the default enhanced desktop mode.</summary>
    SharpBilinear,

    /// <summary>CRT scanlines and aperture mask.</summary>
    Crt,

    /// <summary>xBR/HQx-family smoothing (opt-in).</summary>
    Xbr,
}

/// <summary>Output aspect-ratio policy.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The public setting is named Integer scaling.")]
public enum AspectMode
{
    /// <summary>Integer scale with letterboxing.</summary>
    Integer,

    /// <summary>4:3 aspect with letterboxing.</summary>
    FourThree,

    /// <summary>Stretch to the output rectangle.</summary>
    Stretch,

    /// <summary>Explicit custom aspect ratio.</summary>
    Custom,
}

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

    /// <summary>Gets a value indicating whether the FPS/GPU overlay is shown.</summary>
    public bool ShowOverlay { get; init; }

    /// <summary>Gets the CRT scanline strength (0-1).</summary>
    public float ScanlineStrength { get; init; } = 0.25f;

    /// <summary>Validates the settings.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is out of range.</exception>
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
    }
}

/// <summary>Capabilities reported by a renderer backend.</summary>
/// <param name="Name">Backend name.</param>
/// <param name="MajorVersion">OpenGL major version, or 0 for the CPU fallback.</param>
/// <param name="MinorVersion">OpenGL minor version.</param>
/// <param name="MaxTextureSize">Maximum texture dimension.</param>
/// <param name="SupportsIntegerTextures">Whether R16UI/usampler2D are available.</param>
/// <param name="SupportsVsync">Whether the context supports vsync (swap interval).</param>
/// <param name="Api">The graphics API, or <see cref="RendererApi.Cpu"/> for the fallback.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "NotAccessedPositionalProperty.Global",
    Justification = "Capabilities are reported to host tooling; not every field is read yet.")]
public readonly record struct RendererCapabilities(
    string Name,
    int MajorVersion,
    int MinorVersion,
    int MaxTextureSize,
    bool SupportsIntegerTextures,
    bool SupportsVsync,
    RendererApi Api = RendererApi.Cpu)
{
    /// <summary>Gets a value indicating whether the backend meets the OpenGL 3.3 requirement.</summary>
    public bool SupportsOpenGl33 => Api == RendererApi.OpenGl
        && (MajorVersion > 3 || (MajorVersion == 3 && MinorVersion >= 3));

    /// <summary>Gets a value indicating whether this is an OpenGL ES backend.</summary>
    public bool IsOpenGlEs => Api == RendererApi.OpenGlEs;
}

/// <summary>
/// A presentation backend that consumes completed VDL frames.
/// </summary>
/// <remarks>
/// Renderers must not modify emulated state. The CPU <see cref="FrameAssembler"/> remains the
/// accuracy oracle; the required desktop backend is <c>ThreeDo.Video.OpenGL</c>.
/// </remarks>
public interface IVideoRenderer : IDisposable
{
    /// <summary>Gets the backend name.</summary>
    string Name { get; }

    /// <summary>Gets or sets the renderer settings.</summary>
    RendererSettings Settings { get; set; }

    /// <summary>Gets the detected capabilities. Valid after <see cref="Initialize"/>.</summary>
    RendererCapabilities Capabilities { get; }

    /// <summary>Initializes or recreates GPU resources.</summary>
    /// <param name="width">Output width in pixels.</param>
    /// <param name="height">Output height in pixels.</param>
    void Initialize(int width, int height);

    /// <summary>Resizes the output without recreating source textures.</summary>
    /// <param name="width">Output width in pixels.</param>
    /// <param name="height">Output height in pixels.</param>
    void Resize(int width, int height);

    /// <summary>Presents one completed frame.</summary>
    /// <param name="frame">Completed VDL frame.</param>
    void Present(VdlFrame frame);

    /// <summary>Reads the current output back to a BGRA byte array.</summary>
    /// <param name="destination">Destination of at least width*height*4 bytes.</param>
    /// <param name="width">Output width.</param>
    /// <param name="height">Output height.</param>
    void ReadPixels(Span<byte> destination, out int width, out int height);
}
