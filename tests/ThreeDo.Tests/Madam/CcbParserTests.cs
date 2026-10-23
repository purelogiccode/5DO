using ThreeDo.Madam;
using ThreeDo.Memory;

namespace ThreeDo.Tests.Madam;

public sealed class CcbParserTests
{
    [Fact]
    public void Parse_AbsolutePointersAndCcbPreamble()
    {
        var memory = new ThreeDoMemory();
        var flags = CcbControl.Last | CcbControl.NextPointerAbsolute | CcbControl.SourcePointerAbsolute |
            CcbControl.PlutPointerAbsolute | CcbControl.LoadPlut | CcbControl.CcbPreamble;
        memory.Ram.Write32(0x1000, (uint)flags);
        memory.Ram.Write32(0x1004, 0x2000);
        memory.Ram.Write32(0x1008, 0x3000);
        memory.Ram.Write32(0x100c, 0x4000);
        memory.Ram.Write32(0x1010, 0x11111111);
        memory.Ram.Write32(0x1014, 0x22222222);

        var header = new CcbParser(memory.Ram).Parse(0x1000);

        Assert.Equal(0x1000u, header.Address);
        Assert.Equal(flags, header.Flags);
        Assert.Equal(0x2000u, header.NextCcb);
        Assert.Equal(0x3000u, header.PData);
        Assert.Equal(0x4000u, header.PlutData);
        Assert.Equal(0x11111111u, header.Pre0);
        Assert.Equal(0x22222222u, header.Pre1);
        Assert.Equal(0x1018u, header.BodyAddress);
        Assert.True(header.IsLast);
        Assert.True(header.LoadsPlut);
    }

    [Fact]
    public void Parse_RelativePointers()
    {
        var memory = new ThreeDoMemory();
        var flags = CcbControl.LoadPlut | CcbControl.CcbPreamble;
        memory.Ram.Write32(0x1000, (uint)flags);
        memory.Ram.Write32(0x1004, 0x10);
        memory.Ram.Write32(0x1008, 0x20);
        memory.Ram.Write32(0x100c, 0x30);
        memory.Ram.Write32(0x1010, 0x11111111);
        memory.Ram.Write32(0x1014, 0x22222222);

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
        var memory = new ThreeDoMemory();
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
        var memory = new ThreeDoMemory();
        var flags = CcbControl.CcbPreamble | CcbControl.Packed;
        memory.Ram.Write32(0x1000, (uint)flags);
        memory.Ram.Write32(0x1004, 0);
        memory.Ram.Write32(0x1008, 0);
        memory.Ram.Write32(0x1010, 0x12345678);

        var header = new CcbParser(memory.Ram).Parse(0x1000);

        Assert.Equal(0x12345678u, header.Pre0);
        Assert.Equal(0u, header.Pre1);
        Assert.Equal(0x1014u, header.BodyAddress);
        Assert.True(header.IsPacked);
    }

    [Fact]
    public void Parse_SourcePreambleReadsFromPData()
    {
        var memory = new ThreeDoMemory();
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
        Assert.Equal(0x1010u, header.BodyAddress);
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
