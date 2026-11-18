using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class RendererSettingsTests
{
    [Fact]
    public void VoidAreaColor_DefaultsToTheAppSettingsBackdrop()
    {
        var settings = new RendererSettings();

        Assert.Equal(0xFF101010u, settings.VoidAreaColor);
        Assert.Equal(RendererSettings.DefaultVoidAreaColor, settings.VoidAreaColor);
    }

    [Fact]
    public void VoidAreaColorRgb_ConvertsPackedArgbToNormalizedComponents()
    {
        var settings = new RendererSettings { VoidAreaColor = 0xFF80_4020 };

        var (red, green, blue) = settings.VoidAreaColorRgb;

        Assert.Equal(0x80 / 255f, red);
        Assert.Equal(0x40 / 255f, green);
        Assert.Equal(0x20 / 255f, blue);
    }

    [Theory]
    [InlineData(0xFF000000u, 0f, 0f, 0f)]
    [InlineData(0xFFFFFFFFu, 1f, 1f, 1f)]
    [InlineData(0xFF10_1010u, 16f / 255f, 16f / 255f, 16f / 255f)]
    public void VoidAreaColorRgb_HandlesTheExtremes(uint color, float red, float green, float blue)
    {
        var settings = new RendererSettings { VoidAreaColor = color };

        var rgb = settings.VoidAreaColorRgb;

        Assert.Equal(red, rgb.Red);
        Assert.Equal(green, rgb.Green);
        Assert.Equal(blue, rgb.Blue);
    }
}
