using System.IO;
using FiveDo.Frontend.Avalonia;
using FiveDo.Video;

namespace FiveDo.Tests.Frontend;

public sealed class VkProbeOptionsTests
{
    [Fact]
    public void TryParse_UsesThe1080pDefaults()
    {
        Assert.True(VkProbeOptions.TryParse(
            ["--vk-probe", "probe.json"],
            out VkProbeOptions? options,
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
        Assert.True(VkProbeOptions.TryParseWindow(
            [
                "--vk-window-probe", "window.json",
                "--vk-window-probe-size", "1280x720",
                "--vk-window-probe-frames", "12",
                "--vk-window-probe-warmup", "3",
                "--vk-window-probe-filter", "crt",
                "--vk-window-probe-capture", "window.png",
                "--vk-window-probe-timeout", "9",
            ],
            out VkProbeOptions? options,
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
        Assert.True(VkProbeOptions.TryParseWindow(
            [
                "--vk-window-probe", "window.json",
                "--vk-probe-size", "800x600",
                "--vk-probe-frames", "4",
            ],
            out VkProbeOptions? options,
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
        Assert.True(VkProbeOptions.TryParse(
            [
                "--vk-probe", "probe.json",
                "--vk-probe-size", "3840x2160",
                "--vk-probe-frames", "42",
                "--vk-probe-warmup", "7",
                "--vk-probe-filter", "sharp-bilinear",
                "--vk-probe-capture", "frame.png",
                "--vk-probe-timeout", "5",
            ],
            out VkProbeOptions? options,
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
    [InlineData("--vk-probe-size", "3840")]
    [InlineData("--vk-probe-size", "3840x0")]
    [InlineData("--vk-probe-frames", "0")]
    [InlineData("--vk-probe-warmup", "10001")]
    [InlineData("--vk-probe-timeout", "0")]
    [InlineData("--vk-probe-filter", "unknown")]
    public void TryParse_RejectsInvalidValues(string option, string value)
    {
        Assert.False(VkProbeOptions.TryParse(
            ["--vk-probe", "probe.json", option, value],
            out VkProbeOptions? options,
            out var error));

        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParse_RequiresAnOutputPath()
    {
        Assert.False(VkProbeOptions.TryParse(["--vk-probe"], out VkProbeOptions? options, out var error));
        Assert.Null(options);
        Assert.Contains("output JSON path", error);
    }
}
