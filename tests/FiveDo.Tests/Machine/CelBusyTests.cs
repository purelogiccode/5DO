using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Madam;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Machine;

/// <summary>
/// Integration coverage for the opt-in Opera CEL busy model. The synthetic ROM starts a
/// four-pixel CEL and polls the MADAM status register; only the busy model lets the CPU
/// observe <c>0x10</c> before the deadline expires.
/// </summary>
public sealed class CelBusyTests
{
    [Fact]
    public void RunFrame_OperaCelTimingLetsTheCpuObserveCelBusy()
    {
        FiveDoMachine machine = CreateMachine(CelTimingMode.OperaBusy);

        machine.RunFrame();

        Assert.Equal(1u, machine.Memory.Ram.Read32(0x300));
        Assert.Equal(0UL, machine.Madam.CelBusyCyclesRemaining);
    }

    [Fact]
    public void RunFrame_FreeDoCelTimingNeverShowsCelBusy()
    {
        FiveDoMachine machine = CreateMachine(CelTimingMode.FreeDoCompatible);

        machine.RunFrame();

        Assert.Equal(0u, machine.Memory.Ram.Read32(0x300));
    }

    private static FiveDoMachine CreateMachine(CelTimingMode celTiming)
    {
        var machine = new FiveDoMachine(new EmulatorTuning { CelTiming = celTiming });
        var rom = new byte[EmulatorConstants.RomBankSize];
        uint[] program =
        [
            ArmTestAssembler.Mov(0, EmulatorConstants.MadamBase),
            ArmTestAssembler.Mov(1, 0x100),
            ArmTestAssembler.StrImmediate(1, 0, 0x5a4),
            ArmTestAssembler.StrImmediate(1, 0, 0x100),
            ArmTestAssembler.Mov(4, 0x300),
            ArmTestAssembler.Mov(5, 1),
            ArmTestAssembler.LdrImmediate(2, 0, 0x28),
            ArmTestAssembler.CmpImmediate(2, 0),
            ArmTestAssembler.StrImmediate(5, 4, condition: 0x1),
            ArmTestAssembler.Branch(EmulatorConstants.RomBase + 0x18, EmulatorConstants.RomBase + 0x24),
        ];

        for (var i = 0; i < program.Length; i++)
        {
            WriteBigEndian(rom, i * 4, program[i]);
        }

        machine.LoadBios(rom);
        machine.Cpu.ProgramCounter = EmulatorConstants.RomBase;
        WriteCelScene(machine);
        return machine;
    }

    private static void WriteCelScene(FiveDoMachine machine)
    {
        const uint ccb = 0x100;
        const uint source = 0x200;
        const uint frameBuffer = 0x2000;

        machine.Madam.Poke(0x130, 0x1414);
        machine.Madam.Poke(0x134, 0x00070007);
        machine.Madam.Poke(0x138, frameBuffer);
        machine.Madam.Poke(0x13c, frameBuffer);

        CcbControl flags = CcbControl.NextPointerAbsolute
                           | CcbControl.SourcePointerAbsolute
                           | CcbControl.LoadSize
                           | CcbControl.LoadPixelProcessor
                           | CcbControl.HasPosition
                           | CcbControl.Clockwise
                           | CcbControl.Last;

        machine.Memory.Ram.Write32(ccb, (uint)flags);
        machine.Memory.Ram.Write32(ccb + 4, 0);
        machine.Memory.Ram.Write32(ccb + 8, source);
        machine.Memory.Ram.Write32(ccb + 12, 0);
        machine.Memory.Ram.Write32(ccb + 16, 0);
        machine.Memory.Ram.Write32(ccb + 20, 0);
        machine.Memory.Ram.Write32(ccb + 24, 0x00200000);
        machine.Memory.Ram.Write32(ccb + 28, 0);
        machine.Memory.Ram.Write32(ccb + 32, 0);
        machine.Memory.Ram.Write32(ccb + 36, 0x00020000);
        machine.Memory.Ram.Write32(ccb + 40, 0x00001f00);

        machine.Memory.Ram.Write32(source, 6 | 0x10);
        machine.Memory.Ram.Write32(source + 4, 0);
        WriteBytes(machine, source + 8, 0x7f, 0xfe);
    }

    private static void WriteBytes(FiveDoMachine machine, uint buffer, params byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            machine.Memory.Ram.Write8(buffer + ((uint)i ^ 3), bytes[i]);
        }
    }

    private static void WriteBigEndian(byte[] destination, int offset, uint value)
    {
        destination[offset] = (byte)(value >> 24);
        destination[offset + 1] = (byte)(value >> 16);
        destination[offset + 2] = (byte)(value >> 8);
        destination[offset + 3] = (byte)value;
    }
}
