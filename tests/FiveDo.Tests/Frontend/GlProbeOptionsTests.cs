using System.IO;
using System.Text.Json;
using FiveDo.Frontend.Avalonia;
using FiveDo.Video;

namespace FiveDo.Tests.Frontend;

public sealed class GlProbeOptionsTests
{
    [Fact]
    public void TryParse_UsesThe4KDefaults()
    {
        Assert.True(GlProbeOptions.TryParse(["--gl-probe", "probe.json"], out GlProbeOptions? options, out var error));

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath("probe.json"), options.OutputPath);
        Assert.Equal(3840, options.Width);
        Assert.Equal(2160, options.Height);
        Assert.Equal(120, options.Frames);
        Assert.Equal(30, options.WarmupFrames);
        Assert.Equal(VideoFilter.SharpBilinear, options.Filter);
        Assert.False(options.Vsync);
        Assert.False(options.Offscreen);
        Assert.Null(options.CapturePath);
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Fact]
    public void TryParse_AcceptsSizeFramesFilterAndVsync()
    {
        var parsed = GlProbeOptions.TryParse(
            [
                "--gl-probe", "probe.json",
                "--gl-probe-size", "1920x1080",
                "--gl-probe-frames", "42",
                "--gl-probe-warmup", "7",
                "--gl-probe-filter", "sharp-bilinear",
                "--gl-probe-timeout", "5",
                "--gl-probe-vsync",
                "--gl-probe-offscreen",
            ],
            out GlProbeOptions? options,
            out var error);

        Assert.True(parsed, error);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(1920, options.Width);
        Assert.Equal(1080, options.Height);
        Assert.Equal(42, options.Frames);
        Assert.Equal(7, options.WarmupFrames);
        Assert.Equal(VideoFilter.SharpBilinear, options.Filter);
        Assert.True(options.Vsync);
        Assert.True(options.Offscreen);
        Assert.Equal(5, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("accurate", VideoFilter.Accurate)]
    [InlineData("nearest", VideoFilter.Nearest)]
    [InlineData("sharp", VideoFilter.SharpBilinear)]
    [InlineData("crt", VideoFilter.Crt)]
    [InlineData("xbr", VideoFilter.Xbr)]
    [InlineData("fsr", VideoFilter.Fsr)]
    [InlineData("fsr1", VideoFilter.Fsr)]
    [InlineData("fsr10", VideoFilter.Fsr)]
    [InlineData("hq2x", VideoFilter.Hqx)]
    [InlineData("hq3x", VideoFilter.Hqx3x)]
    [InlineData("hq4x", VideoFilter.Hqx4x)]
    public void TryParse_AcceptsFilterAliases(string text, VideoFilter expected)
    {
        Assert.True(GlProbeOptions.TryParse(
            ["--gl-probe", "probe.json", "--gl-probe-filter", text],
            out GlProbeOptions? options,
            out var error));

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(expected, options.Filter);
    }

    [Fact]
    public void TryParse_AcceptsACapturePath()
    {
        Assert.True(GlProbeOptions.TryParse(
            ["--gl-probe", "probe.json", "--gl-probe-capture", "frame.png"],
            out GlProbeOptions? options,
            out var error));

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath("frame.png"), options.CapturePath);
    }

    [Theory]
    [InlineData("--gl-probe-size", "3840")]
    [InlineData("--gl-probe-size", "3840x0")]
    [InlineData("--gl-probe-size", "3840x2160x1")]
    [InlineData("--gl-probe-frames", "0")]
    [InlineData("--gl-probe-frames", "many")]
    [InlineData("--gl-probe-warmup", "10001")]
    [InlineData("--gl-probe-timeout", "0")]
    [InlineData("--gl-probe-filter", "unknown")]
    public void TryParse_RejectsInvalidValues(string option, string value)
    {
        Assert.False(GlProbeOptions.TryParse(
            ["--gl-probe", "probe.json", option, value],
            out GlProbeOptions? options,
            out var error));

        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParse_RequiresAnOutputPath()
    {
        Assert.False(GlProbeOptions.TryParse(["--gl-probe"], out GlProbeOptions? options, out var error));
        Assert.Null(options);
        Assert.Contains("output JSON path", error);
    }

    [Fact]
    public void TryParse_RejectsAnInvalidOutputPath()
    {
        Assert.False(GlProbeOptions.TryParse(["--gl-probe", "\0"], out GlProbeOptions? options, out var error));
        Assert.Null(options);
        Assert.Contains("output path is invalid", error);
    }

    [Fact]
    public void ReportContext_SerializesTheCamelCaseContract()
    {
        var report = new GlProbeReport(
            Ok: true,
            Error: null,
            RequestedBackend: "Angle",
            Backend: "OpenGL",
            Vendor: "Test Vendor",
            Renderer: "Test Renderer",
            Api: "OpenGl",
            MajorVersion: 4,
            MinorVersion: 6,
            MaxTextureSize: 32768,
            SupportsIntegerTextures: true,
            SupportsVsync: true,
            VsyncRequested: false,
            VsyncActive: false,
            VsyncBackend: "WGL_EXT_swap_control",
            SourceWidth: 320,
            SourceHeight: 240,
            OutputWidth: 3840,
            OutputHeight: 2160,
            Filter: "SharpBilinear",
            Offscreen: true,
            CapturePath: null,
            Parity: null,
            Frames: 120,
            WarmupFrames: 30,
            ElapsedSeconds: 2.5,
            Fps: 48,
            AllocatedBytes: 0,
            AllocatedBytesPerFrame: 0,
            RendererAllocatedBytes: 0,
            RendererAllocatedBytesPerFrame: 0,
            RendererOnlyAllocatedBytes: 0,
            RendererOnlyAllocatedBytesPerFrame: 0,
            RendererMeasureAllocations: true,
            RendererPresentAllocatedBytes: 0,
            UploadAllocatedBytes: 0,
            PaletteAllocatedBytes: 0,
            ScaleAllocatedBytes: 0,
            Present: new GlProbeTiming(0.2, 0.19, 0.3, 0.4, 0.5),
            Gpu: null);

        var json = JsonSerializer.Serialize(report, GlProbeJsonContext.Default.GlProbeReport);

        Assert.Contains("\"ok\": true", json);
        Assert.Contains("\"requestedBackend\": \"Angle\"", json);
        Assert.Contains("\"outputWidth\": 3840", json);
        Assert.Contains("\"vsyncBackend\": \"WGL_EXT_swap_control\"", json);
        Assert.Contains("\"p99Ms\": 0.4", json);
        Assert.Contains("\"gpu\": null", json);
    }

    [Fact]
    public void ReportContext_SerializesCaptureAndParity()
    {
        var report = new GlProbeReport(
            Ok: true,
            Error: null,
            RequestedBackend: "Angle",
            Backend: "OpenGL ES",
            Vendor: "Test Vendor",
            Renderer: "Test Renderer",
            Api: "OpenGlEs",
            MajorVersion: 3,
            MinorVersion: 0,
            MaxTextureSize: 16384,
            SupportsIntegerTextures: true,
            SupportsVsync: true,
            VsyncRequested: false,
            VsyncActive: false,
            VsyncBackend: "EGL_KHR_swap_interval",
            SourceWidth: 320,
            SourceHeight: 240,
            OutputWidth: 320,
            OutputHeight: 240,
            Filter: "Accurate",
            Offscreen: true,
            CapturePath: "parity.png",
            Parity: new GlProbeParity(true, true, 0, 0xD6BD89C5, 0xD6BD89C5, false),
            Frames: 20,
            WarmupFrames: 5,
            ElapsedSeconds: 0.2,
            Fps: 100,
            AllocatedBytes: 0,
            AllocatedBytesPerFrame: 0,
            RendererAllocatedBytes: 0,
            RendererAllocatedBytesPerFrame: 0,
            RendererOnlyAllocatedBytes: 0,
            RendererOnlyAllocatedBytesPerFrame: 0,
            RendererMeasureAllocations: true,
            RendererPresentAllocatedBytes: 0,
            UploadAllocatedBytes: 0,
            PaletteAllocatedBytes: 0,
            ScaleAllocatedBytes: 0,
            Present: null,
            Gpu: null);

        var json = JsonSerializer.Serialize(report, GlProbeJsonContext.Default.GlProbeReport);

        Assert.Contains("\"capturePath\": \"parity.png\"", json);
        Assert.Contains("\"matches\": true", json);
        Assert.Contains("\"differentPixels\": 0", json);
        Assert.Contains("\"cpuHash\": 3602745797", json);
        Assert.Contains("\"verticalFlip\": false", json);
    }
}
