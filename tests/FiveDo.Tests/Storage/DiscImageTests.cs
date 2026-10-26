using System;
using System.Buffers.Binary;
using System.IO;
using CHDSharp.Encoder;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class DiscImageTests
{
    [Fact]
    public void IsoImage_ReadsEverySector()
    {
        using var fixture = new DiscFixture(sectorCount: 64);
        using var image = new IsoImage(fixture.IsoPath);

        Assert.Equal(64, image.SectorCount);
        Assert.Equal(2048, image.SectorSize);
        for (var lba = 0; lba < image.SectorCount; lba++)
        {
            var sector = new byte[2048];
            image.ReadSector(lba, sector);
            Assert.Equal(DiscFixture.Expected(lba), sector);
        }
    }

    [Fact]
    public void BinImage_StripsSyncAndHeader()
    {
        using var fixture = new DiscFixture(sectorCount: 32);
        using var image = new BinImage(fixture.BinPath);

        Assert.Equal(32, image.SectorCount);
        for (var lba = 0; lba < image.SectorCount; lba++)
        {
            var sector = new byte[2048];
            image.ReadSector(lba, sector);
            Assert.Equal(DiscFixture.Expected(lba), sector);
        }
    }

    [Fact]
    public void CueSheet_OpensMode1BinTrack()
    {
        using var fixture = new DiscFixture(sectorCount: 16);
        using var image = CueSheet.Open(fixture.CuePath);

        Assert.Equal(16, image.SectorCount);
        var sector = new byte[2048];
        image.ReadSector(5, sector);
        Assert.Equal(DiscFixture.Expected(5), sector);
    }

    [Fact]
    public void Factory_DetectsIsoBinCue()
    {
        using var fixture = new DiscFixture(sectorCount: 8);

        using (var iso = DiscImageFactory.Open(fixture.IsoPath))
        {
            Assert.IsType<IsoImage>(iso);
        }

        using (var bin = DiscImageFactory.Open(fixture.BinPath))
        {
            Assert.IsType<BinImage>(bin);
        }

        using (var cue = DiscImageFactory.Open(fixture.CuePath))
        {
            Assert.IsType<BinImage>(cue);
        }
    }

    [Fact]
    public void ChdImage_MatchesIsoAndBinSectorStream()
    {
        using var fixture = new DiscFixture(sectorCount: 48);
        ChdEncoder.EncodeCd(fixture.CuePath, fixture.ChdPath);

        using var chd = FiveDo.Storage.Chd.ChdDiscImage.Open(fixture.ChdPath);
        Assert.True(chd.SectorCount >= 48);
        for (var lba = 0; lba < 48; lba++)
        {
            var sector = new byte[2048];
            chd.ReadSector(lba, sector);
            Assert.Equal(DiscFixture.Expected(lba), sector);
        }
    }

    [Fact]
    public void ChdImage_RejectsNonCdImage()
    {
        using var fixture = new DiscFixture(sectorCount: 4);
        ChdEncoder.CreateBlank(fixture.HddPath, 1024 * 1024);

        var exception = Assert.Throws<InvalidDataException>(() => FiveDo.Storage.Chd.ChdDiscImage.Open(fixture.HddPath));
        Assert.Contains("not a CD", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DiscFixture : IDisposable
    {
        private readonly string _directory;

        internal DiscFixture(int sectorCount)
        {
            _directory = Path.Combine(Path.GetTempPath(), $"fivedo-disc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            IsoPath = Path.Combine(_directory, "disc.iso");
            BinPath = Path.Combine(_directory, "disc.bin");
            CuePath = Path.Combine(_directory, "disc.cue");
            ChdPath = Path.Combine(_directory, "disc.chd");
            HddPath = Path.Combine(_directory, "hdd.chd");

            using (var iso = File.Create(IsoPath))
            {
                for (var lba = 0; lba < sectorCount; lba++)
                {
                    iso.Write(Expected(lba));
                }
            }

            using (var bin = File.Create(BinPath))
            {
                for (var lba = 0; lba < sectorCount; lba++)
                {
                    var raw = new byte[2352];
                    raw[0] = 0x00;
                    for (var i = 1; i <= 10; i++)
                    {
                        raw[i] = 0xFF;
                    }

                    raw[11] = 0x00;
                    raw[12] = (byte)((lba / (60 * 75)) % 100);
                    raw[13] = (byte)((lba / 75) % 60);
                    raw[14] = (byte)(lba % 75);
                    raw[15] = 1;
                    Expected(lba).CopyTo(raw.AsSpan(16));
                    bin.Write(raw);
                }
            }

            File.WriteAllText(
                CuePath,
                $"FILE \"{Path.GetFileName(BinPath)}\" BINARY\n"
                + "  TRACK 01 MODE1/2352\n"
                + "    INDEX 01 00:00:00\n");
        }

        internal string IsoPath { get; }

        internal string BinPath { get; }

        internal string CuePath { get; }

        internal string ChdPath { get; }

        internal string HddPath { get; }

        internal static byte[] Expected(int lba)
        {
            var sector = new byte[2048];
            for (var i = 0; i < sector.Length; i += 4)
            {
                BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(i), (uint)((lba * 0x01000193) ^ i));
            }

            return sector;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
