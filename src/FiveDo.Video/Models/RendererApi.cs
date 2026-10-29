using System;
using FiveDo.Core;

namespace FiveDo.Video;


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
