using System;
using FiveDo.Cdrom;
using FiveDo.Core;
using XbusChip = FiveDo.Xbus.Xbus;

namespace FiveDo.Tests.Cdrom;

public sealed class CdromDeviceTests
{
    [Fact]
    public void Initialize_ReportsPresentDiscLikeReference()
    {
        var cd = new CdromDevice();

        Assert.Equal((byte)0xE1, cd.DriveStatus);
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
    public void OperaCycleBased_ReadDataWaitsForTheSectorDeadline()
    {
        var disc = new MemoryDisc(4);
        var cd = new CdromDevice
        {
            TimingMode = CdromTimingMode.OperaCycleBased,
        };
        cd.InsertDisc(disc);
        cd.SetPoll(0x03); // enable status/data FIQs, as the guest driver does

        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02);

        // Both poll bits stay clear until the first sector has been read at the stock
        // 2x drive speed. The status response is published after the transfer finishes.
        Assert.Equal(0, (int)cd.GetPoll() & 0x20);
        Assert.Equal(0, (int)cd.GetPoll() & 0x10);
        Assert.Equal(cd.DataSectorCycles, cd.NextReadyCycle);

        cd.Tick((uint)(cd.DataSectorCycles - 1));
        Assert.Equal(0, (int)cd.GetPoll() & 0x20);

        cd.Tick(1);
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x20);
        Assert.True(cd.FiqPending);

        var first = ReadData(cd, 2048);
        Assert.Equal(disc.Sector(0), first);

        // The next sector is scheduled after the one that was just consumed.
        Assert.Equal(0, (int)cd.GetPoll() & 0x20);
        Assert.Equal(0, (int)cd.GetPoll() & 0x10);
        Assert.Equal(cd.Cycles + cd.DataSectorCycles, cd.NextReadyCycle);

        cd.Tick((uint)cd.DataSectorCycles);
        var second = ReadData(cd, 2048);
        Assert.Equal(disc.Sector(1), second);

        // The 0x10 status becomes readable once the last sector has been consumed.
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x10);
        var status = ReadStatus(cd, 2);
        Assert.Equal(0x10, status[0]);
    }

    [Fact]
    public void OperaCycleBased_SaveLoadPreservesTheSectorDeadline()
    {
        var disc = new MemoryDisc(4);
        var cd = new CdromDevice
        {
            TimingMode = CdromTimingMode.OperaCycleBased,
        };
        cd.InsertDisc(disc);
        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01);
        cd.Tick((uint)(cd.DataSectorCycles / 2));

        var writer = new StateWriter();
        cd.Save(writer);

        var loaded = new CdromDevice
        {
            TimingMode = CdromTimingMode.OperaCycleBased,
        };
        loaded.InsertDisc(disc);
        loaded.Load(new StateReader(writer.WrittenSpan.ToArray()));

        Assert.Equal(CdromTimingMode.OperaCycleBased, loaded.TimingMode);
        Assert.Equal(cd.Cycles, loaded.Cycles);
        Assert.Equal(cd.NextReadyCycle, loaded.NextReadyCycle);

        loaded.Tick((uint)(loaded.NextReadyCycle - loaded.Cycles));
        Assert.Equal(disc.Sector(0), ReadData(loaded, 2048));
    }

    [Fact]
    public void OperaCycleBased_RaisesTheXbusFiqWhenDataBecomesReady()
    {
        var cd = new CdromDevice
        {
            TimingMode = CdromTimingMode.OperaCycleBased,
        };
        var interrupts = 0;
        cd.InterruptRequested = () => interrupts++;
        cd.InsertDisc(new MemoryDisc(4));
        cd.SetPoll(0x03); // enable status/data FIQs, as the guest driver does
        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01);

        Assert.Equal(0, interrupts);
        cd.Tick((uint)cd.DataSectorCycles);
        Assert.Equal(1, interrupts);
    }

    [Fact]
    public void OperaPollSemantics_LocalResetClearsTransferAndGatesFifos()
    {
        var disc = new MemoryDisc(4);
        var cd = new CdromDevice
        {
            TimingMode = CdromTimingMode.OperaCycleBased,
            ConfiguredSpeed = CdSpeed.Unbounded,
        };
        cd.InsertDisc(disc);
        cd.SetPoll(0x03);
        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02);
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x20);

        cd.SetPoll(0x08); // assert local reset
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x08);
        Assert.Equal(0, (int)cd.GetPoll() & 0x30);
        Assert.Equal(0u, cd.GetData());
        Assert.Equal(0u, cd.GetStatus());
        Assert.NotEqual(0, cd.DriveStatus & 0x01);

        Send(cd, 0x83); // commands are ignored while reset is asserted
        Assert.Equal(0, (int)cd.GetPoll() & 0x10);

        cd.SetPoll(0x00); // release
        Assert.Equal(0, (int)cd.GetPoll() & 0x08);
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x80);
        Send(cd, 0x83);
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x10);
    }

    [Fact]
    public void OperaPollSemantics_MediaAccessLatchIsWriteOneToClear()
    {
        var cd = new CdromDevice { TimingMode = CdromTimingMode.OperaCycleBased };
        cd.InsertDisc(new MemoryDisc(4));

        // Expansion-interface reset leaves the media-access latch asserted.
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x80);
        cd.SetPoll(0x80);
        Assert.Equal(0, (int)cd.GetPoll() & 0x80);

        cd.SetPoll(0x08);
        cd.SetPoll(0x00);
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x80);
    }

    [Fact]
    public void OperaPollSemantics_EjectDiscLeavesReadyWithoutDisc()
    {
        var cd = new CdromDevice { TimingMode = CdromTimingMode.OperaCycleBased };
        cd.InsertDisc(new MemoryDisc(4));
        Assert.NotEqual(0, cd.DriveStatus & 0x40);

        cd.EjectDisc();

        Assert.Null(cd.Disc);
        Assert.Equal(0x01, cd.DriveStatus);
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x80);
    }

    [Fact]
    public void DataSectorCycles_MatchesTheStock2xOperaCompensation()
    {
        var cd = new CdromDevice();

        // 12.5 MHz / (75 sectors/s * 2x) = 83,333 cycles; Opera subtracts 167.0 us
        // (2,087 cycles) of measured per-sector overhead.
        Assert.Equal(81_246UL, cd.DataSectorCycles);
    }

    [Fact]
    public void DataSectorCycles_ScalesWithTheConfiguredSpeed()
    {
        var cd = new CdromDevice();

        cd.ConfiguredSpeed = CdSpeed.X1;
        Assert.Equal(164_591UL, cd.DataSectorCycles);

        cd.ConfiguredSpeed = CdSpeed.X2;
        Assert.Equal(81_246UL, cd.DataSectorCycles);

        cd.ConfiguredSpeed = CdSpeed.X4;
        Assert.Equal(39_554UL, cd.DataSectorCycles);

        cd.ConfiguredSpeed = CdSpeed.X8;
        Assert.Equal(18_715UL, cd.DataSectorCycles);

        cd.ConfiguredSpeed = CdSpeed.X16;
        Assert.Equal(8_286UL, cd.DataSectorCycles);

        cd.ConfiguredSpeed = CdSpeed.X24;
        Assert.Equal(4_826UL, cd.DataSectorCycles);

        cd.ConfiguredSpeed = CdSpeed.X32;
        Assert.Equal(3_090UL, cd.DataSectorCycles);

        cd.ConfiguredSpeed = CdSpeed.Unbounded;
        Assert.Equal(0UL, cd.DataSectorCycles);
        Assert.Equal(0, cd.EffectiveSpeed);
    }

    [Fact]
    public void Unbounded_PublishesTheRequestedSectorImmediately()
    {
        var disc = new MemoryDisc(2);
        var cd = new CdromDevice
        {
            TimingMode = CdromTimingMode.OperaCycleBased,
            ConfiguredSpeed = CdSpeed.Unbounded,
        };
        cd.InsertDisc(disc);

        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02);

        Assert.NotEqual(0, (int)cd.GetPoll() & 0x20);
        Assert.Equal(disc.Sector(0), ReadData(cd, 2048));
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x20);
        Assert.Equal(disc.Sector(1), ReadData(cd, 2048));
        Assert.NotEqual(0, (int)cd.GetPoll() & 0x10);
    }

    [Fact]
    public void SingleSpeed_TakesLongerThanTheStockDrive()
    {
        var stock = new CdromDevice();
        var single = new CdromDevice { ConfiguredSpeed = CdSpeed.X1 };

        Assert.True(single.DataSectorCycles > stock.DataSectorCycles);
    }

    [Fact]
    public void UnknownConfiguredSpeed_Throws()
    {
        var cd = new CdromDevice();

        Assert.Throws<ArgumentOutOfRangeException>(() => cd.ConfiguredSpeed = (CdSpeed)3);
    }

    [Fact]
    public void EjectDisc_ResetsToTheReferenceInitialState()
    {
        var cd = new CdromDevice();
        cd.InsertDisc(new MemoryDisc(4));
        cd.EjectDisc();

        // FreeDO XBP_RESET(NULL) calls Init(), which reasserts the tray/disc/spin bits.
        Assert.Equal((byte)0xE1, cd.DriveStatus);
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

        // The reported end must be the real sector count plus the 150-frame lead-in.
        // 225 sectors therefore report 375 frames = 00:05:00. The CD DIPIR rejects
        // discs whose reported end exceeds the volume block count by more than 512
        // frames, so the historical FreeDO double-add must not be reproduced here.
        Assert.Equal(0x85, status[0]);
        Assert.Equal(0x00, status[2]);
        Assert.Equal(0x05, status[3]);
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
        _ = ReadData(cd, 100);
        cd.AudioVolume = 0.25;
        cd.AudioMuted = true;

        var writer = new StateWriter();
        cd.Save(writer);

        var loaded = new CdromDevice();
        loaded.Load(new StateReader(writer.WrittenSpan.ToArray()));

        Assert.Equal(0.25, loaded.AudioVolume);
        Assert.True(loaded.AudioMuted);

        var rest = ReadData(loaded, 200);
        Assert.Equal(ReadData(cd, 200), rest);
        Assert.NotEqual(0, (int)loaded.GetPoll() & 0x20);
        Assert.True(rest.Length > 0);
    }

    [Fact]
    public void ReadData_RecordsTheFrameForLoadProbes()
    {
        var cd = new CdromDevice();
        cd.InsertDisc(new MemoryDisc(4));
        var frame = new FrameClock { Value = 123 };
        cd.FrameProvider = () => frame.Value;

        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01);

        Assert.Equal(123, cd.LastReadFrame);
    }

    [Fact]
    public void ConsecutiveReadIdPollsAfterIdleRaiseMediaAccess()
    {
        var cd = new CdromDevice();
        var frame = new FrameClock();
        cd.FrameProvider = () => frame.Value;

        Send(cd, 0x83);
        frame.Value = 60;
        Send(cd, 0x83);

        Assert.NotEqual(0, (int)cd.GetPoll() & 0x40);
    }

    [Fact]
    public void ReadIdAfterAnotherCommandDoesNotRaiseMediaAccess()
    {
        var cd = new CdromDevice();
        var frame = new FrameClock();
        cd.FrameProvider = () => frame.Value;

        Send(cd, 0x83);
        frame.Value = 60;
        Send(cd, 0x84);
        frame.Value = 120;
        Send(cd, 0x83);

        Assert.Equal(0, (int)cd.GetPoll() & 0x40);
    }

    [Fact]
    public void ConsecutiveReadIdPollsWithShortGapDoNotRaiseMediaAccess()
    {
        var cd = new CdromDevice();
        var frame = new FrameClock();
        cd.FrameProvider = () => frame.Value;

        Send(cd, 0x83);
        frame.Value = 20;
        Send(cd, 0x83);

        Assert.Equal(0, (int)cd.GetPoll() & 0x40);
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

    private sealed class FrameClock
    {
        public long Value { get; set; }
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

        public void ReadSector(int lba, Span<byte> destination)
        {
            _sectors[lba].CopyTo(destination);
        }

        public void Dispose()
        {
        }

        internal byte[] Sector(int lba)
        {
            return _sectors[lba];
        }
    }

    private sealed class FakeInterrupts : IInterruptSink
    {
        public bool NeedFiq => Pending;

        public bool Pending { get; private set; }

        public void GenerateFiq(uint reason1, uint reason2)
        {
            Pending = true;
        }
    }
}
