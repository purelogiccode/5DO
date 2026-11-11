using System;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class MmpxFilterTests
{
    [Fact]
    public void Expand_LeavesAUniformImageUnchanged()
    {
        var source = new FrameBuffer(4, 4);
        Fill(source, 0x20, 0x80, 0xE0);

        var destination = new FrameBuffer(8, 8);
        new MmpxFilter().Expand(source, destination);

        for (var i = 0; i < destination.PixelSpan.Length; i += 4)
        {
            Assert.Equal(0x20, destination.PixelSpan[i]);
            Assert.Equal(0x80, destination.PixelSpan[i + 1]);
            Assert.Equal(0xE0, destination.PixelSpan[i + 2]);
            Assert.Equal(0xFF, destination.PixelSpan[i + 3]);
        }
    }

    [Fact]
    public void Expand_PreservesASinglePixelFeature()
    {
        var source = new FrameBuffer(3, 3);
        Fill(source, 0, 0, 0);
        SetPixel(source, 1, 1, 0xE0, 0x20, 0x20);

        var destination = new FrameBuffer(6, 6);
        new MmpxFilter().Expand(source, destination);

        // The centre source pixel owns the 2x2 block at (2,2)-(3,3). With no
        // neighbouring feature the MMPX rules leave it unchanged.
        for (var y = 2; y < 4; y++)
        {
            for (var x = 2; x < 4; x++)
            {
                var offset = (y * destination.Stride) + (x * 4);
                Assert.Equal(0xE0, destination.PixelSpan[offset]);
                Assert.Equal(0x20, destination.PixelSpan[offset + 1]);
                Assert.Equal(0x20, destination.PixelSpan[offset + 2]);
            }
        }

        // The surrounding pixels stay background.
        Assert.Equal(0, destination.PixelSpan[0]);
        Assert.Equal(0, destination.PixelSpan[1]);
        Assert.Equal(0, destination.PixelSpan[2]);
    }

    [Fact]
    public void Expand_RejectsAWrongDestinationSize()
    {
        var source = new FrameBuffer(4, 4);
        var destination = new FrameBuffer(4, 4);

        Assert.Throws<ArgumentException>(() => new MmpxFilter().Expand(source, destination));
    }

    [Theory]
    [InlineData(VideoFilter.Mmpx, 2)]
    [InlineData(VideoFilter.SharpBilinear, 1)]
    [InlineData(VideoFilter.Hqx, 1)]
    public void ScaleFor_MapsMmpxFilter(VideoFilter filter, int expected)
    {
        Assert.Equal(expected, MmpxFilter.ScaleFor(filter));
    }

    private static void Fill(FrameBuffer frame, byte b, byte g, byte r)
    {
        for (var i = 0; i < frame.PixelSpan.Length; i += 4)
        {
            frame.PixelSpan[i] = b;
            frame.PixelSpan[i + 1] = g;
            frame.PixelSpan[i + 2] = r;
            frame.PixelSpan[i + 3] = 0xFF;
        }
    }

    private static void SetPixel(FrameBuffer frame, int x, int y, byte b, byte g, byte r)
    {
        var offset = (y * frame.Stride) + (x * 4);
        frame.PixelSpan[offset] = b;
        frame.PixelSpan[offset + 1] = g;
        frame.PixelSpan[offset + 2] = r;
        frame.PixelSpan[offset + 3] = 0xFF;
    }
}
