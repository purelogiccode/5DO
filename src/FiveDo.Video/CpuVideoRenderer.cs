using System;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// CPU fallback presenter and accuracy oracle for <see cref="IVideoRenderer"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="FrameAssembler"/> to expand the palette. The desktop frontend uses it only
/// when an OpenGL 3.3 context is unavailable, and tests use it to compare the GPU path.
/// </remarks>
public sealed class CpuVideoRenderer : ICpuFrameRenderer
{
    private readonly MmpxFilter _mmpx = new();
    private FrameBuffer _frame = new(1, 1);
    private FrameBuffer? _sourceFrame;
    private FrameBuffer? _mmpxFrame;
    private RendererSettings _settings = new();

    /// <inheritdoc />
    public string Name => "CPU";

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
    public RendererCapabilities Capabilities => new("CPU", 0, 0, 0, false, false);

    /// <summary>Gets the most recently presented frame buffer.</summary>
    public FrameBuffer Frame => _frame;

    /// <inheritdoc />
    public void Initialize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        _frame = new FrameBuffer(width, height);
        _sourceFrame = null;
        _mmpxFrame = null;
    }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (_frame.Width != width || _frame.Height != height)
        {
            _frame = new FrameBuffer(width, height);
        }
    }

    /// <inheritdoc />
    public void Present(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var sourceWidth = frame.SourceWidth > 0 ? frame.SourceWidth : EmulatorConstants.VideoWidth;
        var sourceHeight = frame.SourceHeight > 0 ? frame.SourceHeight : EmulatorConstants.VideoHeight;
        if (_frame.Width == sourceWidth
            && _frame.Height == sourceHeight
            && Math.Abs(frame.PixelAspect - 1.0) < 1e-9)
        {
            FrameAssembler.Assemble(frame, _frame, sourceWidth, sourceHeight);
            return;
        }

        if (_sourceFrame is null
            || _sourceFrame.Width != sourceWidth
            || _sourceFrame.Height != sourceHeight)
        {
            _sourceFrame = new FrameBuffer(sourceWidth, sourceHeight);
            _mmpxFrame = null;
        }

        FrameAssembler.Assemble(frame, _sourceFrame, sourceWidth, sourceHeight);
        if (_settings.Filter == VideoFilter.Mmpx)
        {
            var mmpxWidth = sourceWidth * MmpxFilter.Scale;
            var mmpxHeight = sourceHeight * MmpxFilter.Scale;
            if (_mmpxFrame is null
                || _mmpxFrame.Width != mmpxWidth
                || _mmpxFrame.Height != mmpxHeight)
            {
                _mmpxFrame = new FrameBuffer(mmpxWidth, mmpxHeight);
            }

            _mmpx.Expand(_sourceFrame, _mmpxFrame);
            FrameScaler.Scale(_mmpxFrame, _frame, _settings, frame.PixelAspect);
            return;
        }

        FrameScaler.Scale(_sourceFrame, _frame, _settings, frame.PixelAspect);
    }

    /// <inheritdoc />
    public void ReadPixels(Span<byte> destination, out int width, out int height)
    {
        width = _frame.Width;
        height = _frame.Height;
        var required = checked(width * height * 4);
        if (destination.Length < required)
        {
            throw new ArgumentException($"The destination needs {required} bytes.", nameof(destination));
        }

        _frame.PixelSpan[..required].CopyTo(destination);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
