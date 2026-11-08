using System;
using System.IO;
using System.Text.Json;
using FiveDo.DiffHarness;

namespace FiveDo.Tests.DiffHarness;

public sealed class DiffBaselineTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "fivedo-diff-baseline-" + Guid.NewGuid().ToString("N"));

    public DiffBaselineTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Baseline_SameReport_ReturnsZero()
    {
        var baseline = Write("baseline.json", Report("0x00000001"));
        var current = Write("current.json", Report("0x00000001"));

        Assert.Equal(0, Program.Main(["baseline", "--baseline", baseline, "--current", current]));
    }

    [Fact]
    public void Baseline_ChangedValue_ReturnsOne()
    {
        var baseline = Write("baseline.json", Report("0x00000001"));
        var current = Write("current.json", Report("0x00000002"));

        Assert.Equal(1, Program.Main(["baseline", "--baseline", baseline, "--current", current]));
    }

    [Fact]
    public void Baseline_Write_EmitsAComparableTrimmedReport()
    {
        var current = Write("current.json", Report("0x00000001"));
        var baseline = Path.Combine(_directory, "baseline.json");

        Assert.Equal(
            0,
            Program.Main(["baseline", "--current", current, "--write", baseline]));

        Assert.True(File.Exists(baseline));
        Assert.DoesNotContain("captures", File.ReadAllText(baseline), StringComparison.Ordinal);
        Assert.Equal(
            0,
            Program.Main(["baseline", "--baseline", baseline, "--current", current]));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string name, DiffReport report)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, JsonSerializer.Serialize(report, DiffJsonContext.Default.DiffReport));
        return path;
    }

    private static DiffReport Report(string frameHash)
    {
        return new DiffReport
        {
            ManifestName = "unit",
            ManifestPath = "manifest.json",
            Titles =
            [
                new DiffTitleResult
                {
                    Id = "one",
                    Name = "one",
                    Status = "Match",
                    Frames = 100,
                    Checkpoints =
                    [
                        new DiffCheckpointResult
                        {
                            Frame = 100,
                            Match = true,
                            Fields =
                            [
                                new DiffFieldResult
                                {
                                    Field = "frameHash",
                                    Match = true,
                                    FiveDo = frameHash,
                                    Opera = frameHash,
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
