using System;
using FiveDo.DiffHarness;

namespace FiveDo.Tests.DiffHarness;

public sealed class HarnessCommonTests
{
    [Fact]
    public void ResolveSeed_DefaultsToTheStablePanelSeed()
    {
        uint seed = HarnessCommon.ResolveSeed(
            new HarnessOptions(),
            new DiffMachineOptions(),
            "title");

        Assert.Equal(HarnessCommon.DefaultSeed, seed);
    }

    [Fact]
    public void ResolveSeed_UsesTheManifestSeed()
    {
        uint seed = HarnessCommon.ResolveSeed(
            new HarnessOptions(),
            new DiffMachineOptions { RandomSeed = "0x10" },
            "title");

        Assert.Equal(16u, seed);
    }

    [Fact]
    public void ResolveSeed_HarnessOverrideWins()
    {
        uint seed = HarnessCommon.ResolveSeed(
            new HarnessOptions { Seed = 7 },
            new DiffMachineOptions { RandomSeed = "9" },
            "title");

        Assert.Equal(7u, seed);
    }

    [Fact]
    public void ResolveSeed_RejectsRandomSeeds()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            HarnessCommon.ResolveSeed(
                new HarnessOptions(),
                new DiffMachineOptions { RandomSeed = "random" },
                "title"));

        Assert.Contains("fixed seed", exception.Message, StringComparison.Ordinal);
    }
}
