using System;
using FiveDo.Core;

namespace FiveDo.Video;


/// <summary>
/// A presentation backend that consumes completed VDL frames.
/// </summary>
/// <remarks>
/// Renderers must not modify emulated state. The CPU <see cref="FrameAssembler"/> remains the
/// accuracy oracle; the required desktop backend is <c>FiveDo.Video.OpenGL</c>.
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
