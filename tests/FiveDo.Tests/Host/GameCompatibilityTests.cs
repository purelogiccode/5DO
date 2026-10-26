using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Machine;

namespace FiveDo.Tests.Host;

public sealed class GameCompatibilityTests
{
    [Theory]
    [InlineData("F3AF1B13", FixMode.BitTiming1)]
    [InlineData("217344B0", FixMode.BitTiming1)]
    [InlineData("DBB419FA", FixMode.BitTiming2)]
    [InlineData("07C32F10", FixMode.BitTiming2)]
    [InlineData("5282889F", FixMode.BitTiming2)]
    [InlineData("7340307E", FixMode.BitTiming2)]
    [InlineData("CB8EE795", FixMode.BitTiming3)]
    [InlineData("1A370EBA", FixMode.BitTiming5)]
    [InlineData("B35C911D", FixMode.BitTiming5)]
    [InlineData("0511D3D2", FixMode.BitTiming6)]
    [InlineData("9F7D72BC", FixMode.BitTiming6)]
    [InlineData("E843C635", FixMode.BitTiming6)]
    [InlineData("2AABA5B9", FixMode.GraphicsStepY)]
    [InlineData("BF61BB32", FixMode.GraphicsStepY)]
    public void ForGameId_MapsThe4DoDatabaseIds(string gameId, FixMode expected)
    {
        Assert.Equal(expected, GameCompatibility.ForGameId(gameId).FixMode);
        Assert.Equal(expected, GameCompatibility.ForGameId(gameId.ToLowerInvariant()).FixMode);
        Assert.Equal(expected, GameCompatibility.ForGameId(gameId + "00112233445566778899AABBCCDDEEFF").FixMode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00000000")]
    [InlineData("FFFFFFFF")]
    public void ForGameId_ReturnsNoneForUnknownIds(string gameId)
    {
        Assert.Equal(GameQuirks.None, GameCompatibility.ForGameId(gameId));
    }

    [Fact]
    public void ForGameId_ReturnsNoneForNull()
    {
        Assert.Equal(GameQuirks.None, GameCompatibility.ForGameId(null));
    }

    [Fact]
    public void ApplyTo_ReplacesTheFixModeAndHonorsAnOverride()
    {
        var machine = new FiveDoMachine();
        Assert.Equal(FixMode.BitTiming6, GameCompatibility.ApplyTo(machine, "9F7D72BC"));
        Assert.Equal(FixMode.BitTiming6, machine.Compatibility.FixMode);

        Assert.Equal(FixMode.None, GameCompatibility.ApplyTo(machine, "9F7D72BC", FixMode.None));
        Assert.Equal(FixMode.None, machine.Compatibility.FixMode);

        Assert.Equal(FixMode.GraphicsStepY, GameCompatibility.ApplyTo(machine, "unknown", FixMode.GraphicsStepY));
        Assert.Equal(FixMode.GraphicsStepY, machine.Compatibility.FixMode);
    }

    [Fact]
    public void AppSettings_ResolvesAutomaticAndPerGameOverrides()
    {
        var settings = new AppSettings();
        Assert.Equal(FixMode.BitTiming6, settings.ResolveFixMode("9F7D72BC"));
        Assert.Equal(FixMode.None, settings.ResolveFixMode("00000000"));
        Assert.Equal(FixMode.None, settings.ResolveFixMode(null));

        settings.SetFixModeOverride("9f7d72bc", FixMode.None);
        Assert.Equal(FixMode.None, settings.ResolveFixMode("9F7D72BC"));
        Assert.Equal(FixMode.None, settings.GetFixModeOverride("9F7D72BC"));

        settings.SetFixModeOverride("9F7D72BC", null);
        Assert.Null(settings.GetFixModeOverride("9F7D72BC"));
        Assert.Equal(FixMode.BitTiming6, settings.ResolveFixMode("9F7D72BC"));

        settings.AutoGameFixes = false;
        Assert.Equal(FixMode.None, settings.ResolveFixMode("2AABA5B9"));
    }

    [Fact]
    public void AppSettings_ResolvesAndClearsPerGameFilterOverrides()
    {
        var settings = new AppSettings { Filter = FiveDo.Video.VideoFilter.SharpBilinear };
        Assert.Equal(FiveDo.Video.VideoFilter.SharpBilinear, settings.ResolveFilter("9F7D72BC"));
        Assert.Equal(FiveDo.Video.VideoFilter.SharpBilinear, settings.ResolveFilter(null));

        settings.SetFilterOverride("9f7d72bc", FiveDo.Video.VideoFilter.Hqx);
        Assert.Equal(FiveDo.Video.VideoFilter.Hqx, settings.ResolveFilter("9F7D72BC"));
        Assert.Equal(FiveDo.Video.VideoFilter.Hqx, settings.GetFilterOverride("9F7D72BC"));

        settings.SetFilterOverride("9F7D72BC", null);
        Assert.Null(settings.GetFilterOverride("9F7D72BC"));
        Assert.Equal(FiveDo.Video.VideoFilter.SharpBilinear, settings.ResolveFilter("9F7D72BC"));
    }
}
