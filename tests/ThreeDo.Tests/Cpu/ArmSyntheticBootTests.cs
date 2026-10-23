using ThreeDo.Core;
using ThreeDo.Tests.TestUtilities;

namespace ThreeDo.Tests.Cpu;

public sealed class ArmSyntheticBootTests
{
    [Fact]
    public void SyntheticRom_ComputesChecksumAndWritesResult()
    {
        var rig = new ArmTestRig();
        var program = new[]
        {
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
            ArmTestAssembler.Branch(0x0300002c, 0x0300002c),
        };

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

        rig.Memory.LoadBios(rom);
        rig.Cpu.ProgramCounter = EmulatorConstants.RomBase;

        rig.Run(1_000_000);

        Assert.Equal(136u, rig.ReadRam(0x1000));
        Assert.Equal(0x0300002cu, rig.Cpu.ProgramCounter);
        Assert.Equal(16u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x03000100u, rig.Cpu.ReadRegister(4));
    }

    private static void WriteBigEndian(byte[] destination, int offset, uint value)
    {
        destination[offset] = (byte)(value >> 24);
        destination[offset + 1] = (byte)(value >> 16);
        destination[offset + 2] = (byte)(value >> 8);
        destination[offset + 3] = (byte)value;
    }
}
