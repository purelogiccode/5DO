using System;
using System.IO;
using FiveDo.DiffHarness;

namespace FiveDo.Tests.DiffHarness;

public sealed class OperaCoreSmokeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "fivedo-opera-smoke-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void OperaCore_ProducesACheckpoint_WhenLocalAssetsExist()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("FIVEDO_OPERA_TESTS"),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        var repoRoot = DiffManifestLoader.FindRepositoryRoot();
        var core = Path.Combine(
            repoRoot,
            "References",
            "CPP_SimpleGui",
            "cmake-build-release",
            "SimpleGui",
            "Core",
            "OperaCore.dll");
        var bios = Path.Combine(repoRoot, "bios", "panafz1.bin");
        var iso = Path.Combine(
            repoRoot,
            "artifacts",
            "diff",
            "work",
            "discs",
            "lemmings-usa-smoke",
            "lemmings-usa-smoke.iso");
        if (!File.Exists(core) || !File.Exists(bios) || !File.Exists(iso))
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        var manifest = new DiffManifest
        {
            Name = "opera smoke",
            Defaults = new DiffDefaults
            {
                Bios = bios,
                Frames = 60,
                Checkpoints = [60],
                TimeoutSeconds = 60,
                OperaCore = core,
                WorkDirectory = Path.Combine(_directory, "work"),
                Compare = new DiffCompareOptions { CompareSamples = false },
            },
            Titles =
            [
                new DiffTitle
                {
                    Id = "lemmings",
                    Disc = iso,
                    Iso = iso,
                },
            ],
        };
        var harness = new HarnessOptions
        {
            OutputDirectory = _directory,
            TimeoutSeconds = 60,
        };

        CaptureResult result = OperaCoreCaptureRunner.Capture(
            harness,
            manifest,
            manifest.Titles![0],
            repoRoot,
            repoRoot,
            _directory);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(60, result.FramesRun);
        CaptureSample checkpoint = Assert.Single(result.Checkpoints);
        Assert.Equal(60, checkpoint.Frame);
        Assert.NotEqual(0u, checkpoint.FrameHash);
    }

    [Fact]
    public void DebugCore_ExposesPcAndCdState_WhenAvailable()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("FIVEDO_OPERA_TESTS"),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        var repoRoot = DiffManifestLoader.FindRepositoryRoot();
        var core = Path.Combine(repoRoot, "artifacts", "diff", "opera-build", "out", "OperaCore.dll");
        var bios = Path.Combine(repoRoot, "bios", "panafz1.bin");
        var iso = Path.Combine(
            repoRoot,
            "artifacts",
            "diff",
            "work",
            "discs",
            "lemmings-usa-smoke",
            "lemmings-usa-smoke.iso");
        if (!File.Exists(core) || !File.Exists(bios) || !File.Exists(iso))
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        var manifest = new DiffManifest
        {
            Name = "debug core smoke",
            Defaults = new DiffDefaults
            {
                Bios = bios,
                Frames = 60,
                Checkpoints = [60],
                TimeoutSeconds = 60,
                OperaCore = core,
                WorkDirectory = Path.Combine(_directory, "work"),
                Compare = new DiffCompareOptions { CompareSamples = false },
            },
            Titles =
            [
                new DiffTitle { Id = "lemmings", Disc = iso, Iso = iso },
            ],
        };
        var harness = new HarnessOptions { OutputDirectory = _directory, TimeoutSeconds = 60 };

        CaptureResult result = OperaCoreCaptureRunner.Capture(
            harness,
            manifest,
            manifest.Titles![0],
            repoRoot,
            repoRoot,
            _directory);

        Assert.True(result.Ok, result.Error);
        CaptureSample checkpoint = Assert.Single(result.Checkpoints);
        Assert.NotNull(checkpoint.Pc);
        Assert.NotNull(checkpoint.ArmStateHash);
        Assert.NotNull(checkpoint.CdPoll);
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
