using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using ThreeDo.Core;
using ThreeDo.Video;
using ThreeDo.Video.OpenGL;

namespace ThreeDo.Frontend.Avalonia.Controls;

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
    private readonly VdlFrameMailbox _mailbox = new();
    private readonly VdlFrame _presentFrame = new();
    private GL? _gl;
    private GlContextAdapter? _adapter;
    private OpenGlRenderer? _renderer;
    private Action<VdlFrame>? _present;
    private RendererSettings _settings = new();
    private long _renderedSequence = -1;
    private int _renderedWidth;
    private int _renderedHeight;
    private bool _captureRequested;

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
    public event Action<RendererCapabilities>? OpenGlReady;

    /// <summary>Raised on the UI thread when GL initialization or shader setup fails.</summary>
    public event Action<Exception>? OpenGlFailed;

    /// <summary>Raised on the UI thread after a requested GL frame capture completes.</summary>
    public event Action<byte[], int, int>? CaptureCompleted;

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
            global::Avalonia.Threading.Dispatcher.UIThread.Post(RequestNextFrameRendering);
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
            _adapter = new GlContextAdapter(_gl);
            _renderer = new OpenGlRenderer(_adapter);
            _present = _renderer.Present;
            _renderer.Settings = _settings;
            var (width, height) = GetPixelSize();
            _renderer.Initialize(width, height);
            var capabilities = _renderer.Capabilities;
            if (!capabilities.SupportsIntegerTextures
                || (!capabilities.SupportsOpenGl33 && !capabilities.IsOpenGlEs))
            {
                throw new NotSupportedException(
                    $"The context reports {capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion}; "
                    + "desktop OpenGL 3.3 or OpenGL ES 3.0 with integer textures is required.");
            }

            OpenGlReady?.Invoke(capabilities);
        }
        catch (Exception exception)
        {
            _renderer?.Dispose();
            _renderer = null;
            _present = null;
            _gl = null;
            OpenGlFailed?.Invoke(exception);
        }
    }

    /// <inheritdoc />
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_renderer is null)
        {
            return;
        }

        if (_adapter is not null)
        {
            _adapter.Framebuffer = fb;
        }

        var (width, height) = GetPixelSize();
        var sequence = _mailbox.Sequence;
        if (!_captureRequested
            && sequence == _renderedSequence
            && width == _renderedWidth
            && height == _renderedHeight)
        {
            return;
        }

        _renderedSequence = sequence;
        _renderedWidth = width;
        _renderedHeight = height;
        _renderer.Settings = _settings;
        _renderer.Resize(width, height);
        if (_present is not null)
        {
            _mailbox.Read(_presentFrame, _present);
        }

        if (_captureRequested)
        {
            _captureRequested = false;
            var buffer = new byte[checked(width * height * 4)];
            _renderer.ReadPixels(buffer, out var captureWidth, out var captureHeight);
            CaptureCompleted?.Invoke(buffer, captureWidth, captureHeight);
        }
    }

    private (int Width, int Height) GetPixelSize()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        return (
            Math.Max(1, (int)Math.Round(Bounds.Width * scaling)),
            Math.Max(1, (int)Math.Round(Bounds.Height * scaling)));
    }

    /// <inheritdoc />
    protected override void OnOpenGlLost()
    {
        _renderer?.Dispose();
        _renderer = null;
        _adapter = null;
        _present = null;
        _gl = null;
        OpenGlFailed?.Invoke(new InvalidOperationException("The OpenGL context was lost."));
    }

    /// <inheritdoc />
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer?.Dispose();
        _renderer = null;
        _adapter = null;
        _present = null;
        _gl = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _renderer?.Dispose();
        _renderer = null;
        _adapter = null;
    }
}
