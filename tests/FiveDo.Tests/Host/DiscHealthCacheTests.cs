using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FiveDo.Host;
using FiveDo.Storage;

namespace FiveDo.Tests.Host;

public sealed class DiscHealthCacheTests
{
    [Fact]
    public void Store_RoundtripsRecords()
    {
        using var folder = new TempFolder();
        var cachePath = Path.Combine(folder.Path, "health.json");
        var cache = new DiscHealthCache();
        cache.Set(new DiscHealthRecord
        {
            Path = Path.Combine(folder.Path, "game.iso"),
            Length = 123,
            LastWriteTimeUtcTicks = 456,
            Status = DiscHealthStatus.Verified,
            GameName = "Game",
            RomName = "Game (Track 1).bin",
            Crc32 = "AABBCCDD",
            Md5 = "00112233445566778899AABBCCDDEEFF",
            Sha1 = "00112233445566778899AABBCCDDEEFF00112233",
            TrackCount = 2,
            MatchedTracks = 2,
            VerifiedUtcTicks = 789,
            Detail = "Known dump.",
        });

        DiscHealthCacheStore.Save(cache, cachePath);
        DiscHealthCache loaded = DiscHealthCacheStore.Load(cachePath);

        DiscHealthRecord record = Assert.Single(loaded.Entries);
        Assert.Equal(DiscHealthStatus.Verified, record.Status);
        Assert.Equal("Game", record.GameName);
        Assert.Equal(2, record.MatchedTracks);
        Assert.Equal("AABBCCDD", record.Crc32);
    }

    [Fact]
    public void Load_ReturnsEmptyCacheForMissingOrInvalidFiles()
    {
        using var folder = new TempFolder();
        Assert.Empty(DiscHealthCacheStore.Load(Path.Combine(folder.Path, "missing.json")).Entries);

        var invalid = Path.Combine(folder.Path, "invalid.json");
        File.WriteAllText(invalid, "{ not json");
        Assert.Empty(DiscHealthCacheStore.Load(invalid).Entries);
    }

    [Fact]
    public void Find_IgnoresStaleRecords()
    {
        using var folder = new TempFolder();
        var disc = Path.Combine(folder.Path, "game.iso");
        File.WriteAllBytes(disc, new byte[2048]);
        var info = new FileInfo(disc);
        var cache = new DiscHealthCache();
        cache.Set(DiscHealthRecord.Create(disc, info, DiscHealthStatus.NoDat));

        Assert.NotNull(cache.Find(disc));

        File.SetLastWriteTimeUtc(disc, DateTime.UtcNow.AddMinutes(5));
        Assert.Null(cache.Find(disc));
    }

    [Fact]
    public void Apply_PopulatesBadgeFields()
    {
        using var folder = new TempFolder();
        var disc = Path.Combine(folder.Path, "game.iso");
        File.WriteAllBytes(disc, new byte[2048]);
        var cache = new DiscHealthCache();
        DiscHealthRecord record = DiscHealthRecord.Create(disc, new FileInfo(disc), DiscHealthStatus.Mismatch);
        record.Detail = "No DAT entry matched.";
        cache.Set(record);

        GameLibraryEntry entry = cache.Apply(new GameLibraryEntry(disc, "ISO", 1, "ID", "Game", true, string.Empty));

        Assert.True(entry.HasDumpBadge);
        Assert.Equal("Not in DAT", entry.DumpBadge);
        Assert.Contains("No DAT entry matched.", entry.DumpTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Summarize_CountsEachStatus()
    {
        using var folder = new TempFolder();
        var cache = new DiscHealthCache();
        var entries = new List<GameLibraryEntry>();
        AddEntry(DiscHealthStatus.Verified);
        AddEntry(DiscHealthStatus.Mismatch);
        AddEntry(DiscHealthStatus.ReadError);
        AddEntry(DiscHealthStatus.NoDat);
        var uncheckedPath = Path.Combine(folder.Path, "unchecked.iso");
        File.WriteAllBytes(uncheckedPath, new byte[2048]);
        entries.Add(new GameLibraryEntry(uncheckedPath, "ISO", 1, "ID", "Unchecked", true, string.Empty));

        DiscHealthSummary summary = cache.Summarize(entries);

        Assert.Equal(1, summary.Verified);
        Assert.Equal(1, summary.Mismatch);
        Assert.Equal(1, summary.ReadError);
        Assert.Equal(1, summary.NoDat);
        Assert.Equal(1, summary.Unchecked);
        Assert.Equal(5, summary.Total);
        Assert.False(summary.Complete);
        Assert.Contains("1 known dump(s)", summary.Text, StringComparison.Ordinal);
        return;

        void AddEntry(DiscHealthStatus status)
        {
            var path = Path.Combine(folder.Path, $"{status}.iso");
            File.WriteAllBytes(path, new byte[2048]);
            DiscHealthRecord record = DiscHealthRecord.Create(path, new FileInfo(path), status);
            cache.Set(record);
            entries.Add(new GameLibraryEntry(path, "ISO", 1, "ID", status.ToString(), true, string.Empty));
        }
    }

    [Fact]
    public void RemoveMissingFiles_DropsDeletedRecords()
    {
        using var folder = new TempFolder();
        var cache = new DiscHealthCache();
        var disc = Path.Combine(folder.Path, "game.iso");
        File.WriteAllBytes(disc, new byte[2048]);
        cache.Set(DiscHealthRecord.Create(disc, new FileInfo(disc), DiscHealthStatus.NoDat));

        File.Delete(disc);
        cache.RemoveMissingFiles();

        Assert.Empty(cache.Entries);
    }

    [Fact]
    public void Entry_BadgeTextCoversEveryStatus()
    {
        var entry = new GameLibraryEntry("game.iso", "ISO", 1, "ID", "Game", true, string.Empty);
        Assert.False(entry.HasDumpBadge);
        Assert.Equal(string.Empty, entry.DumpBadge);

        Assert.Equal("Known dump", (entry with { DumpStatus = DiscHealthStatus.Verified }).DumpBadge);
        Assert.Equal("Not in DAT", (entry with { DumpStatus = DiscHealthStatus.Mismatch }).DumpBadge);
        Assert.Equal("Read error", (entry with { DumpStatus = DiscHealthStatus.ReadError }).DumpBadge);
        Assert.Equal("Checked", (entry with { DumpStatus = DiscHealthStatus.NoDat }).DumpBadge);
    }

    [Fact]
    public void Verify_WithoutDat_ReportsReadableDisc()
    {
        using var folder = new TempFolder();
        var disc = CreateIso(folder.Path, "game.iso", sectors: 4);

        DiscHealthRecord record = LibraryHealthVerifier.Verify(disc, dat: null);

        Assert.Equal(DiscHealthStatus.NoDat, record.Status);
        Assert.Equal(1, record.TrackCount);
        Assert.Contains("Readable", record.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithMatchingDat_ReportsKnownDump()
    {
        using var folder = new TempFolder();
        var disc = CreateIso(folder.Path, "game.iso", sectors: 4);
        byte[] data = File.ReadAllBytes(disc);
        RedumpDat dat = CreateDat("Test Game", "Test Game.bin", data);

        DiscHealthRecord record = LibraryHealthVerifier.Verify(disc, dat);

        Assert.Equal(DiscHealthStatus.Verified, record.Status);
        Assert.Equal("Test Game", record.GameName);
        Assert.Equal("Test Game.bin", record.RomName);
        Assert.Equal(1, record.MatchedTracks);
    }

    [Fact]
    public void Verify_WithMismatchedDat_ReportsMismatch()
    {
        using var folder = new TempFolder();
        var disc = CreateIso(folder.Path, "game.iso", sectors: 4);
        byte[] other = new byte[2048 * 4];
        other[0] = 1;
        RedumpDat dat = CreateDat("Other Game", "Other Game.bin", other);

        DiscHealthRecord record = LibraryHealthVerifier.Verify(disc, dat);

        Assert.Equal(DiscHealthStatus.Mismatch, record.Status);
        Assert.Equal(string.Empty, record.GameName);
    }

    [Fact]
    public void Verify_MissingFile_ReportsReadError()
    {
        using var folder = new TempFolder();
        DiscHealthRecord record = LibraryHealthVerifier.Verify(Path.Combine(folder.Path, "missing.iso"), dat: null);

        Assert.Equal(DiscHealthStatus.ReadError, record.Status);
        Assert.Contains("does not exist", record.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_UnreadableImage_ReportsReadError()
    {
        using var folder = new TempFolder();
        var disc = Path.Combine(folder.Path, "broken.iso");
        File.WriteAllBytes(disc, new byte[1000]);

        DiscHealthRecord record = LibraryHealthVerifier.Verify(disc, dat: null);

        Assert.Equal(DiscHealthStatus.ReadError, record.Status);
        Assert.False(string.IsNullOrWhiteSpace(record.Detail));
    }

    [Fact]
    public void VerifyAll_ReportsProgressInOrder()
    {
        using var folder = new TempFolder();
        string first = CreateIso(folder.Path, "one.iso", sectors: 2);
        string second = CreateIso(folder.Path, "two.iso", sectors: 2);
        var entries = new[]
        {
            new GameLibraryEntry(first, "ISO", 2, "A", "One", true, string.Empty),
            new GameLibraryEntry(second, "ISO", 2, "B", "Two", true, string.Empty),
        };
        var reports = new List<LibraryHealthProgress>();
        var progress = new InlineProgress<LibraryHealthProgress>(reports.Add);

        IReadOnlyList<DiscHealthRecord> records = LibraryHealthVerifier.VerifyAll(entries, dat: null, progress);

        Assert.Equal(2, records.Count);
        Assert.Equal(first, records[0].Path);
        Assert.Equal(second, records[1].Path);
        Assert.Equal(2, reports.Count);
        Assert.Equal(1, reports[0].Completed);
        Assert.Equal(2, reports[0].Total);
    }

    private static string CreateIso(string folder, string name, int sectors)
    {
        var path = Path.Combine(folder, name);
        var data = new byte[2048 * sectors];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        File.WriteAllBytes(path, data);
        return path;
    }

    private static RedumpDat CreateDat(string game, string romName, byte[] data)
    {
        var md5 = Hash(data, HashAlgorithmName.MD5);
        var sha1 = Hash(data, HashAlgorithmName.SHA1);
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"""
            <datafile>
              <game name="{game}">
                <rom name="{romName}" size="{data.Length}" md5="{md5}" sha1="{sha1}"/>
              </game>
            </datafile>
            """);
        return RedumpDat.Parse(builder.ToString());
    }

    private static string Hash(byte[] data, HashAlgorithmName algorithm)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(algorithm);
        hash.AppendData(data);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "5do-health-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A failed cleanup must not hide the test result.
            }
        }
    }
}
