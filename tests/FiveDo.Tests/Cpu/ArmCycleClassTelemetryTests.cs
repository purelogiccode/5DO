using System.Collections.Generic;
using System.Linq;
using FiveDo.Core;
using FiveDo.Cpu;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmCycleClassTelemetryTests
{
    [Fact]
    public void TelemetryIsDisabledByDefault()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Nop());

        Assert.Equal(1, rig.Cpu.Step());
        Assert.All(rig.Cpu.GetCycleClassUsage(), usage => Assert.Equal(0, usage.Instructions));
        Assert.All(rig.Cpu.GetCycleClassUsage(), usage => Assert.Equal(0, usage.Cycles));
        Assert.All(rig.Cpu.GetCycleClassUsage(), usage => Assert.Equal(0, usage.CyclesPerInstruction));
    }

    [Theory]
    [InlineData(ArmInstructionClass.Multiply, 0xe0000091, 3, 3)]
    [InlineData(ArmInstructionClass.Swap, 0xe1000090, 10, 6)]
    [InlineData(ArmInstructionClass.DataProcessing, 0xe3a00001, 1, 1)]
    [InlineData(ArmInstructionClass.Undefined, 0xe6000010, 6, 4)]
    [InlineData(ArmInstructionClass.SingleDataTransfer, 0xe4100001, 6, 4)]
    [InlineData(ArmInstructionClass.BlockTransfer, 0xe8900006, 7, 5)]
    [InlineData(ArmInstructionClass.Branch, 0xea000003, 6, 4)]
    [InlineData(ArmInstructionClass.SoftwareInterrupt, 0xef000000, 6, 4)]
    [InlineData(ArmInstructionClass.Coprocessor, 0xec000000, 6, 4)]
    public void CycleCosts_MatchTheOperaClassTable(
        ArmInstructionClass instructionClass,
        uint instruction,
        int freeDoCycles,
        int arm60Cycles)
    {
        Assert.Equal(instructionClass, ArmDecoder.Classify(instruction));
        Assert.Equal(freeDoCycles, Step(ArmTimingMode.FreeDoCompatible, instruction));
        Assert.Equal(arm60Cycles, Step(ArmTimingMode.Arm60Datasheet, instruction));
    }

    [Theory]
    [InlineData(ArmTimingMode.FreeDoCompatible, 11)]
    [InlineData(ArmTimingMode.Arm60Datasheet, 7)]
    public void SingleDataTransferLoadToPc_ChargesTheExtraSN(ArmTimingMode timing, int expected)
    {
        Assert.Equal(expected, Step(timing, ArmTestAssembler.LdrImmediate(15, 1)));
    }

    [Theory]
    [InlineData(ArmTimingMode.FreeDoCompatible, 12)]
    [InlineData(ArmTimingMode.Arm60Datasheet, 8)]
    public void BlockTransferWithPc_ChargesTheExtraSN(ArmTimingMode timing, int expected)
    {
        var cycles = Step(
            timing,
            ArmTestAssembler.Block(true, 0, 0x8002, preIndex: false, add: true),
            0x1000,
            out var usage);
        Assert.Equal(expected, cycles);
        Assert.Equal(ArmInstructionClass.BlockTransfer, usage.First(u => u.Instructions > 0).InstructionClass);
    }

    [Fact]
    public void EmptyBlockTransfer_UsesTheReferenceWrap()
    {
        Assert.Equal(5, Step(ArmTimingMode.FreeDoCompatible, ArmTestAssembler.Block(true, 0, 0, false, true)));
        Assert.Equal(3, Step(ArmTimingMode.Arm60Datasheet, ArmTestAssembler.Block(true, 0, 0, false, true)));
    }

    [Fact]
    public void DataProcessingShiftAndPcVariantsMatchTheClassTable()
    {
        var shifted = ArmTestAssembler.DpRegisterShiftedByRegister(13, 1, 0, 0, 2);
        Assert.Equal(2, Step(ArmTimingMode.FreeDoCompatible, shifted));
        Assert.Equal(2, Step(ArmTimingMode.Arm60Datasheet, shifted));

        var toPc = ArmTestAssembler.DpRegister(13, 15, 0, 0);
        Assert.Equal(6, Step(ArmTimingMode.FreeDoCompatible, toPc));
        Assert.Equal(4, Step(ArmTimingMode.Arm60Datasheet, toPc));

        var shiftedToPc = ArmTestAssembler.DpRegisterShiftedByRegister(13, 15, 0, 0, 2);
        Assert.Equal(7, Step(ArmTimingMode.FreeDoCompatible, shiftedToPc));
        Assert.Equal(5, Step(ArmTimingMode.Arm60Datasheet, shiftedToPc));
    }

    [Theory]
    [InlineData(ArmTimingMode.FreeDoCompatible)]
    [InlineData(ArmTimingMode.Arm60Datasheet)]
    public void MultiplyEarlyOutAndCapAreModeIndependent(ArmTimingMode timing)
    {
        var small = new ArmTestRig(new EmulatorTuning { ArmTiming = timing });
        small.LoadProgram(
            ArmTestAssembler.Mov(0, 6),
            ArmTestAssembler.Mov(1, 7),
            ArmTestAssembler.Mul(2, 0, 1));
        small.Run(2);
        Assert.Equal(4, small.Cpu.Step());

        var capped = new ArmTestRig(new EmulatorTuning { ArmTiming = timing });
        capped.LoadProgram(
            ArmTestAssembler.Mvn(0, 0),
            ArmTestAssembler.Mvn(1, 0),
            ArmTestAssembler.Mul(2, 0, 1));
        capped.Run(2);
        Assert.Equal(17, capped.Cpu.Step());
    }

    [Theory]
    [InlineData(ArmTimingMode.FreeDoCompatible)]
    [InlineData(ArmTimingMode.Arm60Datasheet)]
    public void ConditionFailedSlot_CostsOneFetchCycle(ArmTimingMode timing)
    {
        var instruction = ArmTestAssembler.Mov(0, 1, condition: 0x0);
        var cycles = Step(timing, instruction, 0x1000, out var usage);

        Assert.Equal(1, cycles);
        var dataProcessing = usage.Single(u => u.InstructionClass == ArmInstructionClass.DataProcessing);
        Assert.Equal(1, dataProcessing.Instructions);
        Assert.Equal(1, dataProcessing.Cycles);
    }

    [Theory]
    [InlineData(ArmTimingMode.FreeDoCompatible)]
    [InlineData(ArmTimingMode.Arm60Datasheet)]
    public void MrsAndMsr_CostOneCycle(ArmTimingMode timing)
    {
        Assert.Equal(1, Step(timing, ArmTestAssembler.Mrs(0)));
        Assert.Equal(1, Step(timing, ArmTestAssembler.Msr(0)));
    }

    [Theory]
    [InlineData(ArmTimingMode.FreeDoCompatible, 6, 8)]
    [InlineData(ArmTimingMode.Arm60Datasheet, 4, 4)]
    public void ByteTransfersKeepTheWordClassCosts(ArmTimingMode timing, int load, int store)
    {
        Assert.Equal(load, Step(timing, ArmTestAssembler.LdrImmediate(0, 1, byteAccess: true)));
        Assert.Equal(store, Step(timing, ArmTestAssembler.StrImmediate(0, 1, byteAccess: true)));
    }

    [Theory]
    [InlineData(ArmTimingMode.FreeDoCompatible)]
    [InlineData(ArmTimingMode.Arm60Datasheet)]
    public void LedgerTotalsMatchTheCpuCounters(ArmTimingMode timing)
    {
        var rig = new ArmTestRig(new EmulatorTuning { ArmTiming = timing });
        rig.Cpu.CycleClassTelemetryEnabled = true;
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 6),
            ArmTestAssembler.Mov(1, 7),
            ArmTestAssembler.Mul(2, 0, 1),
            ArmTestAssembler.Nop(),
            ArmTestAssembler.Branch(0x1010, 0x1010));

        rig.Run(5);

        var usage = rig.Cpu.GetCycleClassUsage();
        Assert.Equal(9, usage.Count);
        Assert.Equal(rig.Cpu.InstructionsExecuted, usage.Sum(u => u.Instructions));
        Assert.Equal(rig.Cpu.CyclesExecuted, usage.Sum(u => u.Cycles));
        Assert.Equal(3, usage.Single(u => u.InstructionClass == ArmInstructionClass.DataProcessing).Instructions);
        Assert.Equal(1, usage.Single(u => u.InstructionClass == ArmInstructionClass.Multiply).Instructions);
        Assert.Equal(1, usage.Single(u => u.InstructionClass == ArmInstructionClass.Branch).Instructions);
    }

    [Fact]
    public void ResetCycleClassUsage_KeepsArchitecturalCounters()
    {
        var rig = new ArmTestRig();
        rig.Cpu.CycleClassTelemetryEnabled = true;
        rig.LoadProgram(ArmTestAssembler.Nop(), ArmTestAssembler.Nop());
        rig.Run(2);

        var instructions = rig.Cpu.InstructionsExecuted;
        var cycles = rig.Cpu.CyclesExecuted;
        rig.Cpu.ResetCycleClassUsage();

        Assert.Equal(instructions, rig.Cpu.InstructionsExecuted);
        Assert.Equal(cycles, rig.Cpu.CyclesExecuted);
        Assert.All(rig.Cpu.GetCycleClassUsage(), usage => Assert.Equal(0, usage.Instructions));
        Assert.All(rig.Cpu.GetCycleClassUsage(), usage => Assert.Equal(0, usage.Cycles));
    }

    [Fact]
    public void StateLoadClearsTheLedger()
    {
        var rig = new ArmTestRig();
        rig.Cpu.CycleClassTelemetryEnabled = true;
        rig.LoadProgram(ArmTestAssembler.Nop(), ArmTestAssembler.Nop());
        rig.Run(2);

        var writer = new StateWriter();
        rig.Cpu.SaveState(writer);
        rig.Cpu.LoadState(new StateReader(writer.ToArray()));

        Assert.Equal(0, rig.Cpu.InstructionsExecuted);
        Assert.All(rig.Cpu.GetCycleClassUsage(), usage => Assert.Equal(0, usage.Instructions));
        Assert.All(rig.Cpu.GetCycleClassUsage(), usage => Assert.Equal(0, usage.Cycles));
    }

    [Fact]
    public void TelemetryDoesNotChangeExecution()
    {
        uint[] program =
        [
            ArmTestAssembler.Mov(0, 6),
            ArmTestAssembler.AddImmediate(1, 0, 7),
            ArmTestAssembler.Mul(2, 0, 1),
        ];

        var disabled = new ArmTestRig();
        disabled.LoadProgram(program);
        var disabledCycles = disabled.Run(program.Length);

        var enabled = new ArmTestRig();
        enabled.Cpu.CycleClassTelemetryEnabled = true;
        enabled.LoadProgram(program);
        var enabledCycles = enabled.Run(program.Length);

        Assert.Equal(disabledCycles, enabledCycles);
        Assert.Equal(disabled.Cpu.ProgramCounter, enabled.Cpu.ProgramCounter);
        Assert.Equal(disabled.Cpu.Cpsr, enabled.Cpu.Cpsr);
        Assert.Equal(disabled.Cpu.ReadRegister(2), enabled.Cpu.ReadRegister(2));
    }

    private static int Step(ArmTimingMode timing, uint instruction, uint address = 0x1000)
    {
        return Step(timing, instruction, address, out _);
    }

    private static int Step(
        ArmTimingMode timing,
        uint instruction,
        uint address,
        out IReadOnlyList<ArmCycleClassUsage> usage)
    {
        var rig = new ArmTestRig(new EmulatorTuning { ArmTiming = timing });
        rig.Cpu.CycleClassTelemetryEnabled = true;
        rig.LoadProgramAt(address, instruction);
        var cycles = rig.Cpu.Step();
        usage = rig.Cpu.GetCycleClassUsage();
        return cycles;
    }
}
