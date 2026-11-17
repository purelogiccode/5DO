using System;
using FiveDo.Video.Vulkan;

namespace FiveDo.Frontend.Avalonia;

/// <summary>Native window used by the Vulkan window probe.</summary>
internal interface IProbeWindow : IDisposable
{
    /// <summary>Gets the native surface descriptor for the renderer.</summary>
    VulkanSurface Surface { get; }

    /// <summary>Processes pending window-system events.</summary>
    /// <exception cref="InvalidOperationException">The window was closed.</exception>
    void PumpMessages();
}
