using FiveDo.Core;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmJitTests
{
    [Fact]
    public void Jit_MatchesInterpreter_ForDataProcessing()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 5),
            ArmTestAssembler.AddImmediate(0, 0, 3),
            ArmTestAssembler.SubImmediate(1, 0, 2, setFlags: true),
            ArmTestAssembler.CmpImmediate(2, 7),
            ArmTestAssembler.MovRegister(3, 1),
            ArmTestAssembler.DpImmediate(0, 4, 3, 0x0f),
            ArmTestAssembler.DpImmediate(12, 5, 4, 0xff),
            ArmTestAssembler.DpImmediate(14, 6, 5, 0x00),
            ArmTestAssembler.DpImmediate(15, 7, 6, 0x0),
            ArmTestAssembler.DpRegister(4, 8, 7, 0, shiftType: 0, shift: 3, setFlags: true),
            ArmTestAssembler.DpRegister(4, 9, 7, 0, shiftType: 1, shift: 4),
            ArmTestAssembler.DpRegister(4, 10, 7, 0, shiftType: 2, shift: 2),
            ArmTestAssembler.DpRegister(4, 11, 7, 0, shiftType: 3, shift: 2));

        pair.RunBlocks(8);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForRegisterSpecifiedShifts()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 0x1200),
            ArmTestAssembler.Mov(1, 4),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 2, 0, 0, 1, shiftType: 0),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 3, 0, 0, 1, shiftType: 1, setFlags: true),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 4, 0, 0, 1, shiftType: 2),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 5, 0, 0, 1, shiftType: 3),
            ArmTestAssembler.Mov(6, 40),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 7, 0, 0, 6, shiftType: 0),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 8, 0, 0, 6, shiftType: 1),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 9, 0, 0, 6, shiftType: 2),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 10, 0, 0, 6, shiftType: 3),
            ArmTestAssembler.Mov(12, 0),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 11, 0, 0, 12, shiftType: 0, setFlags: true));

        pair.RunBlocks(13);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForPcRelativeRegisterShifts()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 0),
            ArmTestAssembler.Mov(1, 2),
            ArmTestAssembler.DpRegisterShiftedByRegister(8, 2, 15, 1, 1, shiftType: 0),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 3, 15, 15, 15, shiftType: 0));

        pair.RunBlocks(4);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForLslCarryOut()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mvn(0, 0),
            ArmTestAssembler.DpRegister(13, 1, 0, 0, shiftType: 0, shift: 1, setFlags: true));

        pair.RunBlocks(2);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForConditionalInstructions()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.CmpImmediate(0, 0),
            ArmTestAssembler.Mov(1, 0x11, condition: 0),
            ArmTestAssembler.Mov(2, 0x22, condition: 1),
            ArmTestAssembler.AddImmediate(1, 1, 1, setFlags: true),
            ArmTestAssembler.SubImmediate(1, 1, 1, setFlags: true),
            ArmTestAssembler.Mov(3, 0x33, condition: 0),
            ArmTestAssembler.Mov(4, 0x44, condition: 1));

        pair.RunBlocks(7);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForLoadsAndStores()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 0x2000),
            ArmTestAssembler.Mov(1, 0x44),
            ArmTestAssembler.StrImmediate(1, 0),
            ArmTestAssembler.LdrImmediate(2, 0),
            ArmTestAssembler.LdrImmediate(3, 0, byteAccess: true),
            ArmTestAssembler.StrImmediate(3, 0, offset: 5, byteAccess: true),
            ArmTestAssembler.LdrImmediate(4, 0, offset: 4),
            ArmTestAssembler.LdrImmediate(5, 0, offset: 8, preIndex: false, writeback: true),
            ArmTestAssembler.StrImmediate(6, 0, offset: 12, preIndex: false, writeback: true));

        pair.RigJit.WriteRam(0x2000, 0x55667788);
        pair.RigI.WriteRam(0x2000, 0x55667788);
        pair.RunBlocks(9);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForBranches()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 3),
            ArmTestAssembler.SubImmediate(0, 0, 1, setFlags: true),
            ArmTestAssembler.Branch(0x1004, 0x1008, condition: 1),
            ArmTestAssembler.Mov(1, 0x55));

        pair.RunBlocks(8);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForLinkBranch()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Branch(0x1008, 0x1000, link: true),
            ArmTestAssembler.Mov(1, 0x55),
            ArmTestAssembler.Mov(2, 0x66));

        pair.RunBlocks(4);
    }

    [Fact]
    public void Jit_RefreshesModifiedCode()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 1),
            ArmTestAssembler.Mov(1, 2));

        pair.RunBlocks(1);

        // Replace the second instruction and run the same block address again.
        pair.RigJit.WriteRam(0x1004, ArmTestAssembler.Mov(1, 9));
        pair.RigI.WriteRam(0x1004, ArmTestAssembler.Mov(1, 9));

        pair.RunBlocks(3);
    }

    [Fact]
    public void Jit_SoftResetClearsCompiledBlocks()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 0),
            ArmTestAssembler.Branch(0x1000, 0x1004));

        pair.RunBlocks(200_000);
        Assert.True(
            pair.RigJit.Cpu.JitBlockCount > 0,
            pair.RigJit.Cpu.JitLastFailure ?? "no blocks compiled");

        pair.RigJit.Cpu.SoftReset();

        Assert.Equal(0, pair.RigJit.Cpu.JitBlockCount);
    }

    [Fact]
    public void Jit_InvalidatesRomBlocksWhenBankChanges()
    {
        JitPair pair = JitPair.CreateRomBanks(
            [ArmTestAssembler.Mov(0, 1), ArmTestAssembler.Branch(0x03000000, 0x03000004)],
            [ArmTestAssembler.Mov(0, 2), ArmTestAssembler.Branch(0x03000000, 0x03000004)]);

        pair.RunBlocks(200_000);
        Assert.True(
            pair.RigJit.Cpu.JitBlockCount > 0,
            pair.RigJit.Cpu.JitLastFailure ?? "no blocks compiled");

        pair.RigI.Memory.SelectRomBank(1);
        pair.RigJit.Memory.SelectRomBank(1);
        pair.RigI.Cpu.ProgramCounter = EmulatorConstants.RomBase;
        pair.RigJit.Cpu.ProgramCounter = EmulatorConstants.RomBase;

        pair.RunBlocks(200_000);
        Assert.Equal(2u, pair.RigJit.Cpu.ReadRegister(0));
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForSyntheticBoot()
    {
        JitPair pair = JitPair.CreateRom(
            ArmTestAssembler.Mov(0, 0),
            ArmTestAssembler.Mov(1, 0),
            ArmTestAssembler.Mov(2, 16),
            ArmTestAssembler.LdrImmediate(4, 15, 28),
            ArmTestAssembler.LdrRegister(3, 4, 1, shiftType: 0, shift: 2),
            ArmTestAssembler.DpRegister(4, 0, 0, 3),
            ArmTestAssembler.AddImmediate(1, 1, 1),
            ArmTestAssembler.CmpRegister(1, 2),
            ArmTestAssembler.Branch(0x03000010, 0x03000020, condition: 0x1),
            ArmTestAssembler.Mov(5, 0x1000),
            ArmTestAssembler.StrImmediate(0, 5),
            ArmTestAssembler.Branch(0x0300002c, 0x0300002c));

        pair.RunBlocks(1_000_000);
        Assert.Equal(136u, pair.RigJit.ReadRam(0x1000));
        Assert.Equal(0x0300002cu, pair.RigJit.Cpu.ProgramCounter);
        Assert.Equal(16u, pair.RigJit.Cpu.ReadRegister(1));
    }

    [Fact]
    public void Jit_CompilesHotBlocks()
    {
        var rig = new ArmTestRig(new EmulatorTuning { CpuBackend = CpuBackendKind.Jit });
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 1),
            ArmTestAssembler.AddImmediate(0, 0, 1),
            ArmTestAssembler.Branch(0x1000, 0x1008));

        for (var i = 0; i < 100_000; i++)
        {
            rig.Cpu.RunJitBlock();
        }

        Assert.True(rig.Cpu.JitBlockCount > 0, rig.Cpu.JitLastFailure ?? "no blocks compiled");
        Assert.False(rig.Cpu.JitCompileFailed);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForHotStoreLoop()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 0x2000),
            ArmTestAssembler.Mov(1, 0),
            ArmTestAssembler.AddImmediate(1, 1, 1),
            ArmTestAssembler.StrImmediate(1, 0),
            ArmTestAssembler.LdrImmediate(2, 0),
            ArmTestAssembler.DpRegister(4, 3, 3, 2),
            ArmTestAssembler.Branch(0x1000, 0x1018, condition: 1));

        pair.RunBlocks(200_000);
    }

    [Fact]
    public void Jit_MatchesInterpreter_ForPcRelativeRegisterMoves()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 1),
            ArmTestAssembler.Mov(1, 2),
            ArmTestAssembler.MovRegister(14, 15),
            ArmTestAssembler.AddImmediate(2, 2, 1),
            ArmTestAssembler.Branch(0x1000, 0x1010));

        pair.RunBlocks(200_000);
    }

    [Fact]
    public void Jit_RefreshesModifiedCompiledCode()
    {
        JitPair pair = JitPair.Create(
            ArmTestAssembler.Mov(0, 1),
            ArmTestAssembler.AddImmediate(1, 1, 1),
            ArmTestAssembler.Branch(0x1000, 0x1008));

        pair.RunBlocks(100_000);

        // The block is now compiled. Rewrite its first instruction through the raw RAM
        // accessor, which must invalidate the compiled block.
        pair.RigJit.WriteRam(0x1000, ArmTestAssembler.Mov(0, 9));
        pair.RigI.WriteRam(0x1000, ArmTestAssembler.Mov(0, 9));

        pair.RunBlocks(100_000);
    }

    private sealed class JitPair
    {
        private JitPair(ArmTestRig interpreter, ArmTestRig jit)
        {
            RigI = interpreter;
            RigJit = jit;
        }

        internal ArmTestRig RigI { get; }

        internal ArmTestRig RigJit { get; }

        internal static JitPair Create(params uint[] program)
        {
            var interpreter = new ArmTestRig();
            var jit = new ArmTestRig(new EmulatorTuning { CpuBackend = CpuBackendKind.Jit });
            interpreter.LoadProgram(program);
            jit.LoadProgram(program);
            return new JitPair(interpreter, jit);
        }

        internal static JitPair CreateRom(params uint[] program)
        {
            var rom = new byte[EmulatorConstants.RomBankSize];
            for (var i = 0; i < program.Length; i++)
            {
                WriteBigEndian(rom, i * 4, program[i]);
            }

            WriteBigEndian(rom, 0x30, 0x03000100);
            for (var i = 0; i < 16; i++)
            {
                WriteBigEndian(rom, 0x100 + (i * 4), (uint)(i + 1));
            }

            var interpreter = new ArmTestRig();
            var jit = new ArmTestRig(new EmulatorTuning { CpuBackend = CpuBackendKind.Jit });
            interpreter.Memory.LoadBios(rom);
            jit.Memory.LoadBios(rom);
            interpreter.Cpu.ProgramCounter = EmulatorConstants.RomBase;
            jit.Cpu.ProgramCounter = EmulatorConstants.RomBase;
            return new JitPair(interpreter, jit);
        }

        internal static JitPair CreateRomBanks(uint[] bank0, uint[] bank1)
        {
            var rom0 = new byte[EmulatorConstants.RomBankSize];
            var rom1 = new byte[EmulatorConstants.RomBankSize];
            for (var i = 0; i < bank0.Length; i++)
            {
                WriteBigEndian(rom0, i * 4, bank0[i]);
            }

            for (var i = 0; i < bank1.Length; i++)
            {
                WriteBigEndian(rom1, i * 4, bank1[i]);
            }

            var interpreter = new ArmTestRig();
            var jit = new ArmTestRig(new EmulatorTuning { CpuBackend = CpuBackendKind.Jit });
            interpreter.Memory.LoadBios(rom0);
            jit.Memory.LoadBios(rom0);
            interpreter.Memory.LoadRom(1, rom1);
            jit.Memory.LoadRom(1, rom1);
            interpreter.Cpu.ProgramCounter = EmulatorConstants.RomBase;
            jit.Cpu.ProgramCounter = EmulatorConstants.RomBase;
            return new JitPair(interpreter, jit);
        }

        private static void WriteBigEndian(byte[] destination, int offset, uint value)
        {
            destination[offset] = (byte)(value >> 24);
            destination[offset + 1] = (byte)(value >> 16);
            destination[offset + 2] = (byte)(value >> 8);
            destination[offset + 3] = (byte)value;
        }

        internal void RunBlocks(int blocks)
        {
            for (var i = 0; i < blocks; i++)
            {
                long before = RigJit.Cpu.InstructionsExecuted;
                int jitCycles = RigJit.Cpu.RunJitBlock();
                var count = (int)(RigJit.Cpu.InstructionsExecuted - before);
                Assert.True(count > 0, "The JIT executed no instructions.");
                var interpreterCycles = 0;
                for (var step = 0; step < count; step++)
                {
                    interpreterCycles += RigI.Cpu.Step();
                }

                Assert.Equal(interpreterCycles, jitCycles);
                Assert.Equal(RigI.Cpu.ProgramCounter, RigJit.Cpu.ProgramCounter);
                Assert.Equal(RigI.Cpu.Cpsr, RigJit.Cpu.Cpsr);
                Assert.Equal(RigI.Cpu.CyclesExecuted, RigJit.Cpu.CyclesExecuted);
                for (var register = 0; register < 16; register++)
                {
                    Assert.Equal(RigI.Cpu.ReadRegister(register), RigJit.Cpu.ReadRegister(register));
                }

                Assert.Equal(RigI.ReadRam(0x2000), RigJit.ReadRam(0x2000));
                Assert.Equal(RigI.ReadRam(0x2004), RigJit.ReadRam(0x2004));
                Assert.Equal(RigI.ReadRam(0x2008), RigJit.ReadRam(0x2008));
                Assert.Equal(RigI.ReadRam(0x200c), RigJit.ReadRam(0x200c));
            }
        }
    }
}



