using System;
using System.IO;
using System.Linq;
using FiveDo.DiffHarness;

namespace FiveDo.Tests.DiffHarness;

public sealed class DiffManifestBuilderTests : IDisposable
{
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
