namespace FiveDo.Host;

/// <summary>Selects the desktop presentation renderer.</summary>
public enum VideoRendererKind
{
    /// <summary>Use the OpenGL presenter (WGL/ANGLE where available).</summary>
    OpenGl,

    /// <summary>Use the optional offscreen Vulkan presenter with CPU readback.</summary>
    Vulkan,

    /// <summary>Use the software CPU presenter.</summary>
    Software,

    /// <summary>Use the optional Direct3D 11 presenter on Windows.</summary>
    Direct3D11,
}
