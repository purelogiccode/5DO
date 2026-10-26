using ThreeDo.Video.OpenGL;

namespace ThreeDo.Tests.Video;

public sealed class SwapIntervalTests
{
    [Fact]
    public void TryCreate_PrefersWgl()
    {
        var backend = SwapInterval.TryCreate(name => name switch
        {
            "wglSwapIntervalEXT" => 1,
            "glXSwapIntervalMESA" => 1,
            "eglSwapInterval" => 1,
            _ => 0,
        });

        Assert.NotNull(backend);
        Assert.Equal("WGL_EXT_swap_control", backend.Name);
    }

    [Fact]
    public void TryCreate_UsesGlxExtWhenDisplayAndDrawableAreAvailable()
    {
        var backend = SwapInterval.TryCreate(name => name switch
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
        var mesa = SwapInterval.TryCreate(name => name == "glXSwapIntervalMESA" ? 1 : 0);
        Assert.NotNull(mesa);
        Assert.Equal("GLX_MESA_swap_control", mesa.Name);

        var sgi = SwapInterval.TryCreate(name => name == "glXSwapIntervalSGI" ? 1 : 0);
        Assert.NotNull(sgi);
        Assert.Equal("GLX_SGI_swap_control", sgi.Name);

        var egl = SwapInterval.TryCreate(name => name is "eglSwapInterval" or "eglGetCurrentDisplay" ? 1 : 0);
        Assert.NotNull(egl);
        Assert.Equal("EGL_KHR_swap_interval", egl.Name);
    }

    [Fact]
    public void TryCreate_ReturnsNullWhenNoPlatformExtensionExists()
    {
        Assert.Null(SwapInterval.TryCreate(_ => 0));
    }

}
