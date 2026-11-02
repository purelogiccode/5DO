using System.IO;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Memory;
using FiveDo.Tests.TestUtilities;
using MadamChip = FiveDo.Madam.Madam;

namespace FiveDo.Tests.Madam;

public sealed class MadamDmaAuditTests
{
    [Fact]
    public void Reset_ClearsTheMctlWrittenGate()
    {
        var madam = new MadamChip();
        Assert.True(madam.ClutDmaEnabled);
        Assert.True(madam.VideoDmaEnabled);

        madam.Poke(0x008, 0);
        Assert.False(madam.ClutDmaEnabled);
        Assert.False(madam.VideoDmaEnabled);

        madam.Reset();

        Assert.True(madam.ClutDmaEnabled);
        Assert.True(madam.VideoDmaEnabled);
    }

    [Fact]
    public void SoftReset_KeepsTheGuestMctlValue()
    {
        var madam = new MadamChip();
        madam.Poke(0x008, 0);

        madam.SoftReset();

        Assert.False(madam.ClutDmaEnabled);
        Assert.False(madam.VideoDmaEnabled);
    }

    [Fact]
    public void StateRoundtrip_PreservesTheMctlWrittenGate()
    {
        var source = new FiveDoMachine();
        source.Madam.Poke(0x008, 0);

        using var state = new MemoryStream();
        source.SaveState(state);
        state.Position = 0;

        var copy = new FiveDoMachine();
        Assert.True(copy.Madam.ClutDmaEnabled);
        copy.LoadState(state);

        Assert.False(copy.Madam.ClutDmaEnabled);
        Assert.False(copy.Madam.VideoDmaEnabled);
    }

    [Fact]
    public void DirectMadamStateRoundtrip_PreservesTheMctlWrittenGate()
    {
        var madam = new MadamChip();
        madam.Poke(0x008, 0);
        var writer = new StateWriter();
        madam.Save(writer);

        var copy = new MadamChip();
        copy.Load(new StateReader(writer.ToArray()));

        Assert.False(copy.ClutDmaEnabled);
        Assert.False(copy.VideoDmaEnabled);
    }

    [Fact]
    public void StateRoundtrip_PreservesEnabledGates()
    {
        var source = new FiveDoMachine();
        source.Madam.Poke(0x008, 0x00006000);

        using var state = new MemoryStream();
        source.SaveState(state);
        state.Position = 0;

        var copy = new FiveDoMachine();
        copy.Madam.Poke(0x008, 0);
        copy.LoadState(state);

        Assert.True(copy.Madam.ClutDmaEnabled);
        Assert.True(copy.Madam.VideoDmaEnabled);
    }

    [Fact]
    public void PbusDma_LeavesTheFreeDoControlSlotUntouched()
    {
        var memory = new FiveDoMemory();
        var interrupts = new FakeInterruptSink();
        var madam = new MadamChip { Bus = memory.Bus, Interrupts = interrupts };
        madam.KeyPressed([0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88]);
        madam.Poke(0x570, 0x1000);
        madam.Poke(0x574, 8);
        madam.Poke(0x578, 0);

        madam.Poke(0x08, 0x8000);

        // FreeDO starts the packet at DEST + 4. Opera writes the Portfolio
        // control-port sludge word (0xFFFFFFFF) at the original DEST first.
        Assert.Equal(0u, memory.Ram.Read32(0x1000));
        Assert.Equal(0x44332211u, memory.Ram.Read32(0x1004));
        Assert.Equal(0x1008u, madam.Peek(0x570));
        Assert.Equal(8u, madam.Peek(0x578));
        Assert.Equal(1, madam.PbusReadCount);
        Assert.Equal(1, interrupts.GenerateCount);
        Assert.Equal(1u, interrupts.LastReason2);
        Assert.Equal(0u, madam.Peek(0x08) & 0x8000u);
    }

    [Fact]
    public void PbusDma_CpuVisibleBytesAreBigEndianOnTheBus()
    {
        var memory = new FiveDoMemory();
        var madam = new MadamChip { Bus = memory.Bus };
        madam.KeyPressed([0x11, 0x22, 0x33, 0x44]);
        madam.Poke(0x570, 0x1000);
        madam.Poke(0x574, 8);

        madam.Poke(0x08, 0x8000);

        Assert.Equal(0x44u, memory.Bus.Read8(0x1004));
        Assert.Equal(0x33u, memory.Bus.Read8(0x1005));
        Assert.Equal(0x22u, memory.Bus.Read8(0x1006));
        Assert.Equal(0x11u, memory.Bus.Read8(0x1007));
    }

    [Theory]
    [InlineData(0, 0, 0x1004u, 4u, 1)]
    [InlineData(4, 0, 0x1004u, 4u, 1)]
    [InlineData(8, 1, 0x1008u, 8u, 1)]
    [InlineData(16, 3, 0x1010u, 16u, 1)]
    [InlineData(24, 5, 0x1018u, 24u, 1)]
    public void PbusDma_MatchesTheFreeDoLengthAndProgressLoop(
        int length,
        int words,
        uint expectedDestination,
        uint expectedProgress,
        int expectedReads)
    {
        var memory = new FiveDoMemory();
        var madam = new MadamChip { Bus = memory.Bus };
        madam.KeyPressed([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]);
        madam.Poke(0x570, 0x1000);
        madam.Poke(0x574, (uint)length);
        madam.Poke(0x578, 0);

        madam.Poke(0x08, 0x8000);

        Assert.Equal(expectedDestination, madam.Peek(0x570));
        Assert.Equal(expectedProgress, madam.Peek(0x578));
        Assert.Equal(expectedReads, madam.PbusReadCount);
        Assert.Equal(0u, memory.Ram.Read32(0x1000 + (((uint)words + 1) * 4)));
        Assert.Equal(0xFFFFFFFCu, madam.Peek(0x574));
    }

    [Fact]
    public void PbusDma_NegativeLengthMakesNoProgressButAcknowledges()
    {
        var memory = new FiveDoMemory();
        var interrupts = new FakeInterruptSink();
        var madam = new MadamChip { Bus = memory.Bus, Interrupts = interrupts };
        madam.KeyPressed([1, 2, 3, 4]);
        madam.Poke(0x570, 0x1000);
        madam.Poke(0x574, 0xfffffffc);
        madam.Poke(0x578, 0);

        madam.Poke(0x08, 0x8000);

        Assert.Equal(0x1000u, madam.Peek(0x570));
        Assert.Equal(0u, madam.Peek(0x578));
        Assert.Equal(0, madam.PbusReadCount);
        Assert.Equal(1, interrupts.GenerateCount);
        Assert.Equal(0u, memory.Ram.Read32(0x1000));
    }
}
