using System;
using FiveDo.Video.Vulkan;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Minimal AppKit window for the macOS Vulkan window probe.
/// </summary>
/// <remarks>
/// The probe owns its own <c>NSWindow</c> and <c>CAMetalLayer</c> because it runs without
/// Avalonia. The window is sized in points so the drawable, the swapchain, and the probe's
/// requested pixel size are all 1:1; a Retina scale is a shell concern, not a probe one.
/// </remarks>
internal sealed class MacProbeWindow : IProbeWindow
{
    private nint _application;
    private nint _window;
    private MacMetalSurface? _surface;

    /// <summary>Creates and shows a metal-backed probe window.</summary>
    /// <param name="width">Window width in points/pixels.</param>
    /// <param name="height">Window height in points/pixels.</param>
    /// <exception cref="InvalidOperationException">AppKit or Metal is unavailable.</exception>
    internal MacProbeWindow(int width, int height)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("The macOS probe window is macOS-only.");
        }

        try
        {
            _application = MacObjC.EnsureApplication(finishLaunching: true);
            _window = MacObjC.CreateWindow("5DO Vulkan Window Probe", width, height);
            _surface = new MacMetalSurface(_window, 0, 0, width, height, scale: 1);
            _surface.SetVisible(true);
            MacObjC.Activate(_application);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public VulkanSurface Surface => _surface is null
        ? throw new ObjectDisposedException(nameof(MacProbeWindow))
        : _surface.Surface;

    /// <inheritdoc />
    public void PumpMessages()
    {
        if (_application == 0)
        {
            return;
        }

        _ = MacObjC.PumpEvents(_application);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _surface?.Dispose();
        _surface = null;
        if (_window != 0)
        {
            MacObjC.CloseWindow(_window);
            _window = 0;
        }

        _application = 0;
    }
}
