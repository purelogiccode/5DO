using System;
using System.Buffers.Binary;
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
public sealed class BiosImageTests
{
    [Fact]
    public void FromBytes_ComputesMd5AndAnvil()
    {
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[28] = 234;

        var image = BiosImage.FromBytes(bios);

        Assert.True(image.IsAnvil);
        Assert.Equal(30, image.AnvilKind);
        Assert.Equal(Convert.ToHexStringLower(MD5.HashData(bios)), image.Md5);
        Assert.Equal(string.Empty, image.Md5Bios2);
    }

    [Fact]
    public void FromBytes_NonAnvil()
    {
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[28] = 1;

        var image = BiosImage.FromBytes(bios);

        Assert.False(image.IsAnvil);
        Assert.Equal(0, image.AnvilKind);
    }

    [Fact]
    public void FromBytes_RejectsBadSizes()
    {
        Assert.Throws<InvalidDataException>(() => BiosImage.FromBytes(new byte[1024]));
        Assert.Throws<InvalidDataException>(() => BiosImage.FromBytes(
            new byte[EmulatorConstants.RomBankSize],
            new byte[EmulatorConstants.RomBankSize + 1]));
    }

    [Fact]
    public void FromBytes_AcceptsShortBios2()
    {
        var bios2 = new byte[512];

        var image = BiosImage.FromBytes(new byte[EmulatorConstants.RomBankSize], bios2);

        Assert.Equal(512, image.Bios2.Length);
        Assert.NotEqual(string.Empty, image.Md5Bios2);
    }
}

public sealed class NvramImageTests
{
    [Fact]
    public void CreateFresh_WritesVolumeHeader()
    {
        var nvram = NvramImage.CreateFresh();
        var data = nvram.ToArray();

        Assert.Equal(EmulatorConstants.NvramSize, data.Length);
        Assert.Equal(0x01, data[0]);
        Assert.Equal("ZZZZZ", System.Text.Encoding.ASCII.GetString(data, 1, 5));
        Assert.Equal(0x02, data[6]);
        Assert.Equal("nvram", System.Text.Encoding.ASCII.GetString(data, 40, 5));
        Assert.Equal(0xffffffffu, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(72, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(76, 4)));
        Assert.Equal(0x00008000u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(80, 4)));
        Assert.Equal(0xfffffffeu, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(84, 4)));
        Assert.Equal(0x00000084u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(100, 4)));
        Assert.Equal(0x855a02b6u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(132, 4)));
        Assert.Equal(0x00007668u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(164, 4)));
    }

    [Fact]
    public void FromBytes_RejectsBadSize()
    {
        Assert.Throws<InvalidDataException>(() => NvramImage.FromBytes(new byte[100]));
    }

    [Fact]
    public void SaveAndLoad_Roundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-nvram-{Guid.NewGuid():N}.bin");
        try
        {
            var image = NvramImage.CreateFresh();
            image.Save(path);
            var loaded = NvramImage.Load(path);

            Assert.Equal(image.ToArray(), loaded.ToArray());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
