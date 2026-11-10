using System;
using FiveDo.Core;
using FiveDo.Frontend.Avalonia.Audio;

namespace FiveDo.Tests.Frontend;

public sealed class FrontendAudioSoakTests
{
    [Fact]
    public void TryParse_ReturnsFalseWithoutTheSoakOption()
    {
        var parsed = FrontendAudioSoak.TryParse(
            ["game.chd", "--bios", "bios.bin"],
            out FrontendAudioSoakOptions? options,
            out var error);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_ReadsDurationDeviceBufferAndRegion()
    {
        var parsed = FrontendAudioSoak.TryParse(
        [
            "--audio-soak", "600",
            "--audio-soak-output", "artifacts/result.json",
            "--audio-device", "OpenAL Soft on Test",
            "--audio-buffer", "80",
            "--audio-sync", "-40",
            "--region", "pal1",
            "--allow-unsigned-discs",
        ],
            out FrontendAudioSoakOptions? options,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(600d, options.Seconds);
        Assert.Contains("result.json", options.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("OpenAL Soft on Test", options.Device);
        Assert.Equal(80, options.BufferMilliseconds);
        Assert.Equal(-40, options.SyncMilliseconds);
        Assert.Equal(VideoRegion.Pal1, options.Region);
        Assert.False(options.AutoRegion);
        Assert.True(options.AllowUnsignedDiscs);
    }

    [Fact]
    public void TryParse_DefaultRegionUsesBiosDetection()
    {
        var parsed = FrontendAudioSoak.TryParse(
            ["--audio-soak", "10"],
            out FrontendAudioSoakOptions? options,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.AutoRegion);
    }

    [Theory]
    [InlineData("--audio-soak", "0")]
    [InlineData("--audio-soak", "3601")]
    [InlineData("--audio-soak", "abc")]
    public void TryParse_RejectsInvalidDuration(string option, string value)
    {
        var parsed = FrontendAudioSoak.TryParse(
            [option, value],
            out FrontendAudioSoakOptions? options,
            out var error);

        Assert.True(parsed);
        Assert.Null(options);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("--audio-buffer", "19")]
    [InlineData("--audio-buffer", "501")]
    [InlineData("--audio-sync", "251")]
    [InlineData("--region", "unknown")]
    public void TryParse_RejectsInvalidAudioOptions(string option, string value)
    {
        var parsed = FrontendAudioSoak.TryParse(
            ["--audio-soak", "10", option, value],
            out FrontendAudioSoakOptions? options,
            out var error);

        Assert.True(parsed);
        Assert.Null(options);
        Assert.NotNull(error);
    }
}
