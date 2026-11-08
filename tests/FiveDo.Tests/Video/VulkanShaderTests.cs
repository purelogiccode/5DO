using System;
using FiveDo.Video;
using FiveDo.Video.Vulkan;

namespace FiveDo.Tests.Video;

public sealed class VulkanShaderTests
{
    [Fact]
    public void GeneratedShaders_AreSpirVModules()
    {
        AssertSpirV(VulkanShaders.Frame_vert);
        AssertSpirV(VulkanShaders.Palette_frag);
        AssertSpirV(VulkanShaders.Scale_frag);
        AssertSpirV(VulkanShaders.Hqx_frag);
        AssertSpirV(VulkanShaders.Xbr_frag);
        AssertSpirV(VulkanShaders.FsrEasu_frag);
        AssertSpirV(VulkanShaders.FsrRcas_frag);
    }

    [Fact]
    public void VulkanApi_IsReportedAsAVulkanBackend()
    {
        var capabilities = new RendererCapabilities(
            "Test Vulkan",
            1,
            3,
            16384,
            SupportsIntegerTextures: true,
            SupportsVsync: false,
            RendererApi.Vulkan);

        Assert.Equal(RendererApi.Vulkan, capabilities.Api);
        Assert.False(capabilities.SupportsOpenGl33);
        Assert.False(capabilities.IsOpenGlEs);
    }

    private static void AssertSpirV(ReadOnlySpan<byte> shader)
    {
        Assert.True(shader.Length >= 20);
        Assert.Equal(0, shader.Length % sizeof(uint));
        Assert.Equal(0x07230203u, BitConverter.ToUInt32(shader));
    }
}
