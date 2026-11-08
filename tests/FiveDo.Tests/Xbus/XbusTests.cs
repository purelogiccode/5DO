using System.Collections.Generic;
using FiveDo.Cdrom;
using FiveDo.Core;
using XbusChip = FiveDo.Xbus.Xbus;

namespace FiveDo.Tests.Xbus;

public sealed class XbusTests
{
    [Fact]
    public void OperaSemantics_DoesNotMaskThePollHighNibble()
    {
        var cd = new CdromDevice();
        var xbus = new XbusChip { OperaSemantics = true };
        xbus.Attach(cd);
        xbus.SetSelect(0x80);

        for (var i = 0; i < 7; i++)
        {
            xbus.SetCommandFifo(i == 0 ? 0x83u : 0u);
        }

        Assert.Equal(0x1fu, xbus.GetPoll());

        xbus.OperaSemantics = false;
        Assert.Equal(0x0fu, xbus.GetPoll());
    }

    [Fact]
    public void OperaSemantics_LatchesMediaAccessOnPollAndCommandWrites()
    {
        var device = new MediaDevice();
        var xbus = new XbusChip { OperaSemantics = true };
        var latched = new List<uint>();
        xbus.MediaAccess = latched.Add;
        xbus.Attach(device);
        xbus.SetSelect(0x80);

        xbus.SetPoll(0x01);
        xbus.SetCommandFifo(0x83);

        Assert.Equal(new uint[] { 0x80, 0x80 }, latched);
    }

    [Fact]
    public void FreeDoSemantics_DoesNotLatchMediaAccess()
    {
        var cd = new CdromDevice();
        var xbus = new XbusChip();
        var latched = 0;
        xbus.MediaAccess = _ => latched++;
        xbus.Attach(cd);

        xbus.SetPoll(0x01);
        xbus.SetCommandFifo(0x83);

        Assert.Equal(0, latched);
    }

    [Fact]
    public void FiqPending_ReportsAttachedDeviceLevelAndBuiltInStatus()
    {
        var device = new PendingDevice();
        var xbus = new XbusChip { OperaSemantics = true };
        xbus.Attach(device);

        Assert.True(xbus.FiqPending);

        device.Pending = false;
        xbus.SetSelect(0x0f);
        Assert.False(xbus.FiqPending);

        xbus.SetPoll(0x07);
        for (var i = 0; i < 7; i++)
        {
            xbus.SetCommandFifo(i == 0 ? 0x83u : 0u);
        }

        Assert.True(xbus.FiqPending);
    }

    private sealed class PendingDevice : IXbusDevice
    {
        public bool HasMediaAccess => false;

        public bool Pending { get; set; } = true;

        public bool FiqPending => Pending;

        public void Initialize()
        {
        }

        public void Reset()
        {
        }

        public void SetCommand(byte value)
        {
        }

        public void SetData(uint value)
        {
        }

        public uint GetData() => 0;

        public uint GetStatus() => 0;

        public void SetPoll(uint value)
        {
        }

        public uint GetPoll() => 0;

        public uint GetReserved() => 0;
    }

    private sealed class MediaDevice : IXbusDevice
    {
        public bool HasMediaAccess => true;

        public bool FiqPending => false;

        public void Initialize()
        {
        }

        public void Reset()
        {
        }

        public void SetCommand(byte value)
        {
        }

        public void SetData(uint value)
        {
        }

        public uint GetData() => 0;

        public uint GetStatus() => 0;

        public void SetPoll(uint value)
        {
        }

        public uint GetPoll() => 0;

        public uint GetReserved() => 0;
    }
}
