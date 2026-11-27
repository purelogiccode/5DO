using System;
using System.Buffers.Binary;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Netplay;

internal static class NetplayTestMachines
{
    public static FiveDoMachine CreateSyntheticMachine()
    {
        uint[] program =
        [
            ArmTestAssembler.Mov(0, 0x03300000),
            ArmTestAssembler.Mov(1, 0x000B0000),
            ArmTestAssembler.StrImmediate(1, 0, 0x580),
            ArmTestAssembler.Mov(2, 0x03200000),
            ArmTestAssembler.AddImmediate(2, 2, 0x2000),
            ArmTestAssembler.Mvn(3, 0),
            ArmTestAssembler.StrImmediate(3, 2),
            ArmTestAssembler.Branch(0x0300001c, 0x03000018),
        ];

        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < program.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(i * 4), program[i]);
        }

        var machine = new FiveDoMachine();
        machine.LoadBios(rom);
        machine.Reset();
        return machine;
    }
}
