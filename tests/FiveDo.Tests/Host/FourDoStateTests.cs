using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Machine;

namespace FiveDo.Tests.Host;

public sealed class FourDoStateTests
{
    [Fact]
    public void Parse_RejectsNonFourDoData()
    {
        byte[] data = new byte[128];
        Assert.Throws<InvalidDataException>(() => FourDoSaveState.Parse(data));
    }

    [Fact]
    public void Parse_DetectsThePointerLayoutAndRecords()
    {
        FourDoStateFixture fixture = FourDoStateFixture.Create();
        FourDoSaveState state = FourDoSaveState.Parse(fixture.Bytes);

        Assert.Equal(12, state.PointerSize);
        Assert.Equal(FourDoSaveState.ArmRamSize, state.ArmRam.Length);
        Assert.Equal(FourDoSaveState.ArmRomSize, state.ArmRom.Length);
        Assert.Equal(FourDoSaveState.ArmNvramSize, state.ArmNvram.Length);
        Assert.Equal(FourDoSaveState.VdlSize, state.Vdl.Length);
        Assert.Equal(FourDoSaveState.ClioSize, state.Clio.Length);
        Assert.Equal(FourDoSaveState.MadamSize, state.Madam.Length);
        Assert.Equal(FourDoSaveState.XbusSize, state.Xbus.Length);
    }

    [Fact]
    public void Import_MapsArchitecturalStateIntoTheMachine()
    {
        FourDoStateFixture fixture = FourDoStateFixture.Create();
        byte[] baseState = CreateBaseState();
        byte[] imported = FourDoStateConverter.Import(fixture.Bytes, baseState, out IReadOnlyList<string> warnings);

        FiveDoStateDocument document = FiveDoStateDocument.Parse(imported);
        FiveDoStateSection cpu = document.Find(StateSectionIds.Cpu)!;
        Assert.Equal(0x03000010u, BinaryPrimitives.ReadUInt32LittleEndian(cpu.Payload.AsSpan(60)));
        FiveDoMachine machine = new();
        using (var stream = new MemoryStream(imported))
        {
            machine.LoadState(stream);
        }

        Assert.Equal(0x03000010u, machine.Cpu.ProgramCounter);
        Assert.Equal(0x12345678u, machine.Cpu.State[0]);
        Assert.Equal(0x13u, machine.Cpu.State.Cpsr);
        Assert.True(machine.Cpu.State.FiqPending);
        Assert.False(machine.Cpu.State.SecondRom);
        Assert.Equal(0xDEADBEEFu, machine.Memory.Read32(0x100));
        Assert.Equal(0xCAFEBABEu, machine.Memory.Read32(0x200100));
        Assert.Equal(0xCAFEBABEu, machine.Memory.Words[((3 * 1024 * 1024) + 0x100) / 4]);
        var nvram = new byte[EmulatorConstants.NvramSize];
        machine.Memory.CopyNvram(nvram);
        Assert.Equal(0x5A, nvram[0]);
        Assert.Equal(0xE0000000u, machine.Vdlp.Background);
        Assert.Equal(0x1111u, machine.Vdlp.HeadVdl);
        Assert.Equal(0x2222u, machine.Vdlp.CurrentVdl);
        Assert.Equal(0x3333u, machine.Vdlp.CurrentBitmap);
        Assert.Equal(0x100u, machine.Dsp.ProgramCounter);
        Assert.Equal(0xCAFEBABEu, machine.Dsp.Seed);
        Assert.True(machine.Dsp.IsRunning);
        Assert.Equal(0x02020000u, machine.Clio.Peek(0));
        Assert.Equal(0xCAFEBABEu, machine.Clio.Peek(4));
        Assert.Equal(1000u, machine.Quartz.ArmAccumulator);
        Assert.Equal(0x00400000u, machine.Quartz.VdlAccumulator);
        Assert.Equal(6_250_000u, machine.Quartz.DspAccumulator);
        Assert.Equal(123, machine.Quartz.VdCurrOverline);
        Assert.Equal(1u, machine.Sport.Color);
        Assert.Equal(2u, machine.Sport.Source);
        Assert.Equal(3u, machine.Sport.Destination);
        Assert.Equal(FiveDo.Madam.CelFsmState.InProcess, machine.Madam.Fsm);
        Assert.Equal(0x10, machine.Madam.ReadModulo);
        Assert.Equal(0x20, machine.Madam.WriteModulo);
        Assert.Equal(15, machine.Xbus.SelectedDevice);
        Assert.Equal(0x83u, machine.Xbus.GetStatusFifo());
        Assert.Contains(warnings, warning => warning.Contains("instruction cache", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_ProducesAParseableContainerAndRoundTrips()
    {
        FourDoStateFixture fixture = FourDoStateFixture.Create();
        byte[] baseState = CreateBaseState();
        byte[] imported = FourDoStateConverter.Import(fixture.Bytes, baseState, out _);

        FourDoSaveState parsedImport = FourDoSaveState.Parse(fixture.Bytes);
        Assert.Equal(12, parsedImport.PointerSize);
        Assert.Equal(0xDEADBEEFu, BinaryPrimitives.ReadUInt32BigEndian(parsedImport.ArmRam[0x100..]));

        FiveDoMachine machine = new();
        using (var stream = new MemoryStream(imported))
        {
            machine.LoadState(stream);
        }

        using var saved = new MemoryStream();
        machine.SaveState(saved);
        byte[] exported = FourDoStateConverter.Export(saved.ToArray(), out IReadOnlyList<string> exportWarnings);
        FourDoSaveState parsedExport = FourDoSaveState.Parse(exported);
        Assert.Equal(12, parsedExport.PointerSize);
        Assert.Equal(FourDoSaveState.XbusSize, parsedExport.Xbus.Length);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(parsedExport.Xbus[FourDoSaveState.XbusStructSize..]));
        Assert.Equal(0x03000010u, BinaryPrimitives.ReadUInt32LittleEndian(parsedExport.ArmRegisters[60..]));
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(parsedExport.Clio[(65_536 * 4)..]));
        Assert.Contains(exportWarnings, warning => warning.Contains("CD-ROM", StringComparison.Ordinal));

        byte[] roundTripped = FourDoStateConverter.Import(exported, baseState, out _);
        FiveDoMachine replay = new();
        using (var stream = new MemoryStream(roundTripped))
        {
            replay.LoadState(stream);
        }

        Assert.Equal(machine.Cpu.ProgramCounter, replay.Cpu.ProgramCounter);
        Assert.Equal(machine.Cpu.State[0], replay.Cpu.State[0]);
        Assert.Equal(machine.Cpu.State.Cpsr, replay.Cpu.State.Cpsr);
        Assert.Equal(machine.Memory.Read32(0x100), replay.Memory.Read32(0x100));
        Assert.Equal(machine.Memory.Read32(0x200100), replay.Memory.Read32(0x200100));
        Assert.Equal(machine.Vdlp.HeadVdl, replay.Vdlp.HeadVdl);
        Assert.Equal(machine.Vdlp.CurrentVdl, replay.Vdlp.CurrentVdl);
        Assert.Equal(machine.Dsp.ProgramCounter, replay.Dsp.ProgramCounter);
        Assert.Equal(machine.Dsp.Seed, replay.Dsp.Seed);
        Assert.Equal(machine.Clio.Peek(4), replay.Clio.Peek(4));
        Assert.Equal(machine.Quartz.ArmAccumulator, replay.Quartz.ArmAccumulator);
        Assert.Equal(machine.Quartz.DspAccumulator, replay.Quartz.DspAccumulator);
        Assert.Equal(machine.Quartz.VdlAccumulator, replay.Quartz.VdlAccumulator);
        Assert.Equal(machine.Sport.Color, replay.Sport.Color);
        Assert.Equal(machine.Madam.Fsm, replay.Madam.Fsm);
    }

    [Fact]
    public void Import_RejectsATruncatedRecord()
    {
        FourDoStateFixture fixture = FourDoStateFixture.Create();
        byte[] broken = fixture.Bytes;
        BinaryPrimitives.WriteInt32LittleEndian(broken.AsSpan(3 * sizeof(int)), FourDoSaveState.HeaderSize + 4);

        Assert.Throws<InvalidDataException>(() => FourDoSaveState.Parse(broken));
    }

    private static byte[] CreateBaseState()
    {
        FiveDoMachine machine = new();
        machine.RunFrames(2);
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        return stream.ToArray();
    }

    /// <summary>Builds a small but complete 4DO state with known marker values.</summary>
    private sealed class FourDoStateFixture
    {
        private const int ArmRegisterOffset = 12;

        private readonly byte[] _arm = new byte[ArmRegisterOffset
            + FourDoSaveState.ArmRegisterSize
            + FourDoSaveState.ArmRamSize
            + FourDoSaveState.ArmRomSize
            + FourDoSaveState.ArmNvramSize];

        private readonly byte[] _vdl = new byte[FourDoSaveState.VdlSize];
        private readonly byte[] _dsp = new byte[4 + 0x8000 * 2 + 7489];
        private readonly byte[] _clio = new byte[FourDoSaveState.ClioSize];
        private readonly byte[] _quartz = new byte[FourDoSaveState.QuartzSize];
        private readonly byte[] _sport = new byte[FourDoSaveState.SportSize];
        private readonly byte[] _madam = new byte[FourDoSaveState.MadamSize];
        private readonly byte[] _xbus = new byte[FourDoSaveState.XbusSize];

        private FourDoStateFixture()
        {
            WriteRegisters();
            WriteMemory();
            WriteVdl();
            WriteDsp();
            WriteClio();
            WriteQuartz();
            WriteSport();
            WriteMadam();
            WriteXbus();

            var components = new[] { _arm, _vdl, _dsp, _clio, _quartz, _sport, _madam, _xbus };
            Bytes = new byte[FourDoSaveState.HeaderSize + Sum(components)];
            BinaryPrimitives.WriteUInt32LittleEndian(Bytes, FourDoSaveState.Magic);
            var offset = FourDoSaveState.HeaderSize;
            for (var i = 0; i < components.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(Bytes.AsSpan((i + 1) * sizeof(int)), offset);
                components[i].CopyTo(Bytes, offset);
                offset += components[i].Length;
            }

            BinaryPrimitives.WriteInt32LittleEndian(
                Bytes.AsSpan((components.Length + 1) * sizeof(int)),
                offset);
        }

        public byte[] Bytes { get; }

        public static FourDoStateFixture Create() => new();

        private static int Sum(byte[][] components)
        {
            var total = 0;
            foreach (byte[] component in components)
            {
                total += component.Length;
            }

            return total;
        }

        private void WriteRegisters()
        {
            var registers = _arm.AsSpan(ArmRegisterOffset, FourDoSaveState.ArmRegisterSize);
            BinaryPrimitives.WriteUInt32LittleEndian(registers, 0x12345678); // R0
            BinaryPrimitives.WriteUInt32LittleEndian(registers[60..], 0x03000010); // R15/PC
            BinaryPrimitives.WriteUInt32LittleEndian(registers[176..], 0x13); // CPSR
            registers[180] = 1; // nFIQ
            registers[181] = 0; // SecondROM
            registers[182] = 0; // MAS access exception
        }

        private void WriteMemory()
        {
            var ram = _arm.AsSpan(ArmRegisterOffset + FourDoSaveState.ArmRegisterSize, FourDoSaveState.ArmRamSize);
            BinaryPrimitives.WriteUInt32BigEndian(ram, 0xDEADBEEF);
            BinaryPrimitives.WriteUInt32BigEndian(ram[0x100..], 0xDEADBEEF);
            BinaryPrimitives.WriteUInt32BigEndian(ram[0x200100..], 0xCAFEBABE);
            var rom = _arm.AsSpan(
                ArmRegisterOffset + FourDoSaveState.ArmRegisterSize + FourDoSaveState.ArmRamSize,
                FourDoSaveState.ArmRomSize);
            BinaryPrimitives.WriteUInt32BigEndian(rom, 0x12345678);
            _arm[^(FourDoSaveState.ArmNvramSize)] = 0x5A;
        }

        private void WriteVdl()
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_vdl.AsSpan(96), 0xE0000000);
            BinaryPrimitives.WriteUInt32LittleEndian(_vdl.AsSpan(100), 0x1111);
            BinaryPrimitives.WriteUInt32LittleEndian(_vdl.AsSpan(104), 0x2222);
            BinaryPrimitives.WriteUInt32LittleEndian(_vdl.AsSpan(108), 0x2222);
            BinaryPrimitives.WriteUInt32LittleEndian(_vdl.AsSpan(112), 0x3333);
            BinaryPrimitives.WriteUInt32LittleEndian(_vdl.AsSpan(116), 0x4444);
            BinaryPrimitives.WriteUInt32LittleEndian(_vdl.AsSpan(120), 0x5555);
            BinaryPrimitives.WriteUInt32LittleEndian(_vdl.AsSpan(124), 0x6666);
            BinaryPrimitives.WriteInt32LittleEndian(_vdl.AsSpan(128), 7);
        }

        private void WriteDsp()
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_dsp, 0x1230);
            var tail = _dsp.Length - 7489;
            var registerStage = tail + 256 + 1024 + 4096 + 2048 + 4;
            BinaryPrimitives.WriteUInt32LittleEndian(_dsp.AsSpan(registerStage), 0x100);
            BinaryPrimitives.WriteUInt32LittleEndian(_dsp.AsSpan(registerStage + 16), 0xBAADF00D);
            _dsp[registerStage + 20 + 19] = 1;
            var registerIndex = tail + 256 + 1024 + 4096 + 2048;
            BinaryPrimitives.WriteInt32LittleEndian(_dsp.AsSpan(registerIndex), 9);
            var seed = _dsp.Length - 20;
            BinaryPrimitives.WriteUInt32LittleEndian(_dsp.AsSpan(seed), 0xCAFEBABE);
            _dsp[seed + 4] = 1;
            BinaryPrimitives.WriteUInt16LittleEndian(_dsp.AsSpan(tail + 256 + 1024), 0x1234);
            BinaryPrimitives.WriteUInt16LittleEndian(_dsp.AsSpan(tail + 256 + 1024 + 4096), 0x5678);
        }

        private void WriteClio()
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_clio, 0x0BADF00D);
            BinaryPrimitives.WriteUInt32LittleEndian(_clio.AsSpan(16), 0xCAFEBABE);
            BinaryPrimitives.WriteInt32LittleEndian(_clio.AsSpan(65_536 * 4), 7);
            BinaryPrimitives.WriteInt32LittleEndian(_clio.AsSpan((65_536 * 4) + 12), 0x55);
            BinaryPrimitives.WriteUInt32LittleEndian(_clio.AsSpan((65_536 * 4) + 12 + 52), 0xAA00);
        }

        private void WriteQuartz()
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_quartz, 1000);
            BinaryPrimitives.WriteUInt32LittleEndian(_quartz.AsSpan(4), 0x00800000);
            BinaryPrimitives.WriteUInt32LittleEndian(_quartz.AsSpan(8), 0x00400000);
            BinaryPrimitives.WriteUInt32LittleEndian(_quartz.AsSpan(12), 42);
            BinaryPrimitives.WriteUInt32LittleEndian(_quartz.AsSpan(16), 15_780);
            BinaryPrimitives.WriteUInt32LittleEndian(_quartz.AsSpan(20), 123);
            BinaryPrimitives.WriteUInt32LittleEndian(_quartz.AsSpan(24), 263);
            BinaryPrimitives.WriteUInt32LittleEndian(_quartz.AsSpan(28), 526);
        }

        private void WriteSport()
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_sport, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(_sport.AsSpan(4), 2);
            BinaryPrimitives.WriteUInt32LittleEndian(_sport.AsSpan(8), 3);
        }

        private void WriteMadam()
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_madam, 0x01020000);
            BinaryPrimitives.WriteUInt16LittleEndian(_madam.AsSpan(2112 * 4), 0x7FFF);
            var pbus = 2112 * 4 + 64;
            _madam[pbus] = 0x01;
            BinaryPrimitives.WriteInt32LittleEndian(_madam.AsSpan(pbus + 20), 0x10);
            BinaryPrimitives.WriteInt32LittleEndian(_madam.AsSpan(pbus + 24), 0x20);
            BinaryPrimitives.WriteUInt32LittleEndian(_madam.AsSpan(pbus + 28), 2);
        }

        private void WriteXbus()
        {
            _xbus[0] = 0x0F;
            _xbus[2] = 1;
            _xbus[3] = 2;
            _xbus[4] = 0x83;
            _xbus[259] = 5;
            _xbus[260] = 0x83;
            _xbus[267] = 1;
        }
    }
}
