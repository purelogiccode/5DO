using System;
using FiveDo.Core;
using FiveDo.Frontend.Avalonia;
using FiveDo.Video;
using FiveDo.Video.Direct3D;

namespace FiveDo.Tests.Video;

public sealed class Direct3D11RendererTests
{
    [Fact]
    public void Renderer_MatchesCpuAndHandlesSourceSizeChanges()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_D3D_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var renderer = new Direct3D11Renderer();
        renderer.Settings = new RendererSettings
        {
            Filter = VideoFilter.Accurate,
            Aspect = AspectMode.Stretch,
            IntegerScale = false,
            Vsync = false,
        };

        renderer.Initialize(320, 240);
        VdlFrame lowResolution = CreateFrame(320, 240, 0);
        renderer.Present(lowResolution);
        AssertParity(renderer, lowResolution, 320, 240);

        renderer.Resize(640, 480);
        VdlFrame highResolution = CreateFrame(640, 480, 3);
        renderer.Present(highResolution);
        AssertParity(renderer, highResolution, 640, 480);
    }

    [Fact]
    public void Renderer_LetterboxesWithBlackBars()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_D3D_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var renderer = new Direct3D11Renderer();
        renderer.Settings = new RendererSettings
        {
            Filter = VideoFilter.Accurate,
            Aspect = AspectMode.FourThree,
            IntegerScale = false,
            Vsync = false,
        };
        renderer.Initialize(640, 360);
        renderer.Present(CreateSplitFrame());

        var gpu = new byte[640 * 360 * 4];
        renderer.ReadPixels(gpu, out _, out _);
        var bar = ((180 * 640) + 10) * 4;
        Assert.Equal(0, gpu[bar]);
        Assert.Equal(0, gpu[bar + 1]);
        Assert.Equal(0, gpu[bar + 2]);

        var center = ((180 * 640) + 320) * 4;
        Assert.True(
            gpu[center] > 200 && gpu[center + 2] < 50,
            $"The centered viewport was not blue: B={gpu[center]} G={gpu[center + 1]} R={gpu[center + 2]}.");
    }

    [Fact]
    public void XbrFsrAndHqx_RenderWithoutFallbackAndPreserveTopDownOrientation()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_D3D_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var renderer = new Direct3D11Renderer();
        renderer.Settings = new RendererSettings
        {
            Filter = VideoFilter.Xbr,
            Aspect = AspectMode.Stretch,
            IntegerScale = false,
            Vsync = false,
        };

        renderer.Initialize(320, 240);
        VdlFrame split = CreateSplitFrame();
        renderer.Present(split);
        Assert.False(renderer.FilterFallback);
        AssertSplitOrientation(renderer, 320, 240);

        renderer.Resize(640, 480);
        renderer.Present(split);
        Assert.False(renderer.FilterFallback);
        AssertSplitOrientation(renderer, 640, 480);

        renderer.Settings = new RendererSettings
        {
            Filter = VideoFilter.Fsr,
            Aspect = AspectMode.Stretch,
            IntegerScale = false,
            Vsync = false,
        };
        renderer.Present(split);
        Assert.False(renderer.FilterFallback);
        AssertSplitOrientation(renderer, 640, 480);

        foreach (var hqx in new[] { VideoFilter.Hqx, VideoFilter.Hqx3x, VideoFilter.Hqx4x })
        {
            renderer.Settings = new RendererSettings
            {
                Filter = hqx,
                Aspect = AspectMode.Stretch,
                IntegerScale = false,
                Vsync = false,
            };
            renderer.Present(split);
            Assert.False(renderer.FilterFallback);
            AssertSplitOrientation(renderer, 640, 480);
        }
    }

    [Fact]
    public void Hqx_SyntheticProbeFrame_MatchesCpuExpansion()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_D3D_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var renderer = new Direct3D11Renderer();
        renderer.Settings = new RendererSettings
        {
            Filter = VideoFilter.Hqx,
            Aspect = AspectMode.Stretch,
            IntegerScale = false,
            Vsync = false,
        };

        renderer.Initialize(640, 480);
        var frame = new VdlFrame();
        SyntheticVdlFrame.Initialize(frame);
        for (var phase = 0; phase < 4; phase++)
        {
            SyntheticVdlFrame.Fill(frame, phase);
            renderer.Present(frame);
        }

        Assert.False(renderer.FilterFallback);

        var gpu = new byte[640 * 480 * 4];
        renderer.ReadPixels(gpu, out _, out _);

        var source = new FrameBuffer(320, 240);
        FrameAssembler.Assemble(frame, source, 320, 240);
        var expected = new FrameBuffer(640, 480);
        new HqxFilter().Expand(source, expected);

        var different = 0;
        for (var i = 0; i < expected.PixelSpan.Length; i += 4)
        {
            if (gpu[i] != expected.PixelSpan[i]
                || gpu[i + 1] != expected.PixelSpan[i + 1]
                || gpu[i + 2] != expected.PixelSpan[i + 2])
            {
                different++;
            }
        }

        Assert.Equal(0, different);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Hqx_MatchesCpuExpansion(int scale)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_D3D_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var renderer = new Direct3D11Renderer();
        renderer.Settings = new RendererSettings
        {
            Filter = scale switch
            {
                2 => VideoFilter.Hqx,
                3 => VideoFilter.Hqx3x,
                _ => VideoFilter.Hqx4x,
            },
            Aspect = AspectMode.Stretch,
            IntegerScale = false,
            Vsync = false,
        };

        var width = 320 * scale;
        var height = 240 * scale;
        renderer.Initialize(width, height);
        VdlFrame frame = CreateFrame(320, 240, 0);
        renderer.Present(frame);
        Assert.False(renderer.FilterFallback);

        var gpu = new byte[width * height * 4];
        renderer.ReadPixels(gpu, out var readWidth, out var readHeight);
        Assert.Equal(width, readWidth);
        Assert.Equal(height, readHeight);

        var source = new FrameBuffer(320, 240);
        FrameAssembler.Assemble(frame, source, 320, 240);
        var expected = new FrameBuffer(width, height);
        new HqxFilter().Expand(source, expected);

        var different = 0;
        for (var i = 0; i < expected.PixelSpan.Length; i += 4)
        {
            if (gpu[i] != expected.PixelSpan[i]
                || gpu[i + 1] != expected.PixelSpan[i + 1]
                || gpu[i + 2] != expected.PixelSpan[i + 2])
            {
                different++;
            }
        }

        Assert.Equal(0, different);
    }

    [Fact]
    public void Mmpx_MatchesCpuExpansion()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_D3D_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var renderer = new Direct3D11Renderer();
        renderer.Settings = new RendererSettings
        {
            Filter = VideoFilter.Mmpx,
            Aspect = AspectMode.Stretch,
            IntegerScale = false,
            Vsync = false,
        };

        renderer.Initialize(640, 480);
        VdlFrame frame = CreateFrame(320, 240, 0);
        renderer.Present(frame);
        Assert.False(renderer.FilterFallback);

        var gpu = new byte[640 * 480 * 4];
        renderer.ReadPixels(gpu, out var readWidth, out var readHeight);
        Assert.Equal(640, readWidth);
        Assert.Equal(480, readHeight);

        var source = new FrameBuffer(320, 240);
        FrameAssembler.Assemble(frame, source, 320, 240);
        var expected = new FrameBuffer(640, 480);
        new MmpxFilter().Expand(source, expected);

        var different = 0;
        for (var i = 0; i < expected.PixelSpan.Length; i += 4)
        {
            if (gpu[i] != expected.PixelSpan[i]
                || gpu[i + 1] != expected.PixelSpan[i + 1]
                || gpu[i + 2] != expected.PixelSpan[i + 2])
            {
                different++;
            }
        }

        Assert.Equal(0, different);
    }

    private static VdlFrame CreateSplitFrame()
    {
        var frame = new VdlFrame
        {
            SourceWidth = 320,
            SourceHeight = 240,
        };
        for (var y = 0; y < 240; y++)
        {
            VdlLine line = frame[y];
            line.Width = 320;
            line.OutControl = 0;
            // The background register is 24-bit RGB: red in the top half, blue in the bottom.
            line.Background = y < 120 ? 0x00FF0000u : 0x000000FFu;
            line.Pixels.Clear();
        }

        return frame;
    }

    private static void AssertSplitOrientation(
        Direct3D11Renderer renderer,
        int width,
        int height)
    {
        var gpu = new byte[width * height * 4];
        renderer.ReadPixels(gpu, out var readWidth, out var readHeight);
        Assert.Equal(width, readWidth);
        Assert.Equal(height, readHeight);

        var top = (((height / 4) * width) + (width / 2)) * 4;
        var bottom = ((((3 * height) / 4) * width) + (width / 2)) * 4;
        Assert.True(
            gpu[top + 2] > 200 && gpu[top] < 50,
            $"The top half was not red: B={gpu[top]} G={gpu[top + 1]} R={gpu[top + 2]}.");
        Assert.True(
            gpu[bottom] > 200 && gpu[bottom + 2] < 50,
            $"The bottom half was not blue: B={gpu[bottom]} G={gpu[bottom + 1]} R={gpu[bottom + 2]}.");
    }

    private static VdlFrame CreateFrame(int width, int height, int phase)
    {
        var frame = new VdlFrame
        {
            SourceWidth = width,
            SourceHeight = height,
        };
        for (var y = 0; y < height; y++)
        {
            VdlLine line = frame[y];
            line.Width = width;
            line.OutControl = (y & 1) == 0 ? 0x02000000u : 0u;
            line.Background = (uint)(((y & 0x1f) << 10) | (((y * 3) & 0x1f) << 5) | (y & 0x1f));
            for (var i = 0; i < VdlLine.ClutSize; i++)
            {
                line.ClutR[i] = (byte)((i * 11 + y) & 0xff);
                line.ClutG[i] = (byte)((i * 7 + (y * 2)) & 0xff);
                line.ClutB[i] = (byte)((i * 5 + (y * 3)) & 0xff);
            }

            Span<ushort> pixels = line.Pixels;
            for (var x = 0; x < width; x++)
            {
                var index = (x + y + phase) & 0x1f;
                var fixedClut = (x & 1) == 0 ? 0x8000 : 0;
                pixels[x] = (ushort)((x & 0x1f)
                    | (((y + phase) & 0x1f) << 5)
                    | (index << 10)
                    | fixedClut);
            }
        }

        return frame;
    }

    private static void AssertParity(
        Direct3D11Renderer renderer,
        VdlFrame frame,
        int width,
        int height)
    {
        var gpu = new byte[width * height * 4];
        renderer.ReadPixels(gpu, out var readWidth, out var readHeight);
        Assert.Equal(width, readWidth);
        Assert.Equal(height, readHeight);

        var cpu = new FrameBuffer(width, height);
        FrameAssembler.Assemble(frame, cpu, width, height);
        Span<byte> expected = cpu.PixelSpan;
        var different = 0;
        for (var i = 0; i < expected.Length; i += 4)
        {
            if (gpu[i] != expected[i]
                || gpu[i + 1] != expected[i + 1]
                || gpu[i + 2] != expected[i + 2])
            {
                different++;
            }
        }

        Assert.Equal(0, different);
    }
}

