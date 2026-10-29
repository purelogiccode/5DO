using System;
using FiveDo.Core;

namespace FiveDo.Video;


/// <summary>
/// Optional timing information exposed by a renderer. Implementations may return
/// <see langword="null"/> when GPU timing is unsupported or not yet available.
/// </summary>
public interface IRendererTiming
{
    /// <summary>Gets the most recent GPU frame time in milliseconds, or <see langword="null"/>.</summary>
    double? GpuMilliseconds { get; }
}
