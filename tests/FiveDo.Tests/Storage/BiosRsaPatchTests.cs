using System;
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
public sealed class BiosRsaPatchTests
{
    private static readonly byte[] SearchPattern =
        [0x00, 0x03, 0x13, 0xA0, 0x00, 0x01, 0x11, 0xA0];

    private static readonly byte[] PatchedPattern =
        [0x00, 0x03, 0x13, 0xA0, 0x00, 0x00, 0x11, 0xA0];

    private static readonly byte[] GuardPattern =
        [0x00, 0x02, 0x13, 0xA0, 0x00, 0x01, 0x11, 0xA0];

    [Fact]
    public void CountSites_FindsEveryRetailSiteAndIgnoresTheGuardSite()
    {
        byte[] bios = CreateBiosWithSites(2);

        Assert.Equal(2, BiosRsaPatch.CountSites(bios));
    }

    [Fact]
    public void Apply_PatchesEverySiteAndLeavesTheGuardSiteAlone()
    {
        byte[] bios = CreateBiosWithSites(2);

        var patched = BiosRsaPatch.Apply(bios);

        Assert.Equal(2, patched);
        Assert.Equal(0, BiosRsaPatch.CountSites(bios));
        Assert.True(bios.AsSpan(0x100, SearchPattern.Length).SequenceEqual(PatchedPattern));
        Assert.True(bios.AsSpan(0x200, SearchPattern.Length).SequenceEqual(PatchedPattern));
        Assert.True(bios.AsSpan(0x400, GuardPattern.Length).SequenceEqual(GuardPattern));
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        byte[] bios = CreateBiosWithSites(1);

        Assert.Equal(1, BiosRsaPatch.Apply(bios));
        Assert.Equal(0, BiosRsaPatch.Apply(bios));
    }

    [Fact]
    public void BiosImage_WithoutUnsignedDiscs_KeepsTheSourceBytesAndHash()
    {
        byte[] bios = CreateBiosWithSites(2);

        BiosImage image = BiosImage.FromBytes(bios);

        Assert.False(image.RsaPatched);
        Assert.False(image.RsaDisabled);
        Assert.Equal(2, image.RsaPatchSites);
        Assert.Equal(Md5(bios), image.SourceMd5);
        Assert.Equal(image.SourceMd5, image.Md5);
    }

    [Fact]
    public void BiosImage_WithUnsignedDiscs_PatchesInMemoryAndReportsTheEffectiveHash()
    {
        byte[] retail = CreateBiosWithSites(2);
        byte[] expected = (byte[])retail.Clone();
        BiosRsaPatch.Apply(expected);

        BiosImage image = BiosImage.FromBytes(retail, allowUnsignedDiscs: true, sourceName: "panafz10.bin");

        Assert.True(image.RsaPatched);
        Assert.True(image.RsaDisabled);
        Assert.Equal(2, image.RsaPatchSites);
        Assert.Equal(Md5(retail), image.SourceMd5);
        Assert.Equal(Md5(expected), image.Md5);
        Assert.True(image.Bios1.Span.SequenceEqual(expected));
        // The source array is untouched.
        Assert.True(retail.AsSpan(0x100, SearchPattern.Length).SequenceEqual(SearchPattern));
    }

    [Fact]
    public void BiosImage_WithUnsignedDiscs_ThrowsWhenNoSiteExists()
    {
        byte[] bios = CreateBiosWithSites(0);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => BiosImage.FromBytes(bios, allowUnsignedDiscs: true, sourceName: "unknown.bin"));

        Assert.Contains("RSA check patch site", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CountPatchedSites_FindsAnAlreadyPatchedImage()
    {
        byte[] bios = CreateBiosWithSites(2);
        BiosRsaPatch.Apply(bios);

        Assert.Equal(0, BiosRsaPatch.CountSites(bios));
        Assert.Equal(2, BiosRsaPatch.CountPatchedSites(bios));
    }

    [Fact]
    public void BiosImage_WithUnsignedDiscs_AcceptsAnAlreadyPatchedUnknownImage()
    {
        byte[] bios = CreateBiosWithSites(2);
        BiosRsaPatch.Apply(bios);

        BiosImage image = BiosImage.FromBytes(
            bios,
            allowUnsignedDiscs: true,
            sourceName: "unknown-retail.bin");

        Assert.False(image.RsaPatched);
        Assert.True(image.RsaDisabled);
        Assert.Equal(0, image.RsaPatchSites);
    }

    [Fact]
    public void BiosImage_WithUnsignedDiscs_AcceptsAKnownNoRsaFileNameWithoutASite()
    {
        byte[] bios = CreateBiosWithSites(0);

        BiosImage image = BiosImage.FromBytes(
            bios,
            allowUnsignedDiscs: true,
            sourceName: "panafz10-norsa.bin");

        Assert.False(image.RsaPatched);
        Assert.True(image.RsaDisabled);
        Assert.Equal(0, image.RsaPatchSites);
    }

    [Fact]
    public void DefaultCatalog_MarksTheKnownNoRsaImages()
    {
        Assert.True(BiosCatalog.Default.TryGet("f6c71de7470d16abe4f71b1444883dc8", out BiosDescriptor fz1j));
        Assert.True(fz1j.IsRsaDisabled);
        Assert.True(fz1j.IsPatched);

        Assert.True(BiosCatalog.Default.TryGet("1477bda80dc33731a65468c1f5bcbee9", out BiosDescriptor fz10));
        Assert.True(fz10.IsRsaDisabled);
        Assert.True(fz10.IsPatched);

        Assert.True(BiosCatalog.Default.TryGet("cf11bbb5a16d7af9875cca9de9a15e09", out BiosDescriptor fz10e));
        Assert.True(fz10e.IsRsaDisabled);
        Assert.True(fz10e.IsAnvil);

        Assert.True(BiosCatalog.Default.TryGet("f47264dd47fe30f73ab3c010015c155b", out BiosDescriptor retail));
        Assert.False(retail.IsRsaDisabled);
    }

    [Fact]
    public void LocalRetailFz10_PatchedAtRuntime_MatchesTheKnownNoRsaImage()
    {
        var retailPath = Path.Combine(RepositoryRoot(), "bios", "panafz10.bin");
        if (!File.Exists(retailPath))
        {
            return;
        }

        BiosImage image = BiosImage.Load(retailPath, allowUnsignedDiscs: true);

        Assert.True(image.RsaPatched);
        Assert.Equal("51f2f43ae2f3508a14d9f56597e2d3ce", image.SourceMd5);
        Assert.Equal("1477bda80dc33731a65468c1f5bcbee9", image.Md5);
    }

    [Fact]
    public void LocalRetailFz10e_PatchedAtRuntime_MatchesTheKnownNoRsaImage()
    {
        var retailPath = Path.Combine(RepositoryRoot(), "bios", "panafz10e-anvil.bin");
        if (!File.Exists(retailPath))
        {
            return;
        }

        BiosImage image = BiosImage.Load(retailPath, allowUnsignedDiscs: true);

        Assert.True(image.RsaPatched);
        Assert.Equal("a48e6746bd7edec0f40cff078f0bb19f", image.SourceMd5);
        Assert.Equal("cf11bbb5a16d7af9875cca9de9a15e09", image.Md5);
    }

    private static byte[] CreateBiosWithSites(int sites)
    {
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[28] = 1;
        for (var i = 0; i < sites; i++)
        {
            SearchPattern.CopyTo(bios, 0x100 + (i * 0x100));
        }

        GuardPattern.CopyTo(bios, 0x400);
        return bios;
    }

    private static string Md5(byte[] bytes)
    {
        return Convert.ToHexStringLower(MD5.HashData(bytes));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "5DO.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
