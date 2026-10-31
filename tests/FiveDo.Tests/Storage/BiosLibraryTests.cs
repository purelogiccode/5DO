using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

[SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "MD5 is the 3DO BIOS compatibility identifier under test.")]
public sealed class BiosLibraryTests
{
    [Fact]
    public void DefaultCatalog_KnowsTheCommonBiosImages()
    {
        Assert.True(BiosCatalog.Default.TryGet("F47264DD47FE30F73AB3C010015C155B", out BiosDescriptor fz1));
        Assert.Equal("Panasonic FZ-1", fz1.Name);
        Assert.Equal(BiosTerritory.Usa, fz1.Territory);
        Assert.Equal(BiosVideoStandard.Ntsc, fz1.VideoStandard);

        Assert.True(BiosCatalog.Default.TryGet("A48E6746BD7EDEC0F40CFF078F0BB19F", out BiosDescriptor fz10e));
        Assert.True(fz10e.IsAnvil);
        Assert.Equal(BiosVideoStandard.Pal, fz10e.VideoStandard);

        Assert.True(BiosCatalog.Default.TryGet("428577250F43EDC902EA239C50D2240D", out BiosDescriptor fz10ja));
        Assert.True(fz10ja.IsKanji);
        Assert.Equal(BiosTerritory.Japan, fz10ja.Territory);
    }

    [Fact]
    public void Scan_ClassifiesPrimarySecondaryAndInvalidFiles()
    {
        using var folder = new TempFolder();
        byte[] primary = CreateBios();
        byte[] secondary = CreateSecondary();
        folder.Write("fz1.bin", primary);
        folder.Write("kanji.bin", secondary);
        folder.Write("oversized.bin", new byte[EmulatorConstants.RomBankSize + 1]);

        BiosCatalog catalog = CreateCatalog(primary, "Test FZ-1", BiosImageKind.Primary, BiosTerritory.Usa, BiosVideoStandard.Ntsc, isKanji: false);
        catalog.Add(
            Md5(secondary),
            new BiosDescriptor(
                "Test Kanji",
                BiosImageKind.Secondary,
                BiosTerritory.Japan,
                BiosVideoStandard.Ntsc,
                IsKanji: true,
                IsAnvil: false,
                IsPatched: false,
                Notes: "Font",
                Preference: 10));

        BiosLibrary library = BiosLibrary.Scan(folder.Path, catalog);

        Assert.Equal(2, library.Profiles.Count);
        Assert.Single(library.Primaries);
        Assert.Single(library.Secondaries);
        Assert.Single(library.Errors);
        Assert.Contains("oversized.bin", library.Errors[0], StringComparison.Ordinal);
        Assert.Equal("Test FZ-1", library.Primaries[0].Name);
        Assert.True(library.Secondaries[0].IsKanji);
    }

    [Fact]
    public void Describe_DetectsAnvilAndInfersMetadataFromFileName()
    {
        using var folder = new TempFolder();
        byte[] pal = CreateBios(anvil: true);
        folder.Write("panafz10e-anvil.bin", pal);
        folder.Write("panafz1-kanji.bin", CreateSecondary());
        folder.Write("mystery.bin", CreateBios());

        BiosProfile palProfile = BiosLibrary.Describe(folder.Path + Path.DirectorySeparatorChar + "panafz10e-anvil.bin");
        Assert.Equal(BiosImageKind.Primary, palProfile.Kind);
        Assert.Equal(BiosTerritory.Europe, palProfile.Territory);
        Assert.Equal(BiosVideoStandard.Pal, palProfile.VideoStandard);
        Assert.True(palProfile.IsAnvil);
        Assert.Contains("Unrecognized", palProfile.Notes, StringComparison.Ordinal);

        BiosProfile kanji = BiosLibrary.Describe(folder.Path + Path.DirectorySeparatorChar + "panafz1-kanji.bin");
        Assert.Equal(BiosImageKind.Secondary, kanji.Kind);
        Assert.True(kanji.IsKanji);
        Assert.Equal(BiosTerritory.Japan, kanji.Territory);

        BiosProfile mystery = BiosLibrary.Describe(folder.Path + Path.DirectorySeparatorChar + "mystery.bin");
        Assert.Equal(BiosTerritory.Unknown, mystery.Territory);
        Assert.Equal(BiosVideoStandard.Unknown, mystery.VideoStandard);
    }

    [Fact]
    public void Describe_RejectsUnsupportedSizes()
    {
        using var folder = new TempFolder();
        folder.Write("empty.bin", []);
        folder.Write("huge.bin", new byte[EmulatorConstants.RomBankSize + 1]);

        Assert.Throws<InvalidDataException>(() =>
            BiosLibrary.Describe(folder.Path + Path.DirectorySeparatorChar + "empty.bin"));
        Assert.Throws<InvalidDataException>(() =>
            BiosLibrary.Describe(folder.Path + Path.DirectorySeparatorChar + "huge.bin"));
    }

    [Fact]
    public void SelectForGame_PrefersTheGameRegion()
    {
        using var folder = new TempFolder();
        byte[] usa = CreateBios(marker: 1);
        byte[] japan = CreateBios(anvil: true, marker: 2);
        byte[] europe = CreateBios(marker: 3);
        folder.Write("usa.bin", usa);
        folder.Write("japan.bin", japan);
        folder.Write("europe.bin", europe);
        BiosCatalog catalog = CreateCatalog(usa, "US BIOS", BiosImageKind.Primary, BiosTerritory.Usa, BiosVideoStandard.Ntsc, isKanji: false, preference: 30);
        catalog.Add(Md5(japan), Descriptor("JP BIOS", BiosTerritory.Japan, BiosVideoStandard.Ntsc, isKanji: true, isAnvil: true, preference: 40));
        catalog.Add(Md5(europe), Descriptor("EU BIOS", BiosTerritory.Europe, BiosVideoStandard.Pal, isKanji: false, isAnvil: true, preference: 40));
        BiosLibrary library = BiosLibrary.Scan(folder.Path, catalog);

        BiosSelection jp = library.SelectForGame(Game("Test", "JP"));
        BiosSelection eu = library.SelectForGame(Game("Test", "EU"));
        BiosSelection us = library.SelectForGame(Game("Test", "US"));

        Assert.Equal("JP BIOS", jp.Primary?.Name);
        Assert.Equal("EU BIOS", eu.Primary?.Name);
        Assert.Equal("US BIOS", us.Primary?.Name);
    }

    [Fact]
    public void SelectForGame_UsesPatchedBiosOnlyForArcadeDiscs()
    {
        using var folder = new TempFolder();
        byte[] retail = CreateBios();
        byte[] patched = CreateBios(anvil: false, marker: 7);
        folder.Write("retail.bin", retail);
        folder.Write("patched.bin", patched);
        BiosCatalog catalog = CreateCatalog(retail, "Retail", BiosImageKind.Primary, BiosTerritory.Usa, BiosVideoStandard.Ntsc, isKanji: false, preference: 30);
        catalog.Add(Md5(patched), Descriptor("Patched", BiosTerritory.Usa, BiosVideoStandard.Ntsc, isKanji: false, isAnvil: false, preference: 50, isPatched: true));
        BiosLibrary library = BiosLibrary.Scan(folder.Path, catalog);

        BiosSelection normal = library.SelectForGame(Game("Crime Patrol (USA)", string.Empty));
        BiosSelection arcade = library.SelectForGame(Game("Crime Patrol (USA) (Arcade CD-ROM)", string.Empty));

        Assert.Equal("Retail", normal.Primary?.Name);
        Assert.Equal("Patched", arcade.Primary?.Name);
    }

    [Fact]
    public void SelectForGame_HonorsThePreferredOverride()
    {
        using var folder = new TempFolder();
        byte[] usa = CreateBios();
        byte[] japan = CreateBios(anvil: true);
        folder.Write("usa.bin", usa);
        folder.Write("japan.bin", japan);
        BiosCatalog catalog = CreateCatalog(usa, "US BIOS", BiosImageKind.Primary, BiosTerritory.Usa, BiosVideoStandard.Ntsc, isKanji: false);
        catalog.Add(Md5(japan), Descriptor("JP BIOS", BiosTerritory.Japan, BiosVideoStandard.Ntsc, isKanji: true, isAnvil: true, preference: 40));
        BiosLibrary library = BiosLibrary.Scan(folder.Path, catalog);

        BiosSelection selection = library.SelectForGame(
            Game("Test", "US"),
            new BiosSelectionOptions(PreferredMd5: Md5(japan)));

        Assert.Equal("JP BIOS", selection.Primary?.Name);
        Assert.Contains("override", selection.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectForGame_PairsAJapaneseFontWhenNeeded()
    {
        using var folder = new TempFolder();
        byte[] usa = CreateBios();
        byte[] font = CreateSecondary();
        folder.Write("usa.bin", usa);
        folder.Write("kanji.bin", font);
        BiosCatalog catalog = CreateCatalog(usa, "US BIOS", BiosImageKind.Primary, BiosTerritory.Usa, BiosVideoStandard.Ntsc, isKanji: false);
        catalog.Add(Md5(font), Descriptor("Kanji font", BiosTerritory.Japan, BiosVideoStandard.Ntsc, isKanji: true, isAnvil: false, preference: 30, kind: BiosImageKind.Secondary));
        BiosLibrary library = BiosLibrary.Scan(folder.Path, catalog);

        BiosSelection selection = library.SelectForGame(Game("Japanese title", "JP"));

        Assert.NotNull(selection.Primary);
        Assert.NotNull(selection.Secondary);
        Assert.True(selection.Secondary!.IsKanji);
    }

    [Fact]
    public void SelectForGame_RequireKanjiWithoutAKanjiBios_ReturnsNone()
    {
        using var folder = new TempFolder();
        byte[] usa = CreateBios();
        folder.Write("usa.bin", usa);
        BiosCatalog catalog = CreateCatalog(usa, "US BIOS", BiosImageKind.Primary, BiosTerritory.Usa, BiosVideoStandard.Ntsc, isKanji: false);
        BiosLibrary library = BiosLibrary.Scan(folder.Path, catalog);

        BiosSelection selection = library.SelectForGame(
            Game("Japanese title", "JP"),
            new BiosSelectionOptions(RequireKanji: true));

        Assert.False(selection.HasBios);
        Assert.Contains("Kanji", selection.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectForGame_NoBios_ReturnsAnExplanation()
    {
        using var folder = new TempFolder();
        BiosLibrary library = BiosLibrary.Scan(folder.Path, new BiosCatalog());

        BiosSelection selection = library.SelectForGame(Game("Test", "US"));

        Assert.False(selection.HasBios);
        Assert.Contains("No primary BIOS", selection.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DesiredTerritories_ParsesRegionStrings()
    {
        Assert.Equal([BiosTerritory.Europe, BiosTerritory.Usa], BiosLibrary.DesiredTerritories("EU-US"));
        Assert.Equal([BiosTerritory.Japan], BiosLibrary.DesiredTerritories("JP"));
        Assert.Equal([BiosTerritory.Unknown], BiosLibrary.DesiredTerritories(null));
    }

    [Fact]
    public void BiosGameRequirements_KnowsTheDocumentedTitles()
    {
        Assert.Equal(
            "f47264dd47fe30f73ab3c010015c155b",
            BiosGameRequirements.PreferredMd5(null, "Slayer (Europe).chd"));
        Assert.Equal(
            "51f2f43ae2f3508a14d9f56597e2d3ce",
            BiosGameRequirements.PreferredMd5(null, "Slayer (USA).chd"));
        Assert.Equal(
            "a48e6746bd7edec0f40cff078f0bb19f",
            BiosGameRequirements.PreferredMd5(null, "Powers Kingdom (Europe).chd"));
        Assert.Null(BiosGameRequirements.PreferredMd5(null, "Lemmings (USA).chd"));
    }

    private static GameRecord Game(string name, string regions)
    {
        return new GameRecord("TEST0001", "checksum", name, "1994", "Publisher", regions);
    }

    private static byte[] CreateBios(bool anvil = false, byte marker = 0)
    {
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[0] = marker;
        bios[28] = anvil ? (byte)234 : (byte)1;
        return bios;
    }

    private static byte[] CreateSecondary()
    {
        var data = new byte[512];
        data[0] = 0x4B;
        return data;
    }

    private static BiosCatalog CreateCatalog(
        byte[] bytes,
        string name,
        BiosImageKind kind,
        BiosTerritory territory,
        BiosVideoStandard standard,
        bool isKanji,
        int preference = 30)
    {
        var catalog = new BiosCatalog();
        catalog.Add(Md5(bytes), Descriptor(name, territory, standard, isKanji, isAnvil: false, preference, kind: kind));
        return catalog;
    }

    private static BiosDescriptor Descriptor(
        string name,
        BiosTerritory territory,
        BiosVideoStandard standard,
        bool isKanji,
        bool isAnvil,
        int preference,
        bool isPatched = false,
        BiosImageKind kind = BiosImageKind.Primary)
    {
        return new BiosDescriptor(
            name,
            kind,
            territory,
            standard,
            isKanji,
            isAnvil,
            isPatched,
            Notes: "Test BIOS",
            preference);
    }

    private static string Md5(byte[] bytes)
    {
        return Convert.ToHexStringLower(MD5.HashData(bytes));
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fivedo-bios-{Guid.NewGuid():N}");
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
