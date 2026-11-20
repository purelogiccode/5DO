using System;
using System.IO;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>
/// Covers the atomic file writer used by settings, NVRAM, library caches, and metadata.
/// </summary>
public sealed class AtomicFileTests
{
    [Fact]
    public void WriteAllText_WritesTheContents()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");

        Assert.True(AtomicFile.WriteAllText(path, "{\"version\":1}", "test settings"));

        Assert.Equal("{\"version\":1}", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_CreatesTheParentDirectory()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nested", "deeper", "data.txt");

        Assert.True(AtomicFile.WriteAllText(path, "data", "nested test"));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void WriteAllText_ReplacesExistingContents()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "old");

        Assert.True(AtomicFile.WriteAllText(path, "new", "replacement test"));

        Assert.Equal("new", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllBytes_WritesTheContents()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.bin");
        byte[] contents = [1, 2, 3, 4, 5];

        Assert.True(AtomicFile.WriteAllBytes(path, contents, "test nvram"));

        Assert.Equal(contents, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WriteAllText_RejectsAnEmptyPath(string path)
    {
        Assert.Throws<ArgumentException>(() => AtomicFile.WriteAllText(path, "data", "description"));
    }

    [Fact]
    public void WriteAllText_RejectsNullContents()
    {
        using var directory = new TempDirectory();

        Assert.Throws<ArgumentNullException>(
            () => AtomicFile.WriteAllText(Path.Combine(directory.Path, "file.txt"), null!, "description"));
    }

    [Fact]
    public void WriteAllText_RejectsAnEmptyDescription()
    {
        using var directory = new TempDirectory();

        Assert.Throws<ArgumentException>(
            () => AtomicFile.WriteAllText(Path.Combine(directory.Path, "file.txt"), "data", " "));
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "5do-atomic-tests-" + Guid.NewGuid().ToString("N"));
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
