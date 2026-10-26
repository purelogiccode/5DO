using System;
using ThreeDo.Core;
using ThreeDo.Video.OpenGL;

namespace ThreeDo.Tests.Video;

public sealed class HqxFilterTests
{
    [Fact]
    public void Expand_MatchesTheReferenceHq2xHash()
    {
        var source = CreateReferenceSource(32, 24);
        var destination = new FrameBuffer(64, 48);

        new HqxFilter().Expand(source, destination);

        var hash = 2166136261u;
        foreach (var value in destination.PixelSpan)
        {
            hash = (hash ^ value) * 16777619u;
        }

        // Generated with hq2x_32_rb from the 4DO reference sources and the same
        // 32x24 image. Pure white (0xFFFFFF) is deliberately excluded because
        // the reference's RGBtoYUV table loop stops before the last entry.
        Assert.Equal(0xA400C421u, hash);
    }

    [Fact]
    public void Expand_LeavesAUniformImageUnchanged()
    {
        var source = new FrameBuffer(4, 4);
        for (var i = 0; i < source.PixelSpan.Length; i += 4)
        {
            source.PixelSpan[i] = 0x20;
            source.PixelSpan[i + 1] = 0x80;
            source.PixelSpan[i + 2] = 0xE0;
            source.PixelSpan[i + 3] = 0xFF;
        }

        var destination = new FrameBuffer(8, 8);
        new HqxFilter().Expand(source, destination);

        for (var i = 0; i < destination.PixelSpan.Length; i += 4)
        {
            Assert.Equal(0x20, destination.PixelSpan[i]);
            Assert.Equal(0x80, destination.PixelSpan[i + 1]);
            Assert.Equal(0xE0, destination.PixelSpan[i + 2]);
            Assert.Equal(0xFF, destination.PixelSpan[i + 3]);
        }
    }

    [Fact]
    public void Expand_RejectsAWrongDestinationSize()
    {
        var source = new FrameBuffer(4, 4);
        var destination = new FrameBuffer(4, 4);

        Assert.Throws<ArgumentException>(() => new HqxFilter().Expand(source, destination));
    }

    [Fact]
    public void Scale_IsTwo()
    {
        Assert.Equal(2, HqxFilter.Scale);
    }

    private static FrameBuffer CreateReferenceSource(int width, int height)
    {
        var source = new FrameBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                uint r = (uint)((x * 7 + y * 3) & 0xFF);
                uint g = (uint)(((x / 3) * 40 + (y / 2) * 20) & 0xFF);
                uint b = (uint)((x ^ y) * 11 & 0xFF);
                if ((x + y) % 5 == 0)
                {
                    r = 254;
                    g = 254;
                    b = 254;
                }

                if ((x * y) % 7 == 0)
                {
                    r = 0;
                    g = 0;
                    b = 0;
                }

                if (x == 10 && y > 4 && y < 18)
                {
                    r = 200;
                    g = 20;
                    b = 20;
                }

                if (y == 12 && x > 3 && x < 28)
                {
                    r = 20;
                    g = 200;
                    b = 20;
                }

                var offset = (y * source.Stride) + (x * 4);
                source.PixelSpan[offset] = (byte)b;
                source.PixelSpan[offset + 1] = (byte)g;
                source.PixelSpan[offset + 2] = (byte)r;
                source.PixelSpan[offset + 3] = 0xFF;
            }
        }

        return source;
    }
}
