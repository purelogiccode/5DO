using System;
using System.IO;
using FiveDo.DiffHarness;

namespace FiveDo.Tests.DiffHarness;

public sealed class DiffManifestTests : IDisposable
{
    private static readonly int[] FinalCheckpoint = [300];
    private static readonly int[] ClampedCheckpoints = [1, 2, 3];
    private static readonly int[] WindowCheckpoints = [98, 99, 100, 101, 102];
    private static readonly string[] MergedArguments = ["--prefetch", "--high-res"];

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "fivedo-diff-tests-" + Guid.NewGuid().ToString("N"));

    public DiffManifestTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Load_AppliesDefaultsAndResolvesFinalCheckpoint()
    {
        var path = Write(
            """
            {
              "schemaVersion": 1,
              "name": "unit",
              "defaults": {
                "bios": "bios/panafz1.bin",
                "frames": 300,
                "options": { "profile": "accuracy", "cdSpeed": "unbounded" }
              },
              "titles": [
                { "id": "one", "disc": "disc.chd" }
              ]
            }
            """);

        DiffManifest manifest = DiffManifestLoader.Load(path);
        DiffTitle title = manifest.Titles![0];

        Assert.Equal("unit", manifest.Name);
        Assert.Equal(300, DiffManifestLoader.ResolveFrames(manifest, title));
        Assert.Equal(FinalCheckpoint, DiffManifestLoader.ResolveCheckpoints(manifest, title));
        Assert.Equal("accuracy", manifest.Defaults!.Options!.Profile);
        Assert.Equal("unbounded", manifest.Defaults.Options.CdSpeed);
    }

    [Fact]
    public void Load_MergesTitleOptionsOverDefaults()
    {
        var path = Write(
            """
            {
              "schemaVersion": 1,
              "name": "unit",
              "defaults": {
                "options": {
                  "profile": "accuracy",
                  "armTiming": "arm60",
                  "extraArgs": ["--prefetch"]
                }
              },
              "titles": [
                {
                  "id": "one",
                  "disc": "disc.chd",
                  "options": {
                    "armTiming": "freedo",
                    "extraArgs": ["--high-res"]
                  }
                }
              ]
            }
            """);

        DiffManifest manifest = DiffManifestLoader.Load(path);
        DiffMachineOptions merged = DiffManifestLoader.MergeOptions(
            manifest.Defaults!.Options,
            manifest.Titles![0].Options);

        Assert.Equal("accuracy", merged.Profile);
        Assert.Equal("freedo", merged.ArmTiming);
        Assert.Equal(MergedArguments, merged.ExtraArgs!);
    }

    [Fact]
    public void MergeOptions_ClioTimersOverrideWinsOverDefaultsAndTitle()
    {
        var defaults = new DiffMachineOptions
        {
            Profile = "accuracy",
            ClioTimers = "opera",
        };
        var title = new DiffMachineOptions
        {
            ArmTiming = "freedo",
            ClioTimers = "opera",
        };

        DiffMachineOptions merged = DiffManifestLoader.MergeOptions(defaults, title, "freedo");

        Assert.Equal("accuracy", merged.Profile);
        Assert.Equal("freedo", merged.ArmTiming);
        Assert.Equal("freedo", merged.ClioTimers);
    }

    [Fact]
    public void Load_RejectsDuplicateIds()
    {
        var path = Write(
            """
            {
              "schemaVersion": 1,
              "name": "unit",
              "titles": [
                { "id": "one", "disc": "a.chd" },
                { "id": "ONE", "disc": "b.chd" }
              ]
            }
            """);

        Assert.Throws<InvalidDataException>(() => DiffManifestLoader.Load(path));
    }

    [Fact]
    public void Load_RejectsCheckpointAfterFrameCount()
    {
        var path = Write(
            """
            {
              "schemaVersion": 1,
              "name": "unit",
              "defaults": { "frames": 100 },
              "titles": [
                { "id": "one", "disc": "a.chd", "checkpoints": [100, 101] }
              ]
            }
            """);

        Assert.Throws<InvalidDataException>(() => DiffManifestLoader.Load(path));
    }

    [Fact]
    public void Load_RejectsUnknownAllowField()
    {
        var path = Write(
            """
            {
              "schemaVersion": 1,
              "name": "unit",
              "titles": [
                {
                  "id": "one",
                  "disc": "a.chd",
                  "allow": [
                    { "field": "nonsense", "checkpoint": 1, "reason": "test" }
                  ]
                }
              ]
            }
            """);

        Assert.Throws<InvalidDataException>(() => DiffManifestLoader.Load(path));
    }

    [Fact]
    public void Load_RejectsAllowWithoutReason()
    {
        var path = Write(
            """
            {
              "schemaVersion": 1,
              "name": "unit",
              "titles": [
                {
                  "id": "one",
                  "disc": "a.chd",
                  "allow": [
                    { "field": "frameHash", "checkpoint": 1, "reason": "" }
                  ]
                }
              ]
            }
            """);

        Assert.Throws<InvalidDataException>(() => DiffManifestLoader.Load(path));
    }

    [Fact]
    public void ExpandCheckpoints_ClampsToOneAndMaxFrame()
    {
        Assert.Equal(
            ClampedCheckpoints,
            DiffManifestLoader.ExpandCheckpoints([2], window: 2, maxFrame: 3));
        Assert.Equal(
            WindowCheckpoints,
            DiffManifestLoader.ExpandCheckpoints([100], window: 2, maxFrame: 200));
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

    private string Write(string json)
    {
        var path = Path.Combine(_directory, "manifest-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
