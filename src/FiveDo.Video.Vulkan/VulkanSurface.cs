using System.Runtime.InteropServices;

namespace FiveDo.Video.Vulkan;

/// <summary>Native window-system surface used for Vulkan presentation.</summary>
public enum VulkanSurfaceKind
{
    /// <summary>A Win32 <c>HWND</c> surface.</summary>
    Win32,

    /// <summary>An X11/Xlib <c>Window</c> surface.</summary>
    Xlib,

    /// <summary>A macOS <c>CAMetalLayer</c> surface (MoltenVK).</summary>
    Metal,
}

/// <summary>
/// Identifies the native window that a Vulkan swapchain presents into.
/// </summary>
/// <param name="Kind">Window-system surface kind.</param>
/// <param name="Window">
/// Native window handle (<c>HWND</c>, X11 <c>Window</c>, or <c>CAMetalLayer*</c>).
/// </param>
/// <param name="Display">X11 <c>Display*</c>; ignored for Win32 and Metal surfaces.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct VulkanSurface(VulkanSurfaceKind Kind, nint Window, nint Display = 0)
{
    /// <summary>Creates a Win32 surface descriptor.</summary>
    /// <param name="hwnd">Win32 window handle.</param>
    /// <returns>The surface descriptor.</returns>
    public static VulkanSurface ForWin32(nint hwnd)
    {
        return new VulkanSurface(VulkanSurfaceKind.Win32, hwnd);
    }

    /// <summary>Creates an X11/Xlib surface descriptor.</summary>
    /// <param name="display">X11 <c>Display*</c> returned by <c>XOpenDisplay</c>.</param>
    /// <param name="window">X11 window id.</param>
    /// <returns>The surface descriptor.</returns>
    public static VulkanSurface ForXlib(nint display, nint window)
    {
        return new VulkanSurface(VulkanSurfaceKind.Xlib, window, display);
    }

    /// <summary>Creates a macOS surface descriptor.</summary>
    /// <param name="caMetalLayer">A <c>CAMetalLayer*</c> owned by the caller.</param>
    /// <returns>The surface descriptor.</returns>
    public static VulkanSurface ForMetal(nint caMetalLayer)
    {
        return new VulkanSurface(VulkanSurfaceKind.Metal, caMetalLayer);
    }
}
