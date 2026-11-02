using System;
using System.Collections.Generic;
using FiveDo.Core;
using FiveDo.Dsp;
using DspCore = FiveDo.Dsp.Dsp;

namespace FiveDo.Tests.Dsp;

/// <summary>
/// Instruction-level tests for the M5 DSP core. Programs are assembled from raw 16-bit words
/// and executed by <see cref="Dsp.Loop"/>, matching the reference's word-oriented encoding.
/// </summary>
public sealed class DspTests
{

    private const ushort Sleep = 0x8380;

    [Fact]
    public void Loop_WhenStopped_ReturnsSampleWithoutExecuting()
    {
        var dsp = new DspCore();

        var first = dsp.Loop();
        Assert.Equal(0u, first);
        Assert.Equal(0u, dsp.ProgramCounter);

        var second = dsp.Loop();
        Assert.Equal(first, second);
    }

    [Fact]
    public void Loop_AddsTwoOperandsAndWritesBackToFirst()
    {
        var program = new ProgramBuilder();
        program.Seed(0x100, 0x10);
        program.Seed(0x101, 0x04);
        program.Word(Alu(2, muxA: 1, muxB: 2, numOps: 2));
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(NonRegister(0x101));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        Assert.Equal(0x14, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void Loop_SubtractsAndWritesBack()
    {
        var program = new ProgramBuilder();
        program.Seed(0x100, 0x08);
        program.Seed(0x101, 0x04);
        program.Word(Alu(4, muxA: 1, muxB: 2, numOps: 2));
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(NonRegister(0x101));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        Assert.Equal(0x04, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void Loop_LogicalOperationsMatchTheAlu()
    {
        var program = new ProgramBuilder();
        program.Seed(0x100, 0x0C);
        program.Seed(0x101, 0x08);
        program.Word(Alu(14, muxA: 1, muxB: 2, numOps: 2)); // XOR
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(NonRegister(0x101));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        Assert.Equal(0x04, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void Loop_ShiftLeftProducesExpectedHighHalf()
    {
        var program = new ProgramBuilder();
        program.Seed(0x100, 0x10);
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1, bs: 1));
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        Assert.Equal(0x20, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void Loop_ArithmeticShiftRightSignExtends()
    {
        var program = new ProgramBuilder();
        program.PrepareSignBit(0x100, 0x110);
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1, bs: 9)); // arithmetic >> 16
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        Assert.Equal(0xFFFF, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void Loop_CarryShiftMovesSignBitIntoTemporaryCarry()
    {
        var program = new ProgramBuilder();
        program.PrepareSignBit(0x100, 0x110);
        program.Seed(0x120, 0x0008);
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 2, bs: 8)); // carry shift, count from operand
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(NonRegister(0x120));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        // The shifted-out sign bit is inserted at bit 16, so the high halfword becomes 0x0001.
        Assert.Equal(0x0001, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void Loop_ClipSaturatesOnOverflow()
    {
        var program = new ProgramBuilder();
        program.Seed(0x100, 0x3FFC);
        program.Seed(0x110, 0x3FFC);
        program.Word(Alu(2, muxA: 1, muxB: 2, numOps: 2));
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(NonRegister(0x110));
        program.Seed(0x111, 0x0008);
        program.Word(Alu(2, muxA: 1, muxB: 2, numOps: 2, bs: 7)); // ADD then CLIP
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(NonRegister(0x111));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        // 0x7FF8 + 8 overflows to 0x80000000; CLIP saturates to 0x7FFFF000.
        Assert.Equal(0x7FFF, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void ProgramBuilder_Seed_WritesValue()
    {
        var program = new ProgramBuilder();
        program.Seed(0x103, 0x2000);
        program.Word(Sleep);
        DspCore dsp = Run(program);
        Assert.Equal(0x2000, dsp.ReadInstructionMemory(0x103));
    }

    [Fact]
    public void Loop_MultiplierBopMasksAfterMultiplying()
    {
        var program = new ProgramBuilder();
        program.Seed(0x103, 0x2000);

        // Y = 1 << 16.
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(Immediate(1));

        // Add (0x2000 * (Y >> 15)) & ~1 twice: 0x10000 -> 0x14000 -> 0x18000.
        program.Word(Alu(2, muxA: 0, muxB: 3, numOps: 1));
        program.Word(Immediate(0x400, justify: true));
        program.Word(Alu(2, muxA: 0, muxB: 3, numOps: 1));
        program.Word(Immediate(0x400, justify: true));

        // Y = (0x2000 * (Y >> 15)) & ~1 = 0x6000, then shift left 8 in two steps.
        // Masking (Y >> 15) before the multiply would produce 0x4000 and high half 0x0040.
        program.Word(Alu(2, muxA: 1, muxB: 3, numOps: 2, bs: 4));
        program.Word(NonRegister(0x103)); // MULT1
        program.Word(Immediate(0)); // ALU1
        program.Word(Alu(0, muxA: 0, muxB: 0, numOps: 1, bs: 4));
        program.Word(NonRegister(0x103, writeBack: true));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        Assert.Equal(0x0060, dsp.ReadInstructionMemory(0x103));
    }

    [Fact]
    public void Loop_UsesImmediateOperandsWithJustifyShift()
    {
        var program = new ProgramBuilder();
        program.Seed(0x100, 0x00);
        program.Word(Alu(12, muxA: 1, muxB: 2, numOps: 2, bs: 6));
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(Immediate(0x123));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        // The one-bit JUSTIFY field is 0, so the loader shifts by 0. The immediate is placed in
        // the high half by MUXB=2 and then shifted left another eight by BS=6.
        Assert.Equal(0x2300, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void Loop_JustifyBitShiftsImmediateByThree()
    {
        var program = new ProgramBuilder();
        program.Seed(0x100, 0x00);
        program.Word(Alu(12, muxA: 1, muxB: 2, numOps: 2, bs: 6));
        program.Word(NonRegister(0x100, writeBack: true));
        program.Word(Immediate(0x123, justify: true));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        // JUSTIFY is sign-extended (-1) and masked, so the immediate is shifted left three.
        Assert.Equal(0x1800, dsp.ReadInstructionMemory(0x100));
    }

    [Fact]
    public void Loop_SupportsR3OperandFormat()
    {
        var program = new ProgramBuilder();
        program.Word(SetRmap(0));
        program.Seed(0x104, 0x00);
        program.Seed(0x105, 0x08);
        program.Seed(0x106, 0x10);
        program.Word(Alu(14, muxA: 1, muxB: 2, numOps: 3)); // XOR ALU1 (R3), ALU2 (R2)
        program.Word(R3Operand(r1: 4, r2: 5, r3: 6));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        // R1 is the write-back register; R3 xor R2 = 0x10 xor 0x08 = 0x18.
        Assert.Equal(0x18, dsp.ReadInstructionMemory(0x104));
    }

    [Fact]
    public void Loop_SupportsR2OperandFormat()
    {
        var program = new ProgramBuilder();
        program.Word(SetRmap(0));
        program.Seed(0x104, 0x0C);
        program.Seed(0x105, 0x04);
        program.Word(Alu(10, muxA: 1, muxB: 2, numOps: 2)); // AND
        program.Word(R2Operand(r1: 4, r2: 5, writeBack: true));
        program.Word(Sleep);

        DspCore dsp = Run(program);
        Assert.Equal(0x04, dsp.ReadInstructionMemory(0x104));
    }

    [Fact]
    public void Loop_BranchTakenWhenZeroAndExact()
    {
        DspCore taken = RunBranchProgram(operand: 0x00);
        Assert.Equal(0x00, taken.ReadInstructionMemory(0x101));

        DspCore notTaken = RunBranchProgram(operand: 0x04);
        Assert.Equal(0x28, notTaken.ReadInstructionMemory(0x101));
    }

    [Fact]
    public void Loop_ReturnsPackedStereoFromDataMemory()
    {
        var dsp = new DspCore();
        dsp.WriteInstructionMemory(0x7e, 0x12);
        dsp.WriteInstructionMemory(0x7f, 0x34);
        var program = new ProgramBuilder();
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x3FE, writeBack: true));
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x3FF, writeBack: true));
        program.Word(Sleep);

        _ = Run(program, dsp);
        Assert.Equal(0x00340012u, dsp.Loop());
    }

    [Fact]
    public void Semaphore_RoundtripsArmAndDspWrites()
    {
        var dsp = new DspCore();

        // The DSP writes Sema4Data and then acknowledges with Sema4Status bit 0.
        var program = new ProgramBuilder();
        program.Word(Alu(12, muxA: 1, muxB: 2, numOps: 2));
        program.Word(NonRegister(0x3ED, writeBack: true)); // writes 0x04 to Sema4Data
        program.Word(Immediate(0x01));
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x3EC, writeBack: true)); // DSP ACK
        program.Word(Sleep);

        _ = Run(program, dsp);
        Assert.Equal(0x00050001u, dsp.ArmReadSemaphore());

        dsp.ArmWriteSemaphore(0x1234);
        Assert.Equal(0x00081234u, dsp.ArmReadSemaphore());
        Assert.Equal(0x8, dsp.SemaphoreStatus);
        Assert.Equal(0x1234, dsp.SemaphoreData);
    }

    [Fact]
    public void Loop_TracksPerTickInstructionMetrics()
    {
        var dsp = new DspCore();
        var program = new ProgramBuilder();
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x3EE, writeBack: true));
        program.Word(Sleep);
        for (var i = 0; i < program.Words.Count; i++)
        {
            dsp.WriteMemory(i, program.Words[i]);
        }

        dsp.SetRunning(true);
        _ = dsp.Loop();
        dsp.SetRunning(false);
        _ = dsp.Loop();

        Assert.Equal(2, dsp.LoopCalls);
        Assert.Equal(1, dsp.RunningLoops);
        Assert.Equal(1, dsp.SleepingLoops);
        Assert.True(dsp.InstructionsExecuted > 0);
        Assert.Equal(dsp.InstructionsExecuted, dsp.MaxInstructionsPerLoop);
        Assert.Equal(dsp.InstructionsExecuted, dsp.AverageInstructionsPerLoop);
    }

    [Fact]
    public void Loop_GeneratesAudioFiqWhenDspWritesInterruptRegister()
    {
        var host = new RecordingDspHost();
        var dsp = new DspCore { Host = host };
        var program = new ProgramBuilder();
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x3EE, writeBack: true));
        program.Word(Sleep);

        _ = Run(program, dsp);

        Assert.Equal(1, host.Fiqs);
        Assert.Equal(0x800u, host.LastFiqReason);
    }

    [Fact]
    public void Loop_ReadsCpuSuppliedRegistersBeforeTheFifo()
    {
        var host = new RecordingDspHost { InputFifoValue = 0xBEEF };
        var dsp = new DspCore { Host = host };
        dsp.WriteInstructionMemory(0x70, 0x0055);

        var program = new ProgramBuilder();
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x70));
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x70));
        program.Word(Sleep);

        _ = Run(program, dsp);

        Assert.Equal(1, host.InputFifoReads);
        Assert.Equal(0, host.LastInputChannel);
    }

    [Fact]
    public void Loop_ReadsCpuSuppliedHighFifoFromInstructionMemory()
    {
        var host = new RecordingDspHost { InputFifoValue = 0xBEEF };
        var dsp = new DspCore { Host = host };
        dsp.WriteInstructionMemory(0x70, 0x1234);

        var program = new ProgramBuilder();
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0xF0));
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x3F0, writeBack: true));
        program.Word(Sleep);

        _ = Run(program, dsp);

        Assert.Equal(0, host.InputFifoReads);
        Assert.Equal(1, host.OutputFifoWrites);
        Assert.Equal(0x1234, host.LastOutputValue);
    }

    [Fact]
    public void Loop_WritesOutputFifoThroughHost()
    {
        var host = new RecordingDspHost();
        var dsp = new DspCore { Host = host };
        var program = new ProgramBuilder();
        program.Word(Alu(12, muxA: 1, muxB: 2, numOps: 2));
        program.Word(NonRegister(0x3F0, writeBack: true));
        program.Word(Immediate(0x02));
        program.Word(Sleep);

        _ = Run(program, dsp);

        Assert.Equal(1, host.OutputFifoWrites);
        Assert.Equal(0, host.LastOutputChannel);
        Assert.Equal(0x02, host.LastOutputValue);
    }

    [Fact]
    public void FastRandom_MatchesReferenceSequence()
    {
        var dsp = new DspCore();
        Assert.Equal(0x0622u, dsp.FastRandom());
        Assert.Equal(0xA33Bu, dsp.FastRandom());
        Assert.Equal(0xB540u, dsp.FastRandom());
    }

    [Fact]
    public void ProgramMemory_InitializesToSleep()
    {
        var dsp = new DspCore();
        Assert.Equal(0x8380, dsp.ReadProgramMemory(0));
        Assert.Equal(0x8380, dsp.ReadProgramMemory(0x7ff));
    }

    private static DspCore Run(ProgramBuilder program, DspCore? dsp = null)
    {
        dsp ??= new DspCore();
        for (var i = 0; i < program.Words.Count; i++)
        {
            dsp.WriteMemory(i, program.Words[i]);
        }

        dsp.SetRunning(true);
        _ = dsp.Loop();
        dsp.SetRunning(false);
        return dsp;
    }

    private static DspCore RunBranchProgram(ushort operand)
    {
        var program = new ProgramBuilder();
        program.Seed(0x100, operand);

        // Compare path: load the operand into the accumulator (Y = A), then branch on Z & exact.
        program.Word(Alu(0, muxA: 1, muxB: 0, numOps: 1));
        program.Word(NonRegister(0x100));
        var branchIndex = program.Count;
        program.Word(0xA000); // patched below to target the SLEEP after the marker
        program.Seed(0x101, 0x28);
        var sleepIndex = program.Count;
        program.Word(Sleep);
        program.Patch(branchIndex, (ushort)(0xA000 | sleepIndex));

        return Run(program);
    }

    private static ushort Alu(int alu, int muxA, int muxB, int numOps, int bs = 0, bool m2 = false)
    {
        return (ushort)(
            bs
            | (alu << 4)
            | (muxB << 8)
            | (muxA << 10)
            | ((m2 ? 1 : 0) << 12)
            | (numOps << 13));
    }

    private static ushort NonRegister(int address, bool indirect = false, bool writeBack = false)
    {
        return (ushort)(
            0x8000
            | (address & 0x3ff)
            | (indirect ? 1 << 10 : 0)
            | (writeBack ? 1 << 11 : 0));
    }

    private static ushort Immediate(int value, bool justify = false)
    {
        return (ushort)(0xC000 | (justify ? 1 << 13 : 0) | (value & 0x1fff));
    }

    private static ushort R3Operand(int r1, int r2, int r3)
    {
        return (ushort)((r1 & 0xf) | ((r2 & 0xf) << 5) | ((r3 & 0xf) << 10));
    }

    private static ushort R2Operand(int r1, int r2, bool writeBack)
    {
        return (ushort)(
            0xA000
            | (r1 & 0xf)
            | ((r2 & 0xf) << 5)
            | (1 << 10)
            | (writeBack ? 1 << 11 : 0));
    }

    private static ushort SetRmap(int rmap)
    {
        return (ushort)(0x8000 | (3 << 7) | (rmap & 7));
    }

    /// <summary>Builds sequential DSP program words and keeps an instruction index.</summary>
    private sealed class ProgramBuilder
    {
        private readonly List<ushort> _words = [];

        internal List<ushort> Words => _words;

        internal int Count => _words.Count;

        internal void Word(ushort value)
        {
            _words.Add(value);
        }

        internal void Patch(int index, ushort value)
        {
            _words[index] = value;
        }

        internal void Seed(int address, ushort value)
        {
            // ALU OR: A <- [address] (zero), B <- immediate. The immediate is a signed 13-bit
            // field, so the helper splits the value by shifting it right two and using BS=2 to
            // restore the low bits. Values must be multiples of four.
            if ((value & 3) != 0)
            {
                throw new ArgumentException("Seeded DSP values must be multiples of four.", nameof(value));
            }

            Word(Alu(12, muxA: 1, muxB: 2, numOps: 2, bs: 2));
            Word(NonRegister(address, writeBack: true));
            Word(Immediate(value >> 2));
        }

        internal void PrepareSignBit(int address, int temporary)
        {
            // 0x8000 is not representable by a single 13-bit immediate. Accumulate it in steps.
            Seed(address, 0x3FFC);
            Seed(temporary, 0x3FFC);
            Word(Alu(2, muxA: 1, muxB: 2, numOps: 2));
            Word(NonRegister(address, writeBack: true));
            Word(NonRegister(temporary));

            Seed(temporary + 1, 0x0008);
            Word(Alu(2, muxA: 1, muxB: 2, numOps: 2));
            Word(NonRegister(address, writeBack: true));
            Word(NonRegister(temporary + 1));
        }
    }

    private sealed class RecordingDspHost : IDspHost
    {
        internal ushort InputFifoValue { get; init; }

        internal int InputFifoReads { get; private set; }

        internal int LastInputChannel { get; private set; }

        internal int OutputFifoWrites { get; private set; }

        internal int LastOutputChannel { get; private set; }

        internal ushort LastOutputValue { get; private set; }

        internal int Fiqs { get; private set; }

        internal uint LastFiqReason { get; private set; }

        public ushort ReadInputFifo(ushort channel)
        {
            InputFifoReads++;
            LastInputChannel = channel;
            return InputFifoValue;
        }

        public ushort ReadInputFifoNoIncrement(ushort channel)
        {
            InputFifoReads++;
            LastInputChannel = channel;
            return InputFifoValue;
        }

        public ushort GetInputFifoStatus(byte channel)
        {
            return 2;
        }

        public ushort GetOutputFifoStatus(byte channel)
        {
            return 1;
        }

        public void WriteOutputFifo(ushort channel, ushort value)
        {
            OutputFifoWrites++;
            LastOutputChannel = channel;
            LastOutputValue = value;
        }

        public void GenerateFiq(uint reason1, uint reason2)
        {
            Fiqs++;
            LastFiqReason = reason1;
        }
    }
}
