using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Storage;

namespace FiveDo.Tests.Host;

public sealed class DiscBrowserTests
{
    [Fact]
    public void Create_BuildsTreeAndDiscMetadata()
    {
        var image = SyntheticOperaDisc.Build(
            [
                new SyntheticOperaDisc.EntrySpec("HELLO.TXT", IsDirectory: false, Lba: 2, LengthBytes: 5),
                new SyntheticOperaDisc.EntrySpec("DIR", IsDirectory: true, Lba: 3, LengthBytes: 2048),
                new SyntheticOperaDisc.EntrySpec("Disc label", IsDirectory: false, Lba: 0, LengthBytes: 132, Type: 6),
            ],
            blockCount: 6,
            label: "TESTDISC",
            directories: new Dictionary<uint, IReadOnlyList<SyntheticOperaDisc.EntrySpec>>
            {
                [3] = [new SyntheticOperaDisc.EntrySpec("NESTED.BIN", IsDirectory: false, Lba: 4, LengthBytes: 4)],
            });
        SyntheticOperaDisc.WriteFile(image, 2, "hello"u8);
        SyntheticOperaDisc.WriteFile(image, 4, "nest"u8);

        using var disc = new MemoryDisc(image);
        using var browser = DiscBrowser.Create(disc, "test.iso");

        Assert.Equal("TESTDISC", browser.Label);
        Assert.Equal("Synthetic disc", browser.Comment);
        Assert.Equal("ISO", browser.Format);
        Assert.Equal(6, browser.SectorCount);
        Assert.Equal(FiveDo.Storage.GameId.Compute(disc), browser.GameId);
        Assert.Equal("\\", browser.Root.Path);
        Assert.Equal(
            "DIR,Disc label,HELLO.TXT",
            string.Join(',', browser.Root.Children.Select(node => node.Name)));
        Assert.Equal(
            "\\, \\DIR, \\DIR\\NESTED.BIN, \\Disc label, \\HELLO.TXT",
            string.Join(", ", browser.Nodes.Select(node => node.Path)));
        Assert.Equal(1, browser.Root.Children[0].Depth);
        Assert.Equal(2, browser.Root.Children[0].Children[0].Depth);
        Assert.NotNull(browser.Find("\\DIR\\NESTED.BIN"));
        Assert.NotNull(browser.Find("DIR\\NESTED.BIN"));
        Assert.Null(browser.Find("\\MISSING"));

        DiscInfo info = browser.Info;
        Assert.Equal(3, info.FileCount);
        Assert.Equal(1, info.DirectoryCount);
        Assert.Equal(141, info.TotalFileBytes);
        Assert.Equal("TESTDISC", info.Label);
        Assert.Equal(browser.GameId, info.GameId);
    }

    [Fact]
    public void ExtractFile_WritesBytesAndSanitizesTheName()
    {
        var image = SyntheticOperaDisc.Build(
            [new SyntheticOperaDisc.EntrySpec("BAD:NAME", IsDirectory: false, Lba: 2, LengthBytes: 5)],
            blockCount: 4);
        SyntheticOperaDisc.WriteFile(image, 2, "hello"u8);

        using var disc = new MemoryDisc(image);
        using var browser = DiscBrowser.Create(disc);
        using var destination = new TempDirectory();

        DiscExtractionResult result = browser.Extract(
            browser.Find("\\BAD:NAME")!,
            destination.Path);

        Assert.Equal(1, result.Files);
        Assert.Equal(0, result.Directories);
        Assert.Equal(5, result.Bytes);
        var path = Path.Combine(destination.Path, "BAD_NAME");
        Assert.True(File.Exists(path));
        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public void ExtractDirectory_RecursesAndPreservesTheHierarchy()
    {
        var image = SyntheticOperaDisc.Build(
            [new SyntheticOperaDisc.EntrySpec("DIR", IsDirectory: true, Lba: 2, LengthBytes: 2048)],
            blockCount: 5,
            directories: new Dictionary<uint, IReadOnlyList<SyntheticOperaDisc.EntrySpec>>
            {
                [2] = [new SyntheticOperaDisc.EntrySpec("NESTED.BIN", IsDirectory: false, Lba: 3, LengthBytes: 4)],
            });
        SyntheticOperaDisc.WriteFile(image, 3, "nest"u8);

        using var disc = new MemoryDisc(image);
        using var browser = DiscBrowser.Create(disc);
        using var destination = new TempDirectory();

        DiscExtractionResult result = browser.Extract(
            browser.Find("\\DIR")!,
            destination.Path);

        Assert.Equal(1, result.Files);
        Assert.Equal(1, result.Directories);
        Assert.Equal(4, result.Bytes);
        var path = Path.Combine(destination.Path, "DIR", "NESTED.BIN");
        Assert.True(File.Exists(path));
        Assert.Equal("nest", File.ReadAllText(path));
    }

    [Fact]
    public void ExtractAll_WritesEveryFileOnTheDisc()
    {
        var image = SyntheticOperaDisc.Build(
            [
                new SyntheticOperaDisc.EntrySpec("ONE.BIN", IsDirectory: false, Lba: 2, LengthBytes: 3),
                new SyntheticOperaDisc.EntrySpec("DIR", IsDirectory: true, Lba: 3, LengthBytes: 2048),
            ],
            blockCount: 6,
            directories: new Dictionary<uint, IReadOnlyList<SyntheticOperaDisc.EntrySpec>>
            {
                [3] = [new SyntheticOperaDisc.EntrySpec("TWO.BIN", IsDirectory: false, Lba: 4, LengthBytes: 6)],
            });
        SyntheticOperaDisc.WriteFile(image, 2, "one"u8);
        SyntheticOperaDisc.WriteFile(image, 4, "second"u8);

        using var disc = new MemoryDisc(image);
        using var browser = DiscBrowser.Create(disc);
        using var destination = new TempDirectory();

        DiscExtractionResult result = browser.ExtractAll(destination.Path);

        Assert.Equal(2, result.Files);
        Assert.Equal(2, result.Directories);
        Assert.Equal(9, result.Bytes);
        Assert.Equal("one", File.ReadAllText(Path.Combine(destination.Path, "ONE.BIN")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(destination.Path, "DIR", "TWO.BIN")));
    }

    [Fact]
    public void SanitizeFileName_RemovesTraversalAndReservedNames()
    {
        Assert.Equal("_", DiscBrowser.SanitizeFileName(".."));
        Assert.Equal("_", DiscBrowser.SanitizeFileName("."));
        Assert.Equal("_", DiscBrowser.SanitizeFileName("   "));
        Assert.Equal("A_B", DiscBrowser.SanitizeFileName("A/B"));
        Assert.Equal("A_B", DiscBrowser.SanitizeFileName("A\\B"));
        Assert.Equal("A_B", DiscBrowser.SanitizeFileName("A:B"));
        Assert.Equal("_CON", DiscBrowser.SanitizeFileName("CON"));
        Assert.Equal("_NUL.txt", DiscBrowser.SanitizeFileName("NUL.txt"));
        Assert.Equal("_COM1", DiscBrowser.SanitizeFileName("COM1"));
        Assert.Equal("name", DiscBrowser.SanitizeFileName("name. "));
        Assert.Equal("name", DiscBrowser.SanitizeFileName("name . "));
        Assert.Equal("normal.bin", DiscBrowser.SanitizeFileName("normal.bin"));
    }

    [Fact]
    public void Open_ReadsAnIsoFileAndDetectsTheFormat()
    {
        var image = SyntheticOperaDisc.Build(
            [new SyntheticOperaDisc.EntrySpec("FILE.BIN", IsDirectory: false, Lba: 2, LengthBytes: 3)],
            blockCount: 4);
        SyntheticOperaDisc.WriteFile(image, 2, "iso"u8);
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "test.iso");
        File.WriteAllBytes(path, image);

        using DiscBrowser browser = DiscBrowser.Open(path);

        Assert.Equal("ISO", browser.Format);
        Assert.Equal(Path.GetFullPath(path), browser.SourcePath);
        Assert.Equal("TESTDISC", browser.Label);
        Assert.NotNull(browser.Find("\\FILE.BIN"));
    }

    [Fact]
    public void Open_RejectsMissingImages()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-missing-{Guid.NewGuid():N}.chd");
        Assert.Throws<DiscImageException>(() => DiscBrowser.Open(path));
    }

    private sealed class MemoryDisc(byte[] image) : IDiscSource
    {
        public int SectorCount => image.Length / 2048;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            image.AsSpan(lba * 2048, 2048).CopyTo(destination);
        }

        public void Dispose()
        {
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fivedo-disc-browser-{Guid.NewGuid():N}");
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
