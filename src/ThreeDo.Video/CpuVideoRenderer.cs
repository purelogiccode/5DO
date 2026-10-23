using System;
using ThreeDo.Core;

namespace ThreeDo.Video;

/// <summary>
/// CPU fallback presenter and accuracy oracle for <see cref="IVideoRenderer"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="FrameAssembler"/> to expand the palette. The desktop frontend uses it only
/// when an OpenGL 3.3 context is unavailable, and tests use it to compare the GPU path.
/// </remarks>
public sealed class CpuVideoRenderer : IVideoRenderer
{
    private FrameBuffer _frame = new(1, 1);
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
        FrameAssembler.Assemble(frame, _frame, _frame.Width, _frame.Height);
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
