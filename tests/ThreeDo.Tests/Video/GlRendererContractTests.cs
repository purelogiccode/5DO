using System;
using ThreeDo.Core;
using ThreeDo.Video;

namespace ThreeDo.Tests.VideoRendering;

public sealed class GlRendererContractTests
{
    [Fact]
    public void GpuPalette_MatchesCpuFrameAssembler()
    {
        var frame = new VdlFrame { SourceWidth = 320, SourceHeight = 240 };
        for (var line = 0; line < 240; line++)
        {
            var target = frame[line];
            target.Width = 320;
            target.Background = (uint)(((line % 32) << 10) | ((line % 32) << 5) | (line % 32));
            target.OutControl = line % 2 == 0 ? 0x02000000u : 0u;
            for (var i = 0; i < 32; i++)
            {
                target.ClutR[i] = (byte)(i * 7);
                target.ClutG[i] = (byte)(i * 5);
                target.ClutB[i] = (byte)(i * 3);
            }

            var pixels = target.Pixels;
            for (var x = 0; x < pixels.Length; x++)
            {
                pixels[x] = (ushort)((x + line) switch
                {
                    0 => 0,
                    _ when x % 17 == 0 => (ushort)(0x8000 | ((x * 3) & 0x7fff)),
                    _ => (ushort)((x * 31) & 0x7fff),
                });
            }
        }

        var buffer = new FrameBuffer(320, 240);
        FrameAssembler.Assemble(frame, buffer, 320, 240);

        for (var line = 0; line < 240; line++)
        {
            var source = frame[line];
            for (var x = 0; x < 320; x++)
            {
                var expected = GpuPalette.Expand(source, source.Pixels[x]);
                var offset = (line * buffer.Stride) + (x * 4);
                Assert.Equal(expected.B, buffer.PixelSpan[offset]);
                Assert.Equal(expected.G, buffer.PixelSpan[offset + 1]);
                Assert.Equal(expected.R, buffer.PixelSpan[offset + 2]);
            }
        }
    }

    [Fact]
    public void Uploader_PacksPixelsClutAndControlWords()
    {
        var frame = new VdlFrame { SourceWidth = 4, SourceHeight = 2 };
        var line0 = frame[0];
        line0.Width = 4;
        line0.Pixels[0] = 1;
        line0.Pixels[1] = 2;
        line0.Pixels[2] = 3;
        line0.Pixels[3] = 4;
        line0.Background = 0x1234;
        line0.OutControl = 0x02000000;
        line0.ClutR[0] = 10;
        line0.ClutG[0] = 20;
        line0.ClutB[0] = 30;
        var line1 = frame[1];
        line1.Width = 4;
        line1.Pixels[0] = 5;
        line1.Background = 0x5678;
        line1.ClutR[1] = 40;

        var uploader = new VdlFrameUploader();
        uploader.Update(frame);

        Assert.Equal(4, uploader.Width);
        Assert.Equal(2, uploader.Height);
        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5, 0, 0, 0 }, uploader.Pixels.ToArray());
        Assert.Equal(10, uploader.Clut[0]);
        Assert.Equal(20, uploader.Clut[1]);
        Assert.Equal(30, uploader.Clut[2]);
        Assert.Equal(40, uploader.Clut[(32 * 3) + 3]);
        Assert.Equal(0x1234u, uploader.LineParams[0]);
        Assert.Equal(0x02000000u, uploader.LineParams[1]);
        Assert.Equal(0x5678u, uploader.LineParams[2]);
    }

    [Fact]
    public void Viewport_IntegerScalingCentersImage()
    {
        var settings = new RendererSettings { Aspect = AspectMode.Integer };
        var rect = ViewportCalculator.Compute(320, 240, 1920, 1080, settings);

        Assert.Equal(new ViewportRect(320, 60, 1280, 960), rect);
    }

    [Fact]
    public void Viewport_StretchFillsOutput()
    {
        var settings = new RendererSettings { Aspect = AspectMode.Stretch };
        Assert.Equal(new ViewportRect(0, 0, 1000, 700), ViewportCalculator.Compute(320, 240, 1000, 700, settings));
    }

    [Fact]
    public void Viewport_CustomAspectLetterboxes()
    {
        var settings = new RendererSettings { Aspect = AspectMode.Custom, CustomAspect = 16.0 / 9.0 };
        var rect = ViewportCalculator.Compute(320, 240, 1920, 1080, settings);

        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(1920, rect.Width);
        Assert.Equal(1080, rect.Height);
    }

    [Fact]
    public void RendererSettings_RejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RendererSettings { CustomAspect = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new RendererSettings { ScanlineStrength = 2 }.Validate());
        new RendererSettings().Validate();
    }

    [Fact]
    public void CpuRenderer_MatchesFrameAssembler()
    {
        var frame = new VdlFrame { SourceWidth = 8, SourceHeight = 4 };
        for (var line = 0; line < 4; line++)
        {
            var target = frame[line];
            target.Width = 8;
            target.Background = (uint)(line * 3);
            for (var x = 0; x < 8; x++)
            {
                target.Pixels[x] = (ushort)(x * 5);
            }
        }

        using var renderer = new CpuVideoRenderer();
        renderer.Initialize(8, 4);
        renderer.Present(frame);

        var expected = new FrameBuffer(8, 4);
        FrameAssembler.Assemble(frame, expected, 8, 4);
        Assert.Equal(expected.PixelSpan.ToArray(), renderer.Frame.PixelSpan.ToArray());
        Assert.Equal("CPU", renderer.Name);
        Assert.False(renderer.Capabilities.SupportsOpenGl33);
    }
}
