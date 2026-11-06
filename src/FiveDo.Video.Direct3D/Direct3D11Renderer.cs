using System;
using System.Numerics;
using System.Runtime.InteropServices;
using FiveDo.Core;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace FiveDo.Video.Direct3D;

/// <summary>
/// Optional Direct3D 11 presentation backend for Windows.
/// </summary>
/// <remarks>
/// <para>
/// The renderer mirrors the OpenGL and Vulkan two-stage pipelines: indexed pixels and
/// per-line palette/control data are expanded at native resolution, then a second pass
/// scales and filters the native image into the output. HLSL sources are compiled with
/// the platform D3DCompiler at runtime.
/// </para>
/// <para>
/// OpenGL remains the default presenter on every platform. This backend is opt-in and
/// Windows-only for window presentation; headless readback works wherever Direct3D 11 is
/// available.
/// </para>
/// </remarks>
public sealed unsafe partial class Direct3D11Renderer : ICpuFrameRenderer, IRendererTiming
{
    private const int MaxTextureSize = 16384;
    private const int ClutEntries = VdlFrameUploader.ClutEntries;

    private readonly VdlFrameUploader _uploader = new();
    private readonly HqxFilter _hqxProcessor = new();
    private readonly uint[] _clutRgba = new uint[ClutEntries * VdlFrameUploader.MaxHeight];
    private FrameBuffer _frame = new(1, 1);
    private FrameBuffer? _hqxSourceFrame;
    private FrameBuffer? _hqxCpuFrame;
    private int _hqxSourceWidth;
    private int _hqxSourceHeight;
    private int _hqxScale;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIFactory2? _factory;
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _paletteShader;
    private ID3D11PixelShader? _scaleShader;
    private ID3D11PixelShader? _xbrShader;
    private ID3D11PixelShader? _fsrEasuShader;
    private ID3D11PixelShader? _fsrRcasShader;
    private ID3D11Buffer? _constantBuffer;
    private ID3D11SamplerState? _linearSampler;
    private ID3D11RasterizerState? _rasterizerState;
    private ID3D11Query? _disjointQuery;
    private ID3D11Query? _startTimestamp;
    private ID3D11Query? _endTimestamp;

    private ID3D11Texture2D? _pixelTexture;
    private ID3D11ShaderResourceView? _pixelView;
    private ID3D11Texture2D? _clutTexture;
    private ID3D11ShaderResourceView? _clutView;
    private ID3D11Texture2D? _paramTexture;
    private ID3D11ShaderResourceView? _paramView;
    private ColorTarget? _expanded;
    private ColorTarget? _output;
    private ColorTarget? _xbr;
    private ColorTarget? _fsr;
    private ID3D11Texture2D? _hqxTexture;
    private ID3D11ShaderResourceView? _hqxView;
    private int _hqxWidth;
    private int _hqxHeight;
    private ID3D11Texture2D? _readback;

    private RendererSettings _settings = new();
    private RendererCapabilities _capabilities;
    private string _deviceName = "Direct3D 11";
    private FeatureLevel _featureLevel = FeatureLevel.Level_11_0;
    private int _outputWidth;
    private int _outputHeight;
    private int _sourceWidth;
    private int _sourceHeight;
    private double _sourcePixelAspect = 1.0;
    private bool _initialized;
    private bool _disposed;
    private bool _filterFallback;
    private bool _hasPresentedFrame;
    private bool _timingQueriesPending;
    private double? _gpuMilliseconds;
    private readonly string _outputFormat = Format.B8G8R8A8_UNorm.ToString();
    private Constants _constants;

    /// <summary>Gets a value indicating whether the last present fell back to sharp-bilinear.</summary>
    public bool FilterFallback => _filterFallback;

    /// <summary>Gets the Direct3D adapter name.</summary>
    public string DeviceName => _deviceName;

    /// <summary>Gets the created feature level.</summary>
    public string FeatureLevelName => _featureLevel.ToString();

    /// <summary>Gets or sets a value indicating whether each present copies the output into <see cref="Frame"/>.</summary>
    public bool CaptureToFrame { get; set; }

    /// <summary>Gets the CPU-side frame used by <see cref="CaptureToFrame"/> and callers.</summary>
    public FrameBuffer Frame => _frame;

    /// <inheritdoc />
    public string Name => "Direct3D 11";

    /// <inheritdoc />
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

    /// <inheritdoc />
    public RendererCapabilities Capabilities => _capabilities;

    /// <inheritdoc />
    public double? GpuMilliseconds => _gpuMilliseconds;

    /// <summary>Gets the output format used by the renderer.</summary>
    public string OutputFormat => _outputFormat;

    /// <inheritdoc />
    public void Initialize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);
        DestroyDeviceResources();
        _outputWidth = width;
        _outputHeight = height;
        _sourceWidth = 0;
        _sourceHeight = 0;
        _frame = new FrameBuffer(width, height);
        _filterFallback = false;
        _gpuMilliseconds = null;
        _hasPresentedFrame = false;
        CreateDevice();
        CreatePipelineState();
        CreateOutputTarget();
        CreateExpandedTarget();
        CreateSwapchainResources();
        _initialized = true;
    }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_outputWidth == width && _outputHeight == height)
        {
            return;
        }

        _outputWidth = width;
        _outputHeight = height;
        _frame = new FrameBuffer(width, height);
        _hasPresentedFrame = false;
        if (!_initialized)
        {
            return;
        }

        DestroyOutputTarget();
        CreateOutputTarget();
        ResizeSwapchain(width, height);
    }

    /// <inheritdoc />
    public void Present(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            Initialize(EmulatorConstants.VideoWidth, EmulatorConstants.VideoHeight);
        }

        // Read the previous frame's timestamp queries before issuing a new pair.
        // This keeps presentation non-blocking; the first frame reports no GPU time.
        ReadGpuTiming();

        _uploader.Update(frame);
        var width = _uploader.Width;
        var height = _uploader.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (width != _sourceWidth || height != _sourceHeight)
        {
            _sourceWidth = width;
            _sourceHeight = height;
            CreateSourceTextures();
            RecreateExpandedTarget();
        }

        _sourcePixelAspect = double.IsFinite(frame.PixelAspect) && frame.PixelAspect > 0
            ? frame.PixelAspect
            : 1.0;

        if (IsHqxFilter)
        {
            PrepareHqx(frame);
        }

        UpdateSourceTextures();
        RenderFrame();
        _hasPresentedFrame = true;
        if (CaptureToFrame)
        {
            ReadPixels(_frame.PixelSpan, out _, out _);
        }

        PresentSwapchain();
    }

    /// <inheritdoc />
    public void ReadPixels(Span<byte> destination, out int width, out int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        width = _outputWidth;
        height = _outputHeight;
        var required = checked(width * height * 4);
        if (destination.Length < required)
        {
            throw new ArgumentException($"The destination needs {required} bytes.", nameof(destination));
        }

        if (!_initialized || _context is null || _readback is null || _output is null || !_hasPresentedFrame)
        {
            destination[..required].Clear();
            return;
        }

        _context.CopyResource(_readback, _output.Texture);
        MappedSubresource mapped = _context.Map(_readback, 0);
        try
        {
            var sourceRowPitch = (int)mapped.RowPitch;
            fixed (byte* destinationPointer = destination)
            {
                var source = (byte*)mapped.DataPointer;
                for (var y = 0; y < height; y++)
                {
                    var destinationRow = destinationPointer + (y * width * 4);
                    Buffer.MemoryCopy(
                        source + (y * sourceRowPitch),
                        destinationRow,
                        width * 4,
                        width * 4);
                }
            }
        }
        finally
        {
            _context.Unmap(_readback, 0);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroySwapchainResources();
        DestroyDeviceResources();
    }

    private void CreateDevice()
    {
        Result result = D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_0],
            out ID3D11Device? device,
            out FeatureLevel featureLevel,
            out ID3D11DeviceContext? context);
        if (result.Failure)
        {
            Serilog.Log.Debug("Hardware Direct3D 11 device unavailable; trying WARP.");
            result = D3D11.D3D11CreateDevice(
                null,
                DriverType.Warp,
                DeviceCreationFlags.BgraSupport,
                [FeatureLevel.Level_11_0],
                out device,
                out featureLevel,
                out context);
        }

        result.CheckError();
        _device = device ?? throw new InvalidOperationException("Direct3D 11 did not return a device.");
        _context = context ?? throw new InvalidOperationException("Direct3D 11 did not return an immediate context.");
        _featureLevel = featureLevel;

        using IDXGIDevice dxgiDevice = _device.QueryInterface<IDXGIDevice>();
        using IDXGIAdapter adapter = dxgiDevice.GetAdapter();
        _deviceName = adapter.Description.Description.TrimEnd('\0');
        _factory = adapter.GetParent<IDXGIFactory2>();
        _capabilities = new RendererCapabilities(
            "Direct3D 11",
            11,
            0,
            MaxTextureSize,
            SupportsIntegerTextures: true,
            SupportsVsync: true,
            RendererApi.Direct3D11);
    }

    private void CreatePipelineState()
    {
        _vertexShader = _device!.CreateVertexShader(Compile("Frame.vert", "VSMain", "vs_5_0").ToArray());
        _paletteShader = _device.CreatePixelShader(Compile("Palette.frag", "PSMain", "ps_5_0").ToArray());
        _scaleShader = _device.CreatePixelShader(Compile("Scale.frag", "PSMain", "ps_5_0").ToArray());
        _xbrShader = _device.CreatePixelShader(Compile("Xbr.frag", "PSMain", "ps_5_0").ToArray());
        _fsrEasuShader = _device.CreatePixelShader(Compile("FsrEasu.frag", "PSMain", "ps_5_0").ToArray());
        _fsrRcasShader = _device.CreatePixelShader(Compile("FsrRcas.frag", "PSMain", "ps_5_0").ToArray());
        _constantBuffer = _device.CreateBuffer(
            new BufferDescription(
                (uint)sizeof(Constants),
                BindFlags.ConstantBuffer));
        _linearSampler = _device.CreateSamplerState(new SamplerDescription(
            Filter.MinMagMipLinear,
            TextureAddressMode.Clamp,
            TextureAddressMode.Clamp,
            TextureAddressMode.Clamp,
            0f,
            1,
            ComparisonFunction.Never,
            0f));
        _rasterizerState = _device.CreateRasterizerState(
            new RasterizerDescription(CullMode.None, FillMode.Solid)
            {
                ScissorEnable = false,
                DepthClipEnable = false,
                MultisampleEnable = false,
            });
        _disjointQuery = _device.CreateQuery(QueryType.TimestampDisjoint);
        _startTimestamp = _device.CreateQuery(QueryType.Timestamp);
        _endTimestamp = _device.CreateQuery(QueryType.Timestamp);
    }

    private static ReadOnlyMemory<byte> Compile(string name, string entryPoint, string profile)
    {
        var source = name switch
        {
            "Frame.vert" => Direct3D11Shaders.Vertex,
            "Palette.frag" => Direct3D11Shaders.Palette,
            "Scale.frag" => Direct3D11Shaders.Scale,
            "Xbr.frag" => Direct3D11Shaders.Xbr,
            "FsrEasu.frag" => Direct3D11Shaders.FsrEasu,
            "FsrRcas.frag" => Direct3D11Shaders.FsrRcas,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown shader."),
        };

        try
        {
            return Compiler.Compile(source, entryPoint, name, profile, ShaderFlags.OptimizationLevel3);
        }
        catch (SharpGenException exception)
        {
            throw new InvalidOperationException($"The Direct3D shader {name} did not compile: {exception.Message}", exception);
        }
    }

    /// <summary>Compiles every HLSL source and returns the module names.</summary>
    /// <returns>Names of the compiled modules.</returns>
    internal static string[] CompileAllShaders()
    {
        var names = new string[Direct3D11Shaders.All.Length];
        for (var i = 0; i < Direct3D11Shaders.All.Length; i++)
        {
            (var name, var profile, _) = Direct3D11Shaders.All[i];
            ReadOnlyMemory<byte> bytecode = Compile(
                name,
                profile.StartsWith("vs", StringComparison.Ordinal) ? "VSMain" : "PSMain",
                profile);
            if (bytecode.Length == 0)
            {
                throw new InvalidOperationException($"The Direct3D shader {name} produced no bytecode.");
            }

            names[i] = name;
        }

        return names;
    }

    private void CreateSourceTextures()
    {
        DestroySourceTextures();
        _pixelTexture = _device!.CreateTexture2D(CreateDescription(
            _sourceWidth,
            _sourceHeight,
            Format.R16_UInt,
            BindFlags.ShaderResource));
        _pixelView = _device.CreateShaderResourceView(_pixelTexture);
        _clutTexture = _device.CreateTexture2D(CreateDescription(
            ClutEntries,
            _sourceHeight,
            Format.R8G8B8A8_UNorm,
            BindFlags.ShaderResource));
        _clutView = _device.CreateShaderResourceView(_clutTexture);
        _paramTexture = _device.CreateTexture2D(CreateDescription(
            1,
            _sourceHeight,
            Format.R32G32_UInt,
            BindFlags.ShaderResource));
        _paramView = _device.CreateShaderResourceView(_paramTexture);
    }

    private void UpdateSourceTextures()
    {
        _context!.UpdateSubresource(
            _uploader.Pixels,
            _pixelTexture!,
            rowPitch: (uint)(_sourceWidth * sizeof(ushort)),
            depthPitch: (uint)(_sourceWidth * _sourceHeight * sizeof(ushort)));

        var clutSource = _uploader.Clut;
        for (var y = 0; y < _sourceHeight; y++)
        {
            var sourceRow = y * ClutEntries * 3;
            var destinationRow = y * ClutEntries;
            for (var i = 0; i < ClutEntries; i++)
            {
                var source = sourceRow + (i * 3);
                var red = clutSource[source];
                var green = clutSource[source + 1];
                var blue = clutSource[source + 2];
                _clutRgba[destinationRow + i] = 0xff000000u
                    | ((uint)blue << 16)
                    | ((uint)green << 8)
                    | red;
            }
        }

        _context.UpdateSubresource(
            _clutRgba.AsSpan(0, _sourceHeight * ClutEntries),
            _clutTexture!,
            rowPitch: ClutEntries * sizeof(uint),
            depthPitch: (uint)(_sourceHeight * ClutEntries * sizeof(uint)));
        _context.UpdateSubresource(
            _uploader.LineParams,
            _paramTexture!,
            rowPitch: 2 * sizeof(uint),
            depthPitch: (uint)(_sourceHeight * 2 * sizeof(uint)));
    }

    private bool IsHqxFilter
        => _settings.Filter is VideoFilter.Hqx or VideoFilter.Hqx3x or VideoFilter.Hqx4x;

    private void PrepareHqx(VdlFrame frame)
    {
        var scale = HqxFilter.ScaleFor(_settings.Filter);
        var width = _sourceWidth * scale;
        var height = _sourceHeight * scale;
        if (width > MaxTextureSize || height > MaxTextureSize)
        {
            DestroyHqxTarget();
            return;
        }

        EnsureHqxTargets(width, height);
        EnsureHqxFrames(_sourceWidth, _sourceHeight, scale);
        FrameAssembler.Assemble(frame, _hqxSourceFrame!, _sourceWidth, _sourceHeight);
        _hqxProcessor.Expand(_hqxSourceFrame!, _hqxCpuFrame!);
        _context!.UpdateSubresource(
            _hqxCpuFrame!.PixelSpan,
            _hqxTexture!,
            rowPitch: (uint)(width * 4),
            depthPitch: (uint)(width * height * 4));
    }

    private void EnsureHqxTargets(int width, int height)
    {
        if (_hqxTexture is not null && _hqxWidth == width && _hqxHeight == height)
        {
            return;
        }

        DestroyHqxTarget();
        _hqxWidth = width;
        _hqxHeight = height;
        _hqxTexture = _device!.CreateTexture2D(CreateDescription(
            width,
            height,
            Format.B8G8R8A8_UNorm,
            BindFlags.ShaderResource));
        _hqxView = _device.CreateShaderResourceView(_hqxTexture);
    }

    private void EnsureHqxFrames(int width, int height, int scale)
    {
        if (_hqxCpuFrame is not null
            && _hqxSourceWidth == width
            && _hqxSourceHeight == height
            && _hqxScale == scale)
        {
            return;
        }

        _hqxSourceFrame = new FrameBuffer(width, height);
        _hqxCpuFrame = new FrameBuffer(width * scale, height * scale);
        _hqxSourceWidth = width;
        _hqxSourceHeight = height;
        _hqxScale = scale;
    }

    private void RenderFrame()
    {
        _filterFallback = false;
        var recordTiming = !_timingQueriesPending;
        if (recordTiming)
        {
            _context!.Begin(_disjointQuery);
            _context.End(_startTimestamp);
        }

        RecordPalettePass();
        RecordFilterPass();
        if (recordTiming)
        {
            _context!.End(_endTimestamp);
            _context.End(_disjointQuery);
            _context.Flush();
            _timingQueriesPending = true;
        }
    }

    private void RecordPalettePass()
    {
        SetPipeline(_paletteShader!, _sourceWidth, _sourceHeight);
        UpdateConstants(
            new Vector4(0f, 0f, _sourceWidth, _sourceHeight),
            new Vector2(_sourceWidth, _sourceHeight),
            _sourceWidth,
            _sourceHeight,
            filterMode: 0);
        _context!.OMSetRenderTargets(_expanded!.RenderTargetView);
        _context.ClearRenderTargetView(_expanded.RenderTargetView, new Color4(0f, 0f, 0f));
        _context.PSSetShaderResource(0, _pixelView!);
        _context.PSSetShaderResource(1, _clutView!);
        _context.PSSetShaderResource(2, _paramView!);
        _context.Draw(3, 0);
    }

    private void RecordFilterPass()
    {
        var filter = _settings.Filter;
        if (filter == VideoFilter.Xbr)
        {
            var xbrWidth = _sourceWidth * 4;
            var xbrHeight = _sourceHeight * 4;
            if (xbrWidth <= MaxTextureSize && xbrHeight <= MaxTextureSize)
            {
                EnsureXbrTarget(xbrWidth, xbrHeight);
                RecordXbrPass(xbrWidth, xbrHeight);
                RecordScalePass(xbrWidth, xbrHeight, filterMode: 1, _xbr!.View);
                return;
            }

            _filterFallback = true;
        }
        else if (filter == VideoFilter.Fsr)
        {
            ViewportRect viewport = ViewportCalculator.Compute(
                _sourceWidth,
                _sourceHeight,
                _outputWidth,
                _outputHeight,
                _settings,
                _sourcePixelAspect);
            if (viewport.Width <= MaxTextureSize && viewport.Height <= MaxTextureSize)
            {
                EnsureFsrTarget(Math.Max(1, viewport.Width), Math.Max(1, viewport.Height));
                RecordFsrEasuPass(viewport);
                RecordFsrRcasPass(viewport);
                return;
            }

            _filterFallback = true;
        }
        else if (IsHqxFilter)
        {
            var scale = HqxFilter.ScaleFor(filter);
            var hqxWidth = _sourceWidth * scale;
            var hqxHeight = _sourceHeight * scale;
            if (hqxWidth <= MaxTextureSize
                && hqxHeight <= MaxTextureSize
                && _hqxView is not null
                && _hqxWidth == hqxWidth
                && _hqxHeight == hqxHeight)
            {
                RecordScalePass(hqxWidth, hqxHeight, filterMode: 1, _hqxView);
                return;
            }

            _filterFallback = true;
        }

        RecordScalePass(
            _sourceWidth,
            _sourceHeight,
            FilterValue(filter),
            _expanded!.View);
    }

    private void RecordXbrPass(int width, int height)
    {
        SetPipeline(_xbrShader!, width, height);
        UpdateConstants(
            new Vector4(0f, 0f, width, height),
            new Vector2(width, height),
            _sourceWidth,
            _sourceHeight,
            filterMode: 1);
        _context!.OMSetRenderTargets(_xbr!.RenderTargetView);
        _context.ClearRenderTargetView(_xbr.RenderTargetView, new Color4(0f, 0f, 0f));
        _context.PSSetShaderResource(0, _expanded!.View);
        _context.Draw(3, 0);
    }

    private void RecordFsrEasuPass(ViewportRect viewport)
    {
        var width = Math.Max(1, viewport.Width);
        var height = Math.Max(1, viewport.Height);
        SetPipeline(_fsrEasuShader!, width, height);
        UpdateConstants(
            new Vector4(0f, 0f, width, height),
            new Vector2(width, height),
            _sourceWidth,
            _sourceHeight,
            filterMode: 1);
        _context!.OMSetRenderTargets(_fsr!.RenderTargetView);
        _context.ClearRenderTargetView(_fsr.RenderTargetView, new Color4(0f, 0f, 0f));
        _context.PSSetShaderResource(0, _expanded!.View);
        _context.Draw(3, 0);
    }

    private void RecordFsrRcasPass(ViewportRect viewport)
    {
        SetPipeline(_fsrRcasShader!, _outputWidth, _outputHeight);
        UpdateConstants(
            new Vector4(
                viewport.X,
                viewport.Y,
                Math.Max(1, viewport.Width),
                Math.Max(1, viewport.Height)),
            new Vector2(_outputWidth, _outputHeight),
            Math.Max(1, viewport.Width),
            Math.Max(1, viewport.Height),
            filterMode: 1,
            sharpness: (1f - _settings.FsrSharpness) * 2f);
        _context!.OMSetRenderTargets(_output!.RenderTargetView);
        _context.ClearRenderTargetView(_output.RenderTargetView, new Color4(0f, 0f, 0f));
        _context.PSSetShaderResource(0, _fsr!.View);
        _context.Draw(3, 0);
    }

    private void RecordScalePass(int sourceWidth, int sourceHeight, int filterMode, ID3D11ShaderResourceView source)
    {
        ViewportRect viewport = ViewportCalculator.Compute(
            _sourceWidth,
            _sourceHeight,
            _outputWidth,
            _outputHeight,
            _settings,
            _sourcePixelAspect);
        SetPipeline(_scaleShader!, _outputWidth, _outputHeight);
        UpdateConstants(
            new Vector4(
                viewport.X,
                viewport.Y,
                Math.Max(1, viewport.Width),
                Math.Max(1, viewport.Height)),
            new Vector2(_outputWidth, _outputHeight),
            sourceWidth,
            sourceHeight,
            filterMode,
            bloom: _settings.Filter == VideoFilter.Crt ? _settings.Bloom : 0f);
        _context!.OMSetRenderTargets(_output!.RenderTargetView);
        _context.ClearRenderTargetView(_output.RenderTargetView, new Color4(0f, 0f, 0f));
        _context.PSSetShaderResource(0, source);
        _context.Draw(3, 0);
    }

    private void SetPipeline(ID3D11PixelShader shader, int viewportWidth, int viewportHeight)
    {
        _context!.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.RSSetState(_rasterizerState);
        _context.VSSetShader(_vertexShader);
        _context.PSSetShader(shader);
        _context.VSSetConstantBuffer(0, _constantBuffer);
        _context.PSSetConstantBuffer(0, _constantBuffer);
        _context.RSSetViewport(new Viewport(0f, 0f, viewportWidth, viewportHeight));
        _context.PSSetSampler(0, _linearSampler);
    }

    private void UpdateConstants(
        Vector4 rect,
        Vector2 outputSize,
        int sourceWidth,
        int sourceHeight,
        int filterMode,
        float bloom = 0f,
        float? sharpness = null)
    {
        _constants = new Constants
        {
            Rect = rect,
            Params = new Vector4(
                _settings.ScanlineStrength,
                bloom,
                _settings.BloomThreshold,
                sharpness ?? _settings.FsrSharpness),
            OutputSize = outputSize,
            SourceWidth = sourceWidth,
            SourceHeight = sourceHeight,
            FilterMode = filterMode,
        };
        _context!.UpdateSubresource(in _constants, _constantBuffer!);
    }

    private static int FilterValue(VideoFilter filter)
    {
        return filter switch
        {
            VideoFilter.Accurate or VideoFilter.Nearest => 0,
            VideoFilter.Crt => 2,
            _ => 1,
        };
    }

    private void ReadGpuTiming()
    {
        if (!_timingQueriesPending)
        {
            return;
        }

        try
        {
            if (_context!.GetData(_disjointQuery!, out QueryDataTimestampDisjoint disjoint))
            {
                _timingQueriesPending = false;
                if (!disjoint.Disjoint
                    && disjoint.Frequency > 0
                    && _context.GetData(_startTimestamp!, out ulong start)
                    && _context.GetData(_endTimestamp!, out ulong end)
                    && end > start)
                {
                    _gpuMilliseconds = (end - start) * 1000.0 / disjoint.Frequency;
                }
            }
        }
        catch (Exception exception) when (exception is SharpGenException or InvalidOperationException)
        {
            _timingQueriesPending = false;
            Serilog.Log.Debug(exception, "Direct3D GPU timing query failed.");
        }
    }

    private void EnsureXbrTarget(int width, int height)
    {
        if (_xbr is not null && _xbr.Width == width && _xbr.Height == height)
        {
            return;
        }

        _xbr?.Dispose();
        _xbr = CreateColorTarget(width, height);
    }

    private void EnsureFsrTarget(int width, int height)
    {
        if (_fsr is not null && _fsr.Width == width && _fsr.Height == height)
        {
            return;
        }

        _fsr?.Dispose();
        _fsr = CreateColorTarget(width, height);
    }

    private void CreateExpandedTarget()
    {
        _expanded?.Dispose();
        _expanded = CreateColorTarget(Math.Max(1, _sourceWidth), Math.Max(1, _sourceHeight));
    }

    private void RecreateExpandedTarget()
    {
        _xbr?.Dispose();
        _xbr = null;
        _fsr?.Dispose();
        _fsr = null;
        CreateExpandedTarget();
    }

    private void CreateOutputTarget()
    {
        _output?.Dispose();
        _output = CreateColorTarget(_outputWidth, _outputHeight);
        _readback?.Dispose();
        _readback = _device!.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)_outputWidth,
            Height = (uint)_outputHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        });
    }

    private ColorTarget CreateColorTarget(int width, int height)
    {
        ID3D11Texture2D texture = _device!.CreateTexture2D(CreateDescription(
            width,
            height,
            Format.B8G8R8A8_UNorm,
            BindFlags.RenderTarget | BindFlags.ShaderResource));
        return new ColorTarget(
            width,
            height,
            texture,
            _device.CreateRenderTargetView(texture),
            _device.CreateShaderResourceView(texture));
    }

    private static Texture2DDescription CreateDescription(
        int width,
        int height,
        Format format,
        BindFlags bindFlags)
    {
        return new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = bindFlags,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None,
        };
    }

    private void DestroySourceTextures()
    {
        _pixelView?.Dispose();
        _pixelView = null;
        _pixelTexture?.Dispose();
        _pixelTexture = null;
        _clutView?.Dispose();
        _clutView = null;
        _clutTexture?.Dispose();
        _clutTexture = null;
        _paramView?.Dispose();
        _paramView = null;
        _paramTexture?.Dispose();
        _paramTexture = null;
    }

    private void DestroyHqxTarget()
    {
        _hqxView?.Dispose();
        _hqxView = null;
        _hqxTexture?.Dispose();
        _hqxTexture = null;
        _hqxWidth = 0;
        _hqxHeight = 0;
    }

    private void DestroyOutputTarget()
    {
        _readback?.Dispose();
        _readback = null;
        _output?.Dispose();
        _output = null;
    }

    private void DestroyDeviceResources()
    {
        DestroySwapchainResources();
        DestroySourceTextures();
        DestroyHqxTarget();
        DestroyOutputTarget();
        _xbr?.Dispose();
        _xbr = null;
        _fsr?.Dispose();
        _fsr = null;
        _expanded?.Dispose();
        _expanded = null;
        _disjointQuery?.Dispose();
        _disjointQuery = null;
        _startTimestamp?.Dispose();
        _startTimestamp = null;
        _endTimestamp?.Dispose();
        _endTimestamp = null;
        _rasterizerState?.Dispose();
        _rasterizerState = null;
        _linearSampler?.Dispose();
        _linearSampler = null;
        _constantBuffer?.Dispose();
        _constantBuffer = null;
        _vertexShader?.Dispose();
        _vertexShader = null;
        _paletteShader?.Dispose();
        _paletteShader = null;
        _scaleShader?.Dispose();
        _scaleShader = null;
        _xbrShader?.Dispose();
        _xbrShader = null;
        _fsrEasuShader?.Dispose();
        _fsrEasuShader = null;
        _fsrRcasShader?.Dispose();
        _fsrRcasShader = null;
        _factory?.Dispose();
        _factory = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;
        _timingQueriesPending = false;
        _hasPresentedFrame = false;
        _initialized = false;
    }

    private sealed class ColorTarget : IDisposable
    {
        public ColorTarget(
            int width,
            int height,
            ID3D11Texture2D texture,
            ID3D11RenderTargetView renderTargetView,
            ID3D11ShaderResourceView view)
        {
            Width = width;
            Height = height;
            Texture = texture;
            RenderTargetView = renderTargetView;
            View = view;
        }

        public int Width { get; }

        public int Height { get; }

        public ID3D11Texture2D Texture { get; }

        public ID3D11RenderTargetView RenderTargetView { get; }

        public ID3D11ShaderResourceView View { get; }

        public void Dispose()
        {
            View.Dispose();
            RenderTargetView.Dispose();
            Texture.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Constants
    {
        public Vector4 Rect;
        public Vector4 Params;
        public Vector2 OutputSize;
        public int SourceWidth;
        public int SourceHeight;
        public int FilterMode;
        public int Pad0;
        public Vector2 Pad1;
    }
}
