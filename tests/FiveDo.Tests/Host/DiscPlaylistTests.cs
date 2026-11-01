using System;
using System.IO;
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
