
namespace FiveDo.Video;


/// <summary>Capabilities reported by a renderer backend.</summary>
/// <param name="Name">Backend name.</param>
/// <param name="MajorVersion">Graphics API major version, or 0 for the CPU fallback.</param>
/// <param name="MinorVersion">Graphics API minor version.</param>
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
