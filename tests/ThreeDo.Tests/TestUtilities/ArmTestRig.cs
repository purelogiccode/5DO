using ThreeDo.Core;
using ThreeDo.Cpu;
using ThreeDo.Memory;

namespace ThreeDo.Tests.TestUtilities;

internal sealed class FakeInterruptSink : IInterruptSink
{
    public bool NeedFiq { get; set; }

    public int GenerateCount { get; private set; }

    public uint LastReason2 { get; private set; }

    public void GenerateFiq(uint reason1, uint reason2)
    {
        GenerateCount++;
        LastReason2 = reason2;
        NeedFiq = true;
    }
}

internal sealed class ArmTestRig
{
    public ArmTestRig(EmulatorTuning? tuning = null, CompatibilityState? compatibility = null)
    {
        Memory = new ThreeDoMemory(tuning);
        Interrupts = new FakeInterruptSink();
        Cpu = new Arm60(Memory.Bus, Interrupts, tuning, compatibility);
    }

    public ThreeDoMemory Memory { get; }

    public Arm60 Cpu { get; }

    public FakeInterruptSink Interrupts { get; }

    public void LoadProgram(params uint[] instructions) => LoadProgramAt(0x1000, instructions);

    public void LoadProgramAt(uint address, params uint[] instructions)
    {
        for (var i = 0; i < instructions.Length; i++)
        {
            Memory.Ram.Write32(address + ((uint)i * 4), instructions[i]);
        }

        Cpu.ProgramCounter = address;
    }

    public void LoadRomProgram(params uint[] instructions)
    {
        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < instructions.Length; i++)
        {
            var word = instructions[i];
            var offset = i * 4;
            rom[offset] = (byte)(word >> 24);
            rom[offset + 1] = (byte)(word >> 16);
            rom[offset + 2] = (byte)(word >> 8);
            rom[offset + 3] = (byte)word;
        }

        Memory.LoadBios(rom);
        Cpu.ProgramCounter = EmulatorConstants.RomBase;
    }

    public int Run(int instructionCount)
    {
        var cycles = 0;
        for (var i = 0; i < instructionCount; i++)
        {
            cycles += Cpu.Step();
        }

        return cycles;
    }

    public uint ReadRam(uint address) => Memory.Ram.Read32(address);

    public void WriteRam(uint address, uint value) => Memory.Ram.Write32(address, value);

    public byte ReadRamByte(uint address) => Memory.Ram.Read8(address);

    public void WriteRamByte(uint address, byte value) => Memory.Ram.Write8(address, value);
}
