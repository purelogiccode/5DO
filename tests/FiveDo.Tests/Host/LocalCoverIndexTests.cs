using System;
using System.IO;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>
/// Covers the local cover index used by the library and settings windows.
/// </summary>
public sealed class LocalCoverIndexTests
{
    [Fact]
    public void Build_IndexesSupportedCoverExtensionsOnly()
    {
        using var directory = new TempDirectory();
        File.WriteAllBytes(Path.Combine(directory.Path, "Road Rash (USA).png"), []);
        File.WriteAllBytes(Path.Combine(directory.Path, "Road Rash (USA).txt"), []);
        File.WriteAllBytes(Path.Combine(directory.Path, "Gex (Europe).jpg"), []);

        LocalCoverIndex index = LocalCoverIndex.Build(directory.Path, stripAnnotations: false);

        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void FindExact_MatchesTheFileBaseNameCaseInsensitively()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "Road Rash (USA).png");
        File.WriteAllBytes(path, []);
        LocalCoverIndex index = LocalCoverIndex.Build(directory.Path, stripAnnotations: false);

        Assert.Equal(path, index.FindExact("road rash (usa)"));
        Assert.Null(index.FindExact("missing"));
        Assert.Null(index.FindExact(" "));
    }

    [Fact]
    public void Find_ReturnsTheExactCoverWithFullConfidence()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "Road Rash (USA).png");
        File.WriteAllBytes(path, []);
        LocalCoverIndex index = LocalCoverIndex.Build(directory.Path, stripAnnotations: false);

        LocalCoverMatch? match = index.Find(
            title: "Road Rash",
            fileName: "Road Rash (USA)",
            threshold: 0.8,
            fuzzy: true,
            gameRegions: "USA",
            gameYear: "1994");

        Assert.NotNull(match);
        Assert.Equal(path, match.Path);
        Assert.Equal(1.0, match.Score);
    }

    [Fact]
    public void Find_ReturnsNullWhenFuzzyMatchingIsDisabledAndNoExactMatchExists()
    {
        using var directory = new TempDirectory();
        File.WriteAllBytes(Path.Combine(directory.Path, "Road Rash (USA).png"), []);
        LocalCoverIndex index = LocalCoverIndex.Build(directory.Path, stripAnnotations: false);

        LocalCoverMatch? match = index.Find(
            title: "Completely Different Title",
            fileName: null,
            threshold: 0.1,
            fuzzy: false,
            gameRegions: null,
            gameYear: null);

        Assert.Null(match);
    }

    [Fact]
    public void Build_ThrowsForAnEmptyDirectory()
    {
        Assert.Throws<ArgumentException>(() => LocalCoverIndex.Build(" ", stripAnnotations: false));
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "5do-cover-index-tests-" + Guid.NewGuid().ToString("N"));
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
