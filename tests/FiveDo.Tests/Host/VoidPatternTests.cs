using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class VoidPatternTests
{
    [Theory]
    [InlineData("Solid", VoidPattern.Solid)]
    [InlineData("checker", VoidPattern.Checker)]
    [InlineData("Grid", VoidPattern.Grid)]
    [InlineData("SCANLINE", VoidPattern.Scanline)]
    public void ResolveVoidAreaPatternParsesKnownNames(string value, VoidPattern expected)
    {
        var settings = new AppSettings { VoidAreaPattern = value };

        Assert.Equal(expected, settings.ResolveVoidAreaPattern());
    }

    [Theory]
    [InlineData("")]
    [InlineData("spiral")]
    [InlineData("7")]
    public void ResolveVoidAreaPatternFallsBackToSolid(string value)
    {
        var settings = new AppSettings { VoidAreaPattern = value };

        Assert.Equal(VoidPattern.Solid, settings.ResolveVoidAreaPattern());
    }
}
