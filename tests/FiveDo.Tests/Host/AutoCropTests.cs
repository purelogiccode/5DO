using System;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>
/// Covers 4DO-compatible blank-border detection and the one-second crop hysteresis.
/// </summary>
public sealed class AutoCropTests
{
    [Fact]
    public void Detect_ReturnsTheContentBorder()
    {
        VdlFrame frame = CreateFrame(320, 240, left: 16, top: 12, right: 20, bottom: 8, intensity: 0xF0);

        AutoCropInsets crop = AutoCropDetector.Detect(frame);

        Assert.Equal(new AutoCropInsets(16, 12, 20, 8), crop);
    }

    [Fact]
    public void Detect_UsesTheFourDoLimitForABlankFrame()
    {
        VdlFrame frame = CreateFrame(320, 240, left: 0, top: 0, right: 0, bottom: 0, intensity: 0);
        for (var line = 0; line < frame.SourceHeight; line++)
        {
            frame[line].PixelBuffer[..frame.SourceWidth].Clear();
        }

        AutoCropInsets crop = AutoCropDetector.Detect(frame);

        Assert.Equal(new AutoCropInsets(80, 60, 80, 60), crop);
    }

    [Theory]
    [InlineData(0x77, true)]
    [InlineData(0x78, false)]
    public void Detect_TreatsDarkPixelsAsBorder(byte intensity, bool expectFullCrop)
    {
        VdlFrame frame = CreateFrame(320, 240, left: 0, top: 0, right: 0, bottom: 0, intensity);

        AutoCropInsets crop = AutoCropDetector.Detect(frame);

        Assert.Equal(expectFullCrop, crop == new AutoCropInsets(80, 60, 80, 60));
    }

    [Fact]
    public void Detect_ScalesTheLimitForPalDimensions()
    {
        VdlFrame frame = CreateFrame(384, 288, left: 30, top: 20, right: 40, bottom: 10, intensity: 0xFF);

        AutoCropInsets crop = AutoCropDetector.Detect(frame);

        Assert.Equal(new AutoCropInsets(30, 20, 40, 10), crop);
    }

    [Fact]
    public void Detect_DoesNotModifyTheFrame()
    {
        VdlFrame frame = CreateFrame(320, 240, left: 10, top: 10, right: 10, bottom: 10, intensity: 0xAA);
        ushort[] before = frame[120].PixelBuffer[..320].ToArray();

        AutoCropDetector.Detect(frame);

        Assert.Equal(before, frame[120].PixelBuffer[..320].ToArray());
        Assert.Equal(320, frame.SourceWidth);
        Assert.Equal(240, frame.SourceHeight);
    }

    [Fact]
    public void Tracker_RequiresAStableCandidateForOneSecond()
    {
        var tracker = new AutoCropTracker();
        var detected = new AutoCropInsets(8, 8, 8, 8);

        Assert.Equal(AutoCropInsets.None, tracker.Consider(detected, 0));
        Assert.Equal(AutoCropInsets.None, tracker.Consider(detected, 999));
        Assert.Equal(detected, tracker.Consider(detected, 1000));
        Assert.Equal(detected, tracker.Current);
    }

    [Fact]
    public void Tracker_AcceptsMoreVisiblePixelsImmediately()
    {
        var tracker = new AutoCropTracker();
        var initial = new AutoCropInsets(10, 10, 10, 10);
        tracker.Consider(initial, 0);
        Assert.Equal(initial, tracker.Consider(initial, 1000));

        var revealed = new AutoCropInsets(5, 10, 10, 10);

        Assert.Equal(revealed, tracker.Consider(revealed, 1001));
        Assert.Equal(revealed, tracker.Current);
    }

    [Fact]
    public void Tracker_RestartsTheTimerForANewCandidate()
    {
        var tracker = new AutoCropTracker();
        var first = new AutoCropInsets(10, 10, 10, 10);
        var second = new AutoCropInsets(12, 12, 12, 12);

        Assert.Equal(AutoCropInsets.None, tracker.Consider(first, 0));
        Assert.Equal(AutoCropInsets.None, tracker.Consider(second, 500));
        Assert.Equal(AutoCropInsets.None, tracker.Consider(second, 1499));
        Assert.Equal(second, tracker.Consider(second, 1500));
    }

    [Fact]
    public void Tracker_IgnoresAnIdenticalCrop()
    {
        var tracker = new AutoCropTracker();
        var detected = new AutoCropInsets(6, 4, 6, 4);
        tracker.Consider(detected, 0);
        tracker.Consider(detected, 1000);

        Assert.Equal(detected, tracker.Consider(detected, 5000));
        Assert.Equal(detected, tracker.Current);
    }

    [Fact]
    public void Tracker_ResetClearsTheAcceptedCrop()
    {
        var tracker = new AutoCropTracker();
        var detected = new AutoCropInsets(8, 8, 8, 8);
        tracker.Consider(detected, 0);
        Assert.Equal(detected, tracker.Consider(detected, 1000));

        tracker.Reset();

        Assert.Equal(AutoCropInsets.None, tracker.Current);
        Assert.Equal(AutoCropInsets.None, tracker.Consider(detected, 1001));
    }

    [Fact]
    public void TryResolveManualCrop_PrefersAPerGameProfileEvenWhenEmpty()
    {
        var settings = new AppSettings { OverscanPixels = 8 };
        Assert.True(settings.TryResolveManualCrop(null, out CropInsets global));
        Assert.Equal(new CropInsets(8, 8, 8, 8), global);

        settings.SetGameProfile(
            "A1B2C3D4",
            new GameProfile { Overscan = new CropInsets(2, 3, 4, 5) });
        Assert.True(settings.TryResolveManualCrop("a1b2c3d4", out CropInsets perGame));
        Assert.Equal(new CropInsets(2, 3, 4, 5), perGame);

        settings.SetGameProfile(
            "A1B2C3D4",
            new GameProfile { Overscan = CropInsets.None });
        Assert.True(settings.TryResolveManualCrop("a1b2c3d4", out CropInsets none));
        Assert.Equal(CropInsets.None, none);
    }

    [Fact]
    public void TryResolveManualCrop_ReturnsFalseWithoutAnyCrop()
    {
        var settings = new AppSettings();

        Assert.False(settings.TryResolveManualCrop(null, out CropInsets crop));
        Assert.Equal(CropInsets.None, crop);
    }

    private static VdlFrame CreateFrame(
        int width,
        int height,
        int left,
        int top,
        int right,
        int bottom,
        byte intensity)
    {
        var frame = new VdlFrame
        {
            SourceWidth = width,
            SourceHeight = height,
        };
        int contentRight = width - right;
        int contentBottom = height - bottom;
        for (var y = 0; y < height; y++)
        {
            VdlLine line = frame[y];
            line.Width = width;
            line.Background = 0;
            line.OutControl = 0;
            line.ClutR[1] = intensity;
            line.ClutG[1] = intensity;
            line.ClutB[1] = intensity;
            Span<ushort> pixels = line.PixelBuffer;
            for (var x = 0; x < width; x++)
            {
                var content = x >= left && x < contentRight && y >= top && y < contentBottom;
                pixels[x] = content ? (ushort)1 : (ushort)0;
            }
        }

        return frame;
    }
}
