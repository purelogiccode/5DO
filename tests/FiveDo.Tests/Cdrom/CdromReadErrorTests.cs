using System;
using FiveDo.Cdrom;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Cdrom;

public sealed class CdromReadErrorTests
{
    [Fact]
    public void HostReadFailureIsReportedToTheGuestWithoutFaultingEmulation()
    {
        var disc = new FailingDisc(sectorCount: 4, failingLba: 1);
        var cd = new CdromDevice();
        cd.InsertDisc(disc);

        // READ_DATA at LBA 1 = 00:02:01 for one sector.
        Send(cd, 0x10, 0x00, 0x02, 0x01, 0x00, 0x00, 0x01);
        byte[] data = ReadData(cd, 2048);

        Assert.All(data, static value => Assert.Equal(0, value));
        Assert.Equal(1, cd.DiscReadErrorCount);
        Assert.Equal(1, cd.LastDiscReadErrorSector);
        Assert.NotEqual(0, cd.DriveStatus & 0x10);

        // READ_ERROR reports the MEI receive-ECC code for the failed sector.
        Send(cd, 0x82);
        byte[] status = ReadStatus(cd, 10);
        Assert.Equal(0x82, status[0]);
        Assert.Equal(0x02, status[1]);
    }

    [Fact]
    public void ATransferContinuesAfterAFailedSector()
    {
        var disc = new FailingDisc(sectorCount: 4, failingLba: 1);
        var cd = new CdromDevice();
        cd.InsertDisc(disc);

        // Two sectors from LBA 1: the first fails, the second still arrives.
        Send(cd, 0x10, 0x00, 0x02, 0x01, 0x00, 0x00, 0x02);
        byte[] data = ReadData(cd, 4096);

        Assert.All(data.AsSpan(0, 2048).ToArray(), static value => Assert.Equal(0, value));
        Assert.Equal(disc.Sector(2), data[2048..]);
        Assert.Equal(1, cd.DiscReadErrorCount);
    }

    [Fact]
    public void BootProbeFailureFallsBackToThePhysicalSectorCount()
    {
        var disc = new FailingDisc(sectorCount: 6, failingLba: 0);
        var cd = new CdromDevice();

        // InitCd must not throw when the volume header cannot be read.
        cd.InsertDisc(disc);

        Assert.True(cd.DiscPresent);
        Assert.Equal(1, cd.DiscReadErrorCount);
        Assert.Equal(0, cd.LastDiscReadErrorSector);

        // The failed boot probe must not leave the drive in a permanent error state.
        Assert.Equal(0, cd.DriveStatus & 0x10);
    }

    private static void Send(CdromDevice device, params byte[] command)
    {
        foreach (byte value in command)
        {
            device.SetCommand(value);
        }

        for (int i = command.Length; i < 7; i++)
        {
            device.SetCommand(0);
        }
    }

    private static byte[] ReadStatus(CdromDevice device, int count)
    {
        var status = new byte[count];
        for (var i = 0; i < count; i++)
        {
            status[i] = (byte)device.GetStatus();
        }

        return status;
    }

    private static byte[] ReadData(CdromDevice device, int count)
    {
        var data = new byte[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = (byte)device.GetData();
        }

        return data;
    }

    private sealed class FailingDisc : IDiscSource
    {
        private readonly byte[][] _sectors;
        private readonly int _failingLba;

        internal FailingDisc(int sectorCount, int failingLba)
        {
            _failingLba = failingLba;
            _sectors = new byte[sectorCount][];
            for (var lba = 0; lba < sectorCount; lba++)
            {
                var sector = new byte[2048];
                for (var i = 0; i < sector.Length; i++)
                {
                    sector[i] = (byte)((lba * 13) + i + 1);
                }

                _sectors[lba] = sector;
            }

            // A valid 3DO volume header count keeps InitCd on the declared path.
            _sectors[0][0x50] = 0;
            _sectors[0][0x51] = 0;
            _sectors[0][0x52] = 0;
            _sectors[0][0x53] = (byte)sectorCount;
        }

        public int SectorCount => _sectors.Length;

        public int SectorSize => 2048;

        internal byte[] Sector(int lba) => _sectors[lba];

        public void ReadSector(int lba, Span<byte> destination)
        {
            if (lba == _failingLba)
            {
                throw new DiscImageException($"Simulated read failure at sector {lba}.");
            }

            _sectors[lba].CopyTo(destination);
        }

        public void Dispose()
        {
        }
    }
}
