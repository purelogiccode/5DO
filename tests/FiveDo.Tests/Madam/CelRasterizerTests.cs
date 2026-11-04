using FiveDo.Core;
using FiveDo.Madam;
using FiveDo.Memory;
using MadamChip = FiveDo.Madam.Madam;

namespace FiveDo.Tests.Madam;

public sealed class CelRasterizerTests
{
    private const uint FrameBuffer = 0x2000;
    private const uint Ccb = 0x100;
    private const uint SourceData = 0x200;
    private const uint PlutData = 0x300;

    [Fact]
    public void PackedLine_LiteralRunWritesDecodedPixelAndCountsCycles()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x10000,
            flags: CcbControl.Clockwise);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 4, 0x00, 0x40, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(15, cycles);
    }

    [Fact]
    public void PackedLine_TransparentPixelDoesNotWrite()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x10000,
            flags: CcbControl.Clockwise);
        WritePlut(memory, 0x7fff, 0);
        WriteBytes(memory, SourceData + 4, 0x00, 0x40, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(12, cycles);
    }

    [Fact]
    public void PackedLine_RepeatRunDrawsEveryTexel()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x10000,
            flags: CcbControl.Clockwise);
        WritePlut(memory, 0, 0x7fff);

        // Offset 0, type 3 (repeat), count 2, pixel 1, end of row.
        WriteBytes(memory, SourceData + 4, 0x00, 0xc1, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 6));
        Assert.Equal(18, cycles);
    }

    [Fact]
    public void PackedLine_CountsDrawnPixelsForOperaBusy()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        madam.CelTiming = CelTimingMode.OperaBusy;
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x10000,
            flags: CcbControl.Clockwise);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 4, 0x00, 0x40, 0x80);

        madam.Poke(0x5a4, Ccb);
        madam.Poke(0x100, 0);

        Assert.True(madam.TickCel(0));
        Assert.Equal(1, madam.CelPixelsDrawn);
        Assert.Equal(65UL, madam.CelBusyCyclesRemaining);
    }

    [Fact]
    public void PackedRepeatLine_CountsEveryRetainedPixelForOperaBusy()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        madam.CelTiming = CelTimingMode.OperaBusy;
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x10000,
            flags: CcbControl.Clockwise);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 4, 0x00, 0xc1, 0x80);

        madam.Poke(0x5a4, Ccb);
        madam.Poke(0x100, 0);

        Assert.True(madam.TickCel(0));
        Assert.Equal(2, madam.CelPixelsDrawn);
        Assert.Equal(66UL, madam.CelBusyCyclesRemaining);
    }

    [Fact]
    public void PackedLine_SkipXSkipsSourcePixels()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x10000,
            flags: CcbControl.Clockwise);
        WritePlut(memory, 0, 0);
        WritePlut(memory, 1, 0x7fff);

        // PRE0: 1 bpp plus SKIPX=1. The packet is a two-pixel literal run
        // containing 0 then 1; skipping the first source pixel must draw the
        // second at x=0 instead of x=1.
        memory.Ram.Write32(SourceData, 1u | (1u << 24));
        WriteBytes(memory, SourceData + 4, 0x00, 0x41, 0x40, 0x00);

        _ = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(0, memory.Ram.Read16(FrameBuffer + 6));
    }

    [Fact]
    public void PackedScale_LiteralRunFillsScaledRectangle()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x20000,
            hdy: 0,
            vdx: 0,
            vdy: 0x20000,
            flags: CcbControl.Clockwise,
            includePerspective: false);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 4, 0x00, 0x40, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 6));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 0));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 4));
        Assert.Equal(24, cycles);
    }

    [Fact]
    public void PackedScale_MariaStopsAfterFirstPixel()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x20000,
            hdy: 0,
            vdx: 0,
            vdy: 0x20000,
            flags: CcbControl.Clockwise | CcbControl.Maria,
            includePerspective: false);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 4, 0x00, 0x40, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(0, memory.Ram.Read16(FrameBuffer + 6));
        Assert.Equal(15, cycles);
    }

    [Fact]
    public void PackedScale_BitTiming3ExpandsVerticalDelta()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        madam.Compatibility.FixMode = FixMode.BitTiming3;
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x20000,
            hdy: 0,
            vdx: 0,
            vdy: 0x20000,
            flags: CcbControl.Clockwise,
            includePerspective: false);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 4, 0x00, 0x40, 0x80);

        var cycles = RunCel(madam);

        // deltay and ycur are multiplied by five, so twenty texels are drawn.
        Assert.Equal(12 + (20 * 3), cycles);
    }

    [Fact]
    public void PackedArbitrary_LiteralRunFillsQuadWithoutCycleCharge()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x10000,
            flags: CcbControl.Clockwise,
            includePerspective: true,
            hddx: 0x1000,
            hddy: 0);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 4, 0x00, 0x40, 0x80);

        var cycles = RunCel(madam);

        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(14, cycles);
    }

    [Fact]
    public void PackedArbitrary_UsesTheColumnAsXAndTheRowAsY()
    {
        (FiveDoMemory memory, MadamChip madam) = CreateScene();
        WritePackedCcb(
            memory,
            loadPlut: true,
            hdx: 0x10000,
            hdy: 0,
            vdx: 0,
            vdy: 0x20000,
            flags: CcbControl.Clockwise,
            includePerspective: true,
            hddx: 0x1000,
            hddy: 0);
        WritePlut(memory, 0, 0x7fff);
        WriteBytes(memory, SourceData + 4, 0x00, 0x40, 0x80);

        _ = RunCel(madam);

        // The quad covers (0,0)..(1,2) inclusively. Both rows at x=0 are drawn,
        // and the inclusive span endpoint draws the second column as well.
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 2));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer));
        Assert.Equal(0x7ffe, memory.Ram.Read16(FrameBuffer + 6));
    }

    private static (FiveDoMemory Memory, MadamChip Madam) CreateScene()
    {
        var memory = new FiveDoMemory();
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

    private static void WritePackedCcb(
        FiveDoMemory memory,
        bool loadPlut,
        int hdx,
        int hdy,
        int vdx,
        int vdy,
        CcbControl flags,
        bool includePerspective = false,
        int hddx = 0,
        int hddy = 0)
    {
        CcbControl ccbFlags = CcbControl.Last
                              | CcbControl.NextPointerAbsolute
                              | CcbControl.SourcePointerAbsolute
                              | CcbControl.LoadSize
                              | CcbControl.LoadPixelProcessor
                              | CcbControl.HasPosition
                              | CcbControl.Packed
                              | flags;

        if (loadPlut)
        {
            ccbFlags |= CcbControl.LoadPlut | CcbControl.PlutPointerAbsolute;
        }

        if (includePerspective)
        {
            ccbFlags |= CcbControl.LoadPerspective;
        }

        memory.Ram.Write32(Ccb, (uint)ccbFlags);
        memory.Ram.Write32(Ccb + 4, 0);
        memory.Ram.Write32(Ccb + 8, SourceData);

        var address = Ccb + 12;
        if (loadPlut)
        {
            memory.Ram.Write32(address, PlutData);
            address += 4;
        }

        memory.Ram.Write32(address, 0);
        memory.Ram.Write32(address + 4, 0);
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

        // Pixel-processor control: mxf=7, dv1=3, all other fields zero.
        memory.Ram.Write32(address, 0x00001f00);

        // PRE0 with 1 bpp and a single packed row.
        memory.Ram.Write32(SourceData, 1);
    }

    private static void WritePlut(FiveDoMemory memory, ushort entry0, ushort entry1)
    {
        memory.Ram.Write16(PlutData, entry1);
        memory.Ram.Write16(PlutData + 2, entry0);
    }

    private static void WriteBytes(FiveDoMemory memory, uint buffer, params byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            memory.Ram.Write8(buffer + ((uint)i ^ 3), bytes[i]);
        }
    }
}
