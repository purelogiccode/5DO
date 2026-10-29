using System;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// A renderer that exposes its last presented frame as a CPU buffer.
/// </summary>
/// <remarks>
/// The CPU fallback and the optional Vulkan offscreen backend use this contract so the
/// desktop shell can display their output through the same <c>WriteableBitmap</c> path.
/// </remarks>
public interface ICpuFrameRenderer : IVideoRenderer
{
    /// <summary>Gets the most recently presented frame buffer.</summary>
    FrameBuffer Frame { get; }
}
