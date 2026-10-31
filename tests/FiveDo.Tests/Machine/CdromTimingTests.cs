using System;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Machine;

/// <summary>
/// Integration coverage for the CD-ROM timing option. The machine drives the device clock
/// from the CPU cycle batches, so a scheduled sector becomes visible only after
/// <see cref="FiveDoMachine.RunFrame"/> has advanced far enough.
/// </summary>
public sealed class CdromTimingTests
{
    [Fact]
    public void RunFrame_AdvancesTheOperaCdromClockAndPublishesTheSector()
    {
        var machine = new FiveDoMachine(new EmulatorTuning
        {
            CdromTiming = CdromTimingMode.OperaCycleBased,
        });
        LoadLoopRom(machine);
        machine.Cdrom.InsertDisc(new MemoryDisc(2));
        Send(machine.Cdrom, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01);

        Assert.Equal(0, (int)machine.Cdrom.Poll & 0x20);
        Assert.Equal(0UL, machine.Cdrom.Cycles);

        machine.RunFrame();

        Assert.True(machine.Cdrom.Cycles > machine.Cdrom.DataSectorCycles);
        Assert.NotEqual(0, (int)machine.Cdrom.Poll & 0x20);
    }

    [Fact]
    public void RunFrame_KeepsTheFreeDoCdromDataImmediate()
    {
        var machine = new FiveDoMachine(new EmulatorTuning
        {
            CdromTiming = CdromTimingMode.FreeDoCompatible,
        });
        LoadLoopRom(machine);
        machine.Cdrom.InsertDisc(new MemoryDisc(2));
        Send(machine.Cdrom, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01);

        Assert.NotEqual(0, (int)machine.Cdrom.Poll & 0x20);
        machine.RunFrame();
        Assert.NotEqual(0, (int)machine.Cdrom.Poll & 0x20);
    }

    private static void LoadLoopRom(FiveDoMachine machine)
    {
        var rom = new byte[EmulatorConstants.RomBankSize];
        WriteBigEndian(
            rom,
            0,
            ArmTestAssembler.Branch(EmulatorConstants.RomBase, EmulatorConstants.RomBase));
        machine.LoadBios(rom);
        machine.Cpu.ProgramCounter = EmulatorConstants.RomBase;
    }

    private static void Send(FiveDo.Cdrom.CdromDevice device, params byte[] command)
    {
        foreach (var value in command)
        {
            device.SetCommand(value);
        }

        for (var i = command.Length; i < 7; i++)
        {
            device.SetCommand(0);
        }
    }

    private static void WriteBigEndian(byte[] destination, int offset, uint value)
    {
        destination[offset] = (byte)(value >> 24);
        destination[offset + 1] = (byte)(value >> 16);
        destination[offset + 2] = (byte)(value >> 8);
        destination[offset + 3] = (byte)value;
    }

    private sealed class MemoryDisc : IDiscSource
    {
        private readonly byte[][] _sectors;

        internal MemoryDisc(int count)
        {
            _sectors = new byte[count][];
            for (var lba = 0; lba < count; lba++)
            {
                var sector = new byte[2048];
                for (var i = 0; i < sector.Length; i++)
                {
                    sector[i] = (byte)((lba * 31) + i);
                }

                _sectors[lba] = sector;
            }
        }

        public int SectorCount => _sectors.Length;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            _sectors[lba].CopyTo(destination);
        }

        public void Dispose()
        {
        }
    }
}
