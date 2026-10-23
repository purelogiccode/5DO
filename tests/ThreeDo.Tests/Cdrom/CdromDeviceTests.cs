using System;
using ThreeDo.Cdrom;
using ThreeDo.Core;
using XbusChip = ThreeDo.Xbus.Xbus;

namespace ThreeDo.Tests.CdromIo;

public sealed class CdromDeviceTests
{
    [Fact]
    public void Initialize_ReportsPresentDiscLikeReference()
    {
        var cd = new CdromDevice();

        Assert.Equal(0xE1, (int)cd.DriveStatus);
        Assert.Equal(0x0F, (int)cd.GetPoll());
        Assert.False(cd.FiqPending);
    }

    [Fact]
    public void ReadId_ReturnsDocumentedStatusTranscript()
    {
        var cd = new CdromDevice();
        Send(cd, 0x83);

        Assert.Equal(0x1F, (int)cd.GetPoll());
        Assert.True(cd.FiqPending);

        var status = ReadStatus(cd, 12);
        Assert.Equal(0x83, status[0]);
        Assert.Equal(0x00, status[1]);
        Assert.Equal(0x10, status[2]);
        Assert.Equal(0x00, status[3]);
        Assert.Equal(0x01, status[4]);
        Assert.Equal(0xE1, status[11]);
        Assert.Equal(0x0F, (int)cd.GetPoll());
    }

    [Fact]
    public void DataPathCheck_ReturnsMagicBytes()
    {
        var cd = new CdromDevice();
        Send(cd, 0x80);
        var status = ReadStatus(cd, 4);

        Assert.Equal(new byte[] { 0x80, 0xAA, 0x55, 0xE1 }, status);
    }

    [Fact]
    public void ReadData_StreamsRequestedSectors()
    {
        var disc = new MemoryDisc(4);
        var cd = new CdromDevice();
        cd.InsertDisc(disc);

        // 00:02:00 is LBA 0; read two 2048-byte sectors.
        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02);

        Assert.Equal(0x3F, (int)cd.GetPoll());
        var status = ReadStatus(cd, 2);
        Assert.Equal(0x10, status[0]);
        Assert.NotEqual(0, status[1] & 0x01);

        var data = ReadData(cd, 4096);
        Assert.Equal(disc.Sector(0), data.AsSpan(0, 2048).ToArray());
        Assert.Equal(disc.Sector(1), data.AsSpan(2048, 2048).ToArray());
        Assert.Equal(0x0F, (int)cd.GetPoll());
    }

    [Fact]
    public void EjectDisc_ResetsToTheReferenceInitialState()
    {
        var cd = new CdromDevice();
        cd.InsertDisc(new MemoryDisc(4));
        cd.EjectDisc();

        // FreeDO XBP_RESET(NULL) calls Init(), which reasserts the tray/disc/spin bits.
        Assert.Equal(0xE1, (int)cd.DriveStatus);
        Assert.Null(cd.Disc);
    }

    [Fact]
    public void ReadCapacity_ReturnsTotalMsfFromDiscSize()
    {
        var disc = new MemoryDisc(225);
        var cd = new CdromDevice();
        cd.InsertDisc(disc);

        Send(cd, 0x85);
        var status = ReadStatus(cd, 8);

        // Reference quirk: filesize already includes the 150-frame lead-in and LBA2MSF adds
        // it again, so 225 sectors report 525 frames = 00:07:00.
        Assert.Equal(0x85, status[0]);
        Assert.Equal(0x00, status[2]);
        Assert.Equal(0x07, status[3]);
        Assert.Equal(0x00, status[4]);
    }

    [Fact]
    public void SaveLoad_RoundtripsCommandAndDataState()
    {
        var disc = new MemoryDisc(4);
        var cd = new CdromDevice();
        cd.InsertDisc(disc);
        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02);
        _ = ReadStatus(cd, 2);
        var first = ReadData(cd, 100);

        var writer = new StateWriter();
        cd.Save(writer);

        var loaded = new CdromDevice();
        loaded.Load(new StateReader(writer.WrittenSpan.ToArray()));

        var rest = ReadData(loaded, 200);
        Assert.Equal(ReadData(cd, 200), rest);
        Assert.NotEqual(0, (int)loaded.GetPoll() & 0x20);
        Assert.True(rest.Length > 0);
    }

    [Fact]
    public void Xbus_AttachSelectAndCommandRouteToCdrom()
    {
        var cd = new CdromDevice();
        var xbus = new XbusChip();
        var interrupts = new FakeInterrupts();
        xbus.Interrupts = interrupts;

        var slot = xbus.Attach(cd);
        Assert.Equal(0, slot);

        xbus.SetSelect(0);
        xbus.SetPoll(0x03);
        foreach (var value in new byte[] { 0x83, 0, 0, 0, 0, 0, 0 })
        {
            xbus.SetCommandFifo(value);
        }

        Assert.True(interrupts.Pending);
        Assert.Equal(0x13, (int)xbus.GetPoll());
        Assert.Equal(0x83, (int)xbus.GetStatusFifo());
    }

    private static void Send(CdromDevice device, params byte[] command)
    {
        foreach (var value in command)
        {
            device.SetCommand(value);
        }

        for (var i = command.Length; i < 7; i++)
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

    private sealed class MemoryDisc : IDiscSource
    {
        private readonly byte[][] _sectors;

        internal MemoryDisc(int count)
        {
            _sectors = new byte[count][];
            for (var lba = 0; lba < count; lba++)
            {
                var sector = new byte[2048];
                for (var i = 0; i < sector.Length; i++)
                {
                    sector[i] = (byte)((lba * 31) + i);
                }

                _sectors[lba] = sector;
            }
        }

        public int SectorCount => _sectors.Length;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination) => _sectors[lba].CopyTo(destination);

        public void Dispose()
        {
        }

        internal byte[] Sector(int lba) => _sectors[lba];
    }

    private sealed class FakeInterrupts : IInterruptSink
    {
        public bool NeedFiq => Pending;

        public bool Pending { get; private set; }

        public void GenerateFiq(uint reason1, uint reason2) => Pending = true;
    }
}
