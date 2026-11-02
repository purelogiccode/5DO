using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class CropInsetsTests
{
    [Fact]
    public void Uniform_ClampsToSupportedRange()
    {
        Assert.Equal(new CropInsets(32, 32, 32, 32), CropInsets.Uniform(99));
        Assert.Equal(CropInsets.None, CropInsets.Uniform(-5));
    }

    [Fact]
    public void Fits_RequiresSixteenVisiblePixels()
    {
        Assert.True(new CropInsets(0, 0, 0, 0).Fits(320, 240));
        Assert.True(new CropInsets(32, 32, 32, 32).Fits(320, 240));
        Assert.False(new CropInsets(32, 0, 32, 0).Fits(64, 240));
        Assert.False(new CropInsets(0, 32, 0, 32).Fits(320, 64));
    }

    [Fact]
    public void ResolveOverscan_PrefersPerEdgeAndFallsBackToLegacyUniform()
    {
        var settings = new AppSettings { OverscanPixels = 8 };
        Assert.Equal(new CropInsets(8, 8, 8, 8), settings.ResolveOverscan());

        settings.OverscanLeft = 1;
        settings.OverscanTop = 2;
        settings.OverscanRight = 3;
        settings.OverscanBottom = 4;
        Assert.Equal(new CropInsets(1, 2, 3, 4), settings.ResolveOverscan());

        settings.OverscanLeft = 99;
        Assert.Equal(new CropInsets(32, 2, 3, 4), settings.ResolveOverscan());
    }

    [Fact]
    public void SetOverscan_ClearsLegacyUniformValue()
    {
        var settings = new AppSettings { OverscanPixels = 12 };
        settings.SetOverscan(new CropInsets(1, 2, 3, 4));

        Assert.Equal(0, settings.OverscanPixels);
        Assert.Equal(new CropInsets(1, 2, 3, 4), settings.ResolveOverscan());
    }

    [Theory]
    [InlineData("#000000", true)]
    [InlineData("#FFFFFFFF", true)]
    [InlineData("#123456", true)]
    [InlineData("123456", false)]
    [InlineData("#12345", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("", false)]
    public void IsValidColor_AcceptsRgbAndArgb(string value, bool expected)
    {
        Assert.Equal(expected, AppSettings.IsValidColor(value));
    }

    [Fact]
    public void ResolveVoidAreaColor_FallsBackToDefault()
    {
        var settings = new AppSettings { VoidAreaColor = "not-a-color" };
        Assert.Equal(AppSettings.DefaultVoidAreaColor, settings.ResolveVoidAreaColor());

        settings.VoidAreaColor = "#FF808080";
        Assert.Equal("#FF808080", settings.ResolveVoidAreaColor());
    }
}
