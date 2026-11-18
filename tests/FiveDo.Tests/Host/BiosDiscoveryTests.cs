using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class BiosDiscoveryTests
{
    [Fact]
    public void Discover_FindsPrimaryAndSecondaryImages()
    {
        using var folder = new TempFolder();
        folder.Write("panafz1.bin", CreateBios());
        folder.Write("panafz1j-kanji.bin", CreateSecondary());

        BiosDiscoveryResult result = BiosDiscovery.Discover(
            [new BiosDiscoveryLocation(folder.Path, "test")]);

        Assert.True(result.HasBios);
        Assert.Single(result.Folders);
        Assert.Single(result.Folders[0].Primaries);
        Assert.Single(result.Folders[0].Secondaries);
        Assert.Equal("panafz1.bin", Path.GetFileName(result.PreferredPrimary!.Path));
        Assert.Equal("test", result.Folders[0].Source);
        Assert.Equal(folder.Path, result.Folders[0].Path);
    }

    [Fact]
    public void Discover_ReportsMissingFoldersWithoutThrowing()
    {
        using var folder = new TempFolder();
        var missing = Path.Combine(folder.Path, "nope");

        BiosDiscoveryResult result = BiosDiscovery.Discover(
            [
                new BiosDiscoveryLocation(missing, "missing"),
                new BiosDiscoveryLocation(folder.Path, "empty"),
            ]);

        Assert.False(result.HasBios);
        Assert.Equal(2, result.Folders.Count);
        Assert.False(result.Folders[0].Exists);
        Assert.True(result.Folders[1].Exists);
        Assert.Empty(result.Folders[1].Primaries);
        Assert.Single(result.ExistingFolders);
    }

    [Fact]
    public void Discover_PrefersTheFirstFolderWithAPrimary()
    {
        using var first = new TempFolder();
        using var second = new TempFolder();
        first.Write("panafz1.bin", CreateBios());
        second.Write("panafz10.bin", CreateBios());

        BiosDiscoveryResult result = BiosDiscovery.Discover(
            [
                new BiosDiscoveryLocation(first.Path, "first"),
                new BiosDiscoveryLocation(second.Path, "second"),
            ]);

        Assert.Equal(first.Path, Path.GetDirectoryName(result.PreferredPrimary!.Path));
    }

    [Fact]
    public void DefaultFolders_PutsTheOverrideFirstAndDeduplicates()
    {
        using var folder = new TempFolder();
        var biosFolder = Path.Combine(folder.Path, "bios");

        var folders = BiosDiscovery.DefaultFolders(
            overrideDirectory: biosFolder,
            dataDirectory: folder.Path,
            applicationDirectory: folder.Path,
            userProfile: folder.Path,
            documentsDirectory: folder.Path);

        Assert.Equal(biosFolder, folders[0].Path);
        Assert.Equal(BiosDiscovery.BiosDirectoryEnvironmentVariable, folders[0].Source);
        Assert.Single(folders, candidate => string.Equals(
            candidate.Path,
            biosFolder,
            StringComparison.Ordinal));
    }

    private static byte[] CreateBios()
    {
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[0] = 1;
        bios[28] = 1;
        return bios;
    }

    private static byte[] CreateSecondary()
    {
        var font = new byte[EmulatorConstants.RomBankSize];
        font[0] = 0x01;
        for (var i = 1; i <= 5; i++)
        {
            font[i] = 0x5A;
        }

        font[6] = 0x01;
        return font;
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fivedo-bios-discovery-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Write(string name, byte[] bytes)
        {
            File.WriteAllBytes(System.IO.Path.Combine(Path, name), bytes);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
