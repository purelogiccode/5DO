using System;
using FiveDo.Video.OpenGL;

namespace FiveDo.Tests.Video;

public sealed class SwapIntervalTests
{
    [Fact]
    public void TryCreate_PrefersWglOnWindows()
    {
        ISwapIntervalBackend? backend = SwapInterval.TryCreate(name => name switch
        {
            "wglSwapIntervalEXT" => 1,
            "glXSwapIntervalMESA" => 1,
            "eglSwapInterval" => 1,
            _ => 0,
        }, isWindows: true);

        Assert.NotNull(backend);
        Assert.Equal("WGL_EXT_swap_control", backend.Name);
    }

    [Fact]
    public void TryCreate_OffWindows_IgnoresWglAndUsesGlx()
    {
        // Mesa's GLX proc-address loader returns a non-null stub for unknown names,
        // including wglSwapIntervalEXT. The resolver must not treat that as WGL.
        ISwapIntervalBackend? backend = SwapInterval.TryCreate(name => name switch
        {
            "wglSwapIntervalEXT" => 1,
            "glXSwapIntervalEXT" => 2,
            "glXGetCurrentDisplay" => 3,
            "glXGetCurrentDrawable" => 4,
            _ => 0,
        }, isWindows: false);

        Assert.NotNull(backend);
        Assert.Equal("GLX_EXT_swap_control", backend.Name);
    }

    [Fact]
    public void TryCreate_OffWindows_FallsBackToEglWhenGlxIsUnavailable()
    {
        ISwapIntervalBackend? backend = SwapInterval.TryCreate(name => name switch
        {
            "wglSwapIntervalEXT" => 1,
            "eglSwapInterval" => 1,
            "eglGetCurrentDisplay" => 1,
            _ => 0,
        }, isWindows: false);

        Assert.NotNull(backend);
        Assert.Equal("EGL_KHR_swap_interval", backend.Name);
    }

    [Fact]
    public void TryCreate_UsesGlxExtWhenDisplayAndDrawableAreAvailable()
    {
        ISwapIntervalBackend? backend = SwapInterval.TryCreate(name => name switch
        {
            "glXSwapIntervalEXT" => 1,
            "glXGetCurrentDisplay" => 1,
            "glXGetCurrentDrawable" => 1,
            "glXSwapIntervalMESA" => 1,
            _ => 0,
        });

        Assert.NotNull(backend);
        Assert.Equal("GLX_EXT_swap_control", backend.Name);
    }

    [Fact]
    public void TryCreate_FallsBackToGlxMesaThenSgiThenEgl()
    {
        ISwapIntervalBackend? mesa = SwapInterval.TryCreate(name => string.Equals(name, "glXSwapIntervalMESA", StringComparison.Ordinal) ? 1 : 0);
        Assert.NotNull(mesa);
        Assert.Equal("GLX_MESA_swap_control", mesa.Name);

        ISwapIntervalBackend? sgi = SwapInterval.TryCreate(name => string.Equals(name, "glXSwapIntervalSGI", StringComparison.Ordinal) ? 1 : 0);
        Assert.NotNull(sgi);
        Assert.Equal("GLX_SGI_swap_control", sgi.Name);

        ISwapIntervalBackend? egl = SwapInterval.TryCreate(name => name is "eglSwapInterval" or "eglGetCurrentDisplay" ? 1 : 0);
        Assert.NotNull(egl);
        Assert.Equal("EGL_KHR_swap_interval", egl.Name);
    }

    [Fact]
    public void TryCreate_ReturnsNullWhenNoPlatformExtensionExists()
    {
        Assert.Null(SwapInterval.TryCreate(_ => 0));
    }

}
