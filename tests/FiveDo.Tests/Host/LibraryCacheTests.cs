using System;
using System.IO;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>
/// Covers the persistent library metadata cache.
/// </summary>
public sealed class LibraryCacheTests
{
    [Fact]
    public void Load_ReturnsAnEmptyCacheWhenTheFileIsMissing()
    {
        using var directory = new TempDirectory();

        LibraryCache cache = LibraryCacheStore.Load(Path.Combine(directory.Path, "missing.json"));

        Assert.Equal(LibraryCache.CurrentVersion, cache.Version);
        Assert.Empty(cache.Entries);
    }

    [Fact]
    public void SaveAndLoad_RoundTripEntries()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "cache.json");
        var cache = new LibraryCache
        {
            Entries =
            [
                new CachedLibraryEntry
                {
                    Path = "/games/Road Rash.iso",
                    Length = 1234,
                    LastWriteTimeUtcTicks = 5678,
                    Format = "ISO",
                    SectorCount = 2048,
                    GameId = "ABCD1234",
                    Error = "read error",
                },
            ],
        };

        LibraryCacheStore.Save(path, cache);
        LibraryCache loaded = LibraryCacheStore.Load(path);

        CachedLibraryEntry entry = Assert.Single(loaded.Entries);
        Assert.Equal("/games/Road Rash.iso", entry.Path);
        Assert.Equal(1234, entry.Length);
        Assert.Equal(5678, entry.LastWriteTimeUtcTicks);
        Assert.Equal("ISO", entry.Format);
        Assert.Equal(2048, entry.SectorCount);
        Assert.Equal("ABCD1234", entry.GameId);
        Assert.Equal("read error", entry.Error);
    }

    [Fact]
    public void Load_ReturnsAnEmptyCacheForAnUnsupportedVersion()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "cache.json");
        File.WriteAllText(path, "{\"Version\":99,\"Entries\":[{\"Path\":\"x\"}]}");

        LibraryCache cache = LibraryCacheStore.Load(path);

        Assert.Equal(LibraryCache.CurrentVersion, cache.Version);
        Assert.Empty(cache.Entries);
    }

    [Fact]
    public void Load_ReturnsAnEmptyCacheForMalformedJson()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "cache.json");
        File.WriteAllText(path, "not json");

        LibraryCache cache = LibraryCacheStore.Load(path);

        Assert.Empty(cache.Entries);
    }

    [Fact]
    public void Load_DropsNullEntriesAndNullFields()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "cache.json");
        File.WriteAllText(
            path,
            """{"Version":1,"Entries":[null,{},{"Path":"game.iso","Format":null,"GameId":null,"Error":null}]}""");

        LibraryCache cache = LibraryCacheStore.Load(path);

        CachedLibraryEntry entry = Assert.Single(cache.Entries);
        Assert.Equal("game.iso", entry.Path);
        Assert.Equal(string.Empty, entry.Format);
        Assert.Equal(string.Empty, entry.GameId);
        Assert.Equal(string.Empty, entry.Error);
    }

    [Fact]
    public void Save_CreatesTheParentDirectory()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nested", "cache.json");

        LibraryCacheStore.Save(path, new LibraryCache());

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Matches_ComparesTheFileLengthAndTimestamp()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "disc.iso");
        File.WriteAllBytes(path, [1, 2, 3]);
        var info = new FileInfo(path);
        CachedLibraryEntry entry = CachedLibraryEntry.From(
            new GameLibraryEntry(path, "ISO", 1, "ABCD1234", "Test Disc", true, string.Empty),
            info);

        Assert.True(entry.Matches(info));

        File.WriteAllBytes(path, [1, 2, 3, 4]);
        Assert.False(entry.Matches(new FileInfo(path)));
    }

    [Fact]
    public void FromAndMatches_TolerateAnInvalidFileTimestamp()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "disc.iso");
        File.WriteAllBytes(path, [1, 2, 3]);
        Assert.True(TestFileTimes.TrySetInvalidLastWriteTime(path));
        var info = new FileInfo(path);
        CachedLibraryEntry entry = CachedLibraryEntry.From(
            new GameLibraryEntry(path, "ISO", 1, "ABCD1234", "Test Disc", true, string.Empty),
            info);

        Assert.Equal(FileStamp.UnknownTicks, entry.LastWriteTimeUtcTicks);
        Assert.True(entry.Matches(new FileInfo(path)));
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "5do-library-cache-tests-" + Guid.NewGuid().ToString("N"));
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
