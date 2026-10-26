using FiveDo.Video;
using FiveDo.Video.OpenGL;

namespace FiveDo.Tests.Video;

public sealed class OpenGlCapabilitiesTests
{
    [Fact]
    public void Detect_DesktopVersionParsesMajorAndMinor()
    {
        var capabilities = OpenGlCapabilities.Detect("4.6.0 NVIDIA 552.22", 32768);

        Assert.Equal(RendererApi.OpenGl, capabilities.Api);
        Assert.Equal("OpenGL", capabilities.Name);
        Assert.Equal(4, capabilities.MajorVersion);
        Assert.Equal(6, capabilities.MinorVersion);
        Assert.Equal(32768, capabilities.MaxTextureSize);
        Assert.True(capabilities.SupportsIntegerTextures);
        Assert.True(capabilities.SupportsOpenGl33);
        Assert.False(capabilities.IsOpenGlEs);
        Assert.False(capabilities.SupportsVsync);
    }

    [Fact]
    public void Detect_ReportsVsyncOnlyWhenTheHostResolvedASwapIntervalBackend()
    {
        Assert.False(OpenGlCapabilities.Detect("4.6.0", 32768).SupportsVsync);
        Assert.True(OpenGlCapabilities.Detect("4.6.0", 32768, supportsVsync: true).SupportsVsync);
    }

    [Fact]
    public void Detect_OpenGlEsVersionParsesAfterTheApiName()
    {
        var capabilities = OpenGlCapabilities.Detect("OpenGL ES 3.2 (ANGLE 2.1.0)", 16384);

        Assert.Equal(RendererApi.OpenGlEs, capabilities.Api);
        Assert.Equal("OpenGL ES", capabilities.Name);
        Assert.Equal(3, capabilities.MajorVersion);
        Assert.Equal(2, capabilities.MinorVersion);
        Assert.True(capabilities.SupportsIntegerTextures);
        Assert.False(capabilities.SupportsOpenGl33);
        Assert.True(capabilities.IsOpenGlEs);
    }

    [Fact]
    public void Detect_OldDesktopContextDoesNotSupportIntegerTextures()
    {
        var capabilities = OpenGlCapabilities.Detect("2.1 Mesa 8.0", 8192);

        Assert.Equal(2, capabilities.MajorVersion);
        Assert.Equal(1, capabilities.MinorVersion);
        Assert.False(capabilities.SupportsIntegerTextures);
        Assert.False(capabilities.SupportsOpenGl33);
    }

    [Fact]
    public void Detect_OldOpenGlEsContextDoesNotSupportIntegerTextures()
    {
        var capabilities = OpenGlCapabilities.Detect("OpenGL ES 2.0", 4096);

        Assert.Equal(RendererApi.OpenGlEs, capabilities.Api);
        Assert.False(capabilities.SupportsIntegerTextures);
        Assert.False(capabilities.SupportsOpenGl33);
    }

    [Fact]
    public void Detect_UnparseableVersionReportsUnknownVersion()
    {
        var capabilities = OpenGlCapabilities.Detect("unknown", 0);

        Assert.Equal(RendererApi.OpenGl, capabilities.Api);
        Assert.Equal(0, capabilities.MajorVersion);
        Assert.Equal(0, capabilities.MinorVersion);
        Assert.False(capabilities.SupportsIntegerTextures);
        Assert.False(capabilities.SupportsOpenGl33);
    }

    [Fact]
    public void CpuCapabilitiesUseTheCpuApi()
    {
        using var renderer = new CpuVideoRenderer();
        Assert.Equal(RendererApi.Cpu, renderer.Capabilities.Api);
        Assert.False(renderer.Capabilities.IsOpenGlEs);
    }
}
