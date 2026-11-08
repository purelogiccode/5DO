using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FiveDo.Frontend.Avalonia;
using FiveDo.Frontend.Avalonia.ViewModels;
using FiveDo.Host;
using FiveDo.Host.Osd;

namespace FiveDo.Tests.Frontend;

/// <summary>
/// Covers the lightweight presentation models used by the Avalonia shell.
/// </summary>
public sealed class LibraryPresentationTests
{
    [Fact]
    public void LibraryItem_ProjectsScannerFields()
    {
        GameLibraryEntry entry = CreateEntry("ABCDEF0123456789", sectorCount: 123_456);
        using var item = new LibraryItem(entry);

        Assert.Equal("Test Game", item.Name);
        Assert.Equal(entry.Path, item.Path);
        Assert.Equal("ISO", item.Format);
        Assert.True(item.Known);
        Assert.False(item.IsUnknown);
        Assert.Equal("4DO DB", item.DatabaseBadge);
        Assert.Equal("ABCDEF01", item.GameIdText);
        Assert.Equal("123,456 sectors", item.SectorText);
    }

    [Fact]
    public void LibraryItem_ShortGameIdIsUppercased()
    {
        using var item = new LibraryItem(CreateEntry("abc"));

        Assert.Equal("ABC", item.GameIdText);
    }

    [Fact]
    public void LibraryItem_PendingEntryShowsScanningState()
    {
        GameLibraryEntry entry = CreateEntry(string.Empty, pending: true);
        using var item = new LibraryItem(entry);

        Assert.Equal("Scanning", item.DatabaseBadge);
        Assert.Equal("Scanning metadata...", item.SectorText);
    }

    [Fact]
    public void LibraryItem_UnreadableEntryShowsUnreadableImage()
    {
        using var item = new LibraryItem(CreateEntry("00", sectorCount: 0, known: false, error: "bad image"));

        Assert.Equal("Unknown", item.DatabaseBadge);
        Assert.True(item.IsUnknown);
        Assert.Equal("Unreadable image", item.SectorText);
        Assert.Contains("bad image", item.Tip, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryItem_ThrowsForNullEntry()
    {
        Assert.Throws<ArgumentNullException>(() => new LibraryItem(null!));
    }

    [Fact]
    public void LibraryItem_TipListsPathFormatMetadataPlaytimeAndLastPlayed()
    {
        GameLibraryEntry entry = CreateEntry("ABCDEF0123456789") with
        {
            PlaytimeSeconds = 3_900,
            LastPlayedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            ReleaseYear = "1994",
            Publisher = "Test Publisher",
            CoverSource = "igdb",
            CoverConfidence = 0.9,
            CoverMatchName = "cover.png",
        };
        using var item = new LibraryItem(entry);

        var tip = item.Tip;

        Assert.Contains(entry.Path, tip, StringComparison.Ordinal);
        Assert.Contains("ISO", tip, StringComparison.Ordinal);
        Assert.Contains("1h 05m", tip, StringComparison.Ordinal);
        Assert.Contains("Last played", tip, StringComparison.Ordinal);
        Assert.Contains("Cover: igdb", tip, StringComparison.Ordinal);
        Assert.Contains("Matched: cover.png", tip, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryItem_TipOmitsTheCoverLineForTheNoneSource()
    {
        GameLibraryEntry entry = CreateEntry("ABCDEF0123456789") with { CoverSource = "none" };
        using var item = new LibraryItem(entry);

        Assert.DoesNotContain("Cover:", item.Tip, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryItem_UpdateReplacesEntryAndRaisesPropertyChanged()
    {
        using var item = new LibraryItem(CreateEntry("ABCDEF0123456789"));
        var raised = new List<string?>();
        item.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        GameLibraryEntry updated = CreateEntry("ABCDEF0123456789") with { Name = "Renamed Game" };
        item.Update(updated);

        Assert.Equal("Renamed Game", item.Name);
        Assert.Contains(raised, name => string.Equals(name, nameof(LibraryItem.Name), StringComparison.Ordinal));
        Assert.Contains(raised, name => string.Equals(name, nameof(LibraryItem.Tip), StringComparison.Ordinal));
    }

    [Fact]
    public void LibraryItem_UpdateThrowsForNull()
    {
        using var item = new LibraryItem(CreateEntry("ABCDEF0123456789"));

        Assert.Throws<ArgumentNullException>(() => item.Update(null!));
    }

    [Fact]
    public void LibraryItem_SetCoverPathWithTheSameValueDoesNotNotify()
    {
        using var item = new LibraryItem(CreateEntry("ABCDEF0123456789") with { CoverPath = "cover.png" });
        var raised = 0;
        item.PropertyChanged += (_, _) => raised++;

        item.SetCoverPath("COVER.PNG");

        Assert.Equal(0, raised);
    }

    [Fact]
    public void LibraryItem_SetCoverPathToEmptyClearsTheCoverWithoutDecoding()
    {
        using var item = new LibraryItem(CreateEntry("ABCDEF0123456789") with { CoverPath = "cover.png" });
        var raised = new List<string?>();
        item.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        item.SetCoverPath(null);

        Assert.Equal(string.Empty, item.Entry.CoverPath);
        Assert.False(item.HasCover);
        Assert.Contains(raised, name => string.Equals(name, nameof(LibraryItem.HasCover), StringComparison.Ordinal));
    }

    [Fact]
    public void LibraryItem_DisposeIsSafe()
    {
        var item = new LibraryItem(CreateEntry("ABCDEF0123456789"));

        item.Dispose();
        item.Dispose();
    }

    [Fact]
    public void OsdEntry_ProjectsTextAndSeverityBrushes()
    {
        var info = new OsdEntry("hello", OsdSeverity.Info);
        var success = new OsdEntry("ok", OsdSeverity.Success);
        var warning = new OsdEntry("careful", OsdSeverity.Warning);
        var error = new OsdEntry("failed", OsdSeverity.Error);

        Assert.Equal("hello", info.Text);
        Assert.NotSame(info.Accent, success.Accent);
        Assert.NotSame(success.Accent, warning.Accent);
        Assert.NotSame(warning.Accent, error.Accent);
        Assert.NotSame(info.Background, error.Background);
    }

    [Fact]
    public async Task AsyncEvent_RunContainsExceptionsFromTheOperationAsync()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        AsyncEvent.Run(
            async () =>
            {
                try
                {
                    await Task.Yield();
                    throw new InvalidOperationException("expected test failure");
                }
                finally
                {
                    completed.TrySetResult();
                }
            },
            "running an asynchronous test handler");

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static GameLibraryEntry CreateEntry(
        string gameId,
        int sectorCount = 100,
        bool known = true,
        bool pending = false,
        string error = "")
    {
        return new GameLibraryEntry(
            Path: System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test-game.iso"),
            Format: "ISO",
            SectorCount: sectorCount,
            GameId: gameId,
            Name: "Test Game",
            Known: known,
            Error: error,
            CoverPath: string.Empty,
            Pending: pending);
    }
}
