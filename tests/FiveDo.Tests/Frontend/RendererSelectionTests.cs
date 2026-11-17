using FiveDo.Frontend.Avalonia.Views;
using FiveDo.Host;

namespace FiveDo.Tests.Frontend;

public sealed class RendererSelectionTests
{
    [Theory]
    [InlineData(VideoRendererKind.OpenGl)]
    [InlineData(VideoRendererKind.Vulkan)]
    [InlineData(VideoRendererKind.Direct3D11)]
    public void MacOs_MapsGpuRenderersToVulkan(VideoRendererKind kind)
    {
        Assert.Equal(
            VideoRendererKind.Vulkan,
            MainWindow.NormalizeRendererKind(kind, isMacOs: true));
    }

    [Fact]
    public void MacOs_KeepsSoftwareRenderer()
    {
        Assert.Equal(
            VideoRendererKind.Software,
            MainWindow.NormalizeRendererKind(VideoRendererKind.Software, isMacOs: true));
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
