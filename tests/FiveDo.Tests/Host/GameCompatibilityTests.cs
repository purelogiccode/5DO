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
    [InlineData("0511D3D2")]
    [InlineData("9F7D72BC")]
    [InlineData("E843C635")]
    [InlineData("2AABA5B9")]
    [InlineData("BF61BB32")]
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
        Assert.Equal(FixMode.BitTiming3, GameCompatibility.ApplyTo(machine, "CB8EE795"));
        Assert.Equal(FixMode.BitTiming3, machine.Compatibility.FixMode);

        Assert.Equal(FixMode.None, GameCompatibility.ApplyTo(machine, "CB8EE795", FixMode.None));
        Assert.Equal(FixMode.None, machine.Compatibility.FixMode);

        Assert.Equal(FixMode.GraphicsStepY, GameCompatibility.ApplyTo(machine, "unknown", FixMode.GraphicsStepY));
        Assert.Equal(FixMode.GraphicsStepY, machine.Compatibility.FixMode);
    }

    [Fact]
    public void AppSettings_ResolvesAutomaticAndPerGameOverrides()
    {
        var settings = new AppSettings();
        Assert.Equal(FixMode.BitTiming3, settings.ResolveFixMode("CB8EE795"));
        Assert.Equal(FixMode.None, settings.ResolveFixMode("9F7D72BC"));
        Assert.Equal(FixMode.None, settings.ResolveFixMode("00000000"));
        Assert.Equal(FixMode.None, settings.ResolveFixMode(null));

        settings.SetFixModeOverride("9f7d72bc", FixMode.BitTiming6);
        Assert.Equal(FixMode.BitTiming6, settings.ResolveFixMode("9F7D72BC"));
        Assert.Equal(FixMode.BitTiming6, settings.GetFixModeOverride("9F7D72BC"));

        settings.SetFixModeOverride("9F7D72BC", null);
        Assert.Null(settings.GetFixModeOverride("9F7D72BC"));
        Assert.Equal(FixMode.None, settings.ResolveFixMode("9F7D72BC"));

        settings.AutoGameFixes = false;
        Assert.Equal(FixMode.None, settings.ResolveFixMode("CB8EE795"));
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
