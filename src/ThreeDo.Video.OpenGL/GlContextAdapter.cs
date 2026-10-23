using System;
using Silk.NET.OpenGL;

namespace ThreeDo.Video.OpenGL;

/// <summary>
/// Thin adapter over an OpenGL context/function loader. The Avalonia surface creates the
/// context and passes the <see cref="GL"/> instance; this project never references Avalonia.
/// </summary>
public sealed class GlContextAdapter
{
    /// <summary>Initializes a new instance of the <see cref="GlContextAdapter"/> class.</summary>
    /// <param name="gl">Silk.NET OpenGL API for the current context.</param>
    /// <param name="framebuffer">Framebuffer handle to render into; 0 is the default framebuffer.</param>
    public GlContextAdapter(GL gl, int framebuffer = 0)
    {
        ArgumentNullException.ThrowIfNull(gl);
        Gl = gl;
        Framebuffer = framebuffer;
    }

    /// <summary>Gets the OpenGL API.</summary>
    public GL Gl { get; }

    /// <summary>Gets or sets the destination framebuffer handle.</summary>
    public int Framebuffer { get; set; }
}
