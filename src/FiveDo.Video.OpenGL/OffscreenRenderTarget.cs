using System;
using Silk.NET.OpenGL;
using GlPixelFormat = Silk.NET.OpenGL.PixelFormat;

namespace FiveDo.Video.OpenGL;

/// <summary>
/// An RGBA8 framebuffer used to validate output-resolution rendering without a
/// display-sized window.
/// </summary>
/// <remarks>
/// The target is intentionally simple: one color attachment, no depth/stencil, and
/// immutable dimensions for its lifetime. It is a host-side validation tool; the
/// emulation core and the normal presentation path are unchanged.
/// </remarks>
public sealed class OffscreenRenderTarget : IDisposable
{
    private readonly GL _gl;
    private uint _framebuffer;
    private uint _texture;

    private OffscreenRenderTarget(GL gl, uint framebuffer, uint texture, int width, int height)
    {
        _gl = gl;
        _framebuffer = framebuffer;
        _texture = texture;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the framebuffer handle.</summary>
    public uint Framebuffer => _framebuffer;

    /// <summary>Gets the target width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the target height in pixels.</summary>
    public int Height { get; }

    /// <summary>Creates a complete RGBA8 framebuffer.</summary>
    /// <param name="gl">Current OpenGL API.</param>
    /// <param name="width">Target width in pixels.</param>
    /// <param name="height">Target height in pixels.</param>
    /// <returns>The new target.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="gl"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the framebuffer is incomplete.</exception>
    public static OffscreenRenderTarget Create(GL gl, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(gl);

        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            InternalFormat.Rgba8,
            (uint)width,
            (uint)height,
            0,
            GlPixelFormat.Rgba,
            PixelType.UnsignedByte,
            ReadOnlySpan<byte>.Empty);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        var framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            texture,
            0);
        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (status != GLEnum.FramebufferComplete)
        {
            gl.DeleteFramebuffer(framebuffer);
            gl.DeleteTexture(texture);
            throw new InvalidOperationException($"Offscreen framebuffer is incomplete: {status}.");
        }

        return new OffscreenRenderTarget(gl, framebuffer, texture, width, height);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_framebuffer != 0)
        {
            _gl.DeleteFramebuffer(_framebuffer);
            _framebuffer = 0;
        }

        if (_texture != 0)
        {
            _gl.DeleteTexture(_texture);
            _texture = 0;
        }
    }
}
