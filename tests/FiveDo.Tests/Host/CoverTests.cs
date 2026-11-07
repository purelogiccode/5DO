using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>Tests for the online cover catalog, matcher, cache, and download service.</summary>
public sealed class CoverTests
{
    [Fact]
    public void StripAnnotations_RemovesBalancedGroups()
    {
        Assert.Equal("Game", CoverMatcher.StripAnnotations("Game (USA) [v1] {test}"));
        Assert.Equal("Game", CoverMatcher.StripAnnotations("Game (Region (Sub))"));
        Assert.Equal("Game", CoverMatcher.StripAnnotations("Game (USA) "));
        Assert.Equal("(test)", CoverMatcher.StripAnnotations("(test)"));
    }

    [Fact]
    public void Normalize_RemovesArticlesPunctuationAndAnnotations()
    {
        Assert.Equal("horde", CoverMatcher.Normalize("The Horde"));
        Assert.Equal("horde", CoverMatcher.Normalize("Horde, The"));
        Assert.Equal("blade force", CoverMatcher.Normalize("Blade Force (Europe) [!]"));
        Assert.Equal("d and d", CoverMatcher.Normalize("D & D"));
    }

    [Fact]
    public void JaroWinkler_MatchesSimpleLauncherReferenceCases()
    {
        Assert.Equal(1.0, CoverMatcher.CalculateJaroWinklerSimilarity("hello", "hello"), 10);
        Assert.Equal(1.0, CoverMatcher.CalculateJaroWinklerSimilarity("", ""), 10);
        Assert.Equal(0.0, CoverMatcher.CalculateJaroWinklerSimilarity("hello", ""), 10);
        Assert.True(CoverMatcher.CalculateJaroWinklerSimilarity("mario", "marion") > 0.9);
        Assert.True(CoverMatcher.CalculateJaroWinklerSimilarity("martha", "marhta") > 0.9);
    }

    [Fact]
    public void Score_HandlesTokenNoSpaceAndSubtitleAliases()
    {
        Assert.True(
            CoverMatcher.Score(
                "Diving Spot Guide - Marine Tour",
                "Marine Tour - Diving Spot Guide") >= 0.99);
        Assert.True(CoverMatcher.Score("Blade Force", "Bladeforce") >= 0.98);
        Assert.True(
            CoverMatcher.Score(
                "True Golf Classics - Pebble Beach Golf Links",
                "Pebble Beach Golf Links") >= 0.99);
        Assert.True(
            CoverMatcher.Score(
                "Hirata Shougo Interactive Ehon - Cinderella (Japan)",
                "Hirata Shiyougo Interactive Ehon - Cinderella") >= 0.90);
    }

    [Fact]
    public void Score_RejectsKnownFalsePositives()
    {
        Assert.True(CoverMatcher.Score("Footie", "NFL Madden Football") < 0.85);
        Assert.True(CoverMatcher.Score("We Got It", "Winning Post") < 0.85);
        Assert.True(CoverMatcher.Score("T&E VR Golf - Devil's Course", "Sword and Sorcery") < 0.85);
    }

    [Fact]
    public void Score_RejectsSequelNumberMismatch()
    {
        Assert.True(
            CoverMatcher.Score(
                "Chiki Chiki Machine Mou-Race",
                "Chiki Chiki Machine Mou Race 2 - In Space") < 0.85);
        Assert.True(CoverMatcher.Score("Battle Chess", "Battle Chess II") < 0.85);
        Assert.True(CoverMatcher.Score("Street Fighter II", "Street Fighter II Turbo") >= 0.85);
        Assert.True(
            CoverMatcher.Score(
                "Ballz - The Director's Cut Beta 1.0a",
                "Ballz - The Director's Cut") >= 0.90);
        Assert.True(
            CoverMatcher.Score(
                "Sento Monogatari - Sono 1",
                "Sento Monogatari - Sono I") >= 0.90);
    }

    [Fact]
    public void Catalog_LoadsAndLooksUpGameIdAndTitle()
    {
        byte[] image = [1, 2, 3, 4, 5];
        var catalog = CreateCatalog(image, "Battle Chess", "04116B14", "Battle Chess (Europe)", "Battle Chess (USA)");
        string json = JsonSerializer.Serialize(catalog, CoverCatalogJsonContext.Default.CoverCatalog);
        CoverCatalogLoadResult loaded = CoverCatalogLoader.LoadJson(json);
        Assert.True(loaded.Success, loaded.Error);
        CoverCatalogIndex index = loaded.Index!;
        Assert.NotNull(index.FindByGameId("04116B14"));
        CoverCatalogMatch? match = index.FindByTitle(
            "Battle Chess",
            "Battle Chess (USA)",
            threshold: 0.85,
            fuzzy: true,
            gameRegions: "US",
            gameYear: "1993");
        Assert.NotNull(match);
        Assert.Equal("Battle Chess", match.Entry.Name);
    }

    [Fact]
    public void Catalog_RejectsUnsafeFileNames()
    {
        var catalog = CreateCatalog([9, 9], "Unsafe", "04116B14", "Unsafe");
        catalog.Covers[0].File = "../evil.webp";
        string json = JsonSerializer.Serialize(catalog, CoverCatalogJsonContext.Default.CoverCatalog);
        CoverCatalogLoadResult loaded = CoverCatalogLoader.LoadJson(json);
        Assert.False(loaded.Success);
        Assert.Contains("unsafe", loaded.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Store_VerifiesHashAndRejectsMismatch()
    {
        using var temp = new TempDirectory();
        var store = new CoverStore(temp.Path);
        byte[] image = [10, 20, 30];
        var id = Hash(image);
        var path = await store.SaveImageAsync(id, image, CancellationToken.None);
        Assert.True(File.Exists(path));
        Assert.True(store.HasImage(id));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.SaveImageAsync(Hash([1]), image, CancellationToken.None));

        var catalog = CreateCatalog(image, "Cover", "04116B14", "Cover");
        CoverVerifyResult verify = await store.VerifyAsync(catalog, fix: false);
        Assert.Equal(1, verify.Ok);
        Assert.Equal(0, verify.Missing);
        Assert.Equal(0, verify.Corrupt);
    }

    [Fact]
    public async Task Service_DownloadsLocalCatalogImageAndResolvesByGameId()
    {
        using var temp = new TempDirectory();
        byte[] image = [42, 43, 44, 45];
        var id = Hash(image);
        var catalog = CreateCatalog(image, "Battle Chess", "04116B14", "Battle Chess (Europe)");
        string json = JsonSerializer.Serialize(catalog, CoverCatalogJsonContext.Default.CoverCatalog);
        var catalogPath = Path.Combine(temp.Path, "catalog.json");
        await File.WriteAllTextAsync(catalogPath, json, CancellationToken.None);
        var images = Path.Combine(temp.Path, "images");
        Directory.CreateDirectory(images);
        await File.WriteAllBytesAsync(Path.Combine(images, id + ".webp"), image, CancellationToken.None);

        var cache = Path.Combine(temp.Path, "cache");
        var options = new CoverServiceOptions
        {
            CatalogUri = new Uri(catalogPath),
            UseOnlineCovers = true,
            CacheDirectory = cache,
        };
        using var service = new CoverService(options);
        CoverCatalogRefreshResult refresh = await service.RefreshCatalogAsync(force: true);
        Assert.True(refresh.Success, refresh.Error);
        Assert.Equal(1, refresh.CoverCount);

        var entry = new GameLibraryEntry(
            "C:\\games\\Battle Chess.chd",
            "CHD",
            100,
            "04116B14ABCDEF0123456789ABCDEF01",
            "Battle Chess",
            true,
            string.Empty);
        IReadOnlyList<CoverResolution> resolutions = await service.ResolveAllAsync([entry]);
        Assert.Single(resolutions);
        Assert.Equal("catalog-game-id", resolutions[0].CoverSource);
        Assert.True(resolutions[0].NeedsDownload);
        Assert.EndsWith(id + ".webp", resolutions[0].CoverPath, StringComparison.OrdinalIgnoreCase);

        CoverDownloadResult download = await service.DownloadMissingAsync(resolutions);
        Assert.Equal(1, download.Downloaded);
        Assert.Equal(0, download.Failed);
        Assert.True(File.Exists(service.Store.GetImagePath(id)));
    }

    [Fact]
    public async Task Service_ResolvesCatalogTitleWhenGameIdIsUnknown()
    {
        using var temp = new TempDirectory();
        byte[] image = [61, 62, 63];
        var id = Hash(image);
        var catalog = CreateCatalog(image, "Battle Chess", "04116B14", "Battle Chess (Europe)");
        string json = JsonSerializer.Serialize(catalog, CoverCatalogJsonContext.Default.CoverCatalog);
        var catalogPath = Path.Combine(temp.Path, "catalog.json");
        await File.WriteAllTextAsync(catalogPath, json, CancellationToken.None);
        var images = Path.Combine(temp.Path, "images");
        Directory.CreateDirectory(images);
        await File.WriteAllBytesAsync(Path.Combine(images, id + ".webp"), image, CancellationToken.None);

        var options = new CoverServiceOptions
        {
            CatalogUri = new Uri(catalogPath),
            UseOnlineCovers = true,
            CacheDirectory = Path.Combine(temp.Path, "cache"),
        };
        using var service = new CoverService(options);
        await service.RefreshCatalogAsync(force: true);
        var entry = new GameLibraryEntry(
            "C:\\games\\Battle Chess (USA).chd",
            "CHD",
            100,
            string.Empty,
            "Battle Chess",
            false,
            string.Empty);
        IReadOnlyList<CoverResolution> resolutions = await service.ResolveAllAsync([entry]);
        Assert.Single(resolutions);
        Assert.Equal("catalog-exact", resolutions[0].CoverSource);
        CoverDownloadResult download = await service.DownloadMissingAsync(resolutions);
        Assert.Equal(1, download.Downloaded);
        Assert.True(File.Exists(service.Store.GetImagePath(id)));
    }

    [Fact]
    public async Task Service_UsesLocalCoverDirectoryWithoutNetwork()
    {
        using var temp = new TempDirectory();
        var covers = Path.Combine(temp.Path, "covers");
        Directory.CreateDirectory(covers);
        var coverPath = Path.Combine(covers, "BattleChess.png");
        await File.WriteAllBytesAsync(coverPath, [1, 2, 3], CancellationToken.None);

        var options = new CoverServiceOptions
        {
            UseOnlineCovers = false,
            CoverDirectory = covers,
            CacheDirectory = Path.Combine(temp.Path, "cache"),
        };
        using var service = new CoverService(options);
        var entry = new GameLibraryEntry(
            "C:\\games\\Battle Chess.chd",
            "CHD",
            100,
            string.Empty,
            "Battle Chess",
            false,
            string.Empty);
        IReadOnlyList<CoverResolution> resolutions = await service.ResolveAllAsync([entry]);
        Assert.Single(resolutions);
        Assert.Equal("local-exact", resolutions[0].CoverSource);
        Assert.Equal(coverPath, resolutions[0].CoverPath);
    }

    [Fact]
    public async Task Service_HandlesRemoteCatalogAndImage()
    {
        using var temp = new TempDirectory();
        byte[] image = [55, 56, 57];
        var id = Hash(image);
        var catalog = CreateCatalog(image, "Battle Chess", "04116B14", "Battle Chess");
        string catalogJson = JsonSerializer.Serialize(catalog, CoverCatalogJsonContext.Default.CoverCatalog);
        using var handler = new FakeHttpHandler(catalogJson, image);
        using var http = new HttpClient(handler);
        var options = new CoverServiceOptions
        {
            CatalogUri = new Uri("https://example.test/v1/catalog.json"),
            UseOnlineCovers = true,
            CacheDirectory = Path.Combine(temp.Path, "cache"),
        };
        using var service = new CoverService(options, http);
        CoverCatalogRefreshResult refresh = await service.RefreshCatalogAsync(force: true);
        Assert.True(refresh.Success, refresh.Error);
        Assert.Contains(handler.Requests, request => request.Contains("catalog", StringComparison.Ordinal));

        var entry = new GameLibraryEntry(
            "C:\\games\\Battle Chess.chd",
            "CHD",
            100,
            "04116B14ABCDEF0123456789ABCDEF01",
            "Battle Chess",
            true,
            string.Empty);
        IReadOnlyList<CoverResolution> resolutions = await service.ResolveAllAsync([entry]);
        CoverDownloadResult download = await service.DownloadMissingAsync(resolutions);
        Assert.Equal(1, download.Downloaded);
        Assert.True(File.Exists(service.Store.GetImagePath(id)));
        Assert.Contains(handler.Requests, request => request.Contains("images/", StringComparison.Ordinal));
    }

    [Fact]
    public void ServiceOptions_FromSettingsNormalizesThresholdAndUrl()
    {
        var settings = new AppSettings
        {
            CoverCatalogUrl = "https://example.test/base/",
            CoverFuzzyThreshold = 5.0,
        };
        CoverServiceOptions options = CoverServiceOptions.FromSettings(settings);
        Assert.Equal("https://example.test/base/catalog.json", options.CatalogUri.AbsoluteUri);
        Assert.Equal(CoverDefaults.MaxFuzzyThreshold, options.FuzzyThreshold);
        Assert.Equal(
            "https://assets.purelogiccode.com/5do/v1/catalog.json",
            CoverDefaults.NormalizeCatalogUrl(
                "https://assets.purelogiccode.com/simplelauncher-thumbnails/5do/v1"));
    }

    private static CoverCatalog CreateCatalog(
        byte[] image,
        string name,
        string gameId,
        params string[] aliases)
    {
        var id = Hash(image);
        return new CoverCatalog
        {
            PackVersion = "test",
            Covers =
            [
                new CoverCatalogEntry
                {
                    Id = id,
                    File = id + ".webp",
                    Name = name,
                    Aliases = [.. aliases],
                    GameIds = [gameId],
                    Width = 600,
                    Height = 600,
                    Size = image.Length,
                    Regions = ["US"],
                    Year = "1993",
                    Rank = 0,
                },
            ],
        };
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _catalogJson;
        private readonly byte[] _image;

        internal FakeHttpHandler(string catalogJson, byte[] image)
        {
            _catalogJson = catalogJson;
            _image = image;
        }

        internal List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            Requests.Add(url);
            if (url.EndsWith("catalog.json", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_catalogJson, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_image),
            });
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "fivedo-cover-tests-" + Guid.NewGuid().ToString("N"));
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
            }
        }
    }
}
