using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveDo.DiffHarness;

namespace FiveDo.Tests.DiffHarness;

public sealed class DiffManifestBuilderTests : IDisposable
{
    private static readonly int[] PanelCheckpoints = [60, 120, 300];
    private static readonly int[] FinalCheckpoint = [300];

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "fivedo-diff-build-" + Guid.NewGuid().ToString("N"));

    public DiffManifestBuilderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Build_ScansRootsAndDeduplicatesIds()
    {
        var root = Path.Combine(_directory, "games");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllText(Path.Combine(root, "Alpha (USA).chd"), "x");
        File.WriteAllText(Path.Combine(root, "sub", "Alpha (USA).chd"), "x");
        File.WriteAllText(Path.Combine(root, "ignored.iso"), "x");

        DiffManifest manifest = DiffManifestBuilder.Build(
            "unit",
            [new RootSpec("games", root)],
            frames: 100,
            checkpoints: [100],
            bios: "bios/panafz1.bin",
            operaCore: null,
            workDirectory: "artifacts/diff/work",
            extensions: [".chd"]);

        Assert.Equal(2, manifest.Titles!.Length);
        Assert.Equal("unit", manifest.Name);
        Assert.Equal(root, manifest.Roots!["games"]);
        Assert.All(manifest.Titles, title => Assert.StartsWith("${games}/", title.Disc, StringComparison.Ordinal));
        Assert.Equal(2, manifest.Titles.Select(title => title.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(100, manifest.Defaults!.Frames);
    }

    [Fact]
    public void Normalize_ExpandsNamedRoots()
    {
        var path = Path.Combine(_directory, "manifest.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 1,
              "name": "unit",
              "roots": { "games": "D:/games" },
              "titles": [
                { "id": "one", "disc": "${games}/Alpha.chd" }
              ]
            }
            """);

        DiffManifest manifest = DiffManifestLoader.Load(path);

        Assert.Equal("D:/games/Alpha.chd", manifest.Titles![0].Disc);
    }

    [Fact]
    public void BuildPanel_SelectsEvenlySpacedTitlesAndAppliesTest07Policy()
    {
        DiffManifest panel = DiffManifestBuilder.BuildPanel(
            Source(10),
            count: 5,
            frames: 300,
            checkpoints: [60, 120, 300],
            operaCore: "artifacts/diff/opera-build-current/OperaCore.dll",
            workDirectory: "artifacts/diff/work-panel",
            name: "panel");

        Assert.Equal("panel", panel.Name);
        Assert.Equal(5, panel.Titles!.Length);
        Assert.Equal("t0", panel.Titles[0].Id);
        Assert.Equal("t9", panel.Titles[^1].Id);
        Assert.Equal(5, panel.Titles.Select(title => title.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(300, panel.Defaults!.Frames);
        Assert.Equal(PanelCheckpoints, panel.Defaults.Checkpoints);
        Assert.Equal("artifacts/diff/opera-build-current/OperaCore.dll", panel.Defaults.OperaCore);
        Assert.Equal("artifacts/diff/work-panel", panel.Defaults.WorkDirectory);
        Assert.True(panel.Defaults.Compare!.ComparePc);
        Assert.True(panel.Defaults.Compare.CompareCpsr);
        Assert.True(panel.Defaults.Compare.CompareCd);
        Assert.True(panel.Defaults.Compare.CompareSamples);
        Assert.Equal(512, panel.Defaults.Compare.SampleTolerance);
        Assert.All(panel.Titles, title => Assert.Empty(title.Allow!));
        Assert.All(panel.Titles, title => Assert.Null(title.Frames));
        Assert.All(panel.Titles, title => Assert.Null(title.Checkpoints));
    }

    [Fact]
    public void BuildPanel_CountLargerThanSourceReturnsEveryTitle()
    {
        DiffManifest panel = DiffManifestBuilder.BuildPanel(
            Source(3), count: 50, frames: 300, checkpoints: [], null, null, null);

        Assert.Equal(3, panel.Titles!.Length);
        Assert.Equal(FinalCheckpoint, panel.Defaults!.Checkpoints);
    }

    [Fact]
    public void BuildPanel_CountOneReturnsFirstTitle()
    {
        DiffManifest panel = DiffManifestBuilder.BuildPanel(
            Source(10), count: 1, frames: 300, checkpoints: [300], null, null, null);

        DiffTitle title = Assert.Single(panel.Titles!);
        Assert.Equal("t0", title.Id);
    }

    private static DiffManifest Source(int count)
    {
        return new DiffManifest
        {
            Name = "source",
            Roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["games"] = "D:/games",
            },
            Defaults = new DiffDefaults
            {
                Bios = "bios/panafz1.bin",
                Frames = 3000,
                Options = new DiffMachineOptions { Profile = "accuracy" },
            },
            Titles = Enumerable.Range(0, count)
                .Select(index => new DiffTitle
                {
                    Id = "t" + index,
                    Name = "Title " + index,
                    Disc = "${games}/t" + index + ".chd",
                    Frames = 1000,
                    Checkpoints = [1000],
                    Allow =
                    [
                        new DiffAllowance
                        {
                            Field = "frameHash",
                            Checkpoint = 1000,
                            Reason = "inherited",
                        },
                    ],
                })
                .ToArray(),
        };
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
}
