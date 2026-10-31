using System;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class NvramLayoutTests
{
    [Fact]
    public void Resolve_GlobalVersionZeroUsesTheLegacyFile()
    {
        using var directory = new TempDirectory();

        var path = NvramLayout.Resolve(directory.Path, NvramStorageMode.Global, 0, null);

        Assert.Equal(Path.Combine(directory.Path, "nvram.ram"), path);
    }

    [Fact]
    public void Resolve_GlobalVersionedFilesAreIndependent()
    {
        using var directory = new TempDirectory();

        var path = NvramLayout.Resolve(directory.Path, NvramStorageMode.Global, 3, null);

        Assert.Equal(Path.Combine(directory.Path, "nvram.3.ram"), path);
    }

    [Fact]
    public void Resolve_PerGameUsesTheGameIdAndVersion()
    {
        using var directory = new TempDirectory();

        var path = NvramLayout.Resolve(
            directory.Path,
            NvramStorageMode.PerGame,
            2,
            "9F7D72BC00112233445566778899AABB");

        Assert.Equal(
            Path.Combine(directory.Path, "nvram", "per-game", "9f7d72bc00112233445566778899aabb.2.ram"),
            path);
    }

    [Fact]
    public void Resolve_PerGameWithoutAGameIdThrows()
    {
        using var directory = new TempDirectory();

        Assert.Throws<ArgumentException>(() =>
            NvramLayout.Resolve(directory.Path, NvramStorageMode.PerGame, 0, " "));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Resolve_RejectsOutOfRangeVersions(int version)
    {
        using var directory = new TempDirectory();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NvramLayout.Resolve(directory.Path, NvramStorageMode.Global, version, null));
    }

    [Fact]
    public void GetFallbackPaths_PerGameCanMigrateFromSharedAndOlderVersions()
    {
        using var directory = new TempDirectory();

        var fallbacks = NvramLayout.GetFallbackPaths(
            directory.Path,
            NvramStorageMode.PerGame,
            2,
            "game");

        Assert.Equal(3, fallbacks.Count);
        Assert.Equal(
            Path.Combine(directory.Path, "nvram", "per-game", "game.0.ram"),
            fallbacks[0]);
        Assert.Equal(Path.Combine(directory.Path, "nvram.2.ram"), fallbacks[1]);
        Assert.Equal(Path.Combine(directory.Path, "nvram.ram"), fallbacks[2]);
    }

    [Fact]
    public void GetFallbackPaths_GlobalVersionCanMigrateFromLegacy()
    {
        using var directory = new TempDirectory();

        var fallbacks = NvramLayout.GetFallbackPaths(
            directory.Path,
            NvramStorageMode.Global,
            4,
            null);

        Assert.Equal(Path.Combine(directory.Path, "nvram.ram"), Assert.Single(fallbacks));
    }

    [Fact]
    public void NormalizeGameId_ProducesASafeLowerCaseName()
    {
        var normalized = NvramLayout.NormalizeGameId(@"..\A B/C");

        Assert.Equal("___a_b_c", normalized);
    }

    [Fact]
    public void TryParseFileName_RecognizesGlobalAndPerGameImages()
    {
        using var directory = new TempDirectory();
        var global = Path.Combine(directory.Path, "nvram.2.ram");
        var legacy = Path.Combine(directory.Path, "nvram.ram");
        var perGame = Path.Combine(directory.Path, "nvram", "per-game", "abc.7.ram");
        Directory.CreateDirectory(Path.GetDirectoryName(perGame)!);
        File.WriteAllBytes(global, new byte[1]);
        File.WriteAllBytes(legacy, new byte[1]);
        File.WriteAllBytes(perGame, new byte[1]);

        Assert.True(NvramLayout.TryParseFileName(global, out NvramFile? globalFile));
        Assert.Equal(NvramStorageMode.Global, globalFile!.Mode);
        Assert.Equal(2, globalFile.Version);

        Assert.True(NvramLayout.TryParseFileName(legacy, out NvramFile? legacyFile));
        Assert.Equal(0, legacyFile!.Version);

        Assert.True(NvramLayout.TryParseFileName(perGame, out NvramFile? perGameFile));
        Assert.Equal(NvramStorageMode.PerGame, perGameFile!.Mode);
        Assert.Equal(7, perGameFile.Version);
        Assert.Equal("abc", perGameFile.GameId);

        Assert.False(NvramLayout.TryParseFileName(Path.Combine(directory.Path, "nvram.ram.bad"), out _));
        Assert.False(NvramLayout.TryParseFileName(Path.Combine(directory.Path, "nvram.10.ram"), out _));
    }

    [Fact]
    public void Enumerate_FindsBothLayoutsAndIgnoresUnrecognizedFiles()
    {
        using var directory = new TempDirectory();
        var perGameDirectory = Path.Combine(directory.Path, "nvram", "per-game");
        Directory.CreateDirectory(perGameDirectory);
        File.WriteAllBytes(Path.Combine(directory.Path, "nvram.ram"), new byte[1]);
        File.WriteAllBytes(Path.Combine(directory.Path, "nvram.4.ram"), new byte[2]);
        File.WriteAllBytes(Path.Combine(perGameDirectory, "game.1.ram"), new byte[3]);
        File.WriteAllBytes(Path.Combine(perGameDirectory, "notes.txt"), new byte[4]);

        var files = NvramLayout.Enumerate(directory.Path);

        Assert.Equal(3, files.Count);
        Assert.Contains(files, file => file.Mode == NvramStorageMode.Global && file.Version == 0);
        Assert.Contains(files, file => file.Mode == NvramStorageMode.Global && file.Version == 4);
        Assert.Contains(files, file => file.Mode == NvramStorageMode.PerGame
            && string.Equals(file.GameId, "game", StringComparison.Ordinal));
    }

    [Fact]
    public void AppSettings_ResolveNvramPathUsesTheConfiguredDirectory()
    {
        using var directory = new TempDirectory();
        var settings = new AppSettings
        {
            NvramDirectory = directory.Path,
            NvramStorageMode = NvramStorageMode.PerGame,
            NvramVersion = 5,
        };

        var path = settings.ResolveNvramPath("abc");

        Assert.Equal(
            Path.Combine(directory.Path, "nvram", "per-game", "abc.5.ram"),
            path);
        using NvramStore store = settings.CreateNvramStore("abc");
        Assert.Equal(
            new[]
            {
                Path.Combine(directory.Path, "nvram", "per-game", "abc.0.ram"),
                Path.Combine(directory.Path, "nvram.5.ram"),
                Path.Combine(directory.Path, "nvram.ram"),
            },
            store.FallbackPaths.ToArray());
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fivedo-nvram-layout-{Guid.NewGuid():N}");
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
