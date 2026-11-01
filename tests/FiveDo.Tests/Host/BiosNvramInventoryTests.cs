using System;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Storage;

namespace FiveDo.Tests.Host;

public sealed class BiosNvramInventoryTests
{
    [Fact]
    public void Import_CopiesAndClassifiesABiosImage()
    {
        using var source = new TempDirectory();
        using var destination = new TempDirectory();
        var sourcePath = WriteBios(source.Path, "panafz1.bin", fill: 0x11);

        BiosImportResult result = BiosInventory.Import(sourcePath, destination.Path);

        Assert.True(result.Copied);
        Assert.True(File.Exists(result.Path));
        Assert.Equal(Path.Combine(destination.Path, "panafz1.bin"), result.Path);
        Assert.Equal(BiosImageKind.Primary, result.Profile.Kind);
        Assert.Equal(BiosLibrary.Describe(sourcePath).Md5, result.Profile.Md5);

        BiosLibrary library = BiosInventory.Scan(destination.Path);
        Assert.Single(library.Profiles);
        Assert.Single(library.Primaries);
    }

    [Fact]
    public void Import_ReusesAnImageWithTheSameMd5()
    {
        using var source = new TempDirectory();
        using var destination = new TempDirectory();
        var sourcePath = WriteBios(source.Path, "bios.bin", fill: 0x22);

        BiosImportResult first = BiosInventory.Import(sourcePath, destination.Path);
        BiosImportResult second = BiosInventory.Import(sourcePath, destination.Path);

        Assert.True(first.Copied);
        Assert.False(second.Copied);
        Assert.Equal(first.Path, second.Path);
        Assert.Single(Directory.EnumerateFiles(destination.Path, "*.bin"));
    }

    [Fact]
    public void Import_UsesAnMd5SuffixWhenTheNameAlreadyExists()
    {
        using var firstSource = new TempDirectory();
        using var secondSource = new TempDirectory();
        using var destination = new TempDirectory();
        var first = WriteBios(firstSource.Path, "bios.bin", fill: 0x33);
        var second = WriteBios(secondSource.Path, "bios.bin", fill: 0x44);

        BiosImportResult firstResult = BiosInventory.Import(first, destination.Path);
        BiosImportResult secondResult = BiosInventory.Import(second, destination.Path);

        Assert.True(firstResult.Copied);
        Assert.True(secondResult.Copied);
        Assert.NotEqual(firstResult.Path, secondResult.Path, StringComparer.OrdinalIgnoreCase);
        Assert.True(File.Exists(firstResult.Path));
        Assert.True(File.Exists(secondResult.Path));
        Assert.Contains(secondResult.Profile.Md5[..8], Path.GetFileName(secondResult.Path), StringComparison.Ordinal);
    }

    [Fact]
    public void Import_RejectsAnOversizedImage()
    {
        using var source = new TempDirectory();
        using var destination = new TempDirectory();
        var path = Path.Combine(source.Path, "too-big.bin");
        File.WriteAllBytes(path, new byte[EmulatorConstants.RomBankSize + 1]);

        Assert.Throws<InvalidDataException>(() => BiosInventory.Import(path, destination.Path));
    }

    [Fact]
    public void Import_CreatesTheDestinationFolder()
    {
        using var source = new TempDirectory();
        using var root = new TempDirectory();
        var sourcePath = WriteBios(source.Path, "bios.rom", fill: 0x55);
        var folder = Path.Combine(root.Path, "nested", "bios");

        BiosImportResult result = BiosInventory.Import(sourcePath, folder);

        Assert.True(Directory.Exists(folder));
        Assert.True(File.Exists(result.Path));
        Assert.EndsWith(".rom", result.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidCharactersAndReservedNames()
    {
        var invalid = Path.GetInvalidFileNameChars()[0];
        var sanitized = BiosInventory.SanitizeFileName($"bad{invalid}name");
        Assert.DoesNotContain(invalid, sanitized);

        Assert.Equal("_CON.bin", BiosInventory.SanitizeFileName("CON.bin"));
        Assert.Equal("bios", BiosInventory.SanitizeFileName("..."));
    }

    [Fact]
    public void SuggestFileName_DoesNotReturnADirectory()
    {
        using var source = new TempDirectory();
        using var destination = new TempDirectory();
        var profile = BiosLibrary.Describe(WriteBios(source.Path, "my bios.bin", fill: 0x66));
        var name = BiosInventory.SuggestFileName(destination.Path, profile);
        Assert.Equal("my bios.bin", name);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, name);
    }

    [Fact]
    public void NvramEnumerate_ValidatesAndLabelsSharedAndPerGameImages()
    {
        using var root = new TempDirectory();
        NvramImage.CreateFresh().Save(Path.Combine(root.Path, NvramLayout.LegacyFileName));
        var perGameDirectory = Path.Combine(root.Path, "nvram", NvramLayout.PerGameDirectoryName);
        Directory.CreateDirectory(perGameDirectory);
        NvramImage.CreateFresh().Save(Path.Combine(perGameDirectory, "abc123.2.ram"));

        var entries = NvramInventory.Enumerate(root.Path);

        Assert.Equal(2, entries.Count);
        var shared = Assert.Single(entries, entry => entry.File.Mode == NvramStorageMode.Global);
        Assert.True(shared.IsValid);
        Assert.Equal("Shared", shared.GameLabel);
        Assert.Equal(EmulatorConstants.NvramSize, shared.File.Length);

        var perGame = Assert.Single(entries, entry => entry.File.Mode == NvramStorageMode.PerGame);
        Assert.True(perGame.IsValid);
        Assert.Equal(2, perGame.File.Version);
        Assert.Equal("abc123", perGame.File.GameId);
        Assert.Equal("abc123", perGame.GameLabel);
    }

    [Fact]
    public void NvramEnumerate_ReportsInvalidImagesWithoutThrowing()
    {
        using var root = new TempDirectory();
        File.WriteAllBytes(Path.Combine(root.Path, NvramLayout.LegacyFileName), new byte[17]);

        var entry = Assert.Single(NvramInventory.Enumerate(root.Path));

        Assert.False(entry.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(entry.Error));
        Assert.StartsWith("invalid:", entry.StateLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void NvramBackupAndImport_RoundTripTheImage()
    {
        using var root = new TempDirectory();
        using var backup = new TempDirectory();
        var source = Path.Combine(root.Path, NvramLayout.LegacyFileName);
        NvramImage.CreateFresh().Save(source);
        var backupPath = Path.Combine(backup.Path, "copy.ram");

        var returned = NvramInventory.Backup(source, backupPath);
        Assert.Equal(Path.GetFullPath(backupPath), returned);
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(backupPath));

        var replacement = NvramImage.CreateFresh().ToArray();
        replacement[100] = 0x7F;
        var replacementPath = Path.Combine(backup.Path, "replacement.ram");
        File.WriteAllBytes(replacementPath, replacement);

        var imported = NvramInventory.Import(replacementPath, source);
        Assert.Equal(Path.GetFullPath(source), imported);
        Assert.Equal(replacement, File.ReadAllBytes(source));
    }

    [Fact]
    public void NvramImport_RejectsANonNvramFile()
    {
        using var root = new TempDirectory();
        var invalid = Path.Combine(root.Path, "bad.ram");
        File.WriteAllBytes(invalid, new byte[123]);

        Assert.Throws<InvalidDataException>(
            () => NvramInventory.Import(invalid, Path.Combine(root.Path, "nvram.ram")));
    }

    [Fact]
    public void NvramBackupAll_PreservesTheLayout()
    {
        using var root = new TempDirectory();
        using var destination = new TempDirectory();
        NvramImage.CreateFresh().Save(Path.Combine(root.Path, NvramLayout.LegacyFileName));
        var perGameDirectory = Path.Combine(root.Path, "nvram", NvramLayout.PerGameDirectoryName);
        Directory.CreateDirectory(perGameDirectory);
        NvramImage.CreateFresh().Save(Path.Combine(perGameDirectory, "abc.1.ram"));

        var count = NvramInventory.BackupAll(root.Path, destination.Path);

        Assert.Equal(2, count);
        Assert.True(File.Exists(Path.Combine(destination.Path, NvramLayout.LegacyFileName)));
        Assert.True(File.Exists(Path.Combine(destination.Path, "nvram", NvramLayout.PerGameDirectoryName, "abc.1.ram")));
    }

    [Fact]
    public void NvramReset_WritesAFreshVolume()
    {
        using var root = new TempDirectory();
        var path = Path.Combine(root.Path, NvramLayout.LegacyFileName);
        File.WriteAllBytes(path, new byte[EmulatorConstants.NvramSize]);

        var returned = NvramInventory.Reset(path);

        Assert.Equal(Path.GetFullPath(path), returned);
        NvramImage image = NvramImage.Load(path);
        Assert.Equal(EmulatorConstants.NvramSize, image.Data.Length);
        Assert.Equal((byte)'Z', image.Data.Span[1]);
    }

    [Fact]
    public void NvramDelete_RemovesOnlyAnExistingImage()
    {
        using var root = new TempDirectory();
        var path = Path.Combine(root.Path, NvramLayout.LegacyFileName);
        NvramImage.CreateFresh().Save(path);

        Assert.True(NvramInventory.Delete(path));
        Assert.False(File.Exists(path));
        Assert.False(NvramInventory.Delete(path));
    }

    [Fact]
    public void NvramSuggestions_AreSafeAndDescriptive()
    {
        var file = new NvramFile(
            "C:/states/nvram/per-game/ABC123.4.ram",
            NvramStorageMode.PerGame,
            4,
            "abc123",
            EmulatorConstants.NvramSize,
            DateTime.UtcNow);
        var name = NvramInventory.SuggestBackupFileName(file, new DateTimeOffset(2026, 11, 1, 12, 34, 56, TimeSpan.Zero));
        Assert.Equal("nvram-abc123-v4-20261101-123456.ram", name);
        Assert.Equal(
            "nvram-backup-20261101-123456",
            NvramInventory.SuggestBackupFolderName(new DateTimeOffset(2026, 11, 1, 12, 34, 56, TimeSpan.Zero)));
    }

    private static string WriteBios(string folder, string name, byte fill)
    {
        var path = Path.Combine(folder, name);
        var bytes = Enumerable.Repeat(fill, EmulatorConstants.RomBankSize).ToArray();
        bytes[28] = 0;
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "fivedo-bios-nvram-tests",
                Guid.NewGuid().ToString("N"));
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
                // Test cleanup is best effort.
            }
        }
    }
}
