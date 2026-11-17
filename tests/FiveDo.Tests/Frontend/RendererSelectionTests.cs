using FiveDo.Frontend.Avalonia.Views;
using FiveDo.Host;

namespace FiveDo.Tests.Frontend;

public sealed class RendererSelectionTests
{
    [Fact]
    public void MacOs_MapsVulkanToOpenGl()
    {
        Assert.Equal(
            VideoRendererKind.OpenGl,
            MainWindow.NormalizeRendererKind(VideoRendererKind.Vulkan, isMacOs: true));
    }

    [Theory]
    [InlineData(VideoRendererKind.OpenGl)]
    [InlineData(VideoRendererKind.Software)]
    public void MacOs_KeepsNonVulkanRenderers(VideoRendererKind kind)
    {
        Assert.Equal(kind, MainWindow.NormalizeRendererKind(kind, isMacOs: true));
    }

    [Theory]
    [InlineData(VideoRendererKind.Vulkan)]
    [InlineData(VideoRendererKind.OpenGl)]
    [InlineData(VideoRendererKind.Direct3D11)]
    [InlineData(VideoRendererKind.Software)]
    public void NonMacOs_KeepsTheRequestedRenderer(VideoRendererKind kind)
    {
        Assert.Equal(kind, MainWindow.NormalizeRendererKind(kind, isMacOs: false));
    }
}
