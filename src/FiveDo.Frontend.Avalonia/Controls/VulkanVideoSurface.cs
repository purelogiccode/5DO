using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FiveDo.Core;
using FiveDo.Video;
using FiveDo.Video.Vulkan;

namespace FiveDo.Frontend.Avalonia.Controls;

/// <summary>
/// Hosts a native Win32 Vulkan swapchain inside the Avalonia window.
/// </summary>
/// <remarks>
/// <para>
/// The control owns a child HWND positioned over the control's arranged rectangle. The
/// existing <see cref="VulkanOffscreenRenderer"/> renders into its offscreen output image
/// and blits that image into the child window's swapchain, so the steady-state path has no
/// GPU-to-CPU readback and no <c>WriteableBitmap</c> upload.
/// </para>
/// <para>
/// A native child window is used because Avalonia 12 does not expose a Vulkan control
/// base. The child window is click-through (<c>HTTRANSPARENT</c>) and non-activating, so
/// Avalonia keeps keyboard and mouse focus. It is currently Windows-only; other platforms
/// fall back to the offscreen/readback presenter.
/// </para>
/// </remarks>
public sealed class VulkanVideoSurface : Control, IDisposable
{
    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsClipSiblings = 0x04000000;
    private const uint WsClipChildren = 0x02000000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int WmEraseBkgnd = 0x0014;
    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;
    private const int HtTransparent = -1;
    private const int MaNoActivate = 3;
    private const int BlackBrush = 4;
    private const int ErrorClassAlreadyExists = 1410;
    private const string WindowClassName = "FiveDoVulkanVideoSurface";

    private static readonly WndProc WindowProcedure = ChildWindowProcedure;
    private static bool _classRegistered;

    private readonly VdlFrameMailbox _mailbox = new();
    private readonly VdlFrame _presentFrame = new();
    private readonly Action _presentAction;
    private VulkanOffscreenRenderer? _renderer;
    private RendererSettings _settings = new();
    private nint _parentHandle;
    private nint _childHandle;
    private bool _startPending;
    private volatile bool _started;
    private volatile bool _presentationVisible;
    private long _renderedSequence = -1;
    private int _childX = int.MinValue;
    private int _childY;
    private int _childWidth;
    private int _childHeight;
    private bool _firstFrameLogged;
    private double _presentMilliseconds;

    /// <summary>Initializes a new instance of the <see cref="VulkanVideoSurface"/> class.</summary>
    public VulkanVideoSurface()
    {
        _presentAction = PresentLatestFrame;
        ClipToBounds = true;
        IsVisible = false;
    }

    /// <summary>Raised on the UI thread when the Vulkan surface fails.</summary>
    public event EventHandler<VulkanFailedEventArgs>? Failed;

    /// <summary>Raised on the UI thread when the Vulkan device and swapchain are ready.</summary>
    public event EventHandler? Started;

    /// <summary>Raised on the UI thread after a frame is presented.</summary>
    public event EventHandler<FramePresentedEventArgs>? FramePresented;

    /// <summary>Gets or sets the renderer settings.</summary>
    public RendererSettings Settings
    {
        get => _settings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            _settings = value;
            if (_renderer is not null)
            {
                _renderer.Settings = value;
                _renderer.SetVsync(value.Vsync);
            }
        }
    }

    /// <summary>Gets a value indicating whether the native Vulkan surface is running.</summary>
    public bool IsRunning => _started;

    /// <summary>Gets the Vulkan device name, or <see langword="null"/> before initialization.</summary>
    public string? DeviceName => _renderer?.DeviceName;

    /// <summary>Gets the CPU time spent presenting the most recent frame, in milliseconds.</summary>
    public double PresentMilliseconds => _presentMilliseconds;

    /// <summary>Gets the most recent GPU frame time, or <see langword="null"/> when unavailable.</summary>
    public double? GpuMilliseconds => _renderer?.GpuMilliseconds;

    /// <summary>Gets a value indicating whether the swapchain is presenting with FIFO vsync.</summary>
    public bool VsyncActive => _renderer?.SwapchainActive == true
        && string.Equals(_renderer.PresentMode, "fifo", StringComparison.Ordinal);

    /// <summary>
    /// Creates the child window and initializes the Vulkan swapchain.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the surface is ready or will start when the control is
    /// attached; <see langword="false"/> when the platform cannot host the surface.
    /// </returns>
    /// <exception cref="PlatformNotSupportedException">The platform is not Windows.</exception>
    /// <exception cref="InvalidOperationException">The window handle or Vulkan device is unavailable.</exception>
    public bool Start()
    {
        if (_started)
        {
            return true;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The embedded Vulkan surface currently supports Win32 windows only.");
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            // The MainWindow selects its renderer before the control is attached to the
            // visual tree. Start on attachment instead of falling back to the CPU.
            _startPending = true;
            return true;
        }

        var platformHandle = topLevel.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == 0)
        {
            // The native window may not exist yet during startup; retry on layout.
            _startPending = true;
            return true;
        }

        _parentHandle = platformHandle.Handle;

        var (width, height) = GetPixelSize(topLevel);
        _childHandle = CreateChildWindow(_parentHandle, width, height);
        try
        {
            _renderer = new VulkanOffscreenRenderer
            {
                Settings = _settings,
                CaptureToFrame = false,
            };
            _renderer.InitializeSurface(_childHandle, width, height, _settings.Vsync);
        }
        catch (Exception exception)
        {
            Serilog.Log.Debug(exception, "Vulkan surface initialization failed; cleaning up the child window.");
            _renderer?.Dispose();
            _renderer = null;
            DestroyChildWindow();
            _startPending = false;
            throw;
        }

        _started = true;
        _startPending = false;
        _renderedSequence = -1;
        UpdateChildBounds();
        ShowWindow(_childHandle, _presentationVisible ? SwShow : SwHide);
        Started?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Stops presentation and releases the child window and Vulkan device.</summary>
    public void Stop()
    {
        if (!_started && _renderer is null && _childHandle == 0)
        {
            return;
        }

        _started = false;
        _startPending = false;
        _presentationVisible = false;
        _renderedSequence = -1;
        _firstFrameLogged = false;
        _renderer?.Dispose();
        _renderer = null;
        DestroyChildWindow();
        _childX = int.MinValue;
        _childY = 0;
        _childWidth = 0;
        _childHeight = 0;
    }

    /// <summary>Shows or hides the native child window without destroying the device.</summary>
    /// <param name="visible">Whether the emulation video should be visible.</param>
    public void SetPresentationVisible(bool visible)
    {
        _presentationVisible = visible;
        if (_childHandle != 0)
        {
            ShowWindow(_childHandle, visible ? SwShow : SwHide);
        }

        if (visible && _started)
        {
            UpdateChildBounds();
            RequestPresent();
        }
    }

    /// <summary>Copies one machine frame into the presentation mailbox.</summary>
    /// <param name="frame">Completed machine frame.</param>
    public void SubmitFrame(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _mailbox.Write(frame);
        if (_started)
        {
            Dispatcher.UIThread.Post(_presentAction, DispatcherPriority.Render);
        }
    }

    /// <summary>Forces the next render pass to present the latest frame.</summary>
    public void RequestRedraw()
    {
        _renderedSequence = -1;
        if (_started)
        {
            RequestPresent();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
        if (_startPending && !_started)
        {
            try
            {
                Start();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Serilog.Log.Debug(exception, "Vulkan surface start failed during attachment.");
                Stop();
                Failed?.Invoke(this, new VulkanFailedEventArgs(exception));
            }
        }
        else if (_started)
        {
            UpdateChildBounds();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        Stop();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        LayoutUpdated -= OnLayoutUpdated;
        Stop();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_startPending && !_started)
        {
            try
            {
                Start();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Serilog.Log.Debug(exception, "Vulkan surface start failed during layout.");
                Stop();
                Failed?.Invoke(this, new VulkanFailedEventArgs(exception));
            }

            return;
        }

        if (_started)
        {
            UpdateChildBounds();
        }
    }

    private void RequestPresent()
    {
        Dispatcher.UIThread.Post(_presentAction, DispatcherPriority.Render);
    }

    private void PresentLatestFrame()
    {
        if (!_started || !_presentationVisible || _renderer is null)
        {
            return;
        }

        var sequence = _mailbox.Sequence;
        if (sequence == _renderedSequence)
        {
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            _mailbox.Read(_presentFrame, _renderer.Present);
            _renderedSequence = sequence;
            _presentMilliseconds =
                (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            if (!_firstFrameLogged)
            {
                _firstFrameLogged = true;
                Serilog.Log.Information(
                    "Vulkan surface presented its first frame ({Width}x{Height}).",
                    _renderer.SwapchainWidth,
                    _renderer.SwapchainHeight);
            }

            FramePresented?.Invoke(
                this,
                new FramePresentedEventArgs(_presentMilliseconds, _renderer.GpuMilliseconds));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Serilog.Log.Debug(exception, "Vulkan frame presentation failed.");
            Stop();
            Failed?.Invoke(this, new VulkanFailedEventArgs(exception));
        }
    }

    private void UpdateChildBounds()
    {
        if (_childHandle == 0)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        Point? point = this.TranslatePoint(new Point(0, 0), topLevel);
        if (point is null)
        {
            return;
        }

        var scaling = topLevel.RenderScaling;
        var x = (int)Math.Round(point.Value.X * scaling);
        var y = (int)Math.Round(point.Value.Y * scaling);
        var (width, height) = GetPixelSize(topLevel);
        if (x == _childX
            && y == _childY
            && width == _childWidth
            && height == _childHeight)
        {
            return;
        }

        _childX = x;
        _childY = y;
        _childWidth = width;
        _childHeight = height;
        SetWindowPos(
            _childHandle,
            0,
            x,
            y,
            width,
            height,
            SwpNoZOrder | SwpNoActivate | (_presentationVisible ? SwpShowWindow : 0));
        _renderer?.Resize(width, height);
    }

    private (int Width, int Height) GetPixelSize(TopLevel topLevel)
    {
        var scaling = topLevel.RenderScaling;
        var width = Math.Max(1, (int)Math.Round(Bounds.Width * scaling));
        var height = Math.Max(1, (int)Math.Round(Bounds.Height * scaling));
        return (width, height);
    }

    private static nint CreateChildWindow(nint parent, int width, int height)
    {
        nint instance = GetModuleHandle(null);
        EnsureClassRegistered(instance);
        nint handle = CreateWindowEx(
            WsExNoActivate,
            WindowClassName,
            "5DO Vulkan video",
            WsChild | WsVisible | WsClipSiblings | WsClipChildren,
            0,
            0,
            width,
            height,
            parent,
            0,
            instance,
            0);
        if (handle == 0)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        return handle;
    }

    private void DestroyChildWindow()
    {
        if (_childHandle == 0)
        {
            return;
        }

        _ = DestroyWindow(_childHandle);
        _childHandle = 0;
    }

    private static void EnsureClassRegistered(nint instance)
    {
        if (_classRegistered)
        {
            return;
        }

        WndClassEx windowClass = new()
        {
            CbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            LpfnWndProc = WindowProcedure,
            HInstance = instance,
            LpszClassName = WindowClassName,
        };
        if (RegisterClassEx(ref windowClass) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorClassAlreadyExists)
            {
                throw new InvalidOperationException(
                    $"RegisterClassEx failed with Win32 error {error}.");
            }
        }

        _classRegistered = true;
    }

    private static nint ChildWindowProcedure(nint hwnd, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case WmNcHitTest:
                return HtTransparent;
            case WmMouseActivate:
                return MaNoActivate;
            case WmEraseBkgnd:
                if (GetClientRect(hwnd, out Rect rect))
                {
                    _ = FillRect(wParam, ref rect, GetStockObject(BlackBrush));
                }

                return 1;
            default:
                return DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint CbSize;
        public uint Style;
        public WndProc LpfnWndProc;
        public int CbClsExtra;
        public int CbWndExtra;
        public nint HInstance;
        public nint HIcon;
        public nint HCursor;
        public nint HbrBackground;
        public string? LpszMenuName;
        public string LpszClassName;
        public nint HIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint hwnd,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern int FillRect(nint hdc, ref Rect rect, nint brush);

    [DllImport("gdi32.dll")]
    private static extern nint GetStockObject(int index);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
