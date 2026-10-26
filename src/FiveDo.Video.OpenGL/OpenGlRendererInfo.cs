namespace FiveDo.Video.OpenGL;

/// <summary>
/// Identity and minimum requirements for the OpenGL presentation backend.
/// </summary>
/// <remarks>
/// The OpenGL backend is the primary renderer for the desktop frontend. It receives
/// the emulated VDL frame, performs palette expansion in a fragment shader, applies
/// the selected scaling/filter pipeline, and presents the result with vsync.
/// A CPU frame-assembly path remains available as an accuracy oracle and as a
/// fallback for headless runs or systems without a usable OpenGL context.
/// </remarks>
public static class OpenGlRendererInfo
{
    /// <summary>The display name of this backend.</summary>
    public const string BackendName = "OpenGL";

    /// <summary>The GLSL version targeted by the shader pipeline.</summary>
    public const string ShaderLanguage = "GLSL 330 core";

    /// <summary>The minimum required OpenGL major version.</summary>
    public const int MinimumMajorVersion = 3;

    /// <summary>The minimum required OpenGL minor version.</summary>
    public const int MinimumMinorVersion = 3;
}
