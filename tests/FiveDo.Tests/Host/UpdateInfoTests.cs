using System;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>
/// Covers the update notice text and the URL validation used before a browser is opened.
/// </summary>
public sealed class UpdateInfoTests
{
    [Fact]
    public void Summary_WhenAnUpdateIsAvailable_NamesBothVersions()
    {
        var info = new UpdateInfo(
            UpdateAvailable: true,
            CurrentVersion: "1.0.0",
            LatestVersion: "2.0.0",
            ReleaseName: "Release 2.0.0",
            ReleasePageUrl: "https://example.test/releases/tag/v2.0.0",
            DownloadUrl: "https://example.test/download",
            PublishedAtUtc: DateTimeOffset.UnixEpoch,
            AssetName: "5do-win-x64.zip");

        Assert.Contains(FiveDoVersion.ProductName, info.Summary, StringComparison.Ordinal);
        Assert.Contains("2.0.0", info.Summary, StringComparison.Ordinal);
        Assert.Contains("1.0.0", info.Summary, StringComparison.Ordinal);
        Assert.Contains("available", info.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_WhenUpToDate_SaysSo()
    {
        var info = new UpdateInfo(
            UpdateAvailable: false,
            CurrentVersion: "1.0.0",
            LatestVersion: "1.0.0",
            ReleaseName: "Release 1.0.0",
            ReleasePageUrl: "https://example.test/releases/tag/v1.0.0",
            DownloadUrl: "https://example.test/download",
            PublishedAtUtc: null,
            AssetName: null);

        Assert.Contains("up to date", info.Summary, StringComparison.Ordinal);
        Assert.Contains("1.0.0", info.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://example.test/release")]
    [InlineData("file:///c:/release.zip")]
    [InlineData("/relative/path")]
    public void TryOpen_RejectsAnythingThatIsNotAnAbsoluteHttpUrl(string? url)
    {
        Assert.False(UpdateLauncher.TryOpen(url));
    }

    [Fact]
    public void UpdateCheckResult_WithoutInfo_ReportsTheError()
    {
        var result = new UpdateCheckResult(null, "github unreachable", FromCache: false);

        Assert.Null(result.Info);
        Assert.Equal("github unreachable", result.Error);
        Assert.False(result.Ok);
        Assert.False(result.FromCache);
    }

    [Fact]
    public void UpdateCheckResult_WithInfo_IsOk()
    {
        var info = new UpdateInfo(
            UpdateAvailable: false,
            CurrentVersion: "1.0.0",
            LatestVersion: "1.0.0",
            ReleaseName: "Release 1.0.0",
            ReleasePageUrl: "https://example.test/releases/tag/v1.0.0",
            DownloadUrl: "https://example.test/download",
            PublishedAtUtc: null,
            AssetName: null);

        var result = new UpdateCheckResult(info, null, FromCache: true);

        Assert.Same(info, result.Info);
        Assert.True(result.Ok);
        Assert.True(result.FromCache);
        Assert.Null(result.Error);
    }
}
