using System;
using FiveDo.Core;
using FiveDo.Memory;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Memory;

public sealed class MemoryByteOrderTests
{
    [Fact]
    public void Ram_Write32_ExposesBytesInLittleEndianLanes()
    {
        var memory = new FiveDoMemory();
        IRam ram = memory.Ram;

        ram.Write32(0, 0x11223344);

        Assert.Equal(0x44, ram.Read8(0));
        Assert.Equal(0x33, ram.Read8(1));
        Assert.Equal(0x22, ram.Read8(2));
        Assert.Equal(0x11, ram.Read8(3));
        Assert.Equal(0x11223344u, ram.Read32(0));
    }

    [Fact]
    public void CpuByteRead_AppliesFreeDoXor3()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write32(0, 0x11223344);

        Assert.Equal(0x11, memory.Read8(0));
        Assert.Equal(0x22, memory.Read8(1));
        Assert.Equal(0x33, memory.Read8(2));
        Assert.Equal(0x44, memory.Read8(3));
    }

    [Fact]
    public void CpuByteWrite_AppliesFreeDoXor3()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write32(0, 0x11223344);

        memory.Write8(0, 0xAA);

        Assert.Equal(0xAA223344u, memory.Ram.Read32(0));
    }

    [Fact]
    public void CpuHalfwordRead_AppliesFreeDoXor2()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write32(0, 0x11223344);

        Assert.Equal(0x1122, memory.Read16(0));
        Assert.Equal(0x3344, memory.Read16(2));
    }

    [Fact]
    public void CpuHalfwordWrite_AppliesFreeDoXor2()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write32(0, 0x11223344);

        memory.Write16(0, 0xAABB);

        Assert.Equal(0xAABB3344u, memory.Ram.Read32(0));
    }

    [Fact]
    public void Ram_UnalignedRead16_ComposesBytes()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write32(1, 0x11223344);

        Assert.Equal(0x3344, memory.Ram.Read16(1));
        Assert.Equal(0x2233, memory.Ram.Read16(2));
        Assert.Equal(0x1122, memory.Ram.Read16(3));
    }

    [Fact]
    public void Ram_UnalignedRead32_ComposesBytes()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write32(1, 0x11223344);

        Assert.Equal(0x11223344u, memory.Ram.Read32(1));
        Assert.Equal(0x22334400u, memory.Ram.Read32(0));
        Assert.Equal(0x00112233u, memory.Ram.Read32(2));
    }

    [Fact]
    public void Ram_UnalignedWrite32_ComposesBytes()
    {
        var memory = new FiveDoMemory();

        memory.Ram.Write32(1, 0x11223344);

        Assert.Equal(0x44, memory.Ram.Read8(1));
        Assert.Equal(0x33, memory.Ram.Read8(2));
        Assert.Equal(0x22, memory.Ram.Read8(3));
        Assert.Equal(0x11, memory.Ram.Read8(4));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    public void Ram_AllByteAlignments_Roundtrip(uint offset)
    {
        var memory = new FiveDoMemory();
        var value = (byte)(0x10 + offset);

        memory.Ram.Write8(offset, value);

        Assert.Equal(value, memory.Ram.Read8(offset));
        Assert.Equal(value, memory.Read8(offset ^ 3));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    public void Ram_AllHalfwordAlignments_Roundtrip(uint offset)
    {
        var memory = new FiveDoMemory();
        var value = (ushort)(0x1020 + offset);

        memory.Ram.Write16(offset, value);

        Assert.Equal(value, memory.Ram.Read16(offset));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    public void Ram_AllWordAlignments_Roundtrip(uint offset)
    {
        var memory = new FiveDoMemory();
        var value = 0x11223344u + offset;

        memory.Ram.Write32(offset, value);

        Assert.Equal(value, memory.Ram.Read32(offset));
    }

    [Fact]
    public void Ram_OutOfRangeAccess_ReturnsZeroAndIgnoresWrites()
    {
        var memory = new FiveDoMemory();

        memory.Ram.Write32((uint)memory.Size, 0x12345678);

        Assert.Equal(0u, memory.Ram.Read32((uint)memory.Size));
        Assert.Equal(0, memory.Ram.Read8((uint)memory.Size));
    }

    [Fact]
    public void RomLoad_ConvertsBigEndianBiosWords()
    {
        var memory = new FiveDoMemory();
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[0] = 0x01;
        bios[1] = 0x02;
        bios[2] = 0x03;
        bios[3] = 0x04;

        memory.LoadBios(bios);

        Assert.Equal(0x01020304u, memory.Read32(EmulatorConstants.RomBase));
        Assert.Equal(0x01, memory.Read8(EmulatorConstants.RomBase));
        Assert.Equal(0x02, memory.Read8(EmulatorConstants.RomBase + 1));
        Assert.Equal(0x03, memory.Read8(EmulatorConstants.RomBase + 2));
        Assert.Equal(0x04, memory.Read8(EmulatorConstants.RomBase + 3));
        Assert.Equal(0x0102, memory.Read16(EmulatorConstants.RomBase));
        Assert.Equal(0x0304, memory.Read16(EmulatorConstants.RomBase + 2));
    }

    [Fact]
    public void RomBankSelect_SwitchesVisibleBank()
    {
        var memory = new FiveDoMemory();
        var bios1 = new byte[EmulatorConstants.RomBankSize];
        var bios2 = new byte[EmulatorConstants.RomBankSize];
        bios1[0] = 0x01;
        bios1[1] = 0x02;
        bios1[2] = 0x03;
        bios1[3] = 0x04;
        bios2[0] = 0x05;
        bios2[1] = 0x06;
        bios2[2] = 0x07;
        bios2[3] = 0x08;

        memory.LoadBios(bios1, bios2);

        Assert.Equal(0x01020304u, memory.Read32(EmulatorConstants.RomBase));
        Assert.Equal(0, memory.RomBank);

        memory.SelectRomBank(1);

        Assert.Equal(1, memory.RomBank);
        Assert.Equal(0x05060708u, memory.Read32(EmulatorConstants.RomBase));
        Assert.Equal(0x05, memory.Read8(EmulatorConstants.RomBase));

        memory.SelectRomBank(0);

        Assert.Equal(0x01020304u, memory.Read32(EmulatorConstants.RomBase));
    }

    [Fact]
    public void RomWrite_IsIgnored()
    {
        var memory = new FiveDoMemory();
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[0] = 0x01;
        bios[1] = 0x02;
        bios[2] = 0x03;
        bios[3] = 0x04;
        memory.LoadBios(bios);

        memory.Write32(EmulatorConstants.RomBase, 0xFFFFFFFF);
        memory.Write8(EmulatorConstants.RomBase, 0xFF);

        Assert.Equal(0x01020304u, memory.Read32(EmulatorConstants.RomBase));
    }

    [Fact]
    public void NvramWord_StoresOneBytePerWord()
    {
        var memory = new FiveDoMemory();

        memory.Write32(EmulatorConstants.NvramBase, 0x12345678);

        Assert.Equal(0x78, memory.Nvram[0]);
        Assert.Equal(0x78u, memory.Read32(EmulatorConstants.NvramBase));

        memory.Write32(EmulatorConstants.NvramBase + 4, 0xAABBCCDD);

        Assert.Equal(0xDD, memory.Nvram[1]);
        Assert.Equal(0xDDu, memory.Read32(EmulatorConstants.NvramBase + 4));
    }

    [Fact]
    public void NvramByte_StoresOneBytePerWord()
    {
        var memory = new FiveDoMemory();

        memory.Write8(EmulatorConstants.NvramBase, 0x11);
        Assert.Equal(0x11, memory.Nvram[0]);
        Assert.Equal(0x11, memory.Read8(EmulatorConstants.NvramBase));

        memory.Write8(EmulatorConstants.NvramBase + 1, 0x22);
        Assert.Equal(0x22, memory.Nvram[0]);
        Assert.Equal(0x22, memory.Read8(EmulatorConstants.NvramBase + 1));

        memory.Write8(EmulatorConstants.NvramBase + 4, 0x33);
        Assert.Equal(0x33, memory.Nvram[1]);
    }

    [Fact]
    public void NvramWord_WrapsAt32K()
    {
        var memory = new FiveDoMemory();

        memory.Write32(EmulatorConstants.NvramBase + 0x20000, 0x99);
        Assert.Equal(0x99, memory.Nvram[0]);

        memory.Write32(EmulatorConstants.NvramBase + 0x3FFFC, 0x5A);
        Assert.Equal(0x5A, memory.Nvram[^1]);
    }

    [Fact]
    public void Nvram_LoadAndCopy_Roundtrip()
    {
        var memory = new FiveDoMemory();
        var source = new byte[EmulatorConstants.NvramSize];
        for (var i = 0; i < source.Length; i++)
        {
            source[i] = (byte)i;
        }

        memory.LoadNvram(source);

        var destination = new byte[EmulatorConstants.NvramSize];
        memory.CopyNvram(destination);

        Assert.Equal(source, destination);
    }

    [Fact]
    public void LoadRom_WrongSize_Throws()
    {
        var memory = new FiveDoMemory();

        Assert.Throws<ArgumentException>(() => memory.LoadRom(0, new byte[16]));
    }

    [Fact]
    public void LoadRom_InvalidBank_Throws()
    {
        var memory = new FiveDoMemory();

        Assert.Throws<ArgumentOutOfRangeException>(() => memory.LoadRom(2, new byte[EmulatorConstants.RomBankSize]));
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.SelectRomBank(-1));
    }

    [Fact]
    public void LoadNvram_WrongSize_Throws()
    {
        var memory = new FiveDoMemory();

        Assert.Throws<ArgumentException>(() => memory.LoadNvram(new byte[16]));
    }

    [Fact]
    public void UnmappedReads_ReturnSentinel()
    {
        var memory = new FiveDoMemory();

        Assert.Equal(EmulatorConstants.UnmappedValue, memory.Read32(0x03100000));
        Assert.Equal(EmulatorConstants.UnmappedValue, memory.Read32(0x03500000));
        Assert.Equal(unchecked((byte)EmulatorConstants.UnmappedValue), memory.Read8(0x03100000));
        Assert.Equal(unchecked((ushort)EmulatorConstants.UnmappedValue), memory.Read16(0x03100000));
    }

    [Fact]
    public void Memory_AllocationIncludesHighResolutionMirrors()
    {
        var memory = new FiveDoMemory();

        Assert.Equal(EmulatorConstants.RamAllocationSize, memory.Size);
        Assert.Equal(EmulatorConstants.RamSize, FiveDoMemory.VisibleSize);
    }

    [Fact]
    public void BusRoutesMadamWindow()
    {
        var memory = new FiveDoMemory();
        var madam = new FakeMadamBus { ReadValue = 0x12345678 };
        memory.Madam = madam;

        Assert.Equal(0x12345678u, memory.Read32(0x03300004));
        Assert.Equal(4u, madam.LastReadOffset);

        memory.Write32(0x03300600, 0xABCDEF01);
        Assert.Equal(0x600u, madam.LastWriteOffset);
        Assert.Equal(0xABCDEF01u, madam.LastWriteValue);

        Assert.Equal(EmulatorConstants.UnmappedValue, memory.Read32(0x03300800));
    }

    [Fact]
    public void BusRoutesClioWindowAndPcAdjust()
    {
        var memory = new FiveDoMemory();
        var clio = new FakeClioBus { ReadValue = 0xCAFEBABE, PokeResult = true };
        memory.Clio = clio;

        Assert.Equal(0xCAFEBABEu, memory.Read32(0x03401234));
        Assert.Equal(0x1234u, clio.LastReadOffset);

        memory.Write32(0x03405678, 0x01020304);
        Assert.Equal(0x5678u, clio.LastWriteOffset);
        Assert.Equal(0x01020304u, clio.LastWriteValue);
        Assert.True(memory.PcAdjustPending);
        Assert.True(memory.ConsumePcAdjustment());
        Assert.False(memory.PcAdjustPending);
        Assert.False(memory.ConsumePcAdjustment());
    }

    [Fact]
    public void BusRoutesSportWindow()
    {
        var memory = new FiveDoMemory();
        var sport = new FakeSportBus { SourceValue = 0xFEEDFACE };
        memory.Sport = sport;

        Assert.Equal(0xFEEDFACEu, memory.Read32(0x03200100));
        Assert.Equal(0x100u, sport.LastSourceOffset);
        Assert.Equal(EmulatorConstants.UnmappedValue, memory.Read32(0x03202000));

        memory.Write32(0x03204000, 0x11223344);
        Assert.Equal(0x4000u, sport.LastWriteOffset);
        Assert.Equal(0x11223344u, sport.LastWriteValue);
    }

    [Fact]
    public void BusRoutesDiagPort()
    {
        var memory = new FiveDoMemory();
        var diag = new FakeDiagPort { ReadValue = 0xA5A5A5A5 };
        memory.DiagPort = diag;

        Assert.Equal(0xA5A5A5A5u, memory.Read32(0x03180000));

        memory.Write32(0x03180004, 0x5A5A5A5A);
        Assert.Equal(0x5A5A5A5Au, diag.LastSent);
    }

    [Fact]
    public void Reset_ClearsBankAndPcAdjustButKeepsRam()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write32(0, 0x11223344);
        memory.SelectRomBank(1);
        var clio = new FakeClioBus { PokeResult = true };
        memory.Clio = clio;
        memory.Write32(0x03400000, 1);

        memory.Reset();

        Assert.Equal(0, memory.RomBank);
        Assert.False(memory.PcAdjustPending);
        Assert.Equal(0x11223344u, memory.Ram.Read32(0));

        memory.ClearRam();
        Assert.Equal(0u, memory.Ram.Read32(0));
    }
}
