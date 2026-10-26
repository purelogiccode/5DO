using ThreeDo.Frontend.Avalonia.Controls;

namespace ThreeDo.Tests.Frontend;

public sealed class PixelSizeTests
{
    [Theory]
    [InlineData(1280, 720, 1.0, 1280, 720)]
    [InlineData(1280, 720, 1.5, 1920, 1080)]
    [InlineData(1280, 720, 2.0, 2560, 1440)]
    [InlineData(2560, 1440, 1.5, 3840, 2160)]
    [InlineData(320, 240, 1.25, 400, 300)]
    public void Compute_UsesPhysicalPixels(
        double width,
        double height,
        double scaling,
        int expectedWidth,
        int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), PixelSize.Compute(width, height, scaling));
    }

    [Fact]
    public void Compute_RoundsFractionalBoundsAwayFromZero()
    {
        Assert.Equal((1280, 720), PixelSize.Compute(853.333, 480, 1.5));
        Assert.Equal((2, 2), PixelSize.Compute(1.5, 1.5, 1.0));
        Assert.Equal((3, 3), PixelSize.Compute(2.5, 2.5, 1.0));
    }

    [Theory]
    [InlineData(0, 720, 1.0, 1, 720)]
    [InlineData(1280, 0, 1.0, 1280, 1)]
    [InlineData(-10, 720, 1.0, 1, 720)]
    [InlineData(1280, -10, 1.0, 1280, 1)]
    [InlineData(1280, 720, 0, 1, 1)]
    [InlineData(1280, 720, -1, 1, 1)]
    [InlineData(double.NaN, 720, 1.0, 1, 1)]
    [InlineData(1280, double.PositiveInfinity, 1.0, 1, 1)]
    public void Compute_ClampsEachDimensionToOnePixel(
        double width,
        double height,
        double scaling,
        int expectedWidth,
        int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), PixelSize.Compute(width, height, scaling));
    }
}
