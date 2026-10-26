using FiveDo.Madam;
using FiveDo.Memory;

namespace FiveDo.Tests.Madam;

public sealed class CcbParserTests
{
    [Fact]
    public void Parse_AbsolutePointersAndCcbPreamble()
    {
        var memory = new FiveDoMemory();
        var flags = CcbControl.Last | CcbControl.NextPointerAbsolute | CcbControl.SourcePointerAbsolute |
            CcbControl.PlutPointerAbsolute | CcbControl.LoadPlut | CcbControl.CcbPreamble;
        memory.Ram.Write32(0x1000, (uint)flags);
        memory.Ram.Write32(0x1004, 0x2000);
        memory.Ram.Write32(0x1008, 0x3000);
        memory.Ram.Write32(0x100c, 0x4000);
        memory.Ram.Write32(0x1018, 0x11111111);
        memory.Ram.Write32(0x101c, 0x22222222);

        var header = new CcbParser(memory.Ram).Parse(0x1000);

        Assert.Equal(0x1000u, header.Address);
        Assert.Equal(flags, header.Flags);
        Assert.Equal(0x2000u, header.NextCcb);
        Assert.Equal(0x3000u, header.PData);
        Assert.Equal(0x4000u, header.PlutData);
        Assert.Equal(0x11111111u, header.Pre0);
        Assert.Equal(0x22222222u, header.Pre1);
        Assert.Equal(0x1020u, header.BodyAddress);
        Assert.True(header.IsLast);
        Assert.True(header.LoadsPlut);
    }

    [Fact]
    public void Parse_RelativePointers()
    {
        var memory = new FiveDoMemory();
        var flags = CcbControl.LoadPlut | CcbControl.CcbPreamble;
        memory.Ram.Write32(0x1000, (uint)flags);
        memory.Ram.Write32(0x1004, 0x10);
        memory.Ram.Write32(0x1008, 0x20);
        memory.Ram.Write32(0x100c, 0x30);
        memory.Ram.Write32(0x1018, 0x11111111);
        memory.Ram.Write32(0x101c, 0x22222222);

        var header = new CcbParser(memory.Ram).Parse(0x1000);

        Assert.Equal(0x1018u, header.NextCcb);
        Assert.Equal(0x102cu, header.PData);
        Assert.Equal(0x1040u, header.PlutData);
        Assert.Equal(0x11111111u, header.Pre0);
        Assert.Equal(0x22222222u, header.Pre1);
    }

    [Fact]
    public void Parse_InvalidPointersBecomeLastAndInvalid()
    {
        var memory = new FiveDoMemory();
        memory.Ram.Write32(0x1000, (uint)(CcbControl.NextPointerAbsolute | CcbControl.SourcePointerAbsolute));
        memory.Ram.Write32(0x1004, 0x300000);
        memory.Ram.Write32(0x1008, 0x400000);

        var header = new CcbParser(memory.Ram).Parse(0x1000);

        Assert.True(header.NextIsLast);
        Assert.True(header.IsLast);
        Assert.True(header.PDataInvalid);
        Assert.Equal(0u, header.Pre0);
    }

    [Fact]
    public void Parse_PackedCcbPreambleReadsOneWord()
    {
        var memory = new FiveDoMemory();
        var flags = CcbControl.CcbPreamble | CcbControl.Packed;
        memory.Ram.Write32(0x1000, (uint)flags);
        memory.Ram.Write32(0x1004, 0);
        memory.Ram.Write32(0x1008, 0);
        memory.Ram.Write32(0x1018, 0x12345678);

        var header = new CcbParser(memory.Ram).Parse(0x1000);

        Assert.Equal(0x12345678u, header.Pre0);
        Assert.Equal(0u, header.Pre1);
        Assert.Equal(0x101cu, header.BodyAddress);
        Assert.True(header.IsPacked);
    }

    [Fact]
    public void Parse_SourcePreambleReadsFromPData()
    {
        var memory = new FiveDoMemory();
        var flags = CcbControl.SourcePointerAbsolute;
        memory.Ram.Write32(0x1000, (uint)flags);
        memory.Ram.Write32(0x1004, 0);
        memory.Ram.Write32(0x1008, 0x2000);
        memory.Ram.Write32(0x2000, 0xaaaaaaaa);
        memory.Ram.Write32(0x2004, 0xbbbbbbbb);

        var header = new CcbParser(memory.Ram).Parse(0x1000);

        Assert.Equal(0xaaaaaaaau, header.Pre0);
        Assert.Equal(0xbbbbbbbbu, header.Pre1);
        Assert.Equal(0x2008u, header.PDataCursor);
        Assert.Equal(0x1018u, header.BodyAddress);
    }

    [Fact]
    public void Parse_ReadsPositionSizePerspectiveAndPixelControlBeforePreamble()
    {
        var memory = new FiveDoMemory();
        var flags = CcbControl.HasPosition | CcbControl.LoadSize | CcbControl.LoadPerspective |
            CcbControl.LoadPixelProcessor | CcbControl.CcbPreamble;
        memory.Ram.Write32(0x1000, (uint)flags);
        memory.Ram.Write32(0x1004, 0);
        memory.Ram.Write32(0x1008, 0);
        memory.Ram.Write32(0x100c, 0);
        memory.Ram.Write32(0x1010, 0x11110000);
        memory.Ram.Write32(0x1014, 0x22220000);
        memory.Ram.Write32(0x1018, 0x00010000);
        memory.Ram.Write32(0x101c, 0x00020000);
        memory.Ram.Write32(0x1020, 0x00030000);
        memory.Ram.Write32(0x1024, 0x00040000);
        memory.Ram.Write32(0x1028, 0x00050000);
        memory.Ram.Write32(0x102c, 0x00060000);
        memory.Ram.Write32(0x1030, 0x12345678);
        memory.Ram.Write32(0x1034, 0xaabbccdd);
        memory.Ram.Write32(0x1038, 0xeeff0011);

        var header = new CcbParser(memory.Ram).Parse(0x1000);

        Assert.Equal(0x11110000, header.XPosition);
        Assert.Equal(0x22220000, header.YPosition);
        Assert.Equal(0x1000, header.Hdx);
        Assert.Equal(0x2000, header.Hdy);
        Assert.Equal(0x00030000, header.Vdx);
        Assert.Equal(0x00040000, header.Vdy);
        Assert.Equal(0x5000, header.Hddx);
        Assert.Equal(0x6000, header.Hddy);
        Assert.Equal(0x12345678u, header.PixelControl);
        Assert.Equal(0xaabbccddu, header.Pre0);
        Assert.Equal(0xeeff0011u, header.Pre1);
        Assert.Equal(0x103cu, header.BodyAddress);
    }

    [Fact]
    public void HeaderHelpers_ExposeFlagBits()
    {
        var header = new CcbHeader
        {
            Flags = CcbControl.Skip | CcbControl.Packed | CcbControl.LoadPlut |
                CcbControl.PlutPosition | (CcbControl)0x100 | (CcbControl)3,
        };

        Assert.True(header.IsSkipped);
        Assert.True(header.IsPacked);
        Assert.True(header.LoadsPlut);
        Assert.Equal(0x100u, header.PixelOverMode);
        Assert.Equal(3u, header.PlutOffset);
    }
}
