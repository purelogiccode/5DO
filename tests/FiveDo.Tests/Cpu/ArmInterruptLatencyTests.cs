using FiveDo.Core;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmInterruptLatencyTests
{
    [Fact]
    public void FiqEntry_ReportsCycleAndReturnAddress()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Nop());
        long? entryCycle = null;
        uint? returnAddress = null;
        var calls = 0;
        rig.Cpu.FiqEntered = (cycle, address) =>
        {
            calls++;
            entryCycle = cycle;
            returnAddress = address;
        };

        rig.Interrupts.NeedFiq = true;
        var cycles = rig.Cpu.Step();

        Assert.Equal(1, cycles);
        Assert.Equal(1, calls);
        Assert.Equal(1, entryCycle);
        Assert.Equal(0x1008u, returnAddress);
        Assert.Equal(1, rig.Cpu.CyclesExecuted);
        Assert.Equal(0x1cu, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void FiqEntry_CycleIncludesTheInterruptedInstructionCost()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Mov(0, 0x100));
        rig.WriteRam(0x100, 0x11223344);
        long? entryCycle = null;
        rig.Cpu.FiqEntered = (cycle, _) => entryCycle = cycle;

        rig.Interrupts.NeedFiq = true;
        var cycles = rig.Cpu.Step();

        Assert.Equal(1, cycles);
        Assert.Equal(1, entryCycle);

        rig.Cpu.FiqEntered = (cycle, _) => entryCycle = cycle;
        rig.Cpu.Cpsr = 0x13;
        rig.Cpu.ProgramCounter = 0x1000;
        rig.LoadProgram(ArmTestAssembler.LdrImmediate(1, 0));
        rig.Interrupts.NeedFiq = true;
        cycles = rig.Cpu.Step();

        Assert.Equal(6, cycles);
        Assert.Equal(7, entryCycle);
        Assert.Equal(7, rig.Cpu.CyclesExecuted);
    }

    [Fact]
    public void FiqEntry_IsNotReportedWhileMasked()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Nop());
        var calls = 0;
        rig.Cpu.FiqEntered = (_, _) => calls++;
        rig.Cpu.Cpsr = 0x53;
        rig.Interrupts.NeedFiq = true;

        rig.Cpu.Step();

        Assert.Equal(0, calls);
        Assert.Equal(0x1004u, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void FiqEntry_MatchesATrackedRequestToTheInstructionLatency()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.LdrImmediate(1, 0, 0x100));
        rig.WriteRam(0x100, 0x11223344);
        var tracker = new FiqLatencyTracker();
        FiqEntry? entry = null;
        rig.Cpu.FiqEntered = (cycle, address) =>
            entry = tracker.OnEntered(cycle, address, 1u << 9, 0);

        tracker.OnRequested(new FiqRequest(0, 1u << 9, 0));
        rig.Interrupts.NeedFiq = true;
        rig.Cpu.Step();

        Assert.NotNull(entry);
        Assert.True(entry.Value.MatchedRequest);
        Assert.Equal(6, entry.Value.LatencyCycles);
        Assert.Equal(1, tracker.MatchedEntries);
        Assert.Equal(6, tracker.MaxLatencyCycles);
    }

    [Fact]
    public void FiqReentry_CountsAsUnmatchedWhenTheLevelStaysPending()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Nop());
        var tracker = new FiqLatencyTracker();
        var entries = 0;
        rig.Cpu.FiqEntered = (cycle, address) =>
        {
            entries++;
            tracker.OnEntered(cycle, address, 1u << 9, 0);
        };

        tracker.OnRequested(new FiqRequest(0, 1u << 9, 0));
        rig.Interrupts.NeedFiq = true;
        rig.Cpu.Step();
        Assert.Equal(1, entries);
        Assert.Equal(1, tracker.MatchedEntries);

        // The guest clears F but the level-triggered CLIO source is still asserted.
        rig.Cpu.WriteRegister(0, 0x13);
        rig.Memory.Ram.Write32(0x1c, ArmTestAssembler.Msr(0, mask: 0x1));
        rig.Cpu.Step();

        Assert.Equal(2, entries);
        Assert.Equal(1, tracker.UnmatchedEntries);
        Assert.Equal(0x1cu, rig.Cpu.ProgramCounter);
        Assert.Equal(0x11u, rig.Cpu.Cpsr & 0x1f);
    }

    [Fact]
    public void FiqReturn_RestoresTheInterruptedBankAndContinues()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Nop(), ArmTestAssembler.Nop());
        rig.Memory.Ram.Write32(
            0x1c,
            ArmTestAssembler.DpImmediate(2, 15, 14, 4, setFlags: true));
        rig.Interrupts.NeedFiq = true;

        rig.Cpu.Step();
        Assert.Equal(0x1cu, rig.Cpu.ProgramCounter);

        rig.Interrupts.NeedFiq = false;
        rig.Cpu.Step();

        Assert.Equal(0x1004u, rig.Cpu.ProgramCounter);
        Assert.Equal(0x13u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 6) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 7) & 1);
    }

    [Fact]
    public void CyclesExecuted_AccumulatesAndResetsWithTheCpu()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Nop(),
            ArmTestAssembler.Mov(0, 0x22),
            ArmTestAssembler.LdrImmediate(1, 0));

        rig.Run(3);

        Assert.Equal(1 + 1 + 6, rig.Cpu.CyclesExecuted);

        rig.Cpu.Reset();

        Assert.Equal(0, rig.Cpu.CyclesExecuted);
        Assert.Equal(0, rig.Cpu.InstructionsExecuted);
    }
}
