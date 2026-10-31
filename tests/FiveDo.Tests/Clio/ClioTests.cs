using FiveDo.Core;
using FiveDo.Memory;
using FiveDo.Tests.TestUtilities;
using ClioChip = FiveDo.Clio.Clio;
using MadamChip = FiveDo.Madam.Madam;

namespace FiveDo.Tests.Clio;

public sealed class ClioTests
{
    [Fact]
    public void Reset_SetsReferenceDefaults()
    {
        var clio = new ClioChip();

        Assert.Equal(0x40u, clio.Peek(0x28));
        Assert.Equal(0x80u, clio.Peek(0x400));
        Assert.Equal(64u, clio.Peek(0x220));
        Assert.Equal(0x02020000u, clio.Peek(0x0));
        Assert.False(clio.NeedFiq);
    }

    [Fact]
    public void Interrupts_NeedFiqFollowsEnableMasks()
    {
        var clio = new ClioChip();

        clio.GenerateFiq(1u << 9, 0);
        Assert.False(clio.NeedFiq);

        clio.Poke(0x48, 1u << 9);
        Assert.True(clio.NeedFiq);

        clio.Poke(0x44, 1u << 9);
        Assert.False(clio.NeedFiq);

        clio.GenerateFiq(0, 1u << 3);
        Assert.False(clio.NeedFiq);

        clio.Poke(0x68, 1u << 3);
        Assert.True(clio.NeedFiq);
        Assert.Equal(0x80000000u, clio.Peek(0x40) & 0x80000000u);

        clio.Poke(0x64, 1u << 3);
        Assert.False(clio.NeedFiq);
        Assert.Equal(0u, clio.Peek(0x40) & 0x80000000u);
    }

    [Fact]
    public void Timers_DecrementAndRaiseOddFiq()
    {
        var clio = new ClioChip();
        clio.Poke(0x200, 0x10);
        clio.Poke(0x108, 2);

        clio.DoTimers();
        Assert.Equal(1u, clio.Peek(0x108));

        clio.DoTimers();
        Assert.Equal(0u, clio.Peek(0x108));

        clio.DoTimers();
        Assert.Equal(0xFFFFu, clio.Peek(0x108));
        Assert.Equal(1u << 10, clio.Peek(0x40) & (1u << 10));
        Assert.Equal(0u, clio.Peek(0x200) & 0x10);
    }

    [Fact]
    public void Timers_ReloadValueAndKeepDecrement()
    {
        var clio = new ClioChip();
        clio.Poke(0x200, 0x30);
        clio.Poke(0x108, 0);
        clio.Poke(0x10c, 5);

        clio.DoTimers();

        Assert.Equal(5u, clio.Peek(0x108));
        Assert.Equal(0x10u, clio.Peek(0x200) & 0x10);
        Assert.Equal(1u << 10, clio.Peek(0x40) & (1u << 10));
    }

    [Fact]
    public void Timers_CascadeWaitsForPreviousTimerWrap()
    {
        var clio = new ClioChip();
        clio.Poke(0x200, 0x1 | (0x1u << 4) | (0x4u << 4));
        clio.Poke(0x100, 1);
        clio.Poke(0x108, 5);

        clio.DoTimers();
        Assert.Equal(0u, clio.Peek(0x100));
        Assert.Equal(5u, clio.Peek(0x108));

        clio.DoTimers();
        Assert.Equal(4u, clio.Peek(0x108));
    }

    [Fact]
    public void Timers_OperaSlotUsesTheCascadeCarryAcrossSlots()
    {
        var clio = new ClioChip();
        clio.Poke(0x200, 0x1u | (0x5u << 4));
        clio.Poke(0x100, 1);
        clio.Poke(0x108, 5);

        clio.DoTimers(0);
        Assert.Equal(0u, clio.Peek(0x100));
        Assert.False(clio.TimerCarry);

        clio.DoTimers(1);
        Assert.Equal(5u, clio.Peek(0x108));
        Assert.False(clio.TimerCarry);

        clio.DoTimers(0);
        Assert.Equal(0xFFFFu, clio.Peek(0x100));
        Assert.True(clio.TimerCarry);

        clio.DoTimers(1);
        Assert.Equal(4u, clio.Peek(0x108));
        Assert.False(clio.TimerCarry);
    }

    [Fact]
    public void Timers_OperaUnderflowReloadsAndRaisesOddFiq()
    {
        var clio = new ClioChip();
        clio.Poke(0x200, 0x3u);
        clio.Poke(0x100, 0);
        clio.Poke(0x104, 5);

        clio.DoTimers(0);
        Assert.Equal(5u, clio.Peek(0x100));
        Assert.True(clio.TimerCarry);
        Assert.Equal(0u, clio.Peek(0x40) & (1u << 10));

        clio.Poke(0x200, 0x1u << 4);
        clio.Poke(0x108, 0);
        clio.DoTimers(1);
        Assert.Equal(0xFFFFu, clio.Peek(0x108));
        Assert.Equal(1u << 10, clio.Peek(0x40) & (1u << 10));
        Assert.True(clio.TimerCarry);
    }

    [Fact]
    public void Timers_OperaNonReloadDisablesDecrementAndSetsAllOnes()
    {
        var clio = new ClioChip();
        clio.Poke(0x200, 0x1u);
        clio.Poke(0x100, 0);

        clio.DoTimers(0);

        Assert.Equal(0xFFFFu, clio.Peek(0x100));
        Assert.Equal(0u, clio.Peek(0x200) & 0x1u);
        Assert.True(clio.TimerCarry);
    }

    [Fact]
    public void SaveLoad_RoundtripsTheOperaTimerCarry()
    {
        var clio = new ClioChip();
        clio.Poke(0x200, 0x1u);
        clio.Poke(0x100, 0);
        clio.DoTimers(0);
        Assert.True(clio.TimerCarry);

        var writer = new StateWriter();
        clio.Save(writer);
        var copy = new ClioChip();
        copy.Load(new StateReader(writer.ToArray()));

        Assert.True(copy.TimerCarry);
        Assert.Equal(0xFFFFu, copy.Peek(0x100));
    }

    [Fact]
    public void Fifo_ReadsWordsAndAdvances()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write16(0x1002, 0x1234);
        memory.Ram.Write16(0x1000, 0x5678);

        var clio = new ClioChip { Ram = memory.Ram };
        clio.SetFifo(0x400, 0x1000);
        clio.SetFifo(0x404, 4);

        Assert.Equal(0x1234, clio.ReadInputFifoNoIncrement(0));
        Assert.Equal(0x1234, clio.ReadInputFifo(0));
        Assert.Equal(0x5678, clio.ReadInputFifo(0));
        Assert.Equal(0x1004u, clio.FifoStruct(0x400));
    }

    [Fact]
    public void Fifo_InputReloadsNextAddress()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write16(0x2002, 0xabcd);

        var clio = new ClioChip { Ram = memory.Ram };
        clio.SetFifo(0x400, 0x1000);
        clio.SetFifo(0x404, 0);
        clio.SetFifo(0x408, 0x2000);
        clio.SetFifo(0x40c, 0);

        Assert.Equal(0xabcd, clio.ReadInputFifo(0));
        Assert.Equal(1u << 16, clio.Peek(0x40) & (1u << 16));
    }

    [Fact]
    public void Fifo_OutputWritesAndRaisesFiq()
    {
        var memory = new FiveDoMemory();
        var clio = new ClioChip { Ram = memory.Ram };
        clio.SetFifo(0x500, 0x3000);
        clio.SetFifo(0x504, 2);

        clio.WriteOutputFifo(0, 0x1234);
        Assert.Equal(0x1234, memory.Ram.Read16(0x3002));

        clio.WriteOutputFifo(0, 0x5678);
        Assert.Equal(0x1234, memory.Ram.Read16(0x3002));
        Assert.Equal(0x5678, memory.Ram.Read16(0x3000));

        clio.WriteOutputFifo(0, 0x9abc);
        clio.WriteOutputFifo(0, 0xdef0);
        Assert.Equal(1u << 12, clio.Peek(0x40) & (1u << 12));
    }

    [Fact]
    public void RomSelect_SelectsSecondBank()
    {
        var memory = new FiveDoMemory();
        var clio = new ClioChip { RomSelector = memory };

        clio.Poke(0x84, 4);
        Assert.Equal(1, memory.RomBank);

        clio.Poke(0x84, 0);
        Assert.Equal(0, memory.RomBank);
    }

    [Fact]
    public void Dma_WritesBytesAndRaisesFiq29()
    {
        var memory = new FiveDoMemory();
        var madam = new MadamChip();
        madam.Poke(0x540, 0x1000);
        madam.Poke(0x544, 3);

        var xbus = new FakeClioXbus();
        xbus.EnqueueData(0x11, 0x22, 0x33, 0x44);

        var clio = new ClioChip { Ram = memory.Ram, Madam = madam, Xbus = xbus };
        clio.Poke(0x304, 0x100000);

        Assert.Equal(0x44, memory.Ram.Read8(0x1000));
        Assert.Equal(0x33, memory.Ram.Read8(0x1001));
        Assert.Equal(0x22, memory.Ram.Read8(0x1002));
        Assert.Equal(0x11, memory.Ram.Read8(0x1003));
        Assert.Equal(1u << 29, clio.Peek(0x40) & (1u << 29));
        Assert.Equal(0xFFFFFFFCu, madam.Peek(0x544));
        Assert.Equal(0x80u, clio.Peek(0x400) & 0x80u);
    }

    [Fact]
    public void VcntUpdate_WritesLineAndHalfFrame()
    {
        var clio = new ClioChip();
        clio.UpdateVcnt(42, 1);

        Assert.Equal((1u << 11) | 42u, clio.Peek(0x34));
    }

    [Fact]
    public void Poke120_AddsTimerDependentValue()
    {
        var clio = new ClioChip();
        for (var i = 0; i < 17; i++)
        {
            clio.Poke(0x304, 0x100000);
        }

        Assert.True(clio.TimerValue > 800);
        clio.Poke(0x120, 0x30);
        Assert.True(clio.Peek(0x120) > 0x30);
    }

    [Fact]
    public void Poke28_ReturnsPcAdjustOnlyFor0x30()
    {
        var clio = new ClioChip();

        Assert.True(clio.Poke(0x28, 0x30));
        Assert.False(clio.Poke(0x28, 0x40));
    }

    [Fact]
    public void DspWindows_WriteInstructionMemory()
    {
        var dsp = new FiveDo.Dsp.Dsp();
        var clio = new ClioChip { Dsp = dsp };

        clio.Poke(0x3000, 0x12345678);
        Assert.Equal(0x1234, dsp.ReadInstructionMemory(0));
        Assert.Equal(0x5678, dsp.ReadInstructionMemory(1));

        clio.Poke(0x3400, 0x9abc);
        Assert.Equal(0x9abc, dsp.ReadInstructionMemory(0));
    }

    [Fact]
    public void SaveLoad_RoundtripsRegistersAndTimers()
    {
        var clio = new ClioChip();
        clio.Poke(0x200, 0x1);
        clio.Poke(0x108, 7);
        clio.GenerateFiq(1u << 9, 0);

        var writer = new StateWriter();
        clio.Save(writer);
        var copy = new ClioChip();
        copy.Load(new StateReader(writer.ToArray()));

        Assert.Equal(clio.Peek(0x108), copy.Peek(0x108));
        Assert.Equal(clio.Peek(0x200), copy.Peek(0x200));
        Assert.Equal(clio.Peek(0x40), copy.Peek(0x40));
        Assert.Equal(clio.NeedFiq, copy.NeedFiq);
    }
}

