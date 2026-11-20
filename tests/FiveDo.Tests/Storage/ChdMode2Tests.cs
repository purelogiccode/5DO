using System;
using System.Buffers.Binary;
using System.IO;
using CHDSharp.Encoder;
using FiveDo.Core;
using FiveDo.Storage;
using FiveDo.Storage.Chd;

namespace FiveDo.Tests.Storage;

public sealed class ChdMode2Tests
{
    [Fact]
    public void ChdImage_ReturnsTheNativeMode2TrackSector()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fivedo-chd-m2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string binPath = Path.Combine(directory, "disc.bin");
        string cuePath = Path.Combine(directory, "disc.cue");
        string chdPath = Path.Combine(directory, "disc.chd");
        try
        {
            using (FileStream bin = File.Create(binPath))
            {
                for (var lba = 0; lba < 4; lba++)
                {
                    var sector = new byte[2336];
                    sector[0] = 0x00;
                    sector[1] = 0x00;
                    sector[2] = 0x08;
                    sector[3] = 0x00;
                    Expected(lba).CopyTo(sector.AsSpan(8));
                    for (var i = 8 + 2048; i < sector.Length; i++)
                    {
                        sector[i] = (byte)(lba + i);
                    }

                    bin.Write(sector);
                }
            }

            File.WriteAllText(
                cuePath,
                $"FILE \"{Path.GetFileName(binPath)}\" BINARY\n"
                + "  TRACK 01 MODE2/2336\n"
                + "    INDEX 01 00:00:00\n");

            ChdEncoder.EncodeCd(cuePath, chdPath);

            using var cue = CueDiscImage.Open(cuePath);
            using var chd = ChdDiscImage.Open(chdPath);
            DiscTrack cueTrack = Assert.Single(cue.Tracks);
            DiscTrack chdTrack = Assert.Single(chd.Tracks);
            Assert.Equal(2336, cueTrack.SectorSize);
            Assert.Equal(2336, chdTrack.SectorSize);

            var cueNative = new byte[2336];
            var chdNative = new byte[2336];
            Assert.Equal(2336, cue.ReadTrackSector(2, cueNative));
            Assert.Equal(2336, chd.ReadTrackSector(2, chdNative));
            Assert.Equal(cueNative, chdNative);

            var cueData = new byte[2048];
            var chdData = new byte[2048];
            cue.ReadSector(2, cueData);
            chd.ReadSector(2, chdData);
            Assert.Equal(Expected(2), chdData);
            Assert.Equal(cueData, chdData);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] Expected(int lba)
    {
        var sector = new byte[2048];
        for (var i = 0; i < sector.Length; i += 4)
        {
            BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(i), (uint)((lba * 0x01000193) ^ i));
        }

        return sector;
    }
}
