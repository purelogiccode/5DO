using ThreeDo.Core;
using ThreeDo.Madam;
using ThreeDo.Memory;
using MadamChip = ThreeDo.Madam.Madam;

namespace ThreeDo.Tests.Madam;

public sealed class CelLiteralRasterizerTests
{
    private const uint FrameBuffer = 0x2000;
    private const uint Ccb = 0x100;
    private const uint SourceData = 0x200;
    private const uint PlutData = 0x300;

    [Fact]
    public void LiteralLine_DrawsOnePixelAndCountsCycles()
    {
        var (memory, madam) = CreateScene();
        WriteLiteralCcb(memory, vcnt: 0, widthMinusOne: 0, pre0: 1, pre1: 0, loadPlut: true);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 8, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(16, cycles);
    }

    [Fact]
    public void LiteralLine_HonorsBackgroundFlagForZeroPixel()
    {
        var (memory, madam) = CreateScene();
        WriteLiteralCcb(
            memory,
            vcnt: 0,
            widthMinusOne: 0,
            pre0: 1,
            pre1: 0,
            loadPlut: true,
            extraFlags: CcbControl.Background);
        WritePlut(memory, 0x7fff, 0x7fff);
        WriteBytes(memory, SourceData + 8, 0x00);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(16, cycles);
    }

    [Fact]
    public void LiteralScale_DrawsScaledRectangle()
    {
        var (memory, madam) = CreateScene();
        WriteLiteralCcb(
            memory,
            vcnt: 0,
            widthMinusOne: 0,
            pre0: 1,
            pre1: 0,
            loadPlut: true,
            hdx: 0x20000,
            hdy: 0,
            vdx: 0,
            vdy: 0x20000);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 8, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 6));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 0));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 4));
        Assert.Equal(25, cycles);
    }

    [Fact]
    public void LrLine_DrawsTwoRowsAndCountsHalfwordReads()
    {
        var (memory, madam) = CreateScene();
        WriteLrCcb(memory);
        memory.Ram.Write16(0x208, 0x7fff);
        memory.Ram.Write16(0x20a, 0x7fff);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 0));
        Assert.Equal(20, cycles);
    }

    [Fact]
    public void LiteralArbitrary_FillsQuad()
    {
        var (memory, madam) = CreateScene();
        WriteLiteralCcb(
            memory,
            vcnt: 0,
            widthMinusOne: 0,
            pre0: 1,
            pre1: 0,
            loadPlut: true,
            includePerspective: true,
            hddx: 0x1000);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 8, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(15, cycles);
    }

    [Fact]
    public void LiteralLine_BitTiming6UsesDoubledY()
    {
        var (memory, madam) = CreateScene();
        madam.Compatibility.FixMode = FixMode.BitTiming6;
        WriteLiteralCcb(
            memory,
            vcnt: 0,
            widthMinusOne: 0,
            pre0: 1,
            pre1: 0,
            loadPlut: true,
            ypos: 0x10000);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 8, 0x80);

        _ = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 0));
        Assert.Equal(0, memory.Ram.Read16(FrameBuffer + 2));
    }

    [Fact]
    public void LrScale_DrawsScaledRows()
    {
        var (memory, madam) = CreateScene();
        WriteLrCcb(memory, hdx: 0x20000, hdy: 0, vdx: 0, vdy: 0x20000);
        memory.Ram.Write16(0x208, 0x7fff);
        memory.Ram.Write16(0x20a, 0x7fff);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(38, cycles);
    }

    [Fact]
    public void LrArbitrary_FillsQuad()
    {
        var (memory, madam) = CreateScene();
        WriteLrCcb(
            memory,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x10000,
            includePerspective: true,
            hddx: 0x1000);
        memory.Ram.Write16(0x208, 0x7fff);
        memory.Ram.Write16(0x20a, 0x7fff);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(16, cycles);
    }

    private static (ThreeDoMemory Memory, MadamChip Madam) CreateScene()
    {
        var memory = new ThreeDoMemory();
        var madam = new MadamChip { Ram = memory.Ram };
        madam.Poke(0x134, 319u | (239u << 16));
        madam.Poke(0x138, FrameBuffer);
        madam.Poke(0x13c, FrameBuffer);
        return (memory, madam);
    }

    private static int RunCel(MadamChip madam)
    {
        madam.Poke(0x5a4, Ccb);
        madam.Poke(0x100, 0);
        return madam.HandleCel();
    }

    private static void WriteLiteralCcb(
        ThreeDoMemory memory,
        int vcnt,
        int widthMinusOne,
        uint pre0,
        uint pre1,
        bool loadPlut,
        CcbControl extraFlags = CcbControl.None,
        int hdx = 0x10000,
        int hdy = 0,
        int vdx = 0,
        int vdy = 0x10000,
        int xpos = 0,
        int ypos = 0,
        bool includePerspective = false,
        int hddx = 0,
        int hddy = 0)
    {
        var flags = CcbControl.Last
            | CcbControl.NextPointerAbsolute
            | CcbControl.SourcePointerAbsolute
            | CcbControl.LoadSize
            | CcbControl.LoadPixelProcessor
            | CcbControl.HasPosition
            | CcbControl.Clockwise
            | extraFlags;

        if (loadPlut)
        {
            flags |= CcbControl.LoadPlut | CcbControl.PlutPointerAbsolute;
        }

        if (includePerspective)
        {
            flags |= CcbControl.LoadPerspective;
        }

        memory.Ram.Write32(Ccb, (uint)flags);
        memory.Ram.Write32(Ccb + 4, 0);
        memory.Ram.Write32(Ccb + 8, SourceData);

        var address = Ccb + 12;
        if (loadPlut)
        {
            memory.Ram.Write32(address, PlutData);
            address += 4;
        }

        memory.Ram.Write32(address, unchecked((uint)xpos));
        memory.Ram.Write32(address + 4, unchecked((uint)ypos));
        memory.Ram.Write32(address + 8, unchecked((uint)(hdx << 4)));
        memory.Ram.Write32(address + 12, unchecked((uint)(hdy << 4)));
        memory.Ram.Write32(address + 16, unchecked((uint)vdx));
        memory.Ram.Write32(address + 20, unchecked((uint)vdy));
        address += 24;

        if (includePerspective)
        {
            memory.Ram.Write32(address, unchecked((uint)(hddx << 4)));
            memory.Ram.Write32(address + 4, unchecked((uint)(hddy << 4)));
            address += 8;
        }

        memory.Ram.Write32(address, 0x00001f00);

        memory.Ram.Write32(
            SourceData,
            pre0 | ((uint)(vcnt & 0x3ff) << 6));
        memory.Ram.Write32(
            SourceData + 4,
            pre1 | (uint)(widthMinusOne & 0x7ff));
    }

    private static void WriteLrCcb(
        ThreeDoMemory memory,
        int hdx = 0x10000,
        int hdy = 0,
        int vdx = 0,
        int vdy = 0x10000,
        bool includePerspective = false,
        int hddx = 0,
        int hddy = 0)
    {
        var flags = CcbControl.Last
            | CcbControl.NextPointerAbsolute
            | CcbControl.SourcePointerAbsolute
            | CcbControl.LoadSize
            | CcbControl.LoadPixelProcessor
            | CcbControl.HasPosition
            | CcbControl.Clockwise;

        if (includePerspective)
        {
            flags |= CcbControl.LoadPerspective;
        }

        memory.Ram.Write32(Ccb, (uint)flags);
        memory.Ram.Write32(Ccb + 4, 0);
        memory.Ram.Write32(Ccb + 8, SourceData);
        memory.Ram.Write32(Ccb + 12, 0);
        memory.Ram.Write32(Ccb + 16, 0);
        memory.Ram.Write32(Ccb + 20, 0);
        memory.Ram.Write32(Ccb + 24, unchecked((uint)(hdx << 4)));
        memory.Ram.Write32(Ccb + 28, unchecked((uint)(hdy << 4)));
        memory.Ram.Write32(Ccb + 32, unchecked((uint)vdx));
        memory.Ram.Write32(Ccb + 36, unchecked((uint)vdy));

        var address = Ccb + 40;
        if (includePerspective)
        {
            memory.Ram.Write32(address, unchecked((uint)(hddx << 4)));
            memory.Ram.Write32(address + 4, unchecked((uint)(hddy << 4)));
            address += 8;
        }

        memory.Ram.Write32(address, 0x00001f00);

        // 16 bpp linear, one 16-bit row count, LR form with one horizontal pixel.
        memory.Ram.Write32(SourceData, 6 | 0x10);
        memory.Ram.Write32(SourceData + 4, (uint)CcbPreamble1.LrForm);
    }

    private static void WritePlut(ThreeDoMemory memory, ushort entry0, ushort entry1)
    {
        memory.Ram.Write16(PlutData, entry1);
        memory.Ram.Write16(PlutData + 2, entry0);
    }

    private static void WriteBytes(ThreeDoMemory memory, uint buffer, params byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            memory.Ram.Write8(buffer + ((uint)i ^ 3), bytes[i]);
        }
    }
}
