using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using FiveDo.Core;
using FiveDo.Video;
using FiveDo.Video.OpenGL;
using Silk.NET.OpenGL;

namespace FiveDo.Frontend.Avalonia.Controls;

/// <summary>
/// Avalonia OpenGL host for the required 3DO presenter.
/// </summary>
/// <remarks>
/// The emulation thread calls <see cref="SubmitFrame"/>; this control reads the latest snapshot
/// on the UI thread. A GL 3.3 context uses <see cref="OpenGlRenderer"/>; when the context or an
/// integer-texture feature is unavailable the host should swap in <see cref="CpuVideoRenderer"/>
/// (the control exposes <see cref="IsOpenGlAvailable"/> for that decision).
/// </remarks>
public sealed class VideoSurface : OpenGlControlBase, IDisposable
{
    private static readonly Action<VdlFrame> NoOp = _ => { };

    private readonly VdlFrameMailbox _mailbox = new();
    private readonly VdlFrame _presentFrame = new();
    private readonly Action _requestRender;
    private GL? _gl;
    private string? _glVendor;
    private string? _glRenderer;
    private GlContextAdapter? _adapter;
    private OpenGlRenderer? _renderer;
    private OffscreenRenderTarget? _offscreen;
    private Action<VdlFrame>? _present;
    private RendererSettings _settings = new();
    private long _renderedSequence = -1;
    private int _renderedWidth;
    private int _renderedHeight;
    private bool _captureRequested;
    private double _presentMilliseconds;
    private long _rendererAllocatedBytes;
    private long _rendererOnlyAllocatedBytes;

    /// <summary>Initializes a new instance of the <see cref="VideoSurface"/> class.</summary>
    public VideoSurface()
    {
        // Cache the method-group delegate: SubmitFrame is called once per emulated
        // frame, and allocating a new delegate on every frame violates the presenter's
        // zero-allocation steady-state contract.
        _requestRender = RequestNextFrameRendering;
    }

    /// <summary>Gets the CPU time spent presenting the most recent GL frame, in milliseconds.</summary>
    public double PresentMilliseconds => _presentMilliseconds;

    /// <summary>
    /// Gets or sets a value indicating whether the control measures managed allocations
    /// made by the renderer/mailbox present call. Intended for the GPU probe.
    /// </summary>
    public bool MeasureAllocations { get; set; }

    /// <summary>Gets the managed bytes allocated by the renderer/mailbox present call.</summary>
    public long RendererAllocatedBytes => _rendererAllocatedBytes;

    /// <summary>Gets the managed bytes allocated by the renderer present call alone.</summary>
    public long RendererOnlyAllocatedBytes => _rendererOnlyAllocatedBytes;

    /// <summary>Gets a value indicating whether the renderer stage counters are active.</summary>
    public bool RendererMeasureAllocations => _renderer?.MeasureAllocations ?? false;

    /// <summary>Gets managed bytes allocated by the renderer's entire last present call.</summary>
    public long LastRendererPresentAllocatedBytes => _renderer?.LastPresentAllocatedBytes ?? 0;

    /// <summary>Resets the cumulative allocation counters used by the GPU probe.</summary>
    public void ResetAllocationCounters()
    {
        _rendererAllocatedBytes = 0;
        _rendererOnlyAllocatedBytes = 0;
    }

    /// <summary>Gets managed bytes allocated by the renderer's last upload stage.</summary>
    public long LastRendererUploadAllocatedBytes => _renderer?.LastUploadAllocatedBytes ?? 0;

    /// <summary>Gets managed bytes allocated by the renderer's last palette stage.</summary>
    public long LastRendererPaletteAllocatedBytes => _renderer?.LastPaletteAllocatedBytes ?? 0;

    /// <summary>Gets managed bytes allocated by the renderer's last scale/filter stage.</summary>
    public long LastRendererScaleAllocatedBytes => _renderer?.LastScaleAllocatedBytes ?? 0;

    /// <summary>Gets the most recent GPU frame time, or <see langword="null"/> when unavailable.</summary>
    public double? GpuMilliseconds => _renderer?.GpuMilliseconds;

    /// <summary>Gets a value indicating whether the platform accepted the requested vsync state.</summary>
    public bool VsyncActive => _renderer?.VsyncActive ?? false;

    /// <summary>Gets the selected platform swap-interval extension, if any.</summary>
    public string? VsyncBackend => _renderer?.VsyncBackend;

    /// <summary>Gets the GL vendor string captured when the context was created.</summary>
    public string? GlVendor => _glVendor;

    /// <summary>Gets the GL renderer string captured when the context was created.</summary>
    public string? GlRenderer => _glRenderer;

    /// <summary>Gets the physical width of the most recently rendered output.</summary>
    public int RenderedWidth => _renderedWidth;

    /// <summary>Gets the physical height of the most recently rendered output.</summary>
    public int RenderedHeight => _renderedHeight;

    /// <summary>
    /// Raised on the UI thread after a frame is presented. The first value is the CPU
    /// present time in milliseconds; the second is the latest GPU time when available.
    /// </summary>
    public event EventHandler<FramePresentedEventArgs>? FramePresented;

    /// <summary>
    /// Gets or sets a value indicating whether rendering goes to an offscreen target
    /// instead of the window framebuffer. Used by the GPU probe to measure a true 4K
    /// pass on a smaller desktop.
    /// </summary>
    public bool UseOffscreenProbe { get; set; }

    /// <summary>Gets or sets the offscreen probe width in pixels.</summary>
    public int OffscreenProbeWidth { get; set; } = 3840;

    /// <summary>Gets or sets the offscreen probe height in pixels.</summary>
    public int OffscreenProbeHeight { get; set; } = 2160;

    /// <summary>Gets or sets the renderer settings.</summary>
    public RendererSettings Settings
    {
        get => _settings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            _settings = value;
        }
    }

    /// <summary>Raised on the UI thread when the GL renderer is ready.</summary>
    public event EventHandler<OpenGlReadyEventArgs>? OpenGlReady;

    /// <summary>Raised on the UI thread when GL initialization or shader setup fails.</summary>
    public event EventHandler<OpenGlFailedEventArgs>? OpenGlFailed;

    /// <summary>Raised on the UI thread after a requested GL frame capture completes.</summary>
    public event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted;

    /// <summary>Gets a value indicating whether an OpenGL renderer is active.</summary>
    public bool IsOpenGlAvailable => _renderer is not null;

    /// <summary>Gets the capabilities reported by the active GL renderer, if any.</summary>
    public RendererCapabilities? Capabilities => _renderer?.Capabilities;

    /// <summary>Gets the mailbox sequence number of the last submitted frame.</summary>
    public long FrameSequence => _mailbox.Sequence;

    /// <summary>Requests a GL readback of the next rendered output frame.</summary>
    public void RequestCapture()
    {
        if (_renderer is null)
        {
            return;
        }

        _captureRequested = true;
        RequestNextFrameRendering();
    }

    /// <summary>Copies one machine frame into the presentation mailbox.</summary>
    /// <param name="frame">Completed machine frame.</param>
    public void SubmitFrame(VdlFrame frame)
    {
        _mailbox.Write(frame);
        if (_renderer is not null)
        {
            if (global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                _requestRender();
            }
            else
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(_requestRender);
            }
        }
    }

    /// <summary>Renders the latest snapshot (used by the CPU fallback and tests).</summary>
    /// <param name="renderer">Target renderer.</param>
    /// <param name="width">Output width.</param>
    /// <param name="height">Output height.</param>
    public void RenderSnapshot(IVideoRenderer renderer, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.Resize(width, height);
        _mailbox.Read(_presentFrame, renderer.Present);
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any GL context or driver failure must fall back to the CPU presenter instead of crashing the app.")]
    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            _gl = GL.GetApi(gl.GetProcAddress);
            _glVendor = gl.Vendor;
            _glRenderer = gl.Renderer;
            _adapter = new GlContextAdapter(_gl);
            ISwapIntervalBackend? swapInterval = SwapInterval.TryCreate(gl.GetProcAddress);
            _renderer = new OpenGlRenderer(_adapter, swapInterval);
            _present = _renderer.Present;
            _renderer.Settings = _settings;
            var (width, height) = GetPixelSize();
            _renderer.Initialize(width, height);
            RendererCapabilities capabilities = _renderer.Capabilities;
            if (!capabilities.SupportsIntegerTextures
                || (!capabilities.SupportsOpenGl33 && !capabilities.IsOpenGlEs))
            {
                throw new NotSupportedException(
                    $"The context reports {capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion}; "
                    + "desktop OpenGL 3.3 or OpenGL ES 3.0 with integer textures is required.");
            }

            OpenGlReady?.Invoke(this, new OpenGlReadyEventArgs(capabilities));
        }
        catch (Exception exception)
        {
            Serilog.Log.Debug(exception, "OpenGL initialization failed inside the video surface.");
            _renderer?.Dispose();
            _renderer = null;
            _present = null;
            _gl = null;
            OpenGlFailed?.Invoke(this, new OpenGlFailedEventArgs(exception));
        }
    }

    /// <inheritdoc />
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_renderer is null)
        {
            return;
        }

        var (width, height) = UseOffscreenProbe
            ? (OffscreenProbeWidth, OffscreenProbeHeight)
            : GetPixelSize();
        var sequence = _mailbox.Sequence;
        if (!_captureRequested
            && sequence == _renderedSequence
            && width == _renderedWidth
            && height == _renderedHeight)
        {
            return;
        }

        if (_adapter is null)
        {
            return;
        }

        try
        {
            if (UseOffscreenProbe)
            {
                if (_offscreen is null
                    || _offscreen.Width != width
                    || _offscreen.Height != height)
                {
                    _offscreen?.Dispose();
                    _offscreen = OffscreenRenderTarget.Create(_adapter.Gl, width, height);
                }

                _adapter.Framebuffer = (int)_offscreen.Framebuffer;
            }
            else
            {
                _adapter.Framebuffer = fb;
            }
        }
        catch (Exception exception)
        {
            Serilog.Log.Debug(exception, "OpenGL present failed inside the video surface.");
            _renderer.Dispose();
            _renderer = null;
            _present = null;
            OpenGlFailed?.Invoke(this, new OpenGlFailedEventArgs(exception));
            return;
        }

        if (_renderer is null)
        {
            return;
        }

        _renderedSequence = sequence;
        _renderedWidth = width;
        _renderedHeight = height;
        _renderer.MeasureAllocations = MeasureAllocations;
        _renderer.Settings = _settings;
        _renderer.Resize(width, height);
        var presentStart = Stopwatch.GetTimestamp();
        var allocationStart = MeasureAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        _mailbox.Read(_presentFrame, NoOp);
        var rendererAllocationStart = MeasureAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        _present?.Invoke(_presentFrame);
        if (MeasureAllocations)
        {
            var after = GC.GetAllocatedBytesForCurrentThread();
            var rendererDelta = after - rendererAllocationStart;
            _rendererOnlyAllocatedBytes += rendererDelta;
            _rendererAllocatedBytes += after - allocationStart;
        }

        if (_captureRequested)
        {
            _captureRequested = false;
            var buffer = new byte[checked(width * height * 4)];
            _renderer!.ReadPixels(buffer, out var captureWidth, out var captureHeight);
            CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs(buffer, captureWidth, captureHeight));

            // A capture consumer (the GPU probe) may finish the session and dispose
            // the surface synchronously. Do not touch the adapter/renderer after
            // that callback.
            if (_adapter is null || _renderer is null)
            {
                return;
            }
        }

        if (UseOffscreenProbe)
        {
            // Leave the default framebuffer bound for Avalonia's compositor.
            _adapter.Framebuffer = fb;
        }

        _presentMilliseconds =
            (Stopwatch.GetTimestamp() - presentStart) * 1000.0 / Stopwatch.Frequency;
        FramePresented?.Invoke(this, new FramePresentedEventArgs(_presentMilliseconds, GpuMilliseconds));
    }

    private (int Width, int Height) GetPixelSize()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        return PixelSize.Compute(Bounds.Width, Bounds.Height, scaling);
    }

    /// <inheritdoc />
    protected override void OnOpenGlLost()
    {
        // The context is already gone here, so calling GL deletion functions would
        // try to resolve entry points from a dead context and can crash the process.
        // Avalonia calls OnOpenGlDeinit with a current context for normal teardown;
        // after a context loss the driver owns the abandoned objects.
        _offscreen = null;
        _renderer = null;
        _adapter = null;
        _present = null;
        _gl = null;
        OpenGlFailed?.Invoke(this, new OpenGlFailedEventArgs(new InvalidOperationException("The OpenGL context was lost.")));
    }

    /// <inheritdoc />
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _offscreen?.Dispose();
        _offscreen = null;
        _renderer?.Dispose();
        _renderer = null;
        _adapter = null;
        _present = null;
        _gl = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Avalonia invokes OnOpenGlDeinit with the context current during visual-tree
        // teardown; that is where the GL programs and textures are released. This method
        // must stay GL-free because callers (window close/probe shutdown) can run after
        // the context has already gone away.
        _offscreen = null;
        _renderer = null;
        _adapter = null;
        _present = null;
        _gl = null;
    }
}

/// <summary>Event data for <see cref="VideoSurface.FramePresented"/>.</summary>
public sealed class FramePresentedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="FramePresentedEventArgs"/> class.</summary>
    /// <param name="presentMilliseconds">CPU present time in milliseconds.</param>
    /// <param name="gpuMilliseconds">Latest GPU frame time, or <see langword="null"/> when unavailable.</param>
    public FramePresentedEventArgs(double presentMilliseconds, double? gpuMilliseconds)
    {
        PresentMilliseconds = presentMilliseconds;
        GpuMilliseconds = gpuMilliseconds;
    }

    /// <summary>Gets the CPU present time in milliseconds.</summary>
    public double PresentMilliseconds { get; }

    /// <summary>Gets the latest GPU frame time, or <see langword="null"/> when unavailable.</summary>
    public double? GpuMilliseconds { get; }
}

/// <summary>Event data for <see cref="VideoSurface.OpenGlReady"/>.</summary>
public sealed class OpenGlReadyEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="OpenGlReadyEventArgs"/> class.</summary>
    /// <param name="capabilities">Capabilities reported by the active renderer.</param>
    public OpenGlReadyEventArgs(RendererCapabilities capabilities)
    {
        Capabilities = capabilities;
    }

    /// <summary>Gets the capabilities reported by the active renderer.</summary>
    public RendererCapabilities Capabilities { get; }
}

/// <summary>Event data for <see cref="VideoSurface.OpenGlFailed"/>.</summary>
public sealed class OpenGlFailedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="OpenGlFailedEventArgs"/> class.</summary>
    /// <param name="exception">The exception that describes the failure.</param>
    public OpenGlFailedEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>Gets the exception that describes the failure.</summary>
    public Exception Exception { get; }
}

/// <summary>Event data for <see cref="VideoSurface.CaptureCompleted"/>.</summary>
public sealed class CaptureCompletedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="CaptureCompletedEventArgs"/> class.</summary>
    /// <param name="pixels">Captured BGRA pixels.</param>
    /// <param name="width">Capture width in pixels.</param>
    /// <param name="height">Capture height in pixels.</param>
    public CaptureCompletedEventArgs(byte[] pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the captured BGRA pixels.</summary>
    public byte[] Pixels { get; }

    /// <summary>Gets the capture width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the capture height in pixels.</summary>
    public int Height { get; }
}
