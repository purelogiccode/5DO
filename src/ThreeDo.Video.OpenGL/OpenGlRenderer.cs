using System;
using System.Globalization;
using Silk.NET.OpenGL;
using ThreeDo.Core;
using GlPixelFormat = Silk.NET.OpenGL.PixelFormat;

namespace ThreeDo.Video.OpenGL;

/// <summary>
/// The required desktop presenter: a two-stage palette/scale pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Stage 1 expands the indexed VDL pixels and per-line palette into a native-resolution
/// RGBA texture. Stage 2 samples that texture at output resolution and applies the selected
/// filter. This keeps palette work at ~77k pixels per frame instead of ~8.3M at 4K.
/// </para>
/// <para>
/// This renderer is a host service. It never writes emulated state. The CPU
/// <see cref="FrameAssembler"/> remains the accuracy oracle; <see cref="VideoFilter.Accurate"/>
/// uses nearest sampling and integer scaling, and the palette shader mirrors
/// <see cref="GpuPalette"/>.
/// </para>
/// <para>
/// Context loss is handled by calling <see cref="Initialize"/> again; all GL objects are
/// recreated. If a context cannot provide integer textures, the frontend falls back to the
/// CPU image path (the renderer reports that via <see cref="Capabilities"/>).
/// </para>
/// </remarks>
public sealed class OpenGlRenderer : IVideoRenderer
{
    private const int PositionLocation = 0;

    private readonly GlContextAdapter _adapter;
    private readonly VdlFrameUploader _uploader = new();
    private readonly float[] _vertices = [0f, 0f, 1f, 0f, 0f, 1f, 1f, 1f];
    private RendererSettings _settings = new();

    private uint _paletteProgram;
    private uint _scaleProgram;
    private uint _vao;
    private uint _vbo;
    private uint _pixelTexture;
    private uint _clutTexture;
    private uint _paramTexture;
    private uint _paletteFbo;
    private uint _paletteTexture;
    private int _paletteWidth;
    private int _paletteHeight;
    private int _outputWidth;
    private int _outputHeight;
    private int _sourceWidth;
    private int _sourceHeight;
    private int _uPaletteSource;
    private int _uPaletteRect;
    private int _uPaletteOutput;
    private int _uScaleSource;
    private int _uScaleRect;
    private int _uScaleOutput;
    private int _uScaleFilter;
    private int _uScaleScanline;
    private RendererCapabilities _capabilities;
    private bool _isOpenGlEs;
    private bool _initialized;

    /// <summary>Initializes a new instance of the <see cref="OpenGlRenderer"/> class.</summary>
    /// <param name="adapter">OpenGL context adapter.</param>
    public OpenGlRenderer(GlContextAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapter = adapter;
    }

    /// <inheritdoc />
    public string Name => OpenGlRendererInfo.BackendName;

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
    public void Initialize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        DisposeResources();
        var gl = _adapter.Gl;
        _outputWidth = width;
        _outputHeight = height;
        var version = gl.GetStringS(StringName.Version) ?? string.Empty;
        _isOpenGlEs = version.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
        _capabilities = DetectCapabilities(gl, version);

        _paletteProgram = CreateProgram(gl, Shaders.Vertex(_isOpenGlEs), Shaders.FragmentPalette(_isOpenGlEs));
        _scaleProgram = CreateProgram(gl, Shaders.Vertex(_isOpenGlEs), Shaders.FragmentScale(_isOpenGlEs));
        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);
        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, _vertices.AsSpan(), BufferUsageARB.StaticDraw);

        gl.EnableVertexAttribArray(PositionLocation);
        gl.VertexAttribPointer(PositionLocation, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        _pixelTexture = CreateTexture(gl);
        _clutTexture = CreateTexture(gl);
        _paramTexture = CreateTexture(gl);
        AllocateUploadTextures(gl);

        gl.UseProgram(_paletteProgram);
        gl.Uniform1(gl.GetUniformLocation(_paletteProgram, "uPixels"), 0);
        gl.Uniform1(gl.GetUniformLocation(_paletteProgram, "uClut"), 1);
        gl.Uniform1(gl.GetUniformLocation(_paletteProgram, "uParams"), 2);
        _uPaletteSource = gl.GetUniformLocation(_paletteProgram, "uSource");
        _uPaletteRect = gl.GetUniformLocation(_paletteProgram, "uRect");
        _uPaletteOutput = gl.GetUniformLocation(_paletteProgram, "uOutput");

        gl.UseProgram(_scaleProgram);
        gl.Uniform1(gl.GetUniformLocation(_scaleProgram, "uExpanded"), 0);
        _uScaleSource = gl.GetUniformLocation(_scaleProgram, "uSource");
        _uScaleRect = gl.GetUniformLocation(_scaleProgram, "uRect");
        _uScaleOutput = gl.GetUniformLocation(_scaleProgram, "uOutput");
        _uScaleFilter = gl.GetUniformLocation(_scaleProgram, "uFilter");
        _uScaleScanline = gl.GetUniformLocation(_scaleProgram, "uScanline");

        _paletteFbo = 0;
        _paletteTexture = 0;
        _paletteWidth = 0;
        _paletteHeight = 0;
        _initialized = true;
    }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        _outputWidth = width;
        _outputHeight = height;
    }

    /// <inheritdoc />
    public void Present(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!_initialized)
        {
            Initialize(EmulatorConstants.VideoWidth, EmulatorConstants.VideoHeight);
        }

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
        }

        var gl = _adapter.Gl;
        EnsurePaletteTarget(gl, width, height);
        UploadTextures(gl, width, height);

        // Stage 1: expand the indexed frame and per-line palette at native resolution.
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _paletteFbo);
        gl.Viewport(0, 0, (uint)width, (uint)height);
        gl.UseProgram(_paletteProgram);
        gl.Uniform2(_uPaletteSource, width, height);
        gl.Uniform4(_uPaletteRect, 0f, 0f, width, height);
        gl.Uniform2(_uPaletteOutput, width, height);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _pixelTexture);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _clutTexture);
        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, _paramTexture);
        gl.BindVertexArray(_vao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        // Stage 2: scale/filter the expanded texture into the output framebuffer.
        var viewport = ViewportCalculator.Compute(width, height, _outputWidth, _outputHeight, _settings);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)_adapter.Framebuffer);
        gl.Viewport(0, 0, (uint)_outputWidth, (uint)_outputHeight);
        gl.ClearColor(0f, 0f, 0f, 1f);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        gl.UseProgram(_scaleProgram);
        gl.Uniform2(_uScaleSource, width, height);
        gl.Uniform4(_uScaleRect, viewport.X, viewport.Y, viewport.Width, viewport.Height);
        gl.Uniform2(_uScaleOutput, _outputWidth, _outputHeight);
        gl.Uniform1(_uScaleFilter, FilterValue(_settings.Filter));
        gl.Uniform1(_uScaleScanline, _settings.ScanlineStrength);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _paletteTexture);
        gl.BindVertexArray(_vao);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
    }

    /// <inheritdoc />
    public void ReadPixels(Span<byte> destination, out int width, out int height)
    {
        width = _outputWidth;
        height = _outputHeight;
        var required = checked(width * height * 4);
        if (destination.Length < required)
        {
            throw new ArgumentException($"The destination needs {required} bytes.", nameof(destination));
        }

        var gl = _adapter.Gl;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)_adapter.Framebuffer);
        var rgba = new byte[required];
        gl.ReadPixels(0, 0, (uint)width, (uint)height, GlPixelFormat.Rgba, PixelType.UnsignedByte, rgba.AsSpan());
        for (var i = 0; i < required; i += 4)
        {
            destination[i] = rgba[i + 2];
            destination[i + 1] = rgba[i + 1];
            destination[i + 2] = rgba[i];
            destination[i + 3] = 0xff;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeResources();
        _initialized = false;
    }

    private void EnsurePaletteTarget(GL gl, int width, int height)
    {
        if (_paletteFbo != 0 && _paletteWidth == width && _paletteHeight == height)
        {
            return;
        }

        if (_paletteFbo != 0)
        {
            gl.DeleteFramebuffer(_paletteFbo);
            gl.DeleteTexture(_paletteTexture);
        }

        _paletteTexture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _paletteTexture);
        gl.TexStorage2D(TextureTarget.Texture2D, 1, SizedInternalFormat.Rgba8, (uint)width, (uint)height);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _paletteFbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _paletteFbo);
        gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _paletteTexture,
            0);
        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException($"Palette framebuffer is incomplete: {status}.");
        }

        _paletteWidth = width;
        _paletteHeight = height;
    }

    private void AllocateUploadTextures(GL gl)
    {
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _pixelTexture);
        gl.TexStorage2D(
            TextureTarget.Texture2D,
            1,
            SizedInternalFormat.R16ui,
            VdlFrameUploader.MaxWidth,
            VdlFrameUploader.MaxHeight);

        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _clutTexture);
        gl.TexStorage2D(
            TextureTarget.Texture2D,
            1,
            SizedInternalFormat.Rgb8,
            VdlFrameUploader.ClutEntries,
            VdlFrameUploader.MaxHeight);

        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, _paramTexture);
        gl.TexStorage2D(
            TextureTarget.Texture2D,
            1,
            SizedInternalFormat.Rgba32ui,
            1,
            VdlFrameUploader.MaxHeight);
    }

    private void UploadTextures(GL gl, int width, int height)
    {
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _pixelTexture);
        gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)width, (uint)height, GlPixelFormat.RedInteger, PixelType.UnsignedShort, _uploader.Pixels);

        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _clutTexture);
        gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, VdlFrameUploader.ClutEntries, (uint)height, GlPixelFormat.Rgb, PixelType.UnsignedByte, _uploader.Clut);

        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, _paramTexture);
        gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 1, (uint)height, GlPixelFormat.RgbaInteger, PixelType.UnsignedInt, _uploader.LineParams);
    }

    private static uint CreateTexture(GL gl)
    {
        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return texture;
    }

    private static uint CreateProgram(GL gl, string vertexSource, string fragmentSource)
    {
        var vertex = Compile(gl, ShaderType.VertexShader, vertexSource);
        var fragment = Compile(gl, ShaderType.FragmentShader, fragmentSource);
        var program = gl.CreateProgram();
        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.LinkProgram(program);
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0)
        {
            var log = gl.GetProgramInfoLog(program);
            gl.DeleteProgram(program);
            gl.DeleteShader(vertex);
            gl.DeleteShader(fragment);
            throw new InvalidOperationException($"OpenGL program link failed: {log}");
        }

        gl.DetachShader(program, vertex);
        gl.DetachShader(program, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        return program;
    }

    private static uint Compile(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            var log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"OpenGL shader compile failed ({type}): {log}");
        }

        return shader;
    }

    private static RendererCapabilities DetectCapabilities(GL gl, string version)
    {
        var parts = version.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var major = 0;
        var minor = 0;
        if (parts.Length > 0)
        {
            var numbers = parts[0].Split('.');
            if (numbers.Length >= 2)
            {
                _ = int.TryParse(numbers[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out major);
                _ = int.TryParse(numbers[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor);
            }
        }

        var maxTexture = gl.GetInteger(GetPName.MaxTextureSize);
        return new RendererCapabilities(
            OpenGlRendererInfo.BackendName,
            major,
            minor,
            maxTexture,
            major > 3 || (major == 3 && minor >= 3),
            true);
    }

    private static int FilterValue(VideoFilter filter) => filter switch
    {
        VideoFilter.Accurate or VideoFilter.Nearest => 0,
        VideoFilter.Crt => 2,
        _ => 1,
    };

    private void DisposeResources()
    {
        if (_paletteProgram == 0 && _scaleProgram == 0 && _vao == 0)
        {
            return;
        }

        var gl = _adapter.Gl;
        if (_paletteProgram != 0)
        {
            gl.DeleteProgram(_paletteProgram);
            _paletteProgram = 0;
        }

        if (_scaleProgram != 0)
        {
            gl.DeleteProgram(_scaleProgram);
            _scaleProgram = 0;
        }

        if (_pixelTexture != 0)
        {
            gl.DeleteTexture(_pixelTexture);
            _pixelTexture = 0;
        }

        if (_clutTexture != 0)
        {
            gl.DeleteTexture(_clutTexture);
            _clutTexture = 0;
        }

        if (_paramTexture != 0)
        {
            gl.DeleteTexture(_paramTexture);
            _paramTexture = 0;
        }

        if (_paletteFbo != 0)
        {
            gl.DeleteFramebuffer(_paletteFbo);
            _paletteFbo = 0;
        }

        if (_paletteTexture != 0)
        {
            gl.DeleteTexture(_paletteTexture);
            _paletteTexture = 0;
        }

        if (_vbo != 0)
        {
            gl.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        if (_vao != 0)
        {
            gl.DeleteVertexArray(_vao);
            _vao = 0;
        }

        _paletteWidth = 0;
        _paletteHeight = 0;
    }
}
