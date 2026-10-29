using System;
using System.IO;
using FiveDo.Frontend.Avalonia;

namespace FiveDo.Tests.Frontend;

public class FrontendStartupTests
{
    [Fact]
    public void HasFlag_IsCaseInsensitiveAndExact()
    {
        Assert.True(Program.HasFlag(["--SMOKE-TEST"], "--smoke-test"));
        Assert.True(Program.HasFlag(["--version"], "--version"));
        Assert.False(Program.HasFlag(["--smoke-test=1"], "--smoke-test"));
        Assert.False(Program.HasFlag([], "--smoke-test"));
    }

    [Fact]
    public void IsAutomatedRun_DetectsSmokeAndProbeModes()
    {
        Assert.True(Program.IsAutomatedRun(["--smoke-test"]));
        Assert.True(Program.IsAutomatedRun(["--gl-probe", "out.json"]));
        Assert.True(Program.IsAutomatedRun(["--vk-probe", "out.json"]));
        Assert.True(Program.IsAutomatedRun(["--vk-window-probe", "out.json"]));
        Assert.False(Program.IsAutomatedRun([]));
        Assert.False(Program.IsAutomatedRun(["--disc", "game.chd"]));
    }

    [Fact]
    public void FindOptionValue_ReturnsTheFollowingArgument()
    {
        Assert.Equal("out.txt", Program.FindOptionValue(["--smoke-output", "out.txt"], "--smoke-output"));
        Assert.Equal("a.txt", Program.FindOptionValue(["--other", "x", "--SMOKE-OUTPUT", "a.txt"], "--smoke-output"));
        Assert.Null(Program.FindOptionValue(["--smoke-output"], "--smoke-output"));
        Assert.Null(Program.FindOptionValue(["--other", "x"], "--smoke-output"));
    }

    [Fact]
    public void FindDiscPath_IgnoresTheSmokeOutputValue()
    {
        using var directory = new TempDirectory();
        var marker = Path.Combine(directory.Path, "smoke.txt");
        File.WriteAllText(marker, "ok");

        Assert.Null(Program.FindDiscPath(["--smoke-output", marker]));
        Assert.Null(Program.FindDiscPath(["--smoke-test", "--smoke-output", marker]));
    }

    [Fact]
    public void FindDiscPath_IgnoresGlProbeOptionValues()
    {
        using var directory = new TempDirectory();
        var report = Path.Combine(directory.Path, "probe.json");
        File.WriteAllText(report, "{}");

        Assert.Null(Program.FindDiscPath(["--gl-probe", report]));
        Assert.Null(Program.FindDiscPath(["--gl-probe", report, "--gl-probe-size", "3840x2160"]));
        Assert.Null(Program.FindDiscPath(["--gl-probe", report, "--gl-probe-frames", "120"]));
        Assert.Null(Program.FindDiscPath(["--gl-probe", report, "--gl-probe-warmup", "30"]));
        Assert.Null(Program.FindDiscPath(["--gl-probe", report, "--gl-probe-filter", "sharp-bilinear"]));
        Assert.Null(Program.FindDiscPath(["--gl-probe", report, "--gl-probe-timeout", "5"]));
        Assert.Null(Program.FindDiscPath(["--gl-probe", report, "--gl-probe-capture", "frame.png"]));
        Assert.Null(Program.FindDiscPath(["--gl-backend", "angle"]));
        Assert.Null(Program.FindDiscPath(["--gl-probe", report, "--gl-probe-backend", "wgl"]));
    }

    [Fact]
    public void FindDiscPath_ReturnsTheDiscOptionAndPositionalPaths()
    {
        using var directory = new TempDirectory();
        var disc = Path.Combine(directory.Path, "game.iso");
        File.WriteAllText(disc, "disc");
        var expected = Path.GetFullPath(disc);

        Assert.Equal(expected, Program.FindDiscPath(["--disc", disc]));
        Assert.Equal(expected, Program.FindDiscPath(["--bios", "missing.bin", disc]));
        Assert.Equal(expected, Program.FindDiscPath([disc, "--frames", "60"]));
    }

    [Fact]
    public void FindDiscPath_ReturnsNullForMissingOrOptionOnlyArguments()
    {
        Assert.Null(Program.FindDiscPath([]));
        Assert.Null(Program.FindDiscPath(["--version"]));
        Assert.Null(Program.FindDiscPath(
            ["--disc", Path.Combine(Path.GetTempPath(), "missing-fivedo-disc.iso")]));
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fivedo-frontend-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
