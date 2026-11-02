using System.IO;
using FiveDo.Frontend.Avalonia;
using FiveDo.Video;

namespace FiveDo.Tests.Frontend;

public sealed class D3dProbeOptionsTests
{
    [Fact]
    public void TryParse_UsesThe1080pDefaults()
    {
        Assert.True(D3dProbeOptions.TryParse(
            ["--d3d-probe", "probe.json"],
            out D3dProbeOptions? options,
            out var error));

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath("probe.json"), options.OutputPath);
        Assert.Equal(1920, options.Width);
        Assert.Equal(1080, options.Height);
        Assert.Equal(60, options.Frames);
        Assert.Equal(10, options.WarmupFrames);
        Assert.Equal(VideoFilter.Accurate, options.Filter);
        Assert.Null(options.CapturePath);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.False(options.Window);
    }

    [Fact]
    public void TryParseWindow_AcceptsWindowSpecificOptions()
    {
        Assert.True(D3dProbeOptions.TryParseWindow(
            [
                "--d3d-window-probe", "window.json",
                "--d3d-window-probe-size", "1280x720",
                "--d3d-window-probe-frames", "12",
                "--d3d-window-probe-warmup", "3",
                "--d3d-window-probe-filter", "crt",
                "--d3d-window-probe-capture", "window.png",
                "--d3d-window-probe-timeout", "9",
            ],
            out D3dProbeOptions? options,
            out var error));

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.Window);
        Assert.Equal(1280, options.Width);
        Assert.Equal(720, options.Height);
        Assert.Equal(12, options.Frames);
        Assert.Equal(3, options.WarmupFrames);
        Assert.Equal(VideoFilter.Crt, options.Filter);
        Assert.Equal(Path.GetFullPath("window.png"), options.CapturePath);
        Assert.Equal(9, options.TimeoutSeconds);
    }

    [Fact]
    public void TryParseWindow_AcceptsSharedProbeOptionNames()
    {
        Assert.True(D3dProbeOptions.TryParseWindow(
            [
                "--d3d-window-probe", "window.json",
                "--d3d-probe-size", "800x600",
                "--d3d-probe-frames", "4",
            ],
            out D3dProbeOptions? options,
            out var error));

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.Window);
        Assert.Equal(800, options.Width);
        Assert.Equal(600, options.Height);
        Assert.Equal(4, options.Frames);
    }

    [Fact]
    public void TryParse_AcceptsSizeFramesFilterAndCapture()
    {
        Assert.True(D3dProbeOptions.TryParse(
            [
                "--d3d-probe", "probe.json",
                "--d3d-probe-size", "3840x2160",
                "--d3d-probe-frames", "42",
                "--d3d-probe-warmup", "7",
                "--d3d-probe-filter", "sharp-bilinear",
                "--d3d-probe-capture", "frame.png",
                "--d3d-probe-timeout", "5",
            ],
            out D3dProbeOptions? options,
            out var error));

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(3840, options.Width);
        Assert.Equal(2160, options.Height);
        Assert.Equal(42, options.Frames);
        Assert.Equal(7, options.WarmupFrames);
        Assert.Equal(VideoFilter.SharpBilinear, options.Filter);
        Assert.Equal(Path.GetFullPath("frame.png"), options.CapturePath);
        Assert.Equal(5, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("--d3d-probe-size", "3840")]
    [InlineData("--d3d-probe-size", "3840x0")]
    [InlineData("--d3d-probe-frames", "0")]
    [InlineData("--d3d-probe-warmup", "10001")]
    [InlineData("--d3d-probe-timeout", "0")]
    [InlineData("--d3d-probe-filter", "unknown")]
    public void TryParse_RejectsInvalidValues(string option, string value)
    {
        Assert.False(D3dProbeOptions.TryParse(
            ["--d3d-probe", "probe.json", option, value],
            out D3dProbeOptions? options,
            out var error));

        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParse_RequiresAnOutputPath()
    {
        Assert.False(D3dProbeOptions.TryParse(["--d3d-probe"], out D3dProbeOptions? options, out var error));
        Assert.Null(options);
        Assert.Contains("output JSON path", error);
    }
}

