using System;
using System.IO;
using ThreeDo.Host;

namespace ThreeDo.Tests.Host;

public sealed class GameLibraryScannerTests
{
    [Fact]
    public void Scan_FindsSupportedImagesAndReportsErrors()
    {
        using var directory = new TempDirectory();
        var isoPath = Path.Combine(directory.Path, "game.iso");
        using (var file = File.Create(isoPath))
        {
            file.Write(new byte[2048 * 3]);
        }

        File.WriteAllBytes(Path.Combine(directory.Path, "broken.iso"), new byte[100]);
        File.WriteAllText(Path.Combine(directory.Path, "notes.txt"), "ignore me");
        Directory.CreateDirectory(Path.Combine(directory.Path, "sub"));
        using (var file = File.Create(Path.Combine(directory.Path, "sub", "nested.bin")))
        {
            file.Write(new byte[2048]);
        }

        var entries = GameLibraryScanner.Scan([directory.Path]);

        Assert.Equal(3, entries.Count);
        var game = Assert.Single(entries, entry => entry.Path.EndsWith("game.iso", StringComparison.OrdinalIgnoreCase));
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

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"threedo-lib-{Guid.NewGuid():N}");
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
