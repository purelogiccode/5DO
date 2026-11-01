using System;
using System.IO;
using System.Runtime.CompilerServices;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class LibraryStateTests
{
    [Fact]
    public void ResolveKey_PrefersTheGameIdAndNormalizesCase()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");

        Assert.Equal("id:F3AF1B13", LibraryState.ResolveKey("f3af1b13deadbeef", path));
        Assert.Equal(
            "path:" + Path.GetFullPath(path),
            LibraryState.ResolveKey(string.Empty, path));
    }

    [Fact]
    public void ToggleFavorite_PersistsAndAppliesToEntry()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var state = new LibraryState();

        Assert.True(state.ToggleFavorite("F3AF1B13", path));
        GameLibraryEntry entry = state.Apply(CreateEntry(path, "F3AF1B13"));

        Assert.True(entry.Favorite);
        Assert.Equal("★", entry.FavoriteGlyph);
        Assert.False(state.ToggleFavorite("F3AF1B13", path));
        Assert.False(state.Apply(CreateEntry(path, "F3AF1B13")).Favorite);
    }

    [Fact]
    public void RecordLaunch_CountsLaunchesAndSetsTheLastPlayedTimestamp()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var state = new LibraryState();
        var now = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        state.RecordLaunch("F3AF1B13", path, now);
        state.RecordLaunch("F3AF1B13", path, now.AddMinutes(5));

        LibraryGameState saved = Assert.IsType<LibraryGameState>(state.Find("F3AF1B13", path));
        Assert.Equal(2, saved.LaunchCount);
        Assert.Equal(now.AddMinutes(5), saved.LastPlayedUtc);
    }

    [Fact]
    public void AddPlaytime_AccumulatesWholeSecondsAndIgnoresNonPositiveValues()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var state = new LibraryState();
        var now = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        state.AddPlaytime("F3AF1B13", path, TimeSpan.FromSeconds(90.4), now);
        state.AddPlaytime("F3AF1B13", path, TimeSpan.Zero, now);
        state.AddPlaytime("F3AF1B13", path, TimeSpan.FromSeconds(-5), now);

        LibraryGameState saved = Assert.IsType<LibraryGameState>(state.Find("F3AF1B13", path));
        Assert.Equal(90, saved.PlaytimeSeconds);
        Assert.Equal(now, saved.LastPlayedUtc);
        Assert.Equal("1m", state.Apply(CreateEntry(path, "F3AF1B13")).PlaytimeText);
    }

    [Fact]
    public void Apply_CopiesFavoritePlaytimeAndLaunchHistory()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var state = new LibraryState();
        var now = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        state.ToggleFavorite("F3AF1B13", path);
        state.RecordLaunch("F3AF1B13", path, now);
        state.AddPlaytime("F3AF1B13", path, TimeSpan.FromMinutes(75), now);

        GameLibraryEntry entry = state.Apply(CreateEntry(path, "F3AF1B13"));

        Assert.True(entry.Favorite);
        Assert.Equal(4500, entry.PlaytimeSeconds);
        Assert.Equal(now, entry.LastPlayedUtc);
        Assert.Equal(1, entry.LaunchCount);
        Assert.Equal("1h 15m", entry.PlaytimeText);
    }

    [Fact]
    public void PlaytimeText_FormatsLargeValuesWithoutOverflow()
    {
        using var directory = new TempDirectory();
        GameLibraryEntry entry = CreateEntry(Path.Combine(directory.Path, "game.iso"), "F3AF1B13")
            with
        {
            PlaytimeSeconds = long.MaxValue,
        };

        Assert.EndsWith("m", entry.PlaytimeText);
    }

    [Fact]
    public void GetOrCreate_MigratesAPathRecordWhenTheGameIdBecomesKnown()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var state = new LibraryState();
        state.ToggleFavorite(string.Empty, path);

        LibraryGameState migrated = state.GetOrCreate("F3AF1B13", path);

        Assert.True(migrated.Favorite);
        Assert.False(state.Games.ContainsKey("path:" + Path.GetFullPath(path)));
        Assert.True(state.Games.ContainsKey("id:F3AF1B13"));
    }

    [Fact]
    public void Prune_RemovesRecordsThatAreNoLongerInTheLibrary()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var other = Path.Combine(directory.Path, "other.iso");
        var state = new LibraryState();
        state.ToggleFavorite("F3AF1B13", path);
        state.ToggleFavorite(string.Empty, other);

        var removed = state.Prune([CreateEntry(path, "F3AF1B13")]);

        Assert.Equal(1, removed);
        Assert.NotNull(state.Find("F3AF1B13", path));
        Assert.Null(state.Find(string.Empty, other));
    }

    [Fact]
    public void Store_RoundTripsAndRebuildsACorruptFile()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var statePath = Path.Combine(directory.Path, "library-state.json");
        var state = new LibraryState();
        state.ToggleFavorite("F3AF1B13", path);
        state.AddPlaytime("F3AF1B13", path, TimeSpan.FromMinutes(3));

        LibraryStateStore.Save(state, statePath);
        LibraryState loaded = LibraryStateStore.Load(statePath);

        Assert.True(loaded.Find("F3AF1B13", path)?.Favorite);
        Assert.Equal(180, loaded.Find("F3AF1B13", path)?.PlaytimeSeconds);
        Assert.False(File.Exists(statePath + ".tmp"));

        File.WriteAllText(statePath, "{ not json");
        LibraryState rebuilt = LibraryStateStore.Load(statePath);
        Assert.Empty(rebuilt.Games);
    }

    [Fact]
    public void PlaytimeTracker_AccumulatesOnlyRunningTime()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var state = new LibraryState();
        var now = new StrongBox<DateTime>(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        var saves = new StrongBox<int>(0);
        var tracker = new LibraryPlaytimeTracker(state, () => saves.Value++, () => now.Value);

        tracker.Start("F3AF1B13", path);
        now.Value = now.Value.AddSeconds(30);
        tracker.Start("F3AF1B13", path);
        Assert.True(tracker.IsTracking);

        now.Value = now.Value.AddSeconds(60);
        TimeSpan elapsed = tracker.Stop();

        Assert.Equal(TimeSpan.FromSeconds(90), elapsed);
        Assert.False(tracker.IsTracking);
        Assert.Equal(90, state.Find("F3AF1B13", path)?.PlaytimeSeconds);
        Assert.Equal(1, saves.Value);
    }

    [Fact]
    public void PlaytimeTracker_StopsThePreviousGameWhenTheDiscChanges()
    {
        using var directory = new TempDirectory();
        var first = Path.Combine(directory.Path, "first.iso");
        var second = Path.Combine(directory.Path, "second.iso");
        var state = new LibraryState();
        var now = new StrongBox<DateTime>(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        var tracker = new LibraryPlaytimeTracker(state, utcNow: () => now.Value);

        tracker.Start("11111111", first);
        now.Value = now.Value.AddSeconds(45);
        tracker.Start("22222222", second);

        Assert.Equal(45, state.Find("11111111", first)?.PlaytimeSeconds);
        Assert.Equal(second, tracker.CurrentPath);
    }

    [Fact]
    public void MetadataCatalog_AppliesOfflineMetadataByGameIdAndPath()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var metadataPath = Path.Combine(directory.Path, "library-metadata.json");
        LibraryMetadataStore.Save(
            new LibraryMetadata
            {
                Records =
                [
                    new LibraryMetadataRecord
                    {
                        GameId = "F3AF1B13",
                        Title = "Curated Title",
                        Developer = "Developer",
                        Publisher = "Publisher",
                        ReleaseYear = "1995",
                        Genre = "Action",
                        Regions = "USA",
                        Players = 2,
                        Description = "Description",
                    },
                ],
            },
            metadataPath);

        LibraryMetadataCatalog catalog = LibraryMetadataCatalog.Load(metadataPath);
        GameLibraryEntry entry = catalog.Apply(CreateEntry(path, "F3AF1B13"));

        Assert.Equal("Curated Title", entry.Name);
        Assert.Equal("Developer", entry.Developer);
        Assert.Equal("Publisher", entry.Publisher);
        Assert.Equal("1995", entry.ReleaseYear);
        Assert.Equal("Action", entry.Genre);
        Assert.Equal("USA", entry.Regions);
        Assert.Equal(2, entry.Players);
        Assert.Equal("Description", entry.Description);
        Assert.Contains("Action", entry.MetadataText);
    }

    [Fact]
    public void MetadataStore_UpsertReplacesByGameIdOrPathAndRemoveClearsIt()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var metadataPath = Path.Combine(directory.Path, "library-metadata.json");

        LibraryMetadataStore.Upsert(
            new LibraryMetadataRecord { GameId = "F3AF1B13", Title = "First" },
            metadataPath);
        LibraryMetadataStore.Upsert(
            new LibraryMetadataRecord { GameId = "f3af1b13", Title = "Second" },
            metadataPath);
        LibraryMetadataStore.Upsert(
            new LibraryMetadataRecord { Path = path, Publisher = "Path Publisher" },
            metadataPath);

        LibraryMetadataCatalog catalog = LibraryMetadataCatalog.Load(metadataPath);
        Assert.Equal("Second", catalog.Find("F3AF1B13", "other.iso")?.Title);
        Assert.Equal("Path Publisher", catalog.Find(string.Empty, path)?.Publisher);

        LibraryMetadataStore.Upsert(
            new LibraryMetadataRecord { GameId = "F3AF1B13", Path = path, Title = "Combined" },
            metadataPath);
        catalog = LibraryMetadataCatalog.Load(metadataPath);
        Assert.Single(LibraryMetadataStore.Load(metadataPath).Records);
        Assert.Equal("Combined", catalog.Find("F3AF1B13", path)?.Title);

        Assert.True(LibraryMetadataStore.Remove("f3af1b13", path, metadataPath));
        Assert.False(LibraryMetadataStore.Remove("f3af1b13", path, metadataPath));
        Assert.Null(LibraryMetadataCatalog.Load(metadataPath).Find("F3AF1B13", path));
    }

    [Fact]
    public void MetadataCatalog_FallsBackToThePathKeyAndIgnoresUnknownVersions()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "game.iso");
        var metadataPath = Path.Combine(directory.Path, "library-metadata.json");
        LibraryMetadataStore.Save(
            new LibraryMetadata
            {
                Records =
                [
                    new LibraryMetadataRecord { Path = path, Publisher = "Path Publisher" },
                ],
            },
            metadataPath);

        GameLibraryEntry entry = LibraryMetadataCatalog.Load(metadataPath).Apply(CreateEntry(path, string.Empty));
        Assert.Equal("Path Publisher", entry.Publisher);

        File.WriteAllText(metadataPath, "{\"version\":99,\"records\":[]}");
        Assert.Empty(LibraryMetadataCatalog.Load(metadataPath).Apply(CreateEntry(path, "F3AF1B13")).Publisher);
    }

    [Fact]
    public void LibraryQuery_FiltersByCollectionAndSearchesRichMetadata()
    {
        using var directory = new TempDirectory();
        GameLibraryEntry favorite = CreateEntry(Path.Combine(directory.Path, "a.iso"), "AAAAAAAA")
            with
        {
            Favorite = true,
            PlaytimeSeconds = 120,
            LastPlayedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            Publisher = "Electronic Arts",
            ReleaseYear = "1994",
        };
        GameLibraryEntry unplayed = CreateEntry(Path.Combine(directory.Path, "b.iso"), "BBBBBBBB")
            with
        {
            Publisher = "Crystal Dynamics",
            ReleaseYear = "1995",
            Genre = "Platformer",
        };
        GameLibraryEntry[] entries = [favorite, unplayed];

        Assert.Equal(2, LibraryQuery.Apply(entries, string.Empty, LibraryFilter.All, LibrarySort.Title).Count);
        Assert.Single(LibraryQuery.Apply(entries, string.Empty, LibraryFilter.Favorites, LibrarySort.Title));
        Assert.Single(LibraryQuery.Apply(entries, string.Empty, LibraryFilter.Played, LibrarySort.Title));
        Assert.Single(LibraryQuery.Apply(entries, string.Empty, LibraryFilter.Unplayed, LibrarySort.Title));
        Assert.Single(LibraryQuery.Apply(entries, "platformer", LibraryFilter.All, LibrarySort.Title));
        Assert.Single(LibraryQuery.Apply(entries, "electronic", LibraryFilter.All, LibrarySort.Title));
        Assert.Equal(favorite, LibraryQuery.Apply(entries, string.Empty, LibraryFilter.All, LibrarySort.MostPlayed)[0]);
        Assert.Equal(unplayed, LibraryQuery.Apply(entries, string.Empty, LibraryFilter.All, LibrarySort.ReleaseYear)[0]);
        Assert.Equal(unplayed, LibraryQuery.Apply(entries, string.Empty, LibraryFilter.All, LibrarySort.Publisher)[0]);
    }

    private static GameLibraryEntry CreateEntry(string path, string gameId)
    {
        return new GameLibraryEntry(
            path,
            "ISO",
            3,
            gameId,
            Path.GetFileNameWithoutExtension(path),
            gameId.Length > 0,
            string.Empty);
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fivedo-library-{Guid.NewGuid():N}");
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
