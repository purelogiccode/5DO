using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2", "1.2.0")]
    [InlineData("v1.2.3-beta.2", "1.2.3-beta.2")]
    [InlineData("v1.2.3+build.7", "1.2.3")]
    public void ReleaseVersion_ParsesTags(string text, string expected)
    {
        Assert.True(ReleaseVersion.TryParse(text, out ReleaseVersion version));
        Assert.Equal(expected, version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("v1")]
    [InlineData("v1.x.0")]
    [InlineData("v1.2.3-")]
    public void ReleaseVersion_RejectsInvalidText(string text)
    {
        Assert.False(ReleaseVersion.TryParse(text, out _));
    }

    [Fact]
    public void ReleaseVersion_OrdersStableAndPrereleaseVersions()
    {
        Assert.True(ReleaseVersion.TryParse("1.2.0", out var stable));
        Assert.True(ReleaseVersion.TryParse("1.2.0-beta.2", out var beta));
        Assert.True(ReleaseVersion.TryParse("1.2.0-beta.10", out var beta10));
        Assert.True(ReleaseVersion.TryParse("1.10.0", out var newerMinor));

        Assert.True(stable > beta);
        Assert.True(beta10 > beta);
        Assert.True(newerMinor > stable);
        Assert.Equal(0, stable.CompareTo(ReleaseVersion.TryParse("v1.2.0", out var parsed) ? parsed : default));
    }

    [Theory]
    [InlineData(true, false, false, Architecture.X64, "win-x64")]
    [InlineData(true, false, false, Architecture.Arm64, "win-arm64")]
    [InlineData(false, true, false, Architecture.X64, "linux-x64")]
    [InlineData(false, true, false, Architecture.Arm64, "linux-arm64")]
    [InlineData(false, false, true, Architecture.X64, "osx-x64")]
    [InlineData(false, false, true, Architecture.Arm64, "osx-arm64")]
    [InlineData(false, false, false, Architecture.X64, null)]
    [InlineData(true, false, false, Architecture.X86, null)]
    public void PlatformAssetToken_SelectsTheBundleForTheRunningPlatform(
        bool isWindows,
        bool isLinux,
        bool isMacOs,
        Architecture architecture,
        string? expected)
    {
        Assert.Equal(
            expected,
            UpdateChecker.GetPlatformAssetToken(isWindows, isLinux, isMacOs, architecture));
    }

    [Fact]
    public async Task CheckAsync_ReportsANewerReleaseAndSelectsThePlatformAsset()
    {
        using var directory = new TempDirectory();
        var cachePath = Path.Combine(directory.Path, "update-check.json");
        using var client = new HttpClient(new StaticHandler(ReleaseJson(
            "v1.2.0",
            "5DO 1.2.0",
            "https://github.com/purelogiccode/5DO/releases/tag/v1.2.0",
            "win-x64",
            "win-arm64",
            "linux-x64",
            "linux-arm64",
            "osx-x64",
            "osx-arm64")));

        UpdateCheckResult result = await UpdateChecker.CheckAsync(
            new UpdateCheckOptions
            {
                CachePath = cachePath,
                CacheDuration = TimeSpan.FromHours(1),
            },
            client,
            "1.1.0",
            DateTimeOffset.Parse("2026-11-02T12:00:00Z", CultureInfo.InvariantCulture),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.False(result.FromCache);
        Assert.NotNull(result.Info);
        Assert.True(result.Info.UpdateAvailable);
        Assert.Equal("1.2.0", result.Info.LatestVersion);
        Assert.Equal("5DO 1.2.0", result.Info.ReleaseName);
        var assetToken = UpdateChecker.PlatformAssetToken;
        Assert.NotNull(assetToken);
        Assert.Contains(assetToken, result.Info.AssetName);
        Assert.Contains($"5do-1.2.0-{assetToken}.zip", result.Info.DownloadUrl);
        Assert.True(File.Exists(cachePath));
    }

    [Fact]
    public async Task CheckAsync_UsesAFreshCacheWithoutMakingAnotherRequest()
    {
        using var directory = new TempDirectory();
        var cachePath = Path.Combine(directory.Path, "update-check.json");
        var now = DateTimeOffset.Parse("2026-11-02T12:00:00Z", CultureInfo.InvariantCulture);
        using (var client = new HttpClient(new StaticHandler(ReleaseJson(
                   "v1.2.0",
                   "5DO 1.2.0",
                   "https://github.com/purelogiccode/5DO/releases/tag/v1.2.0",
                   "linux-x64"))))
        {
            _ = await UpdateChecker.CheckAsync(
                new UpdateCheckOptions { CachePath = cachePath },
                client,
                "1.1.0",
                now,
                CancellationToken.None);
        }

        using var failingClient = new HttpClient(new FailingHandler());
        UpdateCheckResult result = await UpdateChecker.CheckAsync(
            new UpdateCheckOptions { CachePath = cachePath },
            failingClient,
            "1.1.0",
            now.AddMinutes(5),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.True(result.FromCache);
        Assert.NotNull(result.Info);
        Assert.Equal("1.2.0", result.Info.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_FallsBackToAStaleCacheWhenGitHubIsUnavailable()
    {
        using var directory = new TempDirectory();
        var cachePath = Path.Combine(directory.Path, "update-check.json");
        var now = DateTimeOffset.Parse("2026-11-02T12:00:00Z", CultureInfo.InvariantCulture);
        using (var client = new HttpClient(new StaticHandler(ReleaseJson(
                   "v1.2.0",
                   "5DO 1.2.0",
                   "https://github.com/purelogiccode/5DO/releases/tag/v1.2.0",
                   "win-x64"))))
        {
            _ = await UpdateChecker.CheckAsync(
                new UpdateCheckOptions { CachePath = cachePath },
                client,
                "1.1.0",
                now,
                CancellationToken.None);
        }

        using var failingClient = new HttpClient(new FailingHandler());
        UpdateCheckResult result = await UpdateChecker.CheckAsync(
            new UpdateCheckOptions
            {
                CachePath = cachePath,
                CacheDuration = TimeSpan.FromDays(1),
            },
            failingClient,
            "1.1.0",
            now.AddDays(2),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.True(result.FromCache);
        Assert.NotNull(result.Info);
        Assert.True(result.Info.UpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_ReturnsAnErrorInsteadOfThrowingForMalformedJson()
    {
        using var directory = new TempDirectory();
        using var client = new HttpClient(new StaticHandler("{\"tag_name\":\"not-a-version\"}"));

        UpdateCheckResult result = await UpdateChecker.CheckAsync(
            new UpdateCheckOptions { CachePath = Path.Combine(directory.Path, "cache.json") },
            client,
            "1.1.0",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task CheckAsync_ReportsNoUpdateForTheSameVersion()
    {
        using var directory = new TempDirectory();
        using var client = new HttpClient(new StaticHandler(ReleaseJson(
            "v1.1.0",
            "5DO 1.1.0",
            "https://github.com/purelogiccode/5DO/releases/tag/v1.1.0",
            "win-x64")));

        UpdateCheckResult result = await UpdateChecker.CheckAsync(
            new UpdateCheckOptions { CachePath = Path.Combine(directory.Path, "cache.json") },
            client,
            "1.1.0",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Info);
        Assert.False(result.Info.UpdateAvailable);
    }

    private static string ReleaseJson(string tag, string name, string url, params string[] assetTokens)
    {
        var version = tag.TrimStart('v');
        var assets = string.Join(
            ", ",
            assetTokens.Select(assetToken =>
                $$"""{ "name": "5do-{{version}}-{{assetToken}}.zip", "browser_download_url": "https://github.com/purelogiccode/5DO/releases/download/{{tag}}/5do-{{version}}-{{assetToken}}.zip" }"""));
        return $$"""
            {
              "tag_name": "{{tag}}",
              "name": "{{name}}",
              "html_url": "{{url}}",
              "published_at": "2026-11-02T10:00:00Z",
              "assets": [
                {{assets}}
              ]
            }
            """;
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly string _json;

        internal StaticHandler(string json)
        {
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("offline");
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "5do-update-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Best-effort test cleanup.
            }
        }
    }
}

