using ThreeDo.Core;
using ThreeDo.Memory;
using ThreeDo.Tests.TestUtilities;
using CelFsmState = ThreeDo.Madam.CelFsmState;
using MadamChip = ThreeDo.Madam.Madam;

namespace ThreeDo.Tests.Madam;

public sealed class MadamTests
{
    [Fact]
    public void Reset_SetsReferenceDefaults()
    {
        var madam = new MadamChip();

        Assert.Equal(0x29u, madam.Peek(0x04));
        Assert.Equal(0x01020000u, madam.Peek(0x00));
        Assert.Equal(0xfffffffeu, madam.Peek(0x574) + 2);
        Assert.Equal(CelFsmState.Idle, madam.Fsm);
        Assert.Equal(0u, madam.Peek(0x28));
    }

    [Fact]
    public void Fsm_StatusAndTransitions()
    {
        var madam = new MadamChip();

        madam.Poke(0x100, 0);
        Assert.Equal(CelFsmState.InProcess, madam.Fsm);
        Assert.Equal(0x10u, madam.Peek(0x28));

        madam.Poke(0x10c, 0);
        Assert.Equal(CelFsmState.Suspended, madam.Fsm);
        Assert.Equal(0x30u, madam.Peek(0x28));

        madam.Poke(0x108, 0);
        Assert.Equal(CelFsmState.InProcess, madam.Fsm);

        madam.Poke(0x104, 0);
        Assert.Equal(CelFsmState.Idle, madam.Fsm);
        Assert.Equal(0u, madam.Peek(0x28));
        Assert.Equal(0u, madam.Peek(0x5a4));
    }

    [Fact]
    public void VdlHead_ForwardsToVdlp()
    {
        var vdlp = new FakeVdlp();
        var madam = new MadamChip { Vdlp = vdlp };

        madam.Poke(0x580, 0x2b0000);

        Assert.Equal(1, vdlp.CallCount);
        Assert.Equal(0x2b0000u, vdlp.LastAddress);
        Assert.Equal(0u, madam.Peek(0x580));
    }

    [Fact]
    public void Modulo_RegisterDecodesReadAndWriteValues()
    {
        var madam = new MadamChip();

        madam.Poke(0x130, 0x00000001);
        Assert.Equal(128, madam.ReadModulo);
        Assert.Equal(0, madam.WriteModulo);

        madam.Poke(0x130, 0x00000100);
        Assert.Equal(0, madam.ReadModulo);
        Assert.Equal(128, madam.WriteModulo);
    }

    [Fact]
    public void MatrixCommand1_Multiplies4x4ByVector()
    {
        var madam = new MadamChip();
        madam.Poke(0x600, 0x10000);
        madam.Poke(0x640, 0x20000);

        madam.Poke(0x7fc, 1);
        madam.Poke(0x7fc, 0);

        Assert.Equal(131072u, madam.Peek(0x660));
        Assert.Equal(0u, madam.Peek(0x664));
        Assert.Equal(0u, madam.Peek(0x7fc));
    }

    [Fact]
    public void MatrixCommand2_Multiplies3x3ByVector()
    {
        var madam = new MadamChip();
        madam.Poke(0x600, 0x8000);
        madam.Poke(0x640, 0x40000);

        madam.Poke(0x7fc, 2);
        madam.Poke(0x7fc, 0);

        Assert.Equal(131072u, madam.Peek(0x660));
    }

    [Fact]
    public void MatrixCommand3_HandlesDivisionByZero()
    {
        var madam = new MadamChip();
        madam.Poke(0x600, 0x10000);
        madam.Poke(0x640, 0x20000);
        madam.Poke(0x680, 0);
        madam.Poke(0x684, 0);

        madam.Poke(0x7fc, 3);
        madam.Poke(0x7fc, 0);

        Assert.Equal(0u, madam.Peek(0x668));
    }

    [Fact]
    public void KeyPressed_CopiesAndFillsRemainder()
    {
        var madam = new MadamChip();

        madam.KeyPressed(new byte[] { 1, 2, 3, 4 });

        Assert.Equal(1, madam.PbusQueue[0]);
        Assert.Equal(4, madam.PbusQueue[3]);
        for (var i = 4; i < 20; i++)
        {
            Assert.Equal(0xff, madam.PbusQueue[i]);
        }
    }

    [Fact]
    public void PbusDma_WritesQueueWordAndRaisesFiq()
    {
        var memory = new ThreeDoMemory();
        var interrupts = new FakeInterruptSink();
        var madam = new MadamChip { Bus = memory.Bus, Interrupts = interrupts };
        madam.KeyPressed(new byte[] { 0x11, 0x22, 0x33, 0x44 });
        madam.Poke(0x570, 0x1000);
        madam.Poke(0x574, 8);
        madam.Poke(0x578, 0);

        madam.Poke(0x08, 0x8000);

        Assert.Equal(0x44332211u, memory.Ram.Read32(0x1004));
        Assert.Equal(1, interrupts.GenerateCount);
        Assert.Equal(1u, interrupts.LastReason2);
        Assert.Equal(0xfffffffeu, madam.Peek(0x574) + 2);
    }

    [Fact]
    public void SaveLoad_RoundtripsRegistersAndFsm()
    {
        var madam = new MadamChip();
        madam.Poke(0x100, 0);
        madam.Poke(0x600, 0x1234);
        madam.KeyPressed(new byte[] { 9, 8, 7 });

        var writer = new StateWriter();
        madam.Save(writer);
        var copy = new MadamChip();
        copy.Load(new StateReader(writer.ToArray()));

        Assert.Equal(CelFsmState.InProcess, copy.Fsm);
        Assert.Equal(0x1234u, copy.Peek(0x600));
        Assert.Equal(9, copy.PbusQueue[0]);
        Assert.Equal(0xff, copy.PbusQueue[3]);
    }
}
