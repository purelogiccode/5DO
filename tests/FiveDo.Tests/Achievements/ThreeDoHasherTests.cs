using System;
using System.Security.Cryptography;
using FiveDo.Achievements;
using FiveDo.Core;

namespace FiveDo.Tests.Achievements;

public sealed class ThreeDoHasherTests
{
    [Fact]
    public void HashesOperaFsLaunchMe()
    {
        byte[][] sectors = BuildSyntheticDisc(launchMe: "LaunchMe", fileSize: 3000);
        var disc = new MemoryDisc(sectors);

        Assert.True(ThreeDoHasher.TryHash(disc, out string hash));
        Assert.Equal(ExpectedHash(sectors, 3000), hash);
        Assert.Equal(32, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
    }

    [Fact]
    public void LaunchMeMatchIsCaseInsensitive()
    {
        byte[][] sectors = BuildSyntheticDisc(launchMe: "launchMe", fileSize: 3000);
        var disc = new MemoryDisc(sectors);

        Assert.True(ThreeDoHasher.TryHash(disc, out string hash));
        Assert.Equal(ExpectedHash(sectors, 3000), hash);
    }

    [Fact]
    public void RejectsNonThreeDoDisc()
    {
        byte[][] sectors = BuildSyntheticDisc(launchMe: "LaunchMe", fileSize: 3000);
        sectors[0][0] = 0x02;
        var disc = new MemoryDisc(sectors);

        Assert.False(ThreeDoHasher.TryHash(disc, out string hash));
        Assert.Empty(hash);
    }

    [Fact]
    public void RejectsMissingLaunchMe()
    {
        byte[][] sectors = BuildSyntheticDisc(launchMe: "Other", fileSize: 3000);
        var disc = new MemoryDisc(sectors);

        Assert.False(ThreeDoHasher.TryHash(disc, out _));
    }

    private static string ExpectedHash(byte[][] sectors, int fileSize)
    {
        using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(sectors[0], 0, 132);

        int remaining = fileSize;
        int sector = 2;
        while (remaining > 2048)
        {
            md5.AppendData(sectors[sector++]);
            remaining -= 2048;
        }

        md5.AppendData(sectors[sector], 0, remaining);
        return Convert.ToHexStringLower(md5.GetHashAndReset());
    }

    private static byte[][] BuildSyntheticDisc(string launchMe, int fileSize)
    {
        var sectors = new byte[4][];
        for (int index = 0; index < sectors.Length; index++)
        {
            sectors[index] = new byte[2048];
            for (int b = 0; b < sectors[index].Length; b++)
            {
                sectors[index][b] = (byte)((index * 37) + b);
            }
        }

        byte[] header = sectors[0];
        header[0] = 0x01;
        header[1] = 0x5A;
        header[2] = 0x5A;
        header[3] = 0x5A;
        header[4] = 0x5A;
        header[5] = 0x5A;
        header[6] = 0x01;

        // 2048-byte blocks, root directory at block 1.
        header[0x4D] = 0x00;
        header[0x4E] = 0x08;
        header[0x4F] = 0x00;
        header[0x65] = 0x00;
        header[0x66] = 0x00;
        header[0x67] = 0x01;

        byte[] directory = sectors[1];
        directory[0x02] = 0xFF;
        directory[0x03] = 0xFF; // no continuation
        directory[0x0D] = 0x00;
        directory[0x0E] = 0x00;
        directory[0x0F] = 0x80; // end of entries
        directory[0x12] = 0x00;
        directory[0x13] = 0x40; // first entry at 0x40

        const int Entry = 0x40;
        directory[Entry + 0x03] = 0x02; // file
        directory[Entry + 0x0D] = 0x00;
        directory[Entry + 0x0E] = 0x08;
        directory[Entry + 0x0F] = 0x00; // file block size 2048
        directory[Entry + 0x11] = (byte)(fileSize >> 16);
        directory[Entry + 0x12] = (byte)(fileSize >> 8);
        directory[Entry + 0x13] = (byte)fileSize;
        directory[Entry + 0x45] = 0x00;
        directory[Entry + 0x46] = 0x00;
        directory[Entry + 0x47] = 0x02; // file data at block 2
        directory[Entry + 0x43] = 0x00; // no extra copies

        byte[] name = System.Text.Encoding.ASCII.GetBytes(launchMe);
        name.CopyTo(directory, Entry + 0x20);
        directory[Entry + 0x20 + name.Length] = 0;

        return sectors;
    }

    private sealed class MemoryDisc(byte[][] sectors) : IDiscSource
    {
        public int SectorCount => sectors.Length;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            if ((uint)lba >= (uint)sectors.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(lba));
            }

            sectors[lba].AsSpan(0, Math.Min(destination.Length, 2048)).CopyTo(destination);
        }

        public void Dispose()
        {
        }
    }
}
