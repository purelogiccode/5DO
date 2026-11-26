using System;
using System.Diagnostics;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class FrameInterpolatorTests
{
    private const int Width = 320;
    private const int Height = 240;

    [Fact]
    public void FirstFrame_PresentsCurrentWithoutAPair()
    {
        var interpolator = new FrameInterpolator();
        VdlFrame frame = MakeFrame(20);

        interpolator.Advance(frame, 1000);

        Assert.True(interpolator.HasCurrent);
        Assert.False(interpolator.HasPair);
        VdlFrame presented = Assert.IsType<VdlFrame>(interpolator.FrameAt(0.5));
        Assert.Equal(20, DecodeRed(presented));
        Assert.Equal(1.0, interpolator.PositionAt(2000));
    }

    [Fact]
    public void Endpoints_ReturnTheRetainedFramesWithoutQuantization()
    {
        var interpolator = new FrameInterpolator();
        interpolator.Advance(MakeFrame(10), 1000);
        interpolator.Advance(MakeFrame(200), 1000 + (Stopwatch.Frequency / 60));

        VdlFrame previous = Assert.IsType<VdlFrame>(interpolator.FrameAt(0));
        VdlFrame current = Assert.IsType<VdlFrame>(interpolator.FrameAt(1));

        Assert.Equal(10, DecodeRed(previous));
        Assert.Equal(200, DecodeRed(current));
        Assert.False(IsDirectColor(previous));
        Assert.False(IsDirectColor(current));
        Assert.False(interpolator.LastFrameBlended);
        Assert.Equal(0, interpolator.BlendedFrames);
    }

    [Theory]
    [InlineData(0.25, 66)]
    [InlineData(0.5, 132)]
    [InlineData(0.75, 189)]
    public void IntermediatePosition_BlendsThroughTheFixedClutPath(double position, int expected)
    {
        var interpolator = new FrameInterpolator();
        interpolator.Advance(MakeFrame(0), 1000);
        interpolator.Advance(MakeFrame(255), 1000 + (Stopwatch.Frequency / 60));

        VdlFrame blended = Assert.IsType<VdlFrame>(interpolator.FrameAt(position));

        Assert.True(IsDirectColor(blended));
        Assert.Equal(expected, DecodeRed(blended));
        Assert.Equal(expected, DecodeGreen(blended));
        Assert.Equal(expected, DecodeBlue(blended));
        Assert.True(interpolator.LastFrameBlended);
        Assert.Equal(1, interpolator.BlendedFrames);
        Assert.Equal(
            FrameAssembler.FixedClutEnable,
            blended[0].OutControl & FrameAssembler.FixedClutEnable);
    }

    [Fact]
    public void Position_AdvancesOverTheMeasuredIntervalAndClamps()
    {
        var interpolator = new FrameInterpolator();
        long first = Stopwatch.Frequency * 10;
        long interval = Stopwatch.Frequency / 60;
        interpolator.Advance(MakeFrame(0), first);
        interpolator.Advance(MakeFrame(1), first + interval);

        Assert.InRange(interpolator.IntervalSeconds, 0.016, 0.017);
        Assert.Equal(0.0, interpolator.PositionAt(first + interval), precision: 3);
        Assert.Equal(0.5, interpolator.PositionAt(first + interval + (interval / 2)), precision: 2);
        Assert.Equal(1.0, interpolator.PositionAt(first + (interval * 2)), precision: 3);
        Assert.Equal(1.0, interpolator.PositionAt(first + (interval * 10)), precision: 3);
    }

    [Fact]
    public void ExplicitInterval_OverridesTheObservedGap()
    {
        var interpolator = new FrameInterpolator();
        long interval = Stopwatch.Frequency / 60;
        interpolator.Advance(MakeFrame(0), 1000, interval);
        interpolator.Advance(MakeFrame(1), 1000 + (Stopwatch.Frequency * 5), interval);

        Assert.InRange(interpolator.IntervalSeconds, 0.016, 0.017);
    }

    [Fact]
    public void IncompatibleFrames_DisableThePair()
    {
        var interpolator = new FrameInterpolator();
        interpolator.Advance(MakeFrame(10, Width, Height), 1000);
        VdlFrame smaller = MakeFrame(20, Width, Height - 20);
        interpolator.Advance(smaller, 1000 + (Stopwatch.Frequency / 60));

        Assert.False(interpolator.HasPair);
        VdlFrame presented = Assert.IsType<VdlFrame>(interpolator.FrameAt(0.5));
        Assert.Equal(20, DecodeRed(presented));
        Assert.Equal(Height - 20, presented.SourceHeight);
    }

    [Fact]
    public void PixelAspectMismatch_DisablesThePair()
    {
        VdlFrame even = MakeFrame(10);
        VdlFrame woven = MakeFrame(20);
        woven.PixelAspect = 0.5;
        var interpolator = new FrameInterpolator();

        interpolator.Advance(even, 1000);
        interpolator.Advance(woven, 1000 + (Stopwatch.Frequency / 60));

        Assert.False(interpolator.HasPair);
    }

    [Fact]
    public void Blend_PreservesCurrentLineState()
    {
        VdlFrame first = MakeFrame(10);
        VdlFrame second = MakeFrame(20);
        first.PixelAspect = 0.5;
        second[0].Background = 0x00112233;
        second[0].OutControl = 0x0c00002c;
        second[0].ClutDma = 0x00ff00ff;
        second.PixelAspect = 0.5;
        second[0].ClutR[7] = 42;
        var interpolator = new FrameInterpolator();
        interpolator.Advance(first, 1000);
        interpolator.Advance(second, 1000 + (Stopwatch.Frequency / 60));

        VdlFrame blended = Assert.IsType<VdlFrame>(interpolator.FrameAt(0.5));

        Assert.Equal(0x00112233u, blended[0].Background);
        Assert.Equal(0x0c00002cu | FrameAssembler.FixedClutEnable, blended[0].OutControl);
        Assert.Equal(0x00ff00ffu, blended[0].ClutDma);
        Assert.Equal(0.5, blended.PixelAspect);
        Assert.Equal(42, blended[0].ClutR[7]);
    }

    [Fact]
    public void Blend_DoesNotMutateTheInputs()
    {
        VdlFrame first = MakeFrame(10);
        VdlFrame second = MakeFrame(200);
        ushort[] firstPixels = first[0].PixelBuffer[..Width].ToArray();
        ushort[] secondPixels = second[0].PixelBuffer[..Width].ToArray();
        var interpolator = new FrameInterpolator();
        interpolator.Advance(first, 1000);
        interpolator.Advance(second, 1000 + (Stopwatch.Frequency / 60));

        _ = interpolator.FrameAt(0.5);

        Assert.Equal(firstPixels, first[0].PixelBuffer[..Width].ToArray());
        Assert.Equal(secondPixels, second[0].PixelBuffer[..Width].ToArray());
    }

    [Fact]
    public void Reset_DropsTheRetainedFrames()
    {
        var interpolator = new FrameInterpolator();
        interpolator.Advance(MakeFrame(10), 1000);
        interpolator.Advance(MakeFrame(20), 1000 + (Stopwatch.Frequency / 60));

        interpolator.Reset();

        Assert.False(interpolator.HasCurrent);
        Assert.False(interpolator.HasPair);
        Assert.Null(interpolator.FrameAt(0.5));
    }

    [Fact]
    public void CpuRenderer_PresentsABlendedFrame()
    {
        var interpolator = new FrameInterpolator();
        interpolator.Advance(MakeFrame(0), 1000);
        interpolator.Advance(MakeFrame(255), 1000 + (Stopwatch.Frequency / 60));
        VdlFrame blended = Assert.IsType<VdlFrame>(interpolator.FrameAt(0.5));

        using var renderer = new CpuVideoRenderer
        {
            Settings = new RendererSettings { Filter = VideoFilter.Nearest, IntegerScale = true },
        };
        renderer.Initialize(Width, Height);
        renderer.Present(blended);

        FrameBuffer frame = renderer.Frame;
        var offset = ((Height / 2) * frame.Stride) + ((Width / 2) * 4);
        Assert.Equal(132, frame.PixelSpan[offset + 2]);
        Assert.Equal(132, frame.PixelSpan[offset + 1]);
        Assert.Equal(132, frame.PixelSpan[offset]);
        Assert.Equal(0xff, frame.PixelSpan[offset + 3]);
    }

    private static VdlFrame MakeFrame(byte gray, int width = Width, int height = Height)
    {
        var frame = new VdlFrame { SourceWidth = width, SourceHeight = height };
        for (var y = 0; y < height; y++)
        {
            VdlLine line = frame[y];
            line.Width = width;
            line.ClutR[1] = gray;
            line.ClutG[1] = gray;
            line.ClutB[1] = gray;
            line.PixelBuffer[..width].Fill(Index(1));
        }

        return frame;
    }

    private static bool IsDirectColor(VdlFrame frame)
    {
        return (frame[0].PixelBuffer[0] & 0x8000) != 0
            && (frame[0].OutControl & FrameAssembler.FixedClutEnable) != 0;
    }

    private static byte DecodeRed(VdlFrame frame)
    {
        FrameAssembler.ExpandPixel(frame[0], frame[0].PixelBuffer[0], out var red, out _, out _);
        return red;
    }

    private static byte DecodeGreen(VdlFrame frame)
    {
        FrameAssembler.ExpandPixel(frame[0], frame[0].PixelBuffer[0], out _, out var green, out _);
        return green;
    }

    private static byte DecodeBlue(VdlFrame frame)
    {
        FrameAssembler.ExpandPixel(frame[0], frame[0].PixelBuffer[0], out _, out _, out var blue);
        return blue;
    }

    /// <summary>Builds a pixel whose R, G, and B CLUT indices are all <paramref name="entry"/>.</summary>
    private static ushort Index(ushort entry)
    {
        var value = (ushort)(entry & 0x1f);
        return (ushort)(value | (value << 5) | (value << 10));
    }
}
