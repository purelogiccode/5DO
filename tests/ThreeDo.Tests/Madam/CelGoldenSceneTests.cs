using ThreeDo.Madam;
using ThreeDo.Memory;
using MadamChip = ThreeDo.Madam.Madam;

namespace ThreeDo.Tests.Madam;

/// <summary>
/// Golden CEL scenes: clipping, PLUT reloads, transparency, and a composite frame with a
/// stable VRAM hash. These complement the per-path rasterizer tests.
/// </summary>
public sealed class CelGoldenSceneTests
{
    private const uint FrameBuffer = 0x2000;
    private const uint Ccb1 = 0x100;
    private const uint Ccb2 = 0x400;
    private const uint Ccb3 = 0x700;
    private const uint Source1 = 0x200;
    private const uint Source2 = 0x300;
    private const uint Source3 = 0x800;
    private const uint Plut1 = 0x500;
    private const uint Plut2 = 0x600;

    // 320-pixel line pairs are 1280 bytes apart (two rows share one 32-bit word per pixel).
    private const int WriteModulo = 1280;

    [Fact]
    public void LiteralScale_ClipsAtTopLeftCorner()
    {
        var (memory, madam) = CreateScene(clipX: 0, clipY: 0);
        WriteLiteralCcb(
            memory,
            Ccb3,
            Source3,
            position: (0, 0),
            hdx: 0x20000,
            vdy: 0x20000,
            pre0: 6 | 0x10,
            pre1: 0,
            last: true);
        WriteBytes(memory, Source3 + 8, 0x7f, 0xfe);

        var cycles = RunCel(madam, Ccb3);

        Assert.Equal(0x7ffe, ReadPixel(memory, 0, 0));
        Assert.Equal(0, ReadPixel(memory, 1, 0));
        Assert.Equal(0, ReadPixel(memory, 0, 1));
        Assert.Equal(0, ReadPixel(memory, 1, 1));
        Assert.True(cycles > 0);
    }

    [Fact]
    public void LiteralScale_ClipsAtBottomRightCorner()
    {
        var (memory, madam) = CreateScene(clipX: 7, clipY: 7);
        WriteLiteralCcb(
            memory,
            Ccb3,
            Source3,
            position: (6 << 16, 6 << 16),
            hdx: 0x40000,
            vdy: 0x40000,
            pre0: 6 | 0x10,
            pre1: 0,
            last: true);
        WriteBytes(memory, Source3 + 8, 0x7f, 0xfe);

        _ = RunCel(madam, Ccb3);

        Assert.Equal(0x7ffe, ReadPixel(memory, 6, 6));
        Assert.Equal(0x7ffe, ReadPixel(memory, 7, 6));
        Assert.Equal(0x7ffe, ReadPixel(memory, 6, 7));
        Assert.Equal(0x7ffe, ReadPixel(memory, 7, 7));
        Assert.Equal(0, ReadPixel(memory, 8, 6));
        Assert.Equal(0, ReadPixel(memory, 6, 8));
        Assert.Equal(0, ReadPixel(memory, 5, 5));
    }

    [Fact]
    public void CompositeScene_DrawsCelsInListOrderWithPlutReloads()
    {
        var (memory, madam) = CreateScene(clipX: 7, clipY: 7);

        // First CEL: packed one-texel run at (0,0), palette A.
        WritePackedCcb(memory, Ccb1, nextCcb: Ccb2, last: false, position: (0, 0), source: Source1, plut: Plut1);
        WritePlut(memory, Plut1, entry0: 0, entry1: 0x7fff);
        memory.Ram.Write32(Source1, 1);
        WriteBytes(memory, Source1 + 4, 0x00, 0x40, 0x80);

        // Second CEL: packed one-texel run at (1,0) with a new palette (PLUT reload).
        WritePackedCcb(memory, Ccb2, nextCcb: Ccb3, last: false, position: (1 << 16, 0), source: Source2, plut: Plut2);
        WritePlut(memory, Plut2, entry0: 0, entry1: 0x001f);
        memory.Ram.Write32(Source2, 1);
        WriteBytes(memory, Source2 + 4, 0x00, 0x40, 0x80);

        // Third CEL: scaled 4x4 opaque rectangle at (6,6), clipped to the 8x8 clip window.
        WriteLiteralCcb(
            memory,
            Ccb3,
            Source3,
            position: (6 << 16, 6 << 16),
            hdx: 0x40000,
            vdy: 0x40000,
            pre0: 6 | 0x10,
            pre1: 0,
            last: true);
        WriteBytes(memory, Source3 + 8, 0x7c, 0x00);

        var cycles = RunCel(madam, Ccb1);

        Assert.Equal(0x7ffe, ReadPixel(memory, 0, 0));
        Assert.Equal(0x001e, ReadPixel(memory, 1, 0));
        for (var y = 6; y <= 7; y++)
        {
            for (var x = 6; x <= 7; x++)
            {
                Assert.Equal(0x7c00, ReadPixel(memory, x, y));
            }
        }

        Assert.Equal(0, ReadPixel(memory, 2, 0));
        Assert.Equal(0, ReadPixel(memory, 8, 8));

        // Golden VRAM hash over the 16x16 pixel region and the measured CEL cycle count.
        Assert.Equal(0xfdd5a065u, HashRegion(memory, 16, 16));
        Assert.Equal(54, cycles);

        // The scene is deterministic across repeated runs.
        var (memory2, madam2) = CreateScene(clipX: 7, clipY: 7);
        WriteComposite(memory2);
        _ = RunCel(madam2, Ccb1);
        Assert.Equal(0xfdd5a065u, HashRegion(memory2, 16, 16));
    }

    [Fact]
    public void CompositeScene_TransparentTexelKeepsUnderlyingPixel()
    {
        var (memory, madam) = CreateScene(clipX: 7, clipY: 7);

        // Opaque 16-bit linear CEL scaled to 2x2 at the origin.
        WriteLiteralCcb(
            memory,
            Ccb1,
            Source1,
            position: (0, 0),
            hdx: 0x20000,
            vdy: 0x20000,
            pre0: 6 | 0x10,
            pre1: 0,
            last: false,
            nextCcb: Ccb2);
        WriteBytes(memory, Source1 + 8, 0x7f, 0xfe);

        // Second CEL covers (1,1) with 16-bit pixel zero. With no CCB_BACKGROUND flag the
        // decoder marks it transparent, so the first CEL's pixel must survive.
        WriteLiteralCcb(
            memory,
            Ccb2,
            Source2,
            position: (1 << 16, 1 << 16),
            hdx: 0x10000,
            vdy: 0x10000,
            pre0: 6 | 0x10,
            pre1: 0,
            last: true);
        WriteBytes(memory, Source2 + 8, 0x00, 0x00);

        var cycles = RunCel(madam, Ccb1);

        Assert.Equal(0x7ffe, ReadPixel(memory, 0, 0));
        Assert.Equal(0x7ffe, ReadPixel(memory, 1, 0));
        Assert.Equal(0x7ffe, ReadPixel(memory, 0, 1));
        Assert.Equal(0x7ffe, ReadPixel(memory, 1, 1));
        Assert.True(cycles > 0);
    }

    private static void WriteComposite(ThreeDoMemory memory)
    {
        WritePackedCcb(memory, Ccb1, nextCcb: Ccb2, last: false, position: (0, 0), source: Source1, plut: Plut1);
        WritePlut(memory, Plut1, entry0: 0, entry1: 0x7fff);
        memory.Ram.Write32(Source1, 1);
        WriteBytes(memory, Source1 + 4, 0x00, 0x40, 0x80);

        WritePackedCcb(memory, Ccb2, nextCcb: Ccb3, last: false, position: (1 << 16, 0), source: Source2, plut: Plut2);
        WritePlut(memory, Plut2, entry0: 0, entry1: 0x001f);
        memory.Ram.Write32(Source2, 1);
        WriteBytes(memory, Source2 + 4, 0x00, 0x40, 0x80);

        WriteLiteralCcb(
            memory,
            Ccb3,
            Source3,
            position: (6 << 16, 6 << 16),
            hdx: 0x40000,
            vdy: 0x40000,
            pre0: 6 | 0x10,
            pre1: 0,
            last: true);
        WriteBytes(memory, Source3 + 8, 0x7c, 0x00);
    }

    private static (ThreeDoMemory Memory, MadamChip Madam) CreateScene(int clipX, int clipY)
    {
        var memory = new ThreeDoMemory();
        var madam = new MadamChip { Ram = memory.Ram };
        madam.Poke(0x130, 0x1414);
        madam.Poke(0x134, (uint)(clipX | (clipY << 16)));
        madam.Poke(0x138, FrameBuffer);
        madam.Poke(0x13c, FrameBuffer);
        return (memory, madam);
    }

    private static int RunCel(MadamChip madam, uint firstCcb)
    {
        madam.Poke(0x5a4, firstCcb);
        madam.Poke(0x100, 0);
        return madam.HandleCel();
    }

    private static ushort ReadPixel(ThreeDoMemory memory, int x, int y)
    {
        var offset = ((y >> 1) * WriteModulo) + ((y & 1) << 1) + (x << 2);
        return memory.Ram.Read16((FrameBuffer + (uint)offset) ^ 2);
    }

    private static uint HashRegion(ThreeDoMemory memory, int width, int height)
    {
        var hash = 2166136261u;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = ReadPixel(memory, x, y);
                hash = unchecked((hash ^ value) * 16777619);
            }
        }

        return hash;
    }

    private static void WritePackedCcb(
        ThreeDoMemory memory,
        uint ccb,
        uint nextCcb,
        bool last,
        (int X, int Y) position,
        uint source,
        uint plut)
    {
        var flags = CcbControl.NextPointerAbsolute
            | CcbControl.SourcePointerAbsolute
            | CcbControl.PlutPointerAbsolute
            | CcbControl.LoadPlut
            | CcbControl.LoadSize
            | CcbControl.LoadPixelProcessor
            | CcbControl.HasPosition
            | CcbControl.Packed
            | CcbControl.Clockwise;

        if (last)
        {
            flags |= CcbControl.Last;
        }

        memory.Ram.Write32(ccb, (uint)flags);
        memory.Ram.Write32(ccb + 4, nextCcb);
        memory.Ram.Write32(ccb + 8, source);
        memory.Ram.Write32(ccb + 12, plut);
        memory.Ram.Write32(ccb + 16, unchecked((uint)position.X));
        memory.Ram.Write32(ccb + 20, unchecked((uint)position.Y));
        memory.Ram.Write32(ccb + 24, 0x00100000);
        memory.Ram.Write32(ccb + 28, 0);
        memory.Ram.Write32(ccb + 32, 0);
        memory.Ram.Write32(ccb + 36, 0x00010000);
        memory.Ram.Write32(ccb + 40, 0x00001f00);
    }

    private static void WriteLiteralCcb(
        ThreeDoMemory memory,
        uint ccb,
        uint source,
        (int X, int Y) position,
        int hdx,
        int vdy,
        uint pre0,
        uint pre1,
        bool last,
        uint nextCcb = 0,
        bool loadPlut = false,
        uint plut = 0)
    {
        var flags = CcbControl.NextPointerAbsolute
            | CcbControl.SourcePointerAbsolute
            | CcbControl.LoadSize
            | CcbControl.LoadPixelProcessor
            | CcbControl.HasPosition
            | CcbControl.Clockwise;

        if (last)
        {
            flags |= CcbControl.Last;
        }

        if (loadPlut)
        {
            flags |= CcbControl.LoadPlut | CcbControl.PlutPointerAbsolute;
        }

        memory.Ram.Write32(ccb, (uint)flags);
        memory.Ram.Write32(ccb + 4, nextCcb);
        memory.Ram.Write32(ccb + 8, source);
        memory.Ram.Write32(ccb + 12, plut);
        memory.Ram.Write32(ccb + 16, unchecked((uint)position.X));
        memory.Ram.Write32(ccb + 20, unchecked((uint)position.Y));
        memory.Ram.Write32(ccb + 24, unchecked((uint)(hdx << 4)));
        memory.Ram.Write32(ccb + 28, 0);
        memory.Ram.Write32(ccb + 32, 0);
        memory.Ram.Write32(ccb + 36, unchecked((uint)vdy));
        memory.Ram.Write32(ccb + 40, 0x00001f00);

        memory.Ram.Write32(source, pre0);
        memory.Ram.Write32(source + 4, pre1);
    }

    private static void WritePlut(ThreeDoMemory memory, uint address, ushort entry0, ushort entry1)
    {
        memory.Ram.Write16(address, entry1);
        memory.Ram.Write16(address + 2, entry0);
    }

    private static void WriteBytes(ThreeDoMemory memory, uint buffer, params byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            memory.Ram.Write8(buffer + ((uint)i ^ 3), bytes[i]);
        }
    }
}





