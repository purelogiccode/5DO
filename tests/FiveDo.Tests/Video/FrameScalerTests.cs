using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class FrameScalerTests
{
    [Fact]
    public void Bicubic_PreservesAUniformFrame()
    {
        var source = new FrameBuffer(2, 2);
        Fill(source, 0x40, 0x80, 0xC0);

        var destination = new FrameBuffer(8, 8);
        FrameScaler.Scale(
            source,
            destination,
            new RendererSettings { Aspect = AspectMode.Stretch, Filter = VideoFilter.Bicubic });

        for (var i = 0; i < destination.PixelSpan.Length; i += 4)
        {
            Assert.Equal(0x40, destination.PixelSpan[i]);
            Assert.Equal(0x80, destination.PixelSpan[i + 1]);
            Assert.Equal(0xC0, destination.PixelSpan[i + 2]);
            Assert.Equal(0xFF, destination.PixelSpan[i + 3]);
        }
    }

    [Fact]
    public void Bicubic_InterpolatesBetweenSourcePixels()
    {
        var source = new FrameBuffer(2, 2);
        Fill(source, 0, 0, 0);
        SetPixel(source, 1, 0, 0xFF, 0xFF, 0xFF);
        SetPixel(source, 1, 1, 0xFF, 0xFF, 0xFF);

        var destination = new FrameBuffer(8, 8);
        FrameScaler.Scale(
            source,
            destination,
            new RendererSettings { Aspect = AspectMode.Stretch, Filter = VideoFilter.Bicubic });

        var foundIntermediate = false;
        for (var i = 0; i < destination.PixelSpan.Length; i += 4)
        {
            var value = destination.PixelSpan[i];
            if (value is > 0 and < 0xFF)
            {
                foundIntermediate = true;
                break;
            }
        }

        Assert.True(foundIntermediate, "Bicubic should produce an intermediate value across an edge.");
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
