using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using CHDSharp.Encoder;
using CHDSharp.Encoder.Models;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class VolumeHeaderTests
{
    [Fact]
    public void Parse_ReadsBigEndianFields()
    {
        var sector = BuildSector();

        var header = VolumeHeader.Parse(sector);

        Assert.Equal(1, header.RecordType);
        Assert.Equal(0, header.Flags);
        Assert.Equal("A 3DO disc", header.Comment);
        Assert.Equal("TESTDISC", header.Label);
        Assert.Equal(0xFFFFFFFFu, header.Id);
        Assert.Equal(2048u, header.BlockSize);
        Assert.Equal(12345u, header.BlockCount);
        Assert.Equal(0xFFFFFFFEu, header.RootDirectoryId);
        Assert.Equal(1u, header.RootDirectoryBlocks);
        Assert.Equal(2048u, header.RootDirectoryBlockSize);
        Assert.Equal(4u, header.FirstRootDirectoryCopy);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5351:Do Not Use Broken Cryptographic Algorithms",
        Justification = "The test verifies the 3DO game-ID MD5.")]
    public void GameId_IsMd5OfFirstTwoSectors()
    {
        var disc = new MemoryDisc(2);
        disc.Sectors[0][0] = 1;
        disc.Sectors[0][100] = 2;
        disc.Sectors[1][10] = 3;

        var expected = Convert.ToHexStringLower(MD5.HashData([.. disc.Sectors[0], .. disc.Sectors[1]]));
        Assert.Equal(expected, GameId.Compute(disc));
    }

    [Fact]
    public void ChdChild_OpensAgainstParent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-chd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var binPath = Path.Combine(directory, "disc.bin");
            var cuePath = Path.Combine(directory, "disc.cue");
            var parentPath = Path.Combine(directory, "parent.chd");
            var childPath = Path.Combine(directory, "child.chd");

            using (FileStream bin = File.Create(binPath))
            {
                for (var lba = 0; lba < 32; lba++)
                {
                    var raw = new byte[2352];
                    raw[0] = 0;
                    for (var i = 1; i <= 10; i++)
                    {
                        raw[i] = 0xFF;
                    }

                    raw[11] = 0;
                    raw[15] = 1;
                    for (var i = 16; i < 2064; i++)
                    {
                        raw[i] = (byte)(lba + i);
                    }

                    bin.Write(raw);
                }
            }

            File.WriteAllText(
                cuePath,
                $"FILE \"{Path.GetFileName(binPath)}\" BINARY\n  TRACK 01 MODE1/2352\n    INDEX 01 00:00:00\n");

            ChdEncoder.EncodeCd(cuePath, parentPath);
            ChdEncoder.EncodeCd(cuePath, childPath, options: new ChdEncodeOptions { ParentPath = parentPath });

            using var parent = FiveDo.Storage.Chd.ChdDiscImage.Open(parentPath);
            using var child = FiveDo.Storage.Chd.ChdDiscImage.Open(childPath, parentPath);
            var parentSector = new byte[2048];
            var childSector = new byte[2048];
            for (var lba = 0; lba < 32; lba++)
            {
                parent.ReadSector(lba, parentSector);
                child.ReadSector(lba, childSector);
                Assert.Equal(parentSector, childSector);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] BuildSector()
    {
        var sector = new byte[2048];
        sector[0] = 1;
        "ZZZZZ"u8.CopyTo(sector.AsSpan(1));
        sector[6] = 2;
        "A 3DO disc"u8.CopyTo(sector.AsSpan(8));
        "TESTDISC"u8.CopyTo(sector.AsSpan(40));
        BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(72), 0xFFFFFFFF);
        BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(76), 2048);
        BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(80), 12345);
        BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(84), 0xFFFFFFFE);
        BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(88), 1);
        BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(92), 2048);
        BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(96), 0);
        BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(100), 4);
        return sector;
    }

    private sealed class MemoryDisc : FiveDo.Core.IDiscSource
    {
        internal MemoryDisc(int count)
        {
            Sectors = new byte[count][];
            for (var i = 0; i < count; i++)
            {
                Sectors[i] = new byte[2048];
            }
        }

        internal byte[][] Sectors { get; }

        public int SectorCount => Sectors.Length;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            Sectors[lba].CopyTo(destination);
        }

        public void Dispose()
        {
        }
    }
}
