using FiveDo.Cdrom;
using FiveDo.Core;
using FiveDo.Tests.TestUtilities;
using XbusChip = FiveDo.Xbus.Xbus;

namespace FiveDo.Tests.Xbus;

public sealed class XbusAuditTests
{
    [Fact]
    public void OperaBuiltInResponder_ExposesDeviceBitsAndRaisesFiq()
    {
        var interrupts = new FakeInterruptSink();
        var xbus = new XbusChip { OperaSemantics = true, Interrupts = interrupts };
        xbus.SetSelect(0xf);
        xbus.SetPoll(0x01);
        for (var i = 0; i < 7; i++)
        {
            xbus.SetCommandFifo(i == 0 ? 0x83u : 0u);
        }

        Assert.Equal(1, interrupts.GenerateCount);
        Assert.Equal(0x11u, xbus.GetPoll());

        var status = new uint[12];
        for (var i = 0; i < status.Length; i++)
        {
            status[i] = xbus.GetStatusFifo();
        }

        Assert.Equal(0x83u, status[0]);
        Assert.Equal(0x10u, status[2]);
        Assert.Equal(0x01u, status[4]);
        Assert.Equal(0xe1u, status[11]);
        Assert.Equal(0x01u, xbus.GetPoll());

        xbus.SetPoll(0x08);
        Assert.Equal(0u, xbus.GetPoll());
    }

    [Fact]
    public void FreeDoBuiltInResponder_KeepsRawStatusAndPollShape()
    {
        var interrupts = new FakeInterruptSink();
        var xbus = new XbusChip { Interrupts = interrupts };
        xbus.SetSelect(0xf);
        xbus.SetPoll(0x01);
        for (var i = 0; i < 7; i++)
        {
            xbus.SetCommandFifo(i == 0 ? 0x83u : 0u);
        }

        Assert.Equal(0, interrupts.GenerateCount);
        Assert.Equal(0x01u, xbus.GetPoll());
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(i == 0 ? 0x83u : 0x01u, xbus.GetStatusFifo());
        }
    }

    [Fact]
    public void OperaPollWrite_ClearsTheStatusFifoBeforeItIsRead()
    {
        var xbus = new XbusChip { OperaSemantics = true };
        xbus.SetSelect(0xf);
        for (var i = 0; i < 7; i++)
        {
            xbus.SetCommandFifo(i == 0 ? 0x83u : 0u);
        }

        xbus.SetPoll(0x08);

        Assert.Equal(0u, xbus.GetStatusFifo());
        Assert.Equal(0u, xbus.GetPoll());
    }

    [Fact]
    public void Load_WithAMissingAttachedDevice_ThrowsInsteadOfMisaligningTheStream()
    {
        var source = new XbusChip();
        source.Attach(new CdromDevice());
        var writer = new StateWriter();
        source.Save(writer);

        var target = new XbusChip();

        Assert.Throws<InvalidStateException>(
            () => target.Load(new StateReader(writer.ToArray())));
    }

    [Fact]
    public void Load_WithMatchingDevices_RoundtripsControllerAndDeviceState()
    {
        var source = new XbusChip();
        var cd = new CdromDevice();
        source.Attach(cd);
        source.SetSelect(0x80);
        source.SetPoll(0x03);
        var expectedPoll = source.GetPoll();
        var writer = new StateWriter();
        source.Save(writer);

        var target = new XbusChip();
        var targetCd = new CdromDevice();
        target.Attach(targetCd);
        target.Load(new StateReader(writer.ToArray()));

        Assert.Equal(0x80u, target.SelectedDeviceAddress);
        Assert.Equal(expectedPoll, target.GetPoll());
    }
}
