using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class GameLibraryScannerTests
{
    [Fact]
    public void Scan_FindsSupportedImagesAndReportsErrors()
    {
        using var directory = new TempDirectory();
        var isoPath = Path.Combine(directory.Path, "game.iso");
        using (FileStream file = File.Create(isoPath))
        {
            file.Write(new byte[2048 * 3]);
        }

        File.WriteAllBytes(Path.Combine(directory.Path, "broken.iso"), new byte[100]);
        File.WriteAllText(Path.Combine(directory.Path, "notes.txt"), "ignore me");
        Directory.CreateDirectory(Path.Combine(directory.Path, "sub"));
        using (FileStream file = File.Create(Path.Combine(directory.Path, "sub", "nested.bin")))
        {
            file.Write(new byte[2048]);
        }

        IReadOnlyList<GameLibraryEntry> entries = GameLibraryScanner.Scan([directory.Path]);

        Assert.Equal(3, entries.Count);
        GameLibraryEntry game = Assert.Single(entries, entry => entry.Path.EndsWith("game.iso", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("ISO", game.Format);
        Assert.Equal(3, game.SectorCount);
        Assert.Equal("game", game.Name);
        Assert.False(game.Known);
        Assert.Contains(entries, entry => entry.Path.EndsWith("broken.iso", StringComparison.OrdinalIgnoreCase) && entry.Error.Length > 0);
        Assert.Contains(entries, entry => entry.Path.EndsWith("nested.bin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => entry.Path.EndsWith("notes.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inspect_ReturnsNullForUnknownExtension()
    {
        Assert.Null(GameLibraryScanner.Inspect(Path.Combine(Path.GetTempPath(), "not-a-disc.txt")));
    }

    [Fact]
    public void Scan_FindsCoverArtNextToADisc()
    {
        using var directory = new TempDirectory();
        var isoPath = Path.Combine(directory.Path, "game.iso");
        using (FileStream file = File.Create(isoPath))
        {
            file.Write(new byte[2048 * 3]);
        }

        var coverPath = Path.Combine(directory.Path, "game.png");
        File.WriteAllBytes(coverPath, [0x89, 0x50, 0x4E, 0x47]);

        GameLibraryEntry entry = Assert.Single(GameLibraryScanner.Scan([directory.Path]));

        Assert.Equal(coverPath, entry.CoverPath);
    }

    [Fact]
    public void FindCover_ReturnsEmptyWhenThereIsNoSiblingImage()
    {
        using var directory = new TempDirectory();
        var isoPath = Path.Combine(directory.Path, "game.iso");
        File.WriteAllBytes(isoPath, new byte[2048]);

        Assert.Equal(string.Empty, GameLibraryScanner.FindCover(isoPath));
    }

    [Fact]
    public void Scan_UsesConfiguredCoverDirectory()
    {
        using var discs = new TempDirectory();
        using var covers = new TempDirectory();
        var isoPath = Path.Combine(discs.Path, "Battle Chess (Europe).iso");
        WriteIso(isoPath, sectors: 1);
        var coverPath = Path.Combine(covers.Path, "Battle Chess (Europe).png");
        File.WriteAllBytes(coverPath, [0x89, 0x50, 0x4E, 0x47]);

        GameLibraryEntry entry = Assert.Single(
            GameLibraryScanner.Scan([discs.Path], coverDirectory: covers.Path));

        Assert.Equal(coverPath, entry.CoverPath);
    }

    [Fact]
    public void FindCover_PrefersSiblingOverConfiguredDirectory()
    {
        using var discs = new TempDirectory();
        using var covers = new TempDirectory();
        var isoPath = Path.Combine(discs.Path, "game.iso");
        WriteIso(isoPath, sectors: 1);
        var sibling = Path.Combine(discs.Path, "game.png");
        File.WriteAllBytes(sibling, [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllBytes(
            Path.Combine(covers.Path, "game.png"),
            [0x89, 0x50, 0x4E, 0x47]);

        Assert.Equal(sibling, GameLibraryScanner.FindCover(isoPath, covers.Path));
    }

    [Fact]
    public void FindCover_FallsBackToConfiguredDirectory()
    {
        using var discs = new TempDirectory();
        using var covers = new TempDirectory();
        var isoPath = Path.Combine(discs.Path, "game.iso");
        WriteIso(isoPath, sectors: 1);
        var coverPath = Path.Combine(covers.Path, "game.jpg");
        File.WriteAllBytes(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);

        Assert.Equal(coverPath, GameLibraryScanner.FindCover(isoPath, covers.Path));
    }

    [Fact]
    public void Scan_UsesCachedMetadataForUnchangedFiles()
    {
        using var directory = new TempDirectory();
        var isoPath = Path.Combine(directory.Path, "game.iso");
        WriteIso(isoPath, sectors: 3);
        var cachePath = Path.Combine(directory.Path, "library-cache.json");

        GameLibraryEntry first = Assert.Single(
            GameLibraryScanner.Scan([directory.Path], cachePath: cachePath));
        Assert.Equal(3, first.SectorCount);
        Assert.True(File.Exists(cachePath));
        Assert.False(File.Exists(cachePath + ".tmp"));

        using (new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            GameLibraryEntry cached = Assert.Single(
                GameLibraryScanner.Scan([directory.Path], cachePath: cachePath));
            Assert.Equal(3, cached.SectorCount);
            Assert.Equal(string.Empty, cached.Error);
        }

        WriteIso(isoPath, sectors: 5);
        File.SetLastWriteTimeUtc(isoPath, DateTime.UtcNow.AddMinutes(1));
        GameLibraryEntry changed = Assert.Single(
            GameLibraryScanner.Scan([directory.Path], cachePath: cachePath));
        Assert.Equal(5, changed.SectorCount);
    }

    [Fact]
    public void BeginScan_ListsFilesBeforeMetadataInspection()
    {
        using var directory = new TempDirectory();
        var isoPath = Path.Combine(directory.Path, "game.iso");
        WriteIso(isoPath, sectors: 3);
        var cachePath = Path.Combine(directory.Path, "library-cache.json");

        GameLibraryScanResult initial = GameLibraryScanner.BeginScan(
            [directory.Path],
            cachePath: cachePath);
        GameLibraryEntry placeholder = Assert.Single(initial.Entries);
        Assert.True(placeholder.Pending);
        Assert.Equal("game", placeholder.Name);
        Assert.Single(initial.PendingPaths);

        GameLibraryEntry enriched = Assert.Single(
            GameLibraryScanner.InspectPending(initial.PendingPaths, cachePath));
        Assert.False(enriched.Pending);
        Assert.Equal(3, enriched.SectorCount);

        GameLibraryScanResult cached = GameLibraryScanner.BeginScan(
            [directory.Path],
            cachePath: cachePath);
        GameLibraryEntry cachedEntry = Assert.Single(cached.Entries);
        Assert.False(cachedEntry.Pending);
        Assert.Equal(3, cachedEntry.SectorCount);
        Assert.Empty(cached.PendingPaths);
    }

    [Fact]
    public void Scan_RebuildsACorruptCache()
    {
        using var directory = new TempDirectory();
        var isoPath = Path.Combine(directory.Path, "game.iso");
        WriteIso(isoPath, sectors: 3);
        var cachePath = Path.Combine(directory.Path, "library-cache.json");
        File.WriteAllText(cachePath, "{ this is not json");

        GameLibraryEntry entry = Assert.Single(
            GameLibraryScanner.Scan([directory.Path], cachePath: cachePath));

        Assert.Equal(3, entry.SectorCount);
        Assert.Equal(string.Empty, entry.Error);
    }

    private static void WriteIso(string path, int sectors)
    {
        File.WriteAllBytes(path, new byte[2048 * sectors]);
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fivedo-lib-{Guid.NewGuid():N}");
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
