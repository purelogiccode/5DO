using ThreeDo.Madam;

namespace ThreeDo.Tests.Madam;

public sealed class CelGeometryTests
{
    [Fact]
    public void TryInitVisual_MatchesReferenceVectors()
    {
        // Inputs: flags, packed, xpos, ypos, hdx, hdy, vdx, vdy, hddx, hddy, sprwi, sprhi, clipx, clipy.
        // Outputs: visible, function, wiStart, hiStart, wiLim, hiLim, incX, incY, xpos, ypos.
        var cases = new (int[] Input, int[] Output)[]
        {
            (
                new[] { 0x40000, 0, 0x13f0000, -0x10000, 0x10000, 0, 0, 0x10000, 0, 0, 16, 320, 319, 0 },
                new[] { 1, 0, 0, 1, 1, 2, 0, 0, 0x13f0000, -0x10000 }),
            (
                new[] { 0x60000, 0, -0x20000, 0x8000, 0x20000, 0, 0, 0x20000, 0, 0, 16, 320, 239, 0 },
                new[] { 1, 1, 0, 0, 0, 0, 4, 1, -0x20000, 0x8000 }),
            (
                new[] { 0x60000, 0, 1, 0x100000, -1, 0, -0x10000, -0x8000, 0x10000, -0x8000, 2, 320, 1, 0 },
                new[] { 1, 2, 0, 0, 0, 0, 0, 0, 1, 0x100000 }),
            (
                new[] { 0x20000, 1, 0x100000, 0x13f0000, -0x10000, 0, -0x8000, 0x10000, -0x8000, 0, 2, 2, 239, 0 },
                new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0x100000, 0x13f0000 }),
            (
                new[] { 0, 1, -0x10000, -0x10000, -0x20000, 0x8000, 0, -0x20000, 0, -0x8000, 1, 2, 1, 0 },
                new[] { 0, 0, 0, 0, 0, 0, 0, 0, -0x10000, -0x10000 }),
        };

        foreach (var (input, output) in cases)
        {
            var geometry = Create(input);
            var visible = geometry.TryInitVisual(input[1] != 0);
            var actual = new[]
            {
                visible ? 1 : 0,
                (int)geometry.TexelFunction,
                geometry.TextureWidthStart,
                geometry.TextureHeightStart,
                geometry.TextureWidthLimit,
                geometry.TextureHeightLimit,
                geometry.TexelIncrementX,
                geometry.TexelIncrementY,
                geometry.XPosition1616,
                geometry.YPosition1616,
            };

            Assert.Equal(output, actual);
        }
    }

    [Fact]
    public void TryInitVisual_NoWindingFlagsIsCulled()
    {
        var geometry = new CelGeometry
        {
            XPosition1616 = 0,
            YPosition1616 = 0,
            Hdx1616 = 0x10000,
            Hdy1616 = 0,
            Vdx1616 = 0,
            Vdy1616 = 0x10000,
            SpriteWidth = 8,
            SpriteHeight = 8,
            ClipX = 319,
            ClipY = 239,
        };

        Assert.False(geometry.TryInitVisual(false));
    }

    [Fact]
    public void TryInitVisual_LineMapAdjustsHalfPixelOrigin()
    {
        var geometry = new CelGeometry
        {
            Flags = CcbControl.Clockwise,
            XPosition1616 = 0x13f0000,
            YPosition1616 = -0x10000,
            Hdx1616 = 0x10000,
            Hdy1616 = 0,
            Vdx1616 = 0,
            Vdy1616 = 0x10000,
            SpriteWidth = 16,
            SpriteHeight = 320,
            ClipX = 319,
            ClipY = 0,
        };

        Assert.True(geometry.TryInitVisual(false));
        Assert.Equal(TexelFunction.Line, geometry.TexelFunction);
        Assert.Equal(1, geometry.TextureWidthLimit);
        Assert.Equal(2, geometry.TextureHeightLimit);
        Assert.Equal(1, geometry.TextureHeightStart);
        Assert.Equal(0x13f0000, geometry.XPosition1616);
        Assert.Equal(-0x10000, geometry.YPosition1616);
    }

    [Fact]
    public void TryInitVisual_MatchesReferenceGridHash()
    {
        // Differential hash generated from the FreeDO TestInitVisual/Init_*_Map code.
        var deltas = new[] { -0x20000, -0x10000, -0x8000, -1, 0, 1, 0x8000, 0x10000, 0x20000 };
        var origins = new[] { -0x20000, -0x10000, -0x8000, -1, 0, 1, 0x8000, 0x10000, 0x100000, 0x13f0000 };
        var flagSets = new[] { CcbControl.None, CcbControl.Clockwise, CcbControl.CounterClockwise, CcbControl.Clockwise | CcbControl.CounterClockwise };
        var sizes = new[] { 1, 2, 8, 16, 320 };
        var clips = new[] { 0, 1, 319, 239 };

        var hash = 2166136261u;
        var seed = 0x12345678u;
        for (var count = 0; count < 300000; count++)
        {
            seed = unchecked((seed * 1664525u) + 1013904223u);
            var r = seed;

            var flags = flagSets[r & 3];
            r >>= 2;
            var hdx = deltas[r % 9];
            r /= 9;
            var hdy = deltas[r % 9];
            r /= 9;
            var vdx = deltas[r % 9];
            r /= 9;
            var vdy = deltas[r % 9];
            r /= 9;
            var hddx = (((int)(r & 3)) - 1) * 0x8000;
            r >>= 2;
            var hddy = (((int)(r & 3)) - 1) * 0x8000;
            r >>= 2;
            var xpos = origins[r % 10];
            r /= 10;
            var ypos = origins[r % 10];
            r /= 10;
            var sprwi = sizes[r % 5];
            r /= 5;
            var sprhi = sizes[r % 5];
            r /= 5;
            var clipx = clips[r & 3];
            r >>= 2;
            var clipy = clips[r & 3];

            var geometry = new CelGeometry
            {
                Flags = flags,
                XPosition1616 = xpos,
                YPosition1616 = ypos,
                Hdx1616 = hdx,
                Hdy1616 = hdy,
                Vdx1616 = vdx,
                Vdy1616 = vdy,
                Hddx1616 = hddx,
                Hddy1616 = hddy,
                SpriteWidth = sprwi,
                SpriteHeight = sprhi,
                ClipX = clipx,
                ClipY = clipy,
            };

            var visible = geometry.TryInitVisual((count & 1) != 0);
            hash = Mix(hash, visible ? 1u : 0u);
            hash = Mix(hash, (uint)geometry.TexelFunction);
            hash = Mix(hash, (uint)geometry.TextureWidthStart);
            hash = Mix(hash, (uint)geometry.TextureHeightStart);
            hash = Mix(hash, (uint)geometry.TextureWidthLimit);
            hash = Mix(hash, (uint)geometry.TextureHeightLimit);
            hash = Mix(hash, (uint)geometry.TexelIncrementX);
            hash = Mix(hash, (uint)geometry.TexelIncrementY);
            hash = Mix(hash, (uint)geometry.XPosition1616);
            hash = Mix(hash, (uint)geometry.YPosition1616);
        }

        Assert.Equal(0xfd4044a6u, hash);
    }

    private static CelGeometry Create(int[] input)
    {
        return new CelGeometry
        {
            Flags = (CcbControl)(uint)input[0],
            XPosition1616 = input[2],
            YPosition1616 = input[3],
            Hdx1616 = input[4],
            Hdy1616 = input[5],
            Vdx1616 = input[6],
            Vdy1616 = input[7],
            Hddx1616 = input[8],
            Hddy1616 = input[9],
            SpriteWidth = input[10],
            SpriteHeight = input[11],
            ClipX = input[12],
            ClipY = input[13],
        };
    }

    private static uint Mix(uint hash, uint value)
    {
        for (var i = 0; i < 4; i++)
        {
            hash ^= (value >> (i * 8)) & 0xff;
            hash *= 16777619u;
        }

        return hash;
    }
}
