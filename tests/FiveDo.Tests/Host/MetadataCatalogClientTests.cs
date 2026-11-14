using System;
using System.IO;
using System.Threading.Tasks;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class MetadataCatalogClientTests
{
    private const string CatalogJson = """
        {
          "Version": 1,
          "Records": [
            {
              "GameId": "TEST0001",
              "Title": "Test Game",
              "Developer": "Test Studio",
              "Publisher": "Test Publisher",
              "ReleaseYear": "1995",
              "Genre": "Action",
              "Regions": "US",
              "Players": 2,
              "Description": "External metadata test record."
            },
            {
              "GameId": "TEST0002",
              "Title": "Another Game",
              "Developer": "Other Studio"
            }
          ]
        }
        """;

    [Fact]
    public async Task Lookup_DownloadsFromAFileCatalogAndWritesTheCache()
    {
        using var temp = new TempDirectory();
        var catalog = Path.Combine(temp.Path, "catalog.json");
        var cache = Path.Combine(temp.Path, "cache.json");
        File.WriteAllText(catalog, CatalogJson);

        using var client = new MetadataCatalogClient();
        MetadataLookupResult result = await client.LookupAsync(
            "Test Game",
            null,
            new Uri(catalog).AbsoluteUri,
            cache);

        Assert.NotNull(result.Record);
        Assert.Equal("TEST0001", result.Record.GameId);
        Assert.Equal("Test Studio", result.Record.Developer);
        Assert.False(result.FromCache);
        Assert.Null(result.Warning);
        Assert.True(File.Exists(cache));
    }

    [Fact]
    public async Task Lookup_FallsBackToTheCacheWhenTheCatalogDisappears()
    {
        using var temp = new TempDirectory();
        var catalog = Path.Combine(temp.Path, "catalog.json");
        var cache = Path.Combine(temp.Path, "cache.json");
        File.WriteAllText(catalog, CatalogJson);

        using var client = new MetadataCatalogClient();
        _ = await client.LookupAsync("Test Game", null, new Uri(catalog).AbsoluteUri, cache);
        File.Delete(catalog);

        MetadataLookupResult fallback = await client.LookupAsync(
            "Test Game",
            null,
            new Uri(catalog).AbsoluteUri,
            cache);

        Assert.NotNull(fallback.Record);
        Assert.Equal("TEST0001", fallback.Record.GameId);
        Assert.True(fallback.FromCache);
        Assert.NotNull(fallback.Warning);
    }

    [Fact]
    public async Task Lookup_PrefersTheExactGameIdOverTitleMatches()
    {
        using var temp = new TempDirectory();
        var catalog = Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(catalog, CatalogJson);

        using var client = new MetadataCatalogClient();
        MetadataLookupResult result = await client.LookupAsync(
            "Another Game",
            "TEST0001",
            new Uri(catalog).AbsoluteUri,
            Path.Combine(temp.Path, "cache.json"));

        Assert.NotNull(result.Record);
        Assert.Equal("TEST0001", result.Record.GameId);
        Assert.Equal(2, result.Matches.Count);
    }

    [Fact]
    public async Task Lookup_RejectsAnUnsupportedCatalogVersionAndFallsBackCleanly()
    {
        using var temp = new TempDirectory();
        var catalog = Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(catalog, """{"Version":99,"Records":[{"GameId":"TEST0001","Title":"Test Game"}]}""");

        using var client = new MetadataCatalogClient();
        MetadataLookupResult result = await client.LookupAsync(
            "Test Game",
            null,
            new Uri(catalog).AbsoluteUri,
            Path.Combine(temp.Path, "cache.json"));

        Assert.Null(result.Record);
        Assert.NotNull(result.Warning);
        Assert.False(result.FromCache);
    }

    [Fact]
    public void AttachAndDetachMedia_UpdateTheOfflineRecord()
    {
        using var temp = new TempDirectory();
        var metadata = Path.Combine(temp.Path, "library-metadata.json");
        var manual = Path.Combine(temp.Path, "manual.pdf");
        File.WriteAllText(manual, "manual");

        LibraryMetadataRecord record = new()
        {
            GameId = "TEST0001",
            Title = "Test Game",
            Media = [],
        };
        LibraryMetadataStore.Upsert(record, metadata);

        LibraryMetadataRecord? attached = MetadataCatalogClient.AttachMedia(
            "TEST0001",
            null,
            [manual],
            metadata);
        Assert.NotNull(attached);
        Assert.Contains(
            Path.GetFullPath(manual),
            attached.Media,
            StringComparer.OrdinalIgnoreCase);

        // Attaching the same path twice is idempotent.
        _ = MetadataCatalogClient.AttachMedia("TEST0001", null, [manual], metadata);
        LibraryMetadata loaded = LibraryMetadataStore.Load(metadata);
        Assert.Single(loaded.Records[0].Media);

        LibraryMetadataRecord? detached = MetadataCatalogClient.DetachMedia(
            "TEST0001",
            null,
            [manual],
            metadata);
        Assert.NotNull(detached);
        Assert.Empty(detached.Media);
    }

    [Fact]
    public void AttachMedia_ReturnsNullForAnUnknownGame()
    {
        using var temp = new TempDirectory();
        Assert.Null(MetadataCatalogClient.AttachMedia(
            "MISSING",
            null,
            [Path.Combine(temp.Path, "manual.pdf")],
            Path.Combine(temp.Path, "metadata.json")));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "5do-metadata-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

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
