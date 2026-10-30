using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class OutputResolutionTests
{
    [Theory]
    [InlineData(OutputResolution.Native, 320, 240)]
    [InlineData(OutputResolution.Scale2X, 640, 480)]
    [InlineData(OutputResolution.Scale3X, 960, 720)]
    [InlineData(OutputResolution.Scale4X, 1280, 960)]
    [InlineData(OutputResolution.Hd720, 1280, 720)]
    [InlineData(OutputResolution.FullHd1080, 1920, 1080)]
    [InlineData(OutputResolution.Qhd1440, 2560, 1440)]
    [InlineData(OutputResolution.Uhd2160, 3840, 2160)]
    public void TryGetSize_ReturnsPresetDimensions(OutputResolution resolution, int width, int height)
    {
        Assert.True(OutputResolutionSizes.TryGetSize(resolution, out var actualWidth, out var actualHeight));
        Assert.Equal(width, actualWidth);
        Assert.Equal(height, actualHeight);
    }

    [Fact]
    public void TryGetSize_WindowFollowsTheWindow()
    {
        Assert.False(OutputResolutionSizes.TryGetSize(OutputResolution.Window, out var width, out var height));
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }
}
