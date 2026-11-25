using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class ArchiveDiscImageTests
{
    [Fact]
    public void ZipIso_ReadsEverySectorAndComputesTheSameGameId()
    {
        using var temp = new TempDirectory();
        byte[] iso = BuildIso(8);
        var zipPath = Path.Combine(temp.Path, "disc.zip");
        CreateZip(zipPath, ("disc.iso", iso));

        using IDiscSource source = DiscImageFactory.Open(zipPath);
        Assert.Equal(8, source.SectorCount);
        Assert.Equal(IsoImage.SectorBytes, source.SectorSize);

        var sector = new byte[IsoImage.SectorBytes];
        for (var lba = 0; lba < 8; lba++)
        {
            source.ReadSector(lba, sector);
            Assert.Equal(iso.AsSpan(lba * IsoImage.SectorBytes, IsoImage.SectorBytes).ToArray(), sector);
        }

        var extractedPath = Path.Combine(temp.Path, "extracted.iso");
        File.WriteAllBytes(extractedPath, iso);
        using IDiscSource extracted = DiscImageFactory.Open(extractedPath);
        Assert.Equal(GameId.Compute(extracted), GameId.Compute(source));
    }

    [Fact]
    public void SevenZipCueBin_ReadsDataAndAudioTracks()
    {
        var archivePath = Path.Combine(AppContext.BaseDirectory, "TestData", "archive-cue.7z");
        using IDiscSource source = DiscImageFactory.Open(archivePath);
        var tracked = Assert.IsAssignableFrom<ITrackedDiscSource>(source);
        Assert.Equal(4, tracked.SectorCount);
        Assert.Equal(2, tracked.Tracks.Count);
        Assert.Equal(DiscTrackKind.Data, tracked.Tracks[0].Kind);
        Assert.Equal(DiscTrackKind.Audio, tracked.Tracks[1].Kind);

        byte[] bin = BuildBin();
        var data = new byte[2352];
        for (var lba = 0; lba < 2; lba++)
        {
            tracked.ReadTrackSector(lba, data);
            Assert.Equal(
                bin.AsSpan(lba * 2352, 2352).ToArray(),
                data);
        }

        var audio = new byte[2352];
        for (var lba = 2; lba < 4; lba++)
        {
            tracked.ReadTrackSector(lba, audio);
            Assert.Equal(
                bin.AsSpan(lba * 2352, 2352).ToArray(),
                audio);
        }
    }

    [Fact]
    public void ZipCueInASubfolder_ResolvesTheRelativeBin()
    {
        using var temp = new TempDirectory();
        byte[] bin = BuildBin();
        var zipPath = Path.Combine(temp.Path, "nested.zip");
        CreateZip(
            zipPath,
            ("discs/game.bin", bin),
            ("discs/game.cue", Encoding.UTF8.GetBytes(CueText())));

        using IDiscSource source = DiscImageFactory.Open(zipPath);
        var tracked = Assert.IsAssignableFrom<ITrackedDiscSource>(source);
        Assert.Equal(4, tracked.SectorCount);
        var sector = new byte[2352];
        tracked.ReadTrackSector(0, sector);
        Assert.Equal(bin.AsSpan(0, 2352).ToArray(), sector);
    }

    [Fact]
    public void Archive_RejectsMultipleImages()
    {
        using var temp = new TempDirectory();
        var zipPath = Path.Combine(temp.Path, "multi.zip");
        CreateZip(
            zipPath,
            ("a.iso", BuildIso(2)),
            ("b.iso", BuildIso(2)));

        DiscImageException exception = Assert.Throws<DiscImageException>(
            () => DiscImageFactory.Open(zipPath));
        Assert.Contains("multiple images", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Archive_RejectsChdWithExtractionGuidance()
    {
        using var temp = new TempDirectory();
        var zipPath = Path.Combine(temp.Path, "chd.zip");
        CreateZip(zipPath, ("disc.chd", [1, 2, 3, 4]));

        DiscImageException exception = Assert.Throws<DiscImageException>(
            () => DiscImageFactory.Open(zipPath));
        Assert.Contains("must be extracted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchiveCue_RejectsPregapWithAnActionableMessage()
    {
        using var temp = new TempDirectory();
        var zipPath = Path.Combine(temp.Path, "pregap.zip");
        CreateZip(
            zipPath,
            ("game.bin", BuildBin()),
            ("game.cue", Encoding.UTF8.GetBytes(CueText() + "  PREGAP 00:02:00\n")));

        DiscImageException exception = Assert.Throws<DiscImageException>(
            () => DiscImageFactory.Open(zipPath));
        Assert.Contains("PREGAP", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extract", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncryptedArchive_ReportsAnActionableError()
    {
        var archivePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "archive-encrypted.7z");

        DiscImageException exception = Assert.Throws<DiscImageException>(
            () => DiscImageFactory.Open(archivePath));
        Assert.Contains("encrypted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorruptArchiveDirectory_ReportsADiscImageError()
    {
        var archivePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "archive-corrupt-directory.zip");

        // SharpCompress 1.0.0 throws IndexOutOfRangeException while enumerating this
        // central directory (upstream issue #772). The storage layer must translate it
        // so the library scanner and shell never see a raw parser failure.
        DiscImageException exception = Assert.Throws<DiscImageException>(
            () => DiscImageFactory.Open(archivePath));
        Assert.Contains("Archive", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unreadable directory", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<IndexOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void Archive_IgnoresUnrelatedEntries()
    {
        using var temp = new TempDirectory();
        var zipPath = Path.Combine(temp.Path, "readme.zip");
        CreateZip(
            zipPath,
            ("readme.txt", "not a disc"u8.ToArray()),
            ("disc.iso", BuildIso(4)));

        using IDiscSource source = DiscImageFactory.Open(zipPath);
        Assert.Equal(4, source.SectorCount);
    }

    private static string CueText()
    {
        return "FILE \"game.bin\" BINARY\n"
            + "  TRACK 01 MODE1/2352\n"
            + "    INDEX 01 00:00:00\n"
            + "  TRACK 02 AUDIO\n"
            + "    INDEX 01 00:00:02\n";
    }

    private static byte[] BuildIso(int sectorCount)
    {
        var iso = new byte[sectorCount * IsoImage.SectorBytes];
        for (var lba = 0; lba < sectorCount; lba++)
        {
            for (var i = 0; i < IsoImage.SectorBytes; i++)
            {
                iso[(lba * IsoImage.SectorBytes) + i] = (byte)(((lba * 31) + i) % 251);
            }
        }

        return iso;
    }

    private static byte[] BuildBin()
    {
        var bin = new byte[2352 * 4];
        for (var sector = 0; sector < 4; sector++)
        {
            var offset = sector * 2352;
            if (sector < 2)
            {
                bin[offset] = 0x00;
                for (var i = 1; i <= 10; i++)
                {
                    bin[offset + i] = 0xFF;
                }

                bin[offset + 11] = 0x00;
                bin[offset + 15] = 1;
                for (var i = 0; i < 2048; i++)
                {
                    bin[offset + 16 + i] = (byte)(((sector * 31) + i) % 251);
                }
            }
            else
            {
                for (var i = 0; i < 2352; i++)
                {
                    bin[offset + i] = (byte)(((sector * 47) + i) % 251);
                }
            }
        }

        return bin;
    }

    private static void CreateZip(string path, params (string Name, byte[] Bytes)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((var name, byte[] bytes) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            stream.Write(bytes);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fivedo-archive-{Guid.NewGuid():N}");
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
