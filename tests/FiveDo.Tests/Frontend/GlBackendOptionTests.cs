using Avalonia;
using FiveDo.Frontend.Avalonia;

namespace FiveDo.Tests.Frontend;

public sealed class GlBackendOptionTests
{
    [Theory]
    [InlineData(null, GlBackend.Auto)]
    [InlineData("", GlBackend.Auto)]
    [InlineData("auto", GlBackend.Auto)]
    [InlineData("default", GlBackend.Auto)]
    [InlineData("wgl", GlBackend.Wgl)]
    [InlineData("desktop", GlBackend.Wgl)]
    [InlineData("gl", GlBackend.Wgl)]
    [InlineData("angle", GlBackend.Angle)]
    [InlineData("angleegl", GlBackend.Angle)]
    [InlineData("egl", GlBackend.Angle)]
    [InlineData("gles", GlBackend.Angle)]
    [InlineData("software", GlBackend.Software)]
    [InlineData("cpu", GlBackend.Software)]
    [InlineData("vulkan", GlBackend.Vulkan)]
    [InlineData("vk", GlBackend.Vulkan)]
    public void TryParse_AcceptsAliases(string? text, GlBackend expected)
    {
        Assert.True(GlBackendOption.TryParse(text, out GlBackend backend));
        Assert.Equal(expected, backend);
    }

    [Theory]
    [InlineData("metal")]
    [InlineData("wgl2")]
    [InlineData("dx12")]
    public void TryParse_RejectsUnknownBackends(string text)
    {
        Assert.False(GlBackendOption.TryParse(text, out _));
    }

    [Fact]
    public void ToRenderingModes_PreservesTheRequestedOrder()
    {
        Assert.Equal([Win32RenderingMode.Wgl], GlBackendOption.ToRenderingModes(GlBackend.Wgl));
        Assert.Equal([Win32RenderingMode.AngleEgl], GlBackendOption.ToRenderingModes(GlBackend.Angle));
        Assert.Equal([Win32RenderingMode.Software], GlBackendOption.ToRenderingModes(GlBackend.Software));
        Assert.Equal([Win32RenderingMode.Software], GlBackendOption.ToRenderingModes(GlBackend.Vulkan));
        Assert.Equal(
            [Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
            GlBackendOption.ToRenderingModes(GlBackend.Auto));
    }
}
