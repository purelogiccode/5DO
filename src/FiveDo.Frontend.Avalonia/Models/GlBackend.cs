

namespace FiveDo.Frontend.Avalonia;


/// <summary>
/// Selects the Windows renderer backend used by the Avalonia OpenGL surface.
/// </summary>
public enum GlBackend
{
    /// <summary>Prefer desktop WGL, then ANGLE/GLES, then software.</summary>
    Auto,

    /// <summary>Force the desktop OpenGL (WGL) backend.</summary>
    Wgl,

    /// <summary>Force the ANGLE/EGL OpenGL ES backend.</summary>
    Angle,

    /// <summary>Force Avalonia's software renderer (no OpenGL surface).</summary>
    Software,

    /// <summary>Use the optional Vulkan presenter.</summary>
    Vulkan,

    /// <summary>Use the optional Direct3D 11 presenter on Windows.</summary>
    Direct3D11,
}
