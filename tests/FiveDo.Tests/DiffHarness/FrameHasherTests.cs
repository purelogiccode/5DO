using System;
using FiveDo.DiffHarness;
using FiveDo.Video;

namespace FiveDo.Tests.DiffHarness;

public sealed class FrameHasherTests
{
    [Fact]
    public void HashXrgb8888_MatchesEquivalentBgraBytes()
    {
        uint[] pixels = [0x00112233, 0x00445566, 0x00778899];
        byte[] bgra =
        [
            0x33, 0x22, 0x11, 0xff,
            0x66, 0x55, 0x44, 0xff,
            0x99, 0x88, 0x77, 0xff,
        ];

        Assert.Equal(
            FrameHasher.HashBgra(bgra),
            FrameHasher.HashXrgb8888(pixels, 3, 1, 3));
    }

    [Fact]
    public void HashXrgb8888_HonorsRowStride()
    {
        uint[] pixels =
        [
            0x00010203, 0x00040506, 0x00ffffff,
            0x00070809, 0x000a0b0c, 0x00ffffff,
        ];
        byte[] visible =
        [
            0x03, 0x02, 0x01, 0xff,
            0x06, 0x05, 0x04, 0xff,
            0x09, 0x08, 0x07, 0xff,
            0x0c, 0x0b, 0x0a, 0xff,
        ];

        Assert.Equal(
            FrameHasher.HashBgra(visible),
            FrameHasher.HashXrgb8888(pixels, 2, 2, 3));
    }

    [Fact]
    public void HashXrgb8888_RejectsShortSpan()
    {
        Assert.Throws<ArgumentException>(() =>
            FrameHasher.HashXrgb8888(new uint[2], 3, 1, 3));
    }

    [Fact]
    public void HashBytes_IsStable()
    {
        Assert.Equal(2166136261u, FrameHasher.HashBytes([]));
        Assert.Equal(
            FrameHasher.HashBytes([1, 2, 3, 4]),
            FrameHasher.HashBytes([1, 2, 3, 4]));
    }
}
