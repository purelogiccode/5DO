using System;
using System.Runtime.InteropServices;
using FiveDo.Video.Vulkan;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// X11 child window that hosts a Vulkan swapchain inside the Avalonia toplevel.
/// </summary>
/// <remarks>
/// <para>
/// The window is created with the Avalonia toplevel as its parent, positioned over the
/// control's arranged rectangle, and shaped with an empty input region so pointer and
/// keyboard input continue to reach Avalonia.
/// </para>
/// <para>
/// The surface owns its own <c>Display</c> connection. X11 window ids are server-wide,
/// so a child of another client's window is valid, and using a private connection keeps
/// the interop independent of Avalonia's internal X11 types.
/// </para>
/// </remarks>
internal sealed class X11ChildSurface : IDisposable
{
    private const string X11 = "libX11.so.6";
    private const string XExt = "libXext.so.6";
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;
    private const int Unsorted = 0;
    private const int TrueColor = 4;
    private const int InputOutput = 1;
    private const int AllocNone = 0;
    private const nuint CwBackPixel = 1 << 1;
    private const nuint CwBorderPixel = 1 << 3;
    private const nuint CwColormap = 1 << 13;

    private nint _display;
    private nint _window;
    private nint _colormap;

    /// <summary>Creates an unmapped child window of the supplied X11 window.</summary>
    /// <param name="parent">Avalonia toplevel X11 window id.</param>
    /// <param name="width">Initial width in device pixels.</param>
    /// <param name="height">Initial height in device pixels.</param>
    /// <exception cref="InvalidOperationException">X11 is unavailable or window creation failed.</exception>
    internal X11ChildSurface(nint parent, int width, int height)
    {
        _display = XOpenDisplay(0);
        if (_display == 0)
        {
            throw new InvalidOperationException(
                "XOpenDisplay failed. The Vulkan child surface requires an X11 session.");
        }

        try
        {
            _window = CreateWindow(parent, width, height);
            if (_window == 0)
            {
                throw new InvalidOperationException("XCreateWindow failed.");
            }

            _ = XStoreName(_display, _window, "5DO Vulkan video");
            MakeInputTransparent();
            _ = XSync(_display, 0);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the Vulkan surface descriptor for this window.</summary>
    internal VulkanSurface Surface => VulkanSurface.ForXlib(_display, _window);

    /// <summary>Moves and resizes the child window.</summary>
    /// <param name="x">X offset in device pixels.</param>
    /// <param name="y">Y offset in device pixels.</param>
    /// <param name="width">Width in device pixels.</param>
    /// <param name="height">Height in device pixels.</param>
    internal void SetBounds(int x, int y, int width, int height)
    {
        if (_display == 0 || _window == 0)
        {
            return;
        }

        _ = XMoveResizeWindow(_display, _window, x, y, checked((uint)width), checked((uint)height));
        _ = XRaiseWindow(_display, _window);
        _ = XSync(_display, 0);
    }

    /// <summary>Raises the child window above Avalonia's render window.</summary>
    internal void Raise()
    {
        if (_display == 0 || _window == 0)
        {
            return;
        }

        // Avalonia maps its own render window after the control starts and can restack
        // it above this one; presentation re-raises without a round trip.
        _ = XRaiseWindow(_display, _window);
    }

    /// <summary>Maps or unmaps the child window.</summary>
    /// <param name="visible">Whether the video surface should be visible.</param>
    internal void SetVisible(bool visible)
    {
        if (_display == 0 || _window == 0)
        {
            return;
        }

        if (visible)
        {
            _ = XMapWindow(_display, _window);
            // Avalonia renders into its own child window of the toplevel, created after
            // this one. Raise the video window so the swapchain is not covered.
            _ = XRaiseWindow(_display, _window);
        }
        else
        {
            _ = XUnmapWindow(_display, _window);
        }

        _ = XSync(_display, 0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_display != 0 && _window != 0)
        {
            _ = XDestroyWindow(_display, _window);
            _window = 0;
        }

        if (_display != 0 && _colormap != 0)
        {
            _ = XFreeColormap(_display, _colormap);
            _colormap = 0;
        }

        if (_display != 0)
        {
            _ = XCloseDisplay(_display);
            _display = 0;
        }
    }

    private nint CreateWindow(nint parent, int width, int height)
    {
        int screen = XDefaultScreen(_display);
        nint root = XRootWindow(_display, screen);
        if (XMatchVisualInfo(_display, screen, 24, TrueColor, out XVisualInfo visual) != 0)
        {
            // Avalonia's toplevel uses a depth-32 ARGB visual. A child that inherits it is
            // composited with alpha, so unpainted swapchain regions show the desktop
            // through. A 24-bit TrueColor child is opaque.
            _colormap = XCreateColormap(_display, root, visual.Visual, AllocNone);
            XSetWindowAttributes attributes = new()
            {
                BackgroundPixel = 0,
                BorderPixel = 0,
                Colormap = _colormap,
            };
            return XCreateWindow(
                _display,
                parent,
                0,
                0,
                checked((uint)width),
                checked((uint)height),
                0,
                24,
                InputOutput,
                visual.Visual,
                CwBackPixel | CwBorderPixel | CwColormap,
                ref attributes);
        }

        Serilog.Log.Debug("No 24-bit X11 visual is available; using the parent visual.");
        return XCreateSimpleWindow(
            _display,
            parent,
            0,
            0,
            checked((uint)width),
            checked((uint)height),
            0,
            0,
            0);
    }

    private void MakeInputTransparent()
    {
        try
        {
            // An empty ShapeInput region makes the child click-through: X11 keeps
            // delivering pointer events to the parent window.
            XShapeCombineRectangles(_display, _window, ShapeInput, 0, 0, 0, 0, ShapeSet, Unsorted);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Xext is unavailable; the video area will capture pointer input.
            Serilog.Log.Debug(exception, "X11 input shaping is unavailable.");
        }
    }

    [DllImport(X11)]
    private static extern nint XOpenDisplay(nint displayName);

    [DllImport(X11)]
    private static extern int XCloseDisplay(nint display);

    [DllImport(X11)]
    private static extern int XDefaultScreen(nint display);

    [DllImport(X11)]
    private static extern nint XRootWindow(nint display, int screen);

    [StructLayout(LayoutKind.Sequential)]
    private struct XVisualInfo
    {
        public nint Visual;
        public nuint VisualId;
        public int Screen;
        public int Depth;
        public int Class;
        public nuint RedMask;
        public nuint GreenMask;
        public nuint BlueMask;
        public int ColormapSize;
        public int BitsPerRgb;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XSetWindowAttributes
    {
        public nint BackgroundPixmap;
        public nuint BackgroundPixel;
        public nint BorderPixmap;
        public nuint BorderPixel;
        public int BitGravity;
        public int WinGravity;
        public int BackingStore;
        public nuint BackingPlanes;
        public nuint BackingPixel;
        public int SaveUnder;
        public long EventMask;
        public long DoNotPropagateMask;
        public int OverrideRedirect;
        public nint Colormap;
        public nint Cursor;
    }

    [DllImport(X11)]
    private static extern int XMatchVisualInfo(
        nint display,
        int screen,
        int depth,
        int visualClass,
        out XVisualInfo visualInfo);

    [DllImport(X11)]
    private static extern nint XCreateColormap(nint display, nint root, nint visual, int allocate);

    [DllImport(X11)]
    private static extern int XFreeColormap(nint display, nint colormap);

    [DllImport(X11)]
    private static extern nint XCreateWindow(
        nint display,
        nint parent,
        int x,
        int y,
        uint width,
        uint height,
        uint borderWidth,
        int depth,
        int windowClass,
        nint visual,
        nuint valueMask,
        ref XSetWindowAttributes attributes);

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
    private static extern int XMapWindow(nint display, nint window);

    [DllImport(X11)]
    private static extern int XUnmapWindow(nint display, nint window);

    [DllImport(X11)]
    private static extern int XRaiseWindow(nint display, nint window);

    [DllImport(X11)]
    private static extern int XMoveResizeWindow(
        nint display,
        nint window,
        int x,
        int y,
        uint width,
        uint height);

    [DllImport(X11)]
    private static extern int XDestroyWindow(nint display, nint window);

    [DllImport(X11)]
    private static extern int XSync(nint display, int discard);

    [DllImport(XExt)]
    private static extern void XShapeCombineRectangles(
        nint display,
        nint window,
        int destinationKind,
        int xOffset,
        int yOffset,
        nint rectangles,
        int rectangleCount,
        int operation,
        int ordering);
}
