using System;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Tests.Video;

public sealed class HqxRuleTableTests
{
    [Fact]
    public void Rules_HaveValidEncoding()
    {
        foreach (var scale in new[] { 2, 3, 4 })
        {
            var rules = HqxRuleTable.Rules;
            var rowOffset = HqxRuleTable.RowOffset(scale);
            for (var pattern = 0; pattern < HqxRuleTable.PatternCount; pattern++)
            {
                for (var subpixel = 0; subpixel < scale * scale; subpixel++)
                {
                    var offset = (((rowOffset + subpixel) * HqxRuleTable.PatternCount) + pattern) * 2;
                    var baseRule = rules[offset];
                    var alternateRule = rules[offset + 1];
                    AssertValidRule(baseRule, $"{scale}x pattern {pattern} subpixel {subpixel} base");
                    AssertValidRule(alternateRule, $"{scale}x pattern {pattern} subpixel {subpixel} alternate");

                    var condition = (int)((baseRule >> 27) & 7);
                    if (condition == 7)
                    {
                        Assert.Equal(baseRule, alternateRule);
                    }
                    else
                    {
                        Assert.Equal(condition, (int)((alternateRule >> 27) & 7));
                    }
                }
            }
        }
    }

    // HqxFilterTests owns the C++ reference hashes. This independently decodes the
    // table and cross-checks the production CPU path's indexing and blend.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TableEvaluation_MatchesHqxFilter(int scale)
    {
        FrameBuffer source = CreateReferenceSource(32, 24);
        var expected = new FrameBuffer(source.Width * scale, source.Height * scale);
        new HqxFilter().Expand(source, expected);

        Span<uint> window = stackalloc uint[10];
        Span<bool> conditions = stackalloc bool[4];
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                window[1] = Pixel(source, x - 1, y - 1);
                window[2] = Pixel(source, x, y - 1);
                window[3] = Pixel(source, x + 1, y - 1);
                window[4] = Pixel(source, x - 1, y);
                window[5] = Pixel(source, x, y);
                window[6] = Pixel(source, x + 1, y);
                window[7] = Pixel(source, x - 1, y + 1);
                window[8] = Pixel(source, x, y + 1);
                window[9] = Pixel(source, x + 1, y + 1);

                var pattern = ComputePattern(window);
                conditions[0] = Diff(window[2], window[4]);
                conditions[1] = Diff(window[2], window[6]);
                conditions[2] = Diff(window[4], window[8]);
                conditions[3] = Diff(window[6], window[8]);

                var rowOffset = HqxRuleTable.RowOffset(scale);
                for (var row = 0; row < scale; row++)
                {
                    for (var column = 0; column < scale; column++)
                    {
                        var subpixel = (row * scale) + column;
                        var offset = (((rowOffset + subpixel) * HqxRuleTable.PatternCount) + pattern) * 2;
                        var rule = HqxRuleTable.Rules[offset];
                        var condition = (int)((rule >> 27) & 7);
                        if (condition != 7 && conditions[condition])
                        {
                            rule = HqxRuleTable.Rules[offset + 1];
                        }

                        var actual = EvaluateRule(rule, window);
                        var destinationOffset = (((y * scale) + row) * expected.Stride)
                            + (((x * scale) + column) * 4);
                        Assert.Equal((byte)actual, expected.PixelSpan[destinationOffset]);
                        Assert.Equal((byte)(actual >> 8), expected.PixelSpan[destinationOffset + 1]);
                        Assert.Equal((byte)(actual >> 16), expected.PixelSpan[destinationOffset + 2]);
                    }
                }
            }
        }
    }

    private static void AssertValidRule(uint rule, string description)
    {
        var shift = (int)((rule >> 24) & 7);
        var condition = (int)((rule >> 27) & 7);
        Assert.InRange(shift, 0, 4);
        Assert.True(condition <= 3 || condition == 7, $"{description}: invalid condition {condition}.");

        var weightSum = 0;
        for (var tap = 0; tap < 3; tap++)
        {
            var weight = (int)((rule >> (tap * 8)) & 15);
            var index = (int)((rule >> ((tap * 8) + 4)) & 15);
            weightSum += weight;
            Assert.InRange(index, 0, 8);
        }

        Assert.Equal(1 << shift, weightSum);
    }

    private static uint EvaluateRule(uint rule, ReadOnlySpan<uint> window)
    {
        var w0 = (int)(rule & 15);
        var i0 = (int)((rule >> 4) & 15);
        var w1 = (int)((rule >> 8) & 15);
        var i1 = (int)((rule >> 12) & 15);
        var w2 = (int)((rule >> 16) & 15);
        var i2 = (int)((rule >> 20) & 15);
        var shift = (int)((rule >> 24) & 7);

        var r = ((((window[i0 + 1] >> 16) & 0xFF) * w0)
            + (((window[i1 + 1] >> 16) & 0xFF) * w1)
            + (((window[i2 + 1] >> 16) & 0xFF) * w2)) >> shift;
        var g = ((((window[i0 + 1] >> 8) & 0xFF) * w0)
            + (((window[i1 + 1] >> 8) & 0xFF) * w1)
            + (((window[i2 + 1] >> 8) & 0xFF) * w2)) >> shift;
        var b = (((window[i0 + 1] & 0xFF) * w0)
            + ((window[i1 + 1] & 0xFF) * w1)
            + ((window[i2 + 1] & 0xFF) * w2)) >> shift;
        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }

    private static int ComputePattern(ReadOnlySpan<uint> window)
    {
        var (centerY, centerU, centerV) = ToYuv(window[5]);
        var pattern = 0;
        var flag = 1;
        for (var i = 1; i <= 9; i++)
        {
            if (i == 5)
            {
                continue;
            }

            if (window[i] != window[5])
            {
                var (y, u, v) = ToYuv(window[i]);
                if (Math.Abs(centerY - y) > 48
                    || Math.Abs(centerU - u) > 7
                    || Math.Abs(centerV - v) > 6)
                {
                    pattern |= flag;
                }
            }

            flag <<= 1;
        }

        return pattern;
    }

    private static bool Diff(uint c1, uint c2)
    {
        var (y1, u1, v1) = ToYuv(c1);
        var (y2, u2, v2) = ToYuv(c2);
        return Math.Abs(y1 - y2) > 48
            || Math.Abs(u1 - u2) > 7
            || Math.Abs(v1 - v2) > 6;
    }

    private static (int Y, int U, int V) ToYuv(uint color)
    {
        var r = (int)((color >> 16) & 0xFF);
        var g = (int)((color >> 8) & 0xFF);
        var b = (int)(color & 0xFF);
        var y = (int)((0.299 * r) + (0.587 * g) + (0.114 * b));
        var u = ((int)((-0.169 * r) - (0.331 * g) + (0.5 * b)) + 128) & 0xFF;
        var v = ((int)((0.5 * r) - (0.419 * g) - (0.081 * b)) + 128) & 0xFF;
        return (y, u, v);
    }

    private static uint Pixel(FrameBuffer frame, int x, int y)
    {
        x = Math.Clamp(x, 0, frame.Width - 1);
        y = Math.Clamp(y, 0, frame.Height - 1);
        var offset = (y * frame.Stride) + (x * 4);
        return 0xFF000000u
            | ((uint)frame.PixelSpan[offset + 2] << 16)
            | ((uint)frame.PixelSpan[offset + 1] << 8)
            | frame.PixelSpan[offset];
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
                uint b = (uint)(((x ^ y) * 11) & 0xFF);
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
