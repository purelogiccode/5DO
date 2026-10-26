using FiveDo.Core;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmControlFlowTests
{
    [Fact]
    public void Branch_ChangesProgramCounter()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(
             0x1000,
            ArmTestAssembler.Branch(0x1010, 0x1000),
            ArmTestAssembler.Mov(0, 0x11),
            ArmTestAssembler.Mov(0, 0x22));

        rig.Cpu.Step();

        Assert.Equal(0x1010u, rig.Cpu.ProgramCounter);
        Assert.Equal(0u, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void BranchLink_StoresReturnAddress()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(
             0x1000,
            ArmTestAssembler.Branch(0x1010, 0x1000, link: true));

        rig.Cpu.Step();

        Assert.Equal(0x1004u, rig.Cpu.ReadRegister(14));
        Assert.Equal(0x1010u, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void ConditionalBranch_OnlyTakenWhenConditionPasses()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(
             0x1000,
            ArmTestAssembler.CmpImmediate(0, 0),
            ArmTestAssembler.Branch(0x1010, 0x1004, condition: 0x0),
            ArmTestAssembler.Nop());

        rig.Run(2);

        Assert.Equal(0x1010u, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void Swi_EntersSupervisorModeAndVectors()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(
             0x1000,
            ArmTestAssembler.Swi(0x123456));

        var oldCpsr = rig.Cpu.Cpsr;
        rig.Cpu.Step();

        Assert.Equal(0x00000008u, rig.Cpu.ProgramCounter);
        Assert.Equal(0x13u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 7) & 1);
        Assert.Equal(oldCpsr, rig.Cpu.State.ReadSpsr(3));
        Assert.Equal(0x1004u, rig.Cpu.ReadRegister(14));
    }

    [Fact]
    public void UndefinedInstruction_EntersUndefinedMode()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, 0xe6000010);

        var oldCpsr = rig.Cpu.Cpsr;
        rig.Cpu.Step();

        Assert.Equal(0x00000004u, rig.Cpu.ProgramCounter);
        Assert.Equal(0x1bu, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 7) & 1);
        Assert.Equal(oldCpsr, rig.Cpu.State.ReadSpsr(5));
        Assert.Equal(0x1004u, rig.Cpu.ReadRegister(14));
    }

    [Fact]
    public void Fiq_EntersFiqModeAfterInstruction()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Nop());
        var oldCpsr = rig.Cpu.Cpsr;
        rig.Interrupts.NeedFiq = true;

        rig.Cpu.Step();

        Assert.Equal(0x0000001cu, rig.Cpu.ProgramCounter);
        Assert.Equal(0x11u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 6) & 1);
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 7) & 1);
        Assert.Equal(oldCpsr, rig.Cpu.State.ReadSpsr(1));
        Assert.Equal(0x1008u, rig.Cpu.ReadRegister(14));
    }

    [Fact]
    public void Fiq_IsMaskedByFBit()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Nop());
        rig.Cpu.Cpsr = 0x53;
        rig.Interrupts.NeedFiq = true;

        rig.Cpu.Step();

        Assert.Equal(0x1004u, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void FiqPending_TriggersFiqWithoutInterruptSink()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Nop());
        rig.Cpu.FiqPending = true;

        rig.Cpu.Step();

        Assert.Equal(0x0000001cu, rig.Cpu.ProgramCounter);
        Assert.False(rig.Cpu.FiqPending);
    }

    [Fact]
    public void AnvilBiosSkip_AddsSixtyFourToPc()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Nop());
        rig.Cpu.Compatibility.BiosAnvil = true;

        rig.Cpu.Step();

        Assert.Equal(0x1044u, rig.Cpu.ProgramCounter);
        Assert.Equal(1, rig.Cpu.Compatibility.IsAnvil);
        Assert.False(rig.Cpu.Compatibility.BiosAnvil);
    }

    [Fact]
    public void CrashNburnPcFix_AppliesWhenFixModeEnabled()
    {
        var rig = new ArmTestRig(compatibility: new CompatibilityState { FixMode = FixMode.BitTiming1 });
        rig.LoadProgramAt(0x94d60, ArmTestAssembler.Nop());
        rig.Cpu.WriteRegister(0, 0x113000);
        rig.Cpu.WriteRegister(1, 0x113000);

        rig.Cpu.Step();

        Assert.Equal(0x9e9d0u, rig.Cpu.ProgramCounter);
        Assert.True(rig.Cpu.Compatibility.Cnbfix);
    }
}
