using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Memory;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Memory;

public sealed class MemoryConfigurationTests
{
    private const int Megabyte = 1024 * 1024;

    [Theory]
    [InlineData(MemoryConfiguration.Dram2MbVram1Mb, 2, 1, 0x29u)]
    [InlineData(MemoryConfiguration.Dram2MbVram2Mb, 2, 2, 0x2Au)]
    [InlineData(MemoryConfiguration.Dram4MbVram1Mb, 4, 1, 0x31u)]
    [InlineData(MemoryConfiguration.Dram4MbVram2Mb, 4, 2, 0x32u)]
    [InlineData(MemoryConfiguration.Dram8MbVram1Mb, 8, 1, 0x71u)]
    [InlineData(MemoryConfiguration.Dram8MbVram2Mb, 8, 2, 0x72u)]
    [InlineData(MemoryConfiguration.Dram14MbVram2Mb, 14, 2, 0x62u)]
    [InlineData(MemoryConfiguration.Dram15MbVram1Mb, 15, 1, 0x61u)]
    public void Layout_MatchesOperaMemoryGeometryAndRedSysBits(
        MemoryConfiguration configuration,
        int dramMegabytes,
        int vramMegabytes,
        uint expectedSysBits)
    {
        MemoryLayout layout = MemoryLayout.For(configuration);

        Assert.Equal(dramMegabytes * Megabyte, layout.DramSize);
        Assert.Equal(vramMegabytes * Megabyte, layout.VramSize);
        Assert.Equal((dramMegabytes + vramMegabytes) * Megabyte, layout.RamSize);
        Assert.Equal(dramMegabytes * Megabyte, layout.VramBase);
        Assert.Equal((uint)((vramMegabytes * Megabyte) - 1), layout.VramSizeMask);
        Assert.Equal(4 * vramMegabytes * Megabyte, layout.HiresVramSize);
        Assert.Equal(expectedSysBits, layout.MadamRedSysBits);
    }

    [Theory]
    [InlineData("21", MemoryConfiguration.Dram2MbVram1Mb)]
    [InlineData("41", MemoryConfiguration.Dram4MbVram1Mb)]
    [InlineData("E2", MemoryConfiguration.Dram14MbVram2Mb)]
    [InlineData("e2", MemoryConfiguration.Dram14MbVram2Mb)]
    [InlineData("4/2", MemoryConfiguration.Dram4MbVram2Mb)]
    [InlineData("8MB / 1MB", MemoryConfiguration.Dram8MbVram1Mb)]
    [InlineData("2+2", MemoryConfiguration.Dram2MbVram2Mb)]
    public void TryParse_AcceptsOperaCodesAndSizePairs(string text, MemoryConfiguration expected)
    {
        Assert.True(MemoryLayout.TryParse(text, out MemoryConfiguration actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("31")]
    [InlineData("16/1")]
    [InlineData("3/1")]
    [InlineData("not-a-layout")]
    public void TryParse_RejectsUnsupportedLayouts(string text)
    {
        Assert.False(MemoryLayout.TryParse(text, out _));
    }

    [Theory]
    [InlineData(MemoryConfiguration.Dram2MbVram1Mb, 2, 1)]
    [InlineData(MemoryConfiguration.Dram2MbVram2Mb, 2, 2)]
    [InlineData(MemoryConfiguration.Dram4MbVram1Mb, 4, 1)]
    [InlineData(MemoryConfiguration.Dram4MbVram2Mb, 4, 2)]
    [InlineData(MemoryConfiguration.Dram8MbVram1Mb, 8, 1)]
    [InlineData(MemoryConfiguration.Dram8MbVram2Mb, 8, 2)]
    [InlineData(MemoryConfiguration.Dram14MbVram2Mb, 14, 2)]
    [InlineData(MemoryConfiguration.Dram15MbVram1Mb, 15, 1)]
    public void EveryCapacity_ExposesTheConfiguredDramAndVramWindows(
        MemoryConfiguration configuration,
        int dramMegabytes,
        int vramMegabytes)
    {
        var memory = new FiveDoMemory(new EmulatorTuning { MemoryConfiguration = configuration });
        uint dramTop = ((uint)dramMegabytes * Megabyte) - 4;
        var vramBase = (uint)dramMegabytes * Megabyte;
        uint vramTop = vramBase + ((uint)vramMegabytes * Megabyte) - 4;
        uint firstUnmapped = vramBase + ((uint)vramMegabytes * Megabyte);

        memory.Bus.Write32(dramTop, 0xD00D_0001);
        memory.Bus.Write32(vramBase, 0xD00D_0002);
        memory.Bus.Write32(vramTop, 0xD00D_0003);

        Assert.Equal(0xD00D_0001u, memory.Bus.Read32(dramTop));
        Assert.Equal(0xD00D_0002u, memory.Bus.Read32(vramBase));
        Assert.Equal(0xD00D_0003u, memory.Bus.Read32(vramTop));
        Assert.Equal(EmulatorConstants.UnmappedValue, memory.Bus.Read32(firstUnmapped));
    }

    [Fact]
    public void ExpandedLayout_MovesVramAndRejectsAddressesAboveRamSize()
    {
        var memory = new FiveDoMemory(new EmulatorTuning
        {
            MemoryConfiguration = MemoryConfiguration.Dram4MbVram2Mb,
        });

        Assert.Equal(4 * Megabyte, memory.Layout.VramBase);
        Assert.Equal(6 * Megabyte, memory.Layout.RamSize);

        memory.Bus.Write32(0x003F_FFFC, 0x1122_3344);
        memory.Bus.Write32(0x0040_0000, 0x5566_7788);
        memory.Bus.Write32(0x005F_FFFC, 0x99AA_BBCC);

        Assert.Equal(0x1122_3344u, memory.Bus.Read32(0x003F_FFFC));
        Assert.Equal(0x5566_7788u, memory.Bus.Read32(0x0040_0000));
        Assert.Equal(0x99AA_BBCCu, memory.Bus.Read32(0x005F_FFFC));
        Assert.Equal(EmulatorConstants.UnmappedValue, memory.Bus.Read32(0x0060_0000));
    }

    [Fact]
    public void StockLayout_KeepsTheHistoricalThreeMegabyteBoundary()
    {
        var memory = new FiveDoMemory();
        memory.Bus.Write32(0x002F_FFFC, 0x1234_5678);

        Assert.Equal(0x1234_5678u, memory.Bus.Read32(0x002F_FFFC));
        Assert.Equal(EmulatorConstants.UnmappedValue, memory.Bus.Read32(0x0030_0000));
        Assert.Equal(3 * Megabyte, memory.Layout.RamSize);
    }

    [Fact]
    public void Madam_ReportsTheConfiguredCapacityInRedSysBits()
    {
        var machine = new FiveDoMachine(new EmulatorTuning
        {
            MemoryConfiguration = MemoryConfiguration.Dram8MbVram2Mb,
        });

        Assert.Equal(0x72u, machine.Madam.Peek(0x004));
    }

    [Fact]
    public void HomebrewProbe_SeesAndUsesExpandedDramAndVram()
    {
        var machine = new FiveDoMachine(new EmulatorTuning
        {
            MemoryConfiguration = MemoryConfiguration.Dram4MbVram2Mb,
        });
        machine.LoadBios(BuildHomebrewProbeRom());
        machine.Reset();
        machine.RunFrames(300);

        // The probe writes four distinct values across the top of DRAM, the VRAM base,
        // and the top of the second VRAM bank, then reads them back.
        Assert.Equal(0x11u, machine.Memory.Bus.Read32(0x0000_1000));
        Assert.Equal(0x22u, machine.Memory.Bus.Read32(0x0000_1004));
        Assert.Equal(0x33u, machine.Memory.Bus.Read32(0x0000_1008));
        Assert.Equal(0x44u, machine.Memory.Bus.Read32(0x0000_100c));

        // Marker and the guest-visible RED SYSBITS value for 4 MB DRAM / 2 MB VRAM.
        Assert.Equal(0xA5u, machine.Memory.Bus.Read32(0x0000_2000));
        Assert.Equal(0x32u, machine.Memory.Bus.Read32(0x0000_2004));
    }

    private static byte[] BuildHomebrewProbeRom()
    {
        uint[] program =
        [
            ArmTestAssembler.Branch(0x03000004, 0x03000000),
            ArmTestAssembler.Mov(0, 0x0010_0000),
            ArmTestAssembler.Mov(1, 0x11),
            ArmTestAssembler.StrImmediate(1, 0),
            ArmTestAssembler.Mov(2, 0x003F_0000),
            ArmTestAssembler.Mov(3, 0x22),
            ArmTestAssembler.StrImmediate(3, 2),
            ArmTestAssembler.Mov(4, 0x0041_0000),
            ArmTestAssembler.Mov(5, 0x33),
            ArmTestAssembler.StrImmediate(5, 4),
            ArmTestAssembler.Mov(6, 0x005F_0000),
            ArmTestAssembler.Mov(7, 0x44),
            ArmTestAssembler.StrImmediate(7, 6),
            ArmTestAssembler.LdrImmediate(8, 0),
            ArmTestAssembler.LdrImmediate(9, 2),
            ArmTestAssembler.LdrImmediate(10, 4),
            ArmTestAssembler.LdrImmediate(11, 6),
            ArmTestAssembler.Mov(12, 0x0000_1000),
            ArmTestAssembler.StrImmediate(8, 12),
            ArmTestAssembler.StrImmediate(9, 12, 4),
            ArmTestAssembler.StrImmediate(10, 12, 8),
            ArmTestAssembler.StrImmediate(11, 12, 12),
            ArmTestAssembler.Mov(0, 0x0330_0000),
            ArmTestAssembler.LdrImmediate(1, 0, 4),
            ArmTestAssembler.Mov(2, 0x0000_2000),
            ArmTestAssembler.StrImmediate(1, 2, 4),
            ArmTestAssembler.Mov(3, 0x0000_2000),
            ArmTestAssembler.Mov(4, 0xA5),
            ArmTestAssembler.StrImmediate(4, 3),
        ];

        // Finish with a self-branch at its own physical ROM address.
        var lastAddress = 0x0300_0000u + ((uint)program.Length * 4);
        program = [.. program, ArmTestAssembler.Branch(lastAddress, lastAddress)];

        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < program.Length; i++)
        {
            var offset = i * 4;
            var word = program[i];
            rom[offset] = (byte)(word >> 24);
            rom[offset + 1] = (byte)(word >> 16);
            rom[offset + 2] = (byte)(word >> 8);
            rom[offset + 3] = (byte)word;
        }

        return rom;
    }

    [Fact]
    public void LegacyVersionTwoMemorySection_LoadsTheStockLayout()
    {
        var payload = new byte[
            3 + EmulatorConstants.StockHiresRamSize
            + EmulatorConstants.RomSize
            + EmulatorConstants.NvramSize];
        var ramOffset = 3;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            payload.AsSpan(ramOffset + 0x0010_0000),
            0xCAFE_BABE);

        var writer = new StateWriter();
        writer.WriteBytes(payload);
        var section = new StateSectionReader(
            StateSectionIds.Memory,
            version: 2,
            writer.ToArray(),
            writer.Length,
            compressed: false);

        var memory = new FiveDoMemory();
        memory.SetMemoryConfiguration(MemoryConfiguration.Dram8MbVram2Mb);
        memory.Load(section);

        Assert.Equal(MemoryConfiguration.Dram2MbVram1Mb, memory.Layout.Configuration);
        Assert.Equal(0xCAFE_BABEu, memory.Bus.Read32(0x0010_0000));
    }

    [Fact]
    public void MemorySection_StoresOnlyTheLiveBackingRange()
    {
        var memory = new FiveDoMemory();
        memory.Reset();
        memory.Bus.Write32(0x0010_0000, 0x1234_5678);

        // Bytes above the high-resolution backing range are not addressable and must not
        // be serialized.
        var tailOffset = (uint)(memory.Layout.HiresRamSize + 0x1000);
        memory.Ram.Write32(tailOffset, 0xDEAD_BEEF);
        Assert.Equal(0xDEAD_BEEFu, memory.Ram.Read32(tailOffset));

        var writer = new StateWriter();
        memory.Save(writer);
        var expectedLength = 12
            + memory.Layout.HiresRamSize
            + EmulatorConstants.RomSize
            + EmulatorConstants.NvramSize;
        Assert.Equal(expectedLength, writer.Length);
        Assert.True(writer.Length < memory.Size);

        var copy = new FiveDoMemory();
        copy.Load(new StateReader(writer.ToArray()));

        Assert.Equal(0x1234_5678u, copy.Bus.Read32(0x0010_0000));
        Assert.Equal(0u, copy.Ram.Read32(tailOffset));
    }

    [Fact]
    public void SaveState_RestoresExpandedMemoryContentsAndLayout()
    {
        var direct = new FiveDoMemory(new EmulatorTuning
        {
            MemoryConfiguration = MemoryConfiguration.Dram4MbVram2Mb,
        });
        direct.Bus.Write32(0x0050_0000, 0xCAFE_F00D);
        Assert.Equal(0xCAFE_F00Du, direct.Bus.Read32(0x0050_0000));

        var machine = new FiveDoMachine(new EmulatorTuning
        {
            MemoryConfiguration = MemoryConfiguration.Dram4MbVram2Mb,
        });
        Assert.Equal(6 * Megabyte, machine.Memory.RamSize);
        // The BIOS overlay clears on the first DRAM write, exactly as Opera clears it
        // below DRAM_SIZE. Write DRAM first so the VRAM write below is visible.
        machine.Memory.Bus.Write32(0x0010_0000, 0x1234_5678);
        machine.Memory.Bus.Write32(0x0050_0000, 0xDEAD_BEEF);
        Assert.Equal(0xDEAD_BEEFu, machine.Memory.Bus.Read32(0x0050_0000));
        machine.RunFrames(1);
        Assert.Equal(0xDEAD_BEEFu, machine.Memory.Bus.Read32(0x0050_0000));

        using var state = new MemoryStream();
        machine.SaveState(state);

        var stock = new FiveDoMachine();
        state.Position = 0;
        stock.LoadState(state);

        Assert.Equal(MemoryConfiguration.Dram4MbVram2Mb, stock.Memory.Layout.Configuration);
        Assert.Equal(0xDEAD_BEEFu, stock.Memory.Bus.Read32(0x0050_0000));
        Assert.Contains(
            stock.StateWarnings,
            warning => warning.Contains("memory capacity", StringComparison.Ordinal));
    }
}
