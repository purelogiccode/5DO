using System;
using System.IO;
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

    [Fact]
    public void ExtractIso_WritesEverySectorAndLeavesNoTemporaryFile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "fivedo-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.iso");
            var target = Path.Combine(directory, "out", "target.iso");
            var data = new byte[3 * 2048];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)((i * 37) & 0xFF);
            }

            File.WriteAllBytes(source, data);

            HarnessCommon.ExtractIso(source, target);

            Assert.Equal(data, File.ReadAllBytes(target));
            Assert.False(File.Exists(target + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
