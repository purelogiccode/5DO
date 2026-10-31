using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Storage;

namespace FiveDo.Tests.Host;

[SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "MD5 is the 3DO BIOS compatibility identifier under test.")]
public sealed class BiosResolverTests
{
    [Fact]
    public void Resolve_AutoSelectsARegionAppropriateBios()
    {
        using var folder = new TempFolder();
        byte[] usa = CreateBios(anvil: false);
        byte[] europe = CreateBios(anvil: true);
        folder.Write("panafz1.bin", usa);
        folder.Write("panafz10e-anvil.bin", europe);
        var settings = new AppSettings
        {
            BiosFolder = folder.Path,
            AutoBios = true,
        };

        ResolvedBios result = BiosResolver.Resolve(
            settings,
            "TEST0001",
            Game("Test", "EU"));

        Assert.True(result.HasPrimary);
        Assert.Equal("auto", result.Source);
        Assert.EndsWith("panafz10e-anvil.bin", result.PrimaryPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EU", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_UsesTheConfiguredFallbackWhenAutoIsOff()
    {
        using var folder = new TempFolder();
        folder.Write("panafz1.bin", CreateBios(anvil: false));
        var settings = new AppSettings
        {
            BiosFolder = folder.Path,
            BiosPath = Path.Combine(folder.Path, "panafz1.bin"),
            AutoBios = false,
        };

        ResolvedBios result = BiosResolver.Resolve(settings, "TEST0001", Game("Test", "EU"));

        Assert.Equal("configured", result.Source);
        Assert.Equal(settings.BiosPath, result.PrimaryPath);
    }

    [Fact]
    public void Resolve_AutoNeedsAnExplicitBiosFolder()
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "panafz1.bin");
        folder.Write("panafz1.bin", CreateBios(anvil: false));
        var settings = new AppSettings
        {
            BiosPath = path,
            AutoBios = true,
        };

        ResolvedBios result = BiosResolver.Resolve(settings, "TEST0001", Game("Test", "EU"));

        Assert.Equal("configured", result.Source);
        Assert.Equal(path, result.PrimaryPath);
    }

    [Fact]
    public void Resolve_UsesAPerGameOverride()
    {
        using var folder = new TempFolder();
        byte[] usa = CreateBios(anvil: false);
        byte[] japan = CreateBios(anvil: true);
        folder.Write("usa.bin", usa);
        folder.Write("japan.bin", japan);
        var settings = new AppSettings
        {
            BiosFolder = folder.Path,
            AutoBios = true,
        };
        settings.SetBiosOverride("TEST0001", Md5(japan));

        ResolvedBios result = BiosResolver.Resolve(settings, "TEST0001", Game("Test", "US"));

        Assert.Equal("override", result.Source);
        Assert.EndsWith("japan.bin", result.PrimaryPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_MissingOverrideWarnsAndFallsBack()
    {
        using var folder = new TempFolder();
        folder.Write("panafz1.bin", CreateBios(anvil: false));
        var settings = new AppSettings
        {
            BiosFolder = folder.Path,
            BiosPath = Path.Combine(folder.Path, "panafz1.bin"),
            AutoBios = false,
        };
        settings.SetBiosOverride("TEST0001", "ffffffffffffffffffffffffffffffff");

        ResolvedBios result = BiosResolver.Resolve(settings, "TEST0001", Game("Test", "US"));

        Assert.Equal("configured", result.Source);
        Assert.Contains(result.Warnings, warning => warning.Contains("override", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_NoBios_ReturnsAnExplanation()
    {
        var settings = new AppSettings { AutoBios = false };

        ResolvedBios result = BiosResolver.Resolve(settings, null, null);

        Assert.False(result.HasPrimary);
        Assert.Equal("none", result.Source);
        Assert.Contains("No usable BIOS", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_BiosOverrideNormalizesAndRemoves()
    {
        var settings = new AppSettings();

        settings.SetBiosOverride("abcdef12", "AABBCCDD");
        Assert.Equal("aabbccdd", settings.GetBiosOverride("ABCDEF12"));

        settings.SetBiosOverride("abcdef12", null);
        Assert.Null(settings.GetBiosOverride("abcdef12"));
    }

    [Fact]
    public void AppSettingsStore_RoundtripsBiosSettings()
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "settings.json");
        var settings = new AppSettings
        {
            BiosPath = "bios1.bin",
            Bios2Path = "kanji.bin",
            BiosFolder = "bios",
            AutoBios = false,
        };
        settings.SetBiosOverride("TEST0001", "AABBCCDD");

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal("bios1.bin", loaded.BiosPath);
        Assert.Equal("kanji.bin", loaded.Bios2Path);
        Assert.Equal("bios", loaded.BiosFolder);
        Assert.False(loaded.AutoBios);
        Assert.Equal("aabbccdd", loaded.GetBiosOverride("TEST0001"));
    }

    private static GameRecord Game(string name, string regions)
    {
        return new GameRecord("TEST0001", "checksum", name, "1994", "Publisher", regions);
    }

    private static byte[] CreateBios(bool anvil)
    {
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[0] = 1;
        bios[28] = anvil ? (byte)234 : (byte)1;
        return bios;
    }

    private static string Md5(byte[] bytes)
    {
        return Convert.ToHexStringLower(MD5.HashData(bytes));
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fivedo-bios-resolver-{Guid.NewGuid():N}");
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
