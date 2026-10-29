using System;
using FiveDo.Core;
using FiveDo.Video;
using FiveDo.Video.Vulkan;

namespace FiveDo.Tests.Video;

public sealed class VulkanRendererTests
{
    [Fact]
    public void Renderer_MatchesCpuAndHandlesSourceSizeChanges()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_VULKAN_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var renderer = new VulkanOffscreenRenderer
        {
            Settings = new RendererSettings
            {
                Filter = VideoFilter.Accurate,
                Aspect = AspectMode.Stretch,
                IntegerScale = false,
                Vsync = false,
            },
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
            line.OutControl = (uint)((y & 1) == 0 ? 0x02000000u : 0u);
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
        VulkanOffscreenRenderer renderer,
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
