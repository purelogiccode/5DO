using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class FrameAssemblerTests
{
    [Fact]
    public void Assemble_PixelZeroUsesExpandedBackground()
    {
        var frame = new VdlFrame();
        frame[0].Background = 0x00ffffff;
        var destination = new FrameBuffer(1, 1);

        FrameAssembler.Assemble(frame, destination, 1, 1);

        Assert.Equal(255, destination.PixelSpan[0]);
        Assert.Equal(255, destination.PixelSpan[1]);
        Assert.Equal(255, destination.PixelSpan[2]);
        Assert.Equal(255, destination.PixelSpan[3]);
    }

    [Fact]
    public void Assemble_PixelZeroKeepsAllEightBackgroundBits()
    {
        var frame = new VdlFrame();
        frame[0].Background = 0x00010203;
        var destination = new FrameBuffer(1, 1);

        FrameAssembler.Assemble(frame, destination, 1, 1);

        Assert.Equal(3, destination.PixelSpan[0]);
        Assert.Equal(2, destination.PixelSpan[1]);
        Assert.Equal(1, destination.PixelSpan[2]);
        Assert.Equal(255, destination.PixelSpan[3]);
    }

    [Fact]
    public void Assemble_NonZeroPixelUsesPerLineClut()
    {
        var frame = new VdlFrame();
        frame[0].PixelBuffer[0] = 1 | (1 << 5) | (1 << 10);
        frame[0].ClutB[1] = 10;
        frame[0].ClutG[1] = 20;
        frame[0].ClutR[1] = 30;
        var destination = new FrameBuffer(1, 1);

        FrameAssembler.Assemble(frame, destination, 1, 1);

        Assert.Equal(10, destination.PixelSpan[0]);
        Assert.Equal(20, destination.PixelSpan[1]);
        Assert.Equal(30, destination.PixelSpan[2]);
        Assert.Equal(255, destination.PixelSpan[3]);
    }

    [Fact]
    public void Assemble_FixedClutUsesPixelBit15AndOutControl()
    {
        var frame = new VdlFrame();
        frame[0].OutControl = 0x02000000;
        frame[0].PixelBuffer[0] = 0x8000 | 1 | (1 << 5) | (1 << 10);
        frame[0].ClutB[1] = 99;
        var destination = new FrameBuffer(1, 1);

        FrameAssembler.Assemble(frame, destination, 1, 1);

        Assert.Equal(8, destination.PixelSpan[0]);
        Assert.Equal(8, destination.PixelSpan[1]);
        Assert.Equal(8, destination.PixelSpan[2]);
    }

    [Fact]
    public void Assemble_FixedClutRequiresPixelBit15()
    {
        var frame = new VdlFrame();
        frame[0].OutControl = 0x02000000;
        frame[0].PixelBuffer[0] = 1 | (1 << 5) | (1 << 10);
        frame[0].ClutB[1] = 77;
        var destination = new FrameBuffer(1, 1);

        FrameAssembler.Assemble(frame, destination, 1, 1);

        Assert.Equal(77, destination.PixelSpan[0]);
    }

    [Fact]
    public void Assemble_RgbaFormatWritesRedFirst()
    {
        var frame = new VdlFrame();
        frame[0].PixelBuffer[0] = 1 | (1 << 5) | (1 << 10);
        frame[0].ClutB[1] = 10;
        frame[0].ClutG[1] = 20;
        frame[0].ClutR[1] = 30;
        var destination = new FrameBuffer(1, 1, PixelFormat.Rgba8888);

        FrameAssembler.Assemble(frame, destination, 1, 1);

        Assert.Equal(30, destination.PixelSpan[0]);
        Assert.Equal(20, destination.PixelSpan[1]);
        Assert.Equal(10, destination.PixelSpan[2]);
    }

    [Fact]
    public void Assemble1555_PacksBackgroundAndClutPixels()
    {
        var frame = CreateTwoPixelFrame();
        var destination = new ushort[2];

        FrameAssembler.Assemble1555(frame, destination, 2, 2, 1);

        Assert.Equal(0x0886, destination[0]);
        Assert.Equal(0x0c41, destination[1]);
    }

    [Fact]
    public void Assemble565_PacksBackgroundAndClutPixels()
    {
        var frame = CreateTwoPixelFrame();
        var destination = new ushort[2];

        FrameAssembler.Assemble565(frame, destination, 2, 2, 1);

        Assert.Equal(0x1106, destination[0]);
        Assert.Equal(0x18a1, destination[1]);
    }

    [Fact]
    public void AssembleXrgb8888_PacksBackgroundAndClutPixels()
    {
        var frame = CreateTwoPixelFrame();
        var destination = new uint[2];

        FrameAssembler.AssembleXrgb8888(frame, destination, 2, 2, 1);

        Assert.Equal(0xff102030u, destination[0]);
        Assert.Equal(0xff1e140au, destination[1]);
    }

    private static VdlFrame CreateTwoPixelFrame()
    {
        var frame = new VdlFrame();
        frame[0].Background = 0x00102030;
        frame[0].PixelBuffer[0] = 0;
        frame[0].PixelBuffer[1] = 1 | (1 << 5) | (1 << 10);
        frame[0].ClutB[1] = 10;
        frame[0].ClutG[1] = 20;
        frame[0].ClutR[1] = 30;
        return frame;
    }

    [Fact]
    public void Expand5_ReplicatesBits()
    {
        Assert.Equal(0, FrameAssembler.Expand5(0));
        Assert.Equal(8, FrameAssembler.Expand5(1));
        Assert.Equal(255, FrameAssembler.Expand5(31));
        Assert.Equal(0x84, FrameAssembler.Expand5(16));
    }
}
