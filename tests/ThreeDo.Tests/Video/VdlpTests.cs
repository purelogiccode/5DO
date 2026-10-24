using ThreeDo.Core;
using ThreeDo.Memory;
using ThreeDo.Video;

namespace ThreeDo.Tests.Video;

public sealed class VdlpTests
{
    private const uint VramOffset = 2 * 1024 * 1024;

    [Fact]
    public void Reset_WritesStartupVdl()
    {
        var memory = new ThreeDoMemory();
        var vdlp = new Vdlp(memory.Ram);

        Assert.Equal(0x00004410u, memory.Ram.Read32(0xb0000 + VramOffset));
        Assert.Equal(0xb0000u, vdlp.HeadVdl);
        Assert.Equal(320u, vdlp.Modulo);
    }

    [Fact]
    public void ProcessVdl_SetsHead()
    {
        var memory = new ThreeDoMemory();
        var vdlp = new Vdlp(memory.Ram);

        vdlp.ProcessVdl(0x12345);

        Assert.Equal(0x12345u, vdlp.HeadVdl);
    }

    [Fact]
    public void DoLine_CopiesPixelsClutBackgroundAndControl()
    {
        var memory = new ThreeDoMemory();
        var vdlp = new Vdlp(memory.Ram);
        var frame = new VdlFrame();
        const uint head = 0x1000;
        const uint bitmap = 0x8000;
        var header = 1u | (3u << 9) | (1u << 15) | (1u << 16) | (1u << 21);

        WriteVdl(memory, head, header, bitmap, bitmap, 0);
        WriteVdl(memory, head + 16, 0x01112233, 0xe0010203, 0xc001002c);
        for (var i = 0; i < 320; i++)
        {
            memory.Ram.Write16(VramOffset + bitmap + ((uint)i * 2), (ushort)(i % 32));
        }

        vdlp.ProcessVdl(head);
        vdlp.DoLine(0, frame);
        vdlp.DoLine(16, frame);

        Assert.Equal(0, frame[0].Pixels[0]);
        Assert.Equal(1, frame[0].Pixels[1]);
        Assert.Equal(31, frame[0].Pixels[31]);
        Assert.Equal(0x11, frame[0].ClutR[1]);
        Assert.Equal(0x22, frame[0].ClutG[1]);
        Assert.Equal(0x33, frame[0].ClutB[1]);
        Assert.Equal(0x030201u, frame[0].Background);
        Assert.Equal(0xc001002cu, frame[0].OutControl);
        Assert.Equal(header, frame[0].ClutDma);
        Assert.Equal(bitmap + 2 + (320u * 4) - 2, vdlp.CurrentBitmap);
        Assert.Equal(bitmap + 2 + (320u * 4) - 2, vdlp.PreviousBitmap);
    }

    [Fact]
    public void DoLine_Line255CopiesAndLine256DoesNot()
    {
        var memory = new ThreeDoMemory();
        var vdlp = new Vdlp(memory.Ram);
        var frame = new VdlFrame();
        const uint head = 0x2000;
        const uint bitmap = 0x9000;
        var header = 300u | (1u << 15) | (1u << 16) | (1u << 21);

        WriteVdl(memory, head, header, bitmap, bitmap, 0);
        for (var i = 0; i < 320; i++)
        {
            memory.Ram.Write16(VramOffset + bitmap + ((uint)i * 2), 7);
        }

        vdlp.ProcessVdl(head);
        vdlp.DoLine(0, frame);
        vdlp.DoLine(255, frame);
        Assert.Equal(7, frame[239].Pixels[0]);

        vdlp.DoLine(256, frame);
        Assert.Equal(7, frame[239].Pixels[1]);
    }

    [Fact]
    public void DoLine_Modulo384UsesBitmapStride()
    {
        var memory = new ThreeDoMemory();
        var vdlp = new Vdlp(memory.Ram);
        var frame = new VdlFrame();
        const uint head = 0x3000;
        const uint bitmap = 0x8000;
        var header = 1u | (1u << 15) | (1u << 16) | (1u << 21) | (1u << 23);
        var storage = bitmap + VramOffset;

        WriteVdl(memory, head, header, bitmap, bitmap, 0);
        for (var i = 0; i < 384; i++)
        {
            memory.Ram.Write16(storage + ((uint)i * 2), (ushort)(i & 0x1f));
        }

        vdlp.ProcessVdl(head);
        vdlp.DoLine(0, frame);
        vdlp.DoLine(16, frame);

        Assert.Equal(384, frame[0].Width);
        Assert.Equal(383 & 0x1f, frame[0].Pixels[383]);
        Assert.Equal(0, frame[0].PixelBuffer[384]);
        Assert.Equal(bitmap + (384u * 4), vdlp.CurrentBitmap);
    }

    [Fact]
    public void ExecuteVdl_FixedClutCommandFillsGrayscale()
    {
        var memory = new ThreeDoMemory();
        var vdlp = new Vdlp(memory.Ram);
        var frame = new VdlFrame();
        const uint head = 0x4000;
        const uint bitmap = 0x8000;
        var header = 1u | (1u << 9) | (1u << 15) | (1u << 16) | (1u << 21);

        WriteVdl(memory, head, header, bitmap, bitmap, 0);
        WriteVdl(memory, head + 16, 0xffffffff);

        vdlp.ProcessVdl(head);
        vdlp.DoLine(0, frame);
        vdlp.DoLine(16, frame);

        Assert.Equal(0, frame[0].ClutR[0]);
        Assert.Equal(8, frame[0].ClutR[1]);
        Assert.Equal(255, frame[0].ClutR[31]);
    }

    [Fact]
    public void SaveLoad_RoundtripsState()
    {
        var memory = new ThreeDoMemory();
        var vdlp = new Vdlp(memory.Ram);
        vdlp.ProcessVdl(0x23456);

        var writer = new StateWriter();
        vdlp.Save(writer);
        var copy = new Vdlp(memory.Ram);
        copy.Load(new StateReader(writer.ToArray()));

        Assert.Equal(vdlp.HeadVdl, copy.HeadVdl);
        Assert.Equal(vdlp.Modulo, copy.Modulo);
        Assert.Equal(vdlp.CurrentVdl, copy.CurrentVdl);
    }

    private static void WriteVdl(ThreeDoMemory memory, uint address, params uint[] words)
    {
        for (var i = 0; i < words.Length; i++)
        {
            memory.Ram.Write32(address + ((uint)i * 4) + VramOffset, words[i]);
        }
    }
}
