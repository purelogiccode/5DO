using System;
using System.Runtime.InteropServices;
using FiveDo.Video.Vulkan;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Minimal X11 window for the Linux Vulkan window probe.
/// </summary>
/// <remarks>
/// The probe needs a real X11 window so <c>VK_KHR_xlib_surface</c> can present through
/// the same windowing path a desktop shell would use. The class intentionally owns its
/// own <c>Display</c> connection: the probe is a standalone process, so there is no
/// Avalonia platform handle to borrow.
/// </remarks>
internal sealed class X11ProbeWindow : IProbeWindow
{
    private const string X11 = "libX11.so.6";
    private const int DestroyNotify = 17;
    private const long StructureNotifyMask = 1L << 17;
    private const long ExposureMask = 1L << 15;

    private nint _display;
    private nint _window;

    /// <summary>Opens the default display and creates a mapped window.</summary>
    /// <param name="width">Window width in pixels.</param>
    /// <param name="height">Window height in pixels.</param>
    /// <exception cref="InvalidOperationException">X11 is unavailable or window creation failed.</exception>
    internal X11ProbeWindow(int width, int height)
    {
        _display = XOpenDisplay(0);
        if (_display == 0)
        {
            throw new InvalidOperationException(
                "XOpenDisplay failed. Set DISPLAY and XAUTHORITY, or use the offscreen probe.");
        }

        try
        {
            int screen = XDefaultScreen(_display);
            nint root = XRootWindow(_display, screen);
            _window = XCreateSimpleWindow(
                _display,
                root,
                0,
                0,
                checked((uint)width),
                checked((uint)height),
                0,
                0,
                0);
            if (_window == 0)
            {
                throw new InvalidOperationException("XCreateSimpleWindow failed.");
            }

            _ = XStoreName(_display, _window, "5DO Vulkan Window Probe");
            _ = XSelectInput(_display, _window, StructureNotifyMask | ExposureMask);
            _ = XMapWindow(_display, _window);
            _ = XFlush(_display);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public VulkanSurface Surface => VulkanSurface.ForXlib(_display, _window);

    /// <inheritdoc />
    public void PumpMessages()
    {
        if (_display == 0)
        {
            return;
        }

        while (XPending(_display) > 0)
        {
            _ = XNextEvent(_display, out XEvent xEvent);
            if (xEvent.Type == DestroyNotify)
            {
                throw new InvalidOperationException("The Vulkan window was closed.");
            }
        }

        _ = XFlush(_display);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_display != 0 && _window != 0)
        {
            _ = XDestroyWindow(_display, _window);
            _window = 0;
        }

        if (_display != 0)
        {
            _ = XCloseDisplay(_display);
            _display = 0;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEvent
    {
        [FieldOffset(0)]
        public int Type;
    }

    [DllImport(X11)]
    private static extern nint XOpenDisplay(nint displayName);

    [DllImport(X11)]
    private static extern int XDefaultScreen(nint display);

    [DllImport(X11)]
    private static extern nint XRootWindow(nint display, int screen);

    [DllImport(X11)]
    private static extern nint XCreateSimpleWindow(
        nint display,
        nint parent,
        int x,
        int y,
        uint width,
        uint height,
        uint borderWidth,
        nuint border,
        nuint background);

    [DllImport(X11)]
    private static extern int XStoreName(nint display, nint window, string windowName);

    [DllImport(X11)]
    private static extern int XSelectInput(nint display, nint window, long eventMask);

    [DllImport(X11)]
    private static extern int XMapWindow(nint display, nint window);

    [DllImport(X11)]
    private static extern int XFlush(nint display);

    [DllImport(X11)]
    private static extern int XPending(nint display);

    [DllImport(X11)]
    private static extern int XNextEvent(nint display, out XEvent xEvent);

    [DllImport(X11)]
    private static extern int XDestroyWindow(nint display, nint window);

    [DllImport(X11)]
    private static extern int XCloseDisplay(nint display);
}
