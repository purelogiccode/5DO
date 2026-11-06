using System;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class InterlaceProcessorTests
{
    private const int Width = 320;
    private const int Height = 240;

    [Fact]
    public void Bob_ReturnsTheFieldUnchanged()
    {
        VdlFrame field = MakeField(Height, y => (ushort)(1 + (y % 29)));
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Bob };

        VdlFrame first = processor.Process(field, secondField: false);
        VdlFrame second = processor.Process(field, secondField: true);

        Assert.Same(field, first);
        Assert.Same(field, second);
        Assert.False(processor.IsInterlaced);
        Assert.Equal(Height, second.SourceHeight);
        Assert.Equal(1.0, second.PixelAspect);
    }

    [Fact]
    public void Weave_InterleavesFieldsIntoADoubleHeightFrame()
    {
        VdlFrame previous = MakeField(Height, _ => 10);
        VdlFrame current = MakeField(Height, _ => 20);
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Weave };

        _ = processor.Process(previous, secondField: false);
        VdlFrame woven = processor.Process(current, secondField: true);

        Assert.Equal(Height * 2, woven.SourceHeight);
        Assert.Equal(Width, woven.SourceWidth);
        Assert.Equal(0.5, woven.PixelAspect);
        for (var y = 0; y < Height; y++)
        {
            // The first field supplies even rows; the second field supplies odd rows.
            Assert.Equal(Index(10), woven[y * 2].Pixels[0]);
            Assert.Equal(Index(20), woven[(y * 2) + 1].Pixels[0]);
            Assert.Equal(Width, woven[y * 2].Width);
            Assert.Equal(Width, woven[(y * 2) + 1].Width);
        }
    }

    [Fact]
    public void Weave_CarriesPalettesBackgroundsAndControlWords()
    {
        VdlFrame previous = MakeField(Height, _ => 3);
        VdlFrame current = MakeField(Height, _ => 4);
        current[0].Background = 0x0000ff00;
        current[0].OutControl = 0xc001002c;
        current[0].ClutDma = 0x00ff00ff;
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Weave };

        _ = processor.Process(previous, secondField: false);
        VdlFrame woven = processor.Process(current, secondField: true);

        VdlLine line = woven[1];
        Assert.Equal(0x0000ff00u, line.Background);
        Assert.Equal(0xc001002cu, line.OutControl);
        Assert.Equal(0x00ff00ffu, line.ClutDma);
        Assert.Equal(current[0].ClutR[7], line.ClutR[7]);
    }

    [Fact]
    public void Weave_WithoutAnOppositeFieldFallsBackToBob()
    {
        VdlFrame field = MakeField(Height, _ => 5);
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Weave };

        Assert.Same(field, processor.Process(field, secondField: false));
        Assert.Same(field, processor.Process(field, secondField: false));

        VdlFrame woven = processor.Process(field, secondField: true);
        Assert.Equal(480, woven.SourceHeight);
    }

    [Fact]
    public void Weave_RejectsAHeightThatCannotBeDoubled()
    {
        VdlFrame previous = MakeField(480, _ => 5);
        VdlFrame current = MakeField(480, _ => 6);
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Weave };

        _ = processor.Process(previous, secondField: false);
        VdlFrame presented = processor.Process(current, secondField: true);

        Assert.Same(current, presented);
        Assert.Equal(480, presented.SourceHeight);
    }

    [Fact]
    public void Weave_HandlesPalDoubleHeight()
    {
        VdlFrame previous = MakeField(288, _ => 5);
        VdlFrame current = MakeField(288, _ => 6);
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Weave };

        _ = processor.Process(previous, secondField: false);
        VdlFrame woven = processor.Process(current, secondField: true);

        Assert.Equal(576, woven.SourceHeight);
        Assert.Equal(0.5, woven.PixelAspect);
    }

    [Fact]
    public void Blend_AveragesFieldColorsIntoADirectColorFrame()
    {
        VdlFrame previous = MakeField(Height, _ => 1);
        VdlFrame current = MakeField(Height, _ => 2);
        for (var y = 0; y < Height; y++)
        {
            previous[y].ClutR[1] = 255;
            previous[y].ClutG[1] = 0;
            previous[y].ClutB[1] = 0;
            current[y].ClutR[2] = 0;
            current[y].ClutG[2] = 0;
            current[y].ClutB[2] = 255;
        }

        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Blend };
        _ = processor.Process(previous, secondField: false);
        VdlFrame blended = processor.Process(current, secondField: true);

        Assert.Equal(Height, blended.SourceHeight);
        Assert.Equal(1.0, blended.PixelAspect);
        Assert.NotEqual(0u, blended[0].OutControl & FrameAssembler.FixedClutEnable);
        ushort pixel = blended[0].Pixels[0];
        Assert.NotEqual(0, pixel & 0x8000);
        FrameAssembler.ExpandPixel(blended[0], pixel, out var r, out var g, out var b);
        Assert.Equal(132, r);
        Assert.Equal(0, g);
        Assert.Equal(132, b);
    }

    [Fact]
    public void Blend_UsesTheCurrentColorWhenThePreviousLineIsNarrower()
    {
        VdlFrame previous = MakeField(Height, _ => 1);
        VdlFrame current = MakeField(Height, _ => 2);
        previous[0].Width = 0;
        for (var y = 0; y < Height; y++)
        {
            current[y].ClutR[2] = 200;
            current[y].ClutG[2] = 100;
            current[y].ClutB[2] = 50;
        }

        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Blend };
        _ = processor.Process(previous, secondField: false);
        VdlFrame blended = processor.Process(current, secondField: true);

        FrameAssembler.ExpandPixel(blended[0], blended[0].Pixels[0], out var r, out var g, out var b);
        Assert.Equal(198, r);
        Assert.Equal(99, g);
        Assert.Equal(49, b);
    }

    [Fact]
    public void Auto_DetectsComplementaryFieldsAndLatchesWeave()
    {
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Auto };
        VdlFrame even = MakeInterlacedField(secondField: false);
        VdlFrame odd = MakeInterlacedField(secondField: true);

        for (var pair = 0; pair < 12; pair++)
        {
            _ = processor.Process(even, secondField: false);
            _ = processor.Process(odd, secondField: true);
        }

        Assert.True(processor.IsInterlaced);
        VdlFrame woven = processor.Process(even, secondField: false);
        Assert.Equal(480, woven.SourceHeight);
        Assert.Equal(0.5, woven.PixelAspect);
    }

    [Fact]
    public void Auto_LeavesProgressiveFieldsAlone()
    {
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Auto };
        VdlFrame first = MakeField(Height, y => (ushort)(1 + (y % 29)));
        VdlFrame second = MakeField(Height, y => (ushort)(1 + (y % 29)));

        for (var pair = 0; pair < 60; pair++)
        {
            VdlFrame presentedA = processor.Process(first, secondField: false);
            VdlFrame presentedB = processor.Process(second, secondField: true);
            Assert.Same(first, presentedA);
            Assert.Same(second, presentedB);
        }

        Assert.False(processor.IsInterlaced);
    }

    [Fact]
    public void Auto_ReturnsToBobAfterSustainedProgressiveContent()
    {
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Auto };
        VdlFrame even = MakeInterlacedField(secondField: false);
        VdlFrame odd = MakeInterlacedField(secondField: true);
        for (var pair = 0; pair < 12; pair++)
        {
            _ = processor.Process(even, secondField: false);
            _ = processor.Process(odd, secondField: true);
        }

        Assert.True(processor.IsInterlaced);
        VdlFrame progressive = MakeField(Height, y => (ushort)(1 + (y % 29)));
        for (var pair = 0; pair < 8; pair++)
        {
            _ = processor.Process(progressive, secondField: false);
            _ = processor.Process(progressive, secondField: true);
        }

        Assert.True(processor.IsInterlaced);
        for (var pair = 0; pair < 8; pair++)
        {
            _ = processor.Process(progressive, secondField: false);
            _ = processor.Process(progressive, secondField: true);
        }

        Assert.False(processor.IsInterlaced);
    }

    [Fact]
    public void Reset_ClearsTheRetainedField()
    {
        VdlFrame previous = MakeField(Height, _ => 10);
        VdlFrame current = MakeField(Height, _ => 20);
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Weave };
        _ = processor.Process(previous, secondField: false);
        processor.Reset();

        Assert.Same(current, processor.Process(current, secondField: true));
        Assert.False(processor.IsInterlaced);
    }

    [Fact]
    public void ModeChange_ClearsThePairing()
    {
        VdlFrame previous = MakeField(Height, _ => 10);
        VdlFrame current = MakeField(Height, _ => 20);
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Weave };
        _ = processor.Process(previous, secondField: false);

        processor.Mode = DeinterlaceMode.Blend;

        Assert.Same(current, processor.Process(current, secondField: true));
    }

    [Fact]
    public void UnknownMode_IsRejected()
    {
        var processor = new InterlaceProcessor();

        Assert.Throws<ArgumentOutOfRangeException>(() => processor.Mode = (DeinterlaceMode)99);
    }

    [Fact]
    public void WovenPixelAspect_PreservesTheFourThreeViewport()
    {
        var settings = new RendererSettings();
        ViewportRect progressive = ViewportCalculator.Compute(320, 240, 1280, 960, settings);
        ViewportRect interlaced = ViewportCalculator.Compute(320, 480, 1280, 960, settings, 0.5);

        Assert.Equal(progressive, interlaced);
        Assert.Equal(1280, interlaced.Width);
        Assert.Equal(960, interlaced.Height);
    }

    [Fact]
    public void CpuRenderer_PresentsAWovenFrameWithoutLetterboxing()
    {
        VdlFrame previous = MakeField(Height, _ => 1);
        VdlFrame current = MakeField(Height, _ => 2);
        var processor = new InterlaceProcessor { Mode = DeinterlaceMode.Weave };
        _ = processor.Process(previous, secondField: false);
        VdlFrame woven = processor.Process(current, secondField: true);

        using var renderer = new CpuVideoRenderer();
        renderer.Initialize(640, 480);
        renderer.Present(woven);

        Assert.Equal(640, renderer.Frame.Width);
        Assert.Equal(480, renderer.Frame.Height);
        // 4:3 with half-height rows fills the whole 640x480 target; the first woven
        // row comes from the first field (palette entry 1 = 8).
        Assert.Equal(8, renderer.Frame.PixelSpan[0]);
        Assert.Equal(8, renderer.Frame.PixelSpan[1]);
        Assert.Equal(8, renderer.Frame.PixelSpan[2]);
    }

    private static VdlFrame MakeInterlacedField(bool secondField)
    {
        // A 480-line source image where each row has a distinct luminance. Even and odd
        // fields contain complementary rows, exactly like a real 480i pair.
        return MakeField(
            Height,
            y => (ushort)(1 + (((y * 2) + (secondField ? 1 : 0)) % 29)));
    }

    private static VdlFrame MakeField(int height, Func<int, ushort> pixel)
    {
        var frame = new VdlFrame
        {
            SourceWidth = Width,
            SourceHeight = height,
        };
        for (var y = 0; y < height; y++)
        {
            VdlLine line = frame[y];
            line.Width = Width;
            for (var i = 0; i < VdlLine.ClutSize; i++)
            {
                var value = (byte)(i * 8);
                line.ClutR[i] = value;
                line.ClutG[i] = value;
                line.ClutB[i] = value;
            }

            line.PixelBuffer[..Width].Fill(Index(pixel(y)));
        }

        return frame;
    }

    /// <summary>Builds a pixel whose R, G, and B CLUT indices are all <paramref name="entry"/>.</summary>
    private static ushort Index(ushort entry)
    {
        var value = (ushort)(entry & 0x1f);
        return (ushort)(value | (value << 5) | (value << 10));
    }
}
