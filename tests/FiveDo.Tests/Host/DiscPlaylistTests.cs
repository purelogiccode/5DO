using System;
using System.IO;
using System.Linq;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class DiscPlaylistTests
{
    [Fact]
    public void Load_ResolvesRelativePathsAndExtendedTitles()
    {
        using var temp = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "disc1.iso"), string.Empty);
        File.WriteAllText(Path.Combine(temp.Path, "disc2.iso"), string.Empty);
        var playlistPath = Path.Combine(temp.Path, "game.m3u");
        File.WriteAllText(
            playlistPath,
            """
            #EXTM3U
            #EXTINF:0,Opening disc
            disc1.iso

            #EXTINF:-1,Final disc
            disc2.iso
            """);

        DiscPlaylist playlist = DiscPlaylist.Load(playlistPath);

        Assert.Equal(2, playlist.Count);
        Assert.Equal(0, playlist.CurrentIndex);
        Assert.Equal("Opening disc", playlist.Current.DisplayName);
        Assert.Equal(Path.Combine(temp.Path, "disc1.iso"), playlist.Current.Path);
        Assert.Equal("disc 1/2", playlist.FormatPosition());

        DiscPlaylistEntry next = playlist.MoveNext();
        Assert.Equal(1, playlist.CurrentIndex);
        Assert.Equal("Final disc", next.DisplayName);
        Assert.Equal(Path.Combine(temp.Path, "disc2.iso"), next.Path);

        Assert.Equal("Opening disc", playlist.MoveNext().DisplayName);
        Assert.Equal("Final disc", playlist.MovePrevious().DisplayName);
        Assert.Throws<ArgumentOutOfRangeException>(() => playlist.Select(2));
    }

    [Fact]
    public void Load_RejectsAFileWithoutEntries()
    {
        using var temp = new TemporaryDirectory();
        var playlistPath = Path.Combine(temp.Path, "empty.m3u");
        File.WriteAllText(playlistPath, "#EXTM3U\n# nothing here\n");

        Assert.Throws<InvalidDataException>(() => DiscPlaylist.Load(playlistPath));
    }

    [Fact]
    public void EditAndSave_RoundTripsEntriesInOrder()
    {
        using var temp = new TemporaryDirectory();
        string first = Path.Combine(temp.Path, "disc1.iso");
        string second = Path.Combine(temp.Path, "disc2.iso");
        string third = Path.Combine(temp.Path, "disc3.iso");
        string fourth = Path.Combine(temp.Path, "disc4.iso");
        File.WriteAllText(first, string.Empty);
        File.WriteAllText(second, string.Empty);
        File.WriteAllText(third, string.Empty);
        File.WriteAllText(fourth, string.Empty);

        DiscPlaylist playlist = DiscPlaylist.Create(
        [
            new DiscPlaylistEntry(first, "One"),
            new DiscPlaylistEntry(second, "Two"),
            new DiscPlaylistEntry(third, "Three"),
        ]);
        Assert.Equal(string.Empty, playlist.SourcePath);
        playlist.Add(fourth, "Four");
        Assert.True(playlist.MoveEntryUp(3));
        Assert.False(playlist.MoveEntryUp(0));
        Assert.True(playlist.MoveEntryDown(1));
        playlist.Select(1);
        playlist.RemoveAt(0);

        var savedPath = Path.Combine(temp.Path, "edited.m3u8");
        string written = playlist.Save(savedPath);

        Assert.Equal(Path.GetFullPath(savedPath), written);
        Assert.Equal(3, playlist.Count);
        Assert.Equal("Four", playlist.Current.DisplayName);
        var text = File.ReadAllText(savedPath);
        Assert.StartsWith("#EXTM3U", text, StringComparison.Ordinal);
        Assert.Contains("#EXTINF:-1,Two", text, StringComparison.Ordinal);

        DiscPlaylist reloaded = DiscPlaylist.Load(savedPath);
        Assert.Equal(3, reloaded.Count);
        Assert.Equal(
            ["Four", "Two", "Three"],
            reloaded.Entries.Select(entry => entry.DisplayName),
            StringComparer.Ordinal);
        Assert.All(reloaded.Entries, entry => Assert.True(File.Exists(entry.Path)));
    }

    [Fact]
    public void Save_RejectsAnEmptyPlaylistOrMissingPath()
    {
        DiscPlaylist playlist = DiscPlaylist.Create([]);

        Assert.Throws<InvalidOperationException>(() => playlist.Save());
        Assert.Throws<InvalidOperationException>(() => playlist.Save(Path.GetTempFileName()));
    }

    [Fact]
    public void RemoveAt_KeepsTheSelectionValid()
    {
        DiscPlaylist playlist = DiscPlaylist.Create(
        [
            new DiscPlaylistEntry(Path.GetTempPath() + "a.iso", null),
            new DiscPlaylistEntry(Path.GetTempPath() + "b.iso", null),
        ]);
        playlist.Select(1);

        playlist.RemoveAt(1);

        Assert.Equal(1, playlist.Count);
        Assert.Equal(0, playlist.CurrentIndex);
        Assert.Equal("a", playlist.Current.DisplayName);
        playlist.RemoveAt(0);
        Assert.Equal(0, playlist.Count);
        Assert.Equal(0, playlist.CurrentIndex);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fivedo-playlist-{Guid.NewGuid():N}");
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
