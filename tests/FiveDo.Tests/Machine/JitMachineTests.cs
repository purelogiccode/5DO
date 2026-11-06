using System.IO;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Machine;

public sealed class JitMachineTests
{
    [Fact]
    public void Jit_RunsFramesWithTheSameCpuAndRamStateAsTheInterpreter()
    {
        uint[] program =
        [
            ArmTestAssembler.Mov(0, 0),
            ArmTestAssembler.Mov(1, 0x1000),
            ArmTestAssembler.AddImmediate(0, 0, 1),
            ArmTestAssembler.StrImmediate(0, 1),
            ArmTestAssembler.Branch(0x03000008, 0x03000010),
        ];
        byte[] rom = BuildRom(program);

        FiveDoMachine interpreter = CreateMachine(CpuBackendKind.Interpreter, rom);
        FiveDoMachine jit = CreateMachine(CpuBackendKind.Jit, rom);

        interpreter.RunFrames(30);
        jit.RunFrames(30);

        Assert.Equal(interpreter.Cpu.ProgramCounter, jit.Cpu.ProgramCounter);
        Assert.Equal(interpreter.Cpu.Cpsr, jit.Cpu.Cpsr);
        Assert.Equal(interpreter.Cpu.CyclesExecuted, jit.Cpu.CyclesExecuted);
        Assert.Equal(interpreter.Cpu.InstructionsExecuted, jit.Cpu.InstructionsExecuted);
        for (var register = 0; register < 16; register++)
        {
            Assert.Equal(interpreter.Cpu.ReadRegister(register), jit.Cpu.ReadRegister(register));
        }

        var interpreterRam = new byte[interpreter.Memory.Size];
        var jitRam = new byte[jit.Memory.Size];
        interpreter.Memory.CopyRam(0, interpreterRam);
        jit.Memory.CopyRam(0, jitRam);
        Assert.Equal(interpreterRam, jitRam);
    }

    [Fact]
    public void Jit_StateRoundTripMatchesInterpreter()
    {
        uint[] program =
        [
            ArmTestAssembler.Mov(0, 0),
            ArmTestAssembler.Mov(1, 0x1000),
            ArmTestAssembler.AddImmediate(0, 0, 1),
            ArmTestAssembler.StrImmediate(0, 1),
            ArmTestAssembler.Branch(0x03000008, 0x03000010),
        ];
        byte[] rom = BuildRom(program);

        FiveDoMachine interpreter = CreateMachine(CpuBackendKind.Interpreter, rom);
        FiveDoMachine jit = CreateMachine(CpuBackendKind.Jit, rom);
        interpreter.RunFrames(30);
        jit.RunFrames(30);

        using var state = new MemoryStream();
        jit.SaveState(state, compress: false);
        FiveDoMachine loaded = CreateMachine(CpuBackendKind.Jit, rom);
        state.Position = 0;
        loaded.LoadState(state);

        interpreter.RunFrames(30);
        loaded.RunFrames(30);

        Assert.Equal(interpreter.Cpu.ProgramCounter, loaded.Cpu.ProgramCounter);
        Assert.Equal(interpreter.Cpu.Cpsr, loaded.Cpu.Cpsr);
        for (var register = 0; register < 16; register++)
        {
            Assert.Equal(interpreter.Cpu.ReadRegister(register), loaded.Cpu.ReadRegister(register));
        }

        var interpreterRam = new byte[interpreter.Memory.Size];
        var loadedRam = new byte[loaded.Memory.Size];
        interpreter.Memory.CopyRam(0, interpreterRam);
        loaded.Memory.CopyRam(0, loadedRam);
        Assert.Equal(interpreterRam, loadedRam);
    }

    private static FiveDoMachine CreateMachine(CpuBackendKind backend, byte[] rom)
    {
        var machine = new FiveDoMachine(new EmulatorTuning { CpuBackend = backend });
        machine.LoadBios(rom);
        machine.Reset();
        return machine;
    }

    private static byte[] BuildRom(uint[] program)
    {
        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < program.Length; i++)
        {
            uint word = program[i];
            var offset = i * 4;
            rom[offset] = (byte)(word >> 24);
            rom[offset + 1] = (byte)(word >> 16);
            rom[offset + 2] = (byte)(word >> 8);
            rom[offset + 3] = (byte)word;
        }

        return rom;
    }
}

