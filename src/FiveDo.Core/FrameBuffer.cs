using System;

namespace FiveDo.Core;

/// <summary>
/// A CPU-expanded frame buffer used by the headless renderer and as the accuracy oracle
/// for the OpenGL presenter.
/// </summary>
public sealed class FrameBuffer
{
    private readonly byte[] _pixels;

    /// <summary>Initializes a new instance of the <see cref="FrameBuffer"/> class.</summary>
    /// <param name="width">Frame width in pixels. Must be positive.</param>
    /// <param name="height">Frame height in lines. Must be positive.</param>
    /// <param name="format">Pixel layout. The default is <see cref="PixelFormat.Bgra8888"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
    public FrameBuffer(int width, int height, PixelFormat format = PixelFormat.Bgra8888)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The frame width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The frame height must be positive.");
        }

        Width = width;
        Height = height;
        Format = format;
        Stride = checked(width * 4);
        _pixels = new byte[checked(Stride * height)];
    }

    /// <summary>Gets the frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the frame height in lines.</summary>
    public int Height { get; }

    /// <summary>Gets the number of bytes between the start of consecutive lines.</summary>
    public int Stride { get; }

    /// <summary>Gets the pixel layout.</summary>
    public PixelFormat Format { get; }

    /// <summary>Gets the backing pixel memory.</summary>
    public Memory<byte> Pixels => _pixels;

    /// <summary>Gets the backing pixel memory as a span.</summary>
    public Span<byte> PixelSpan => _pixels;

    /// <summary>Clears the frame to transparent black.</summary>
    public void Clear() => _pixels.AsSpan().Clear();
}
