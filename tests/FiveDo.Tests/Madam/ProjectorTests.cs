using System.Collections.Generic;
using FiveDo.Madam;

namespace FiveDo.Tests.Madam;

public sealed class ProjectorTests
{
    // Expected values generated from FreeDO's PPROJ_OUTPUT.
    public static IEnumerable<object[]> ReferenceVectors()
    {
        yield return new object[] { 0x0000u, 0x0000u, 0x0000u, CcbControl.None, 0x00000000u, 0x0000u, 0x0000u, 0x0000u };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.None, 0x00000000u, 0x0000u, 0x0000u, 0x7ffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.PlutPosition, 0x00000000u, 0x0000u, 0x0000u, 0x7ffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.PlutPosition, 0x08000000u, 0x0000u, 0x0000u, 0x7ffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.PlutPosition, 0x08000000u, 0x00004000u, 0x0000u, 0x7ffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.PlutPosition, 0x40000000u, 0x0000u, 0x0000u, 0xfffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.PlutPosition, 0x80000000u, 0x0000u, 0x0000u, 0xfffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.PlutPosition, 0xc0000000u, 0x0000u, 0x0000u, 0xfffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.None, 0x10000000u, 0x0000u, 0x0000u, 0x7fffu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.None, 0x20000000u, 0x0000u, 0x0000u, 0x7ffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.None, 0x30000000u, 0x0000u, 0x0000u, 0x7ffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.None, 0x00000000u, 0x0000u, 0x00008001u, 0x7ffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.None, 0x08000000u, 0x0000u, 0x00008001u, 0x7ffeu };
        yield return new object[] { 0x8001u, 0x7ffeu, 0x0000u, CcbControl.None, 0x08000000u, 0x00004000u, 0x00008001u, 0x7ffeu };
        yield return new object[] { 0x0000u, 0x7ffeu, 0x0000u, CcbControl.None, 0x00000000u, 0x0000u, 0x00008001u, 0x7ffeu };
        yield return new object[] { 0xffffu, 0xffffu, 0xffffu, (CcbControl)0xffffffffu, 0xffffffffu, 0xffffffffu, 0xffffffffu, 0xffffu };
    }

    [Theory]
    [MemberData(nameof(ReferenceVectors))]
    public void Apply_MatchesFreeDoReference(
        uint decoderOutput,
        uint processorOutput,
        uint framePixel,
        CcbControl ccbFlags,
        uint ccbControl0,
        uint pre1,
        uint celOriginVh,
        uint expected)
    {
        var actual = Projector.Apply(
            (ushort)decoderOutput,
            (ushort)processorOutput,
            (ushort)framePixel,
            ccbFlags,
            ccbControl0,
            pre1,
            celOriginVh);

        Assert.Equal((ushort)expected, actual);
    }

    [Fact]
    public void Apply_PlutPositionUsesDecoderVhBits()
    {
        var fromOrigin = Projector.Apply(0x8001, 0x7ffe, 0, CcbControl.None, 0, 0, 0);
        var fromDecoder = Projector.Apply(0x8001, 0x7ffe, 0, CcbControl.PlutPosition, 0, 0, 0);

        Assert.Equal(0x7ffe, fromOrigin);
        Assert.Equal(0x7ffe, fromDecoder);
    }

    [Fact]
    public void Apply_SwapHvSwapsBit15AndBit0()
    {
        var swapped = Projector.Apply(
            0x8000,
            0x7ffe,
            0,
            CcbControl.PlutPosition,
            (uint)CcbControl0.SwapHV | (uint)B15Position.PixelDecoder | (uint)B0Position.PixelDecoder,
            0,
            0);

        Assert.Equal(0x7fff, swapped);
    }

    [Fact]
    public void Apply_NoSwapPreventsHvSwap()
    {
        var swapped = Projector.Apply(
            0x8000,
            0x7ffe,
            0,
            CcbControl.PlutPosition,
            (uint)CcbControl0.SwapHV | (uint)B15Position.PixelDecoder | (uint)B0Position.PixelDecoder,
            (uint)CcbPreamble1.NoSwap,
            0);

        Assert.Equal(0xfffe, swapped);
    }

    [Fact]
    public void Apply_B15ModesOverrideBit15()
    {
        Assert.Equal(0x7ffe, Projector.Apply(0x8001, 0x7ffe, 0, CcbControl.PlutPosition, (uint)B15Position.Zero, 0, 0));
        Assert.Equal(0xfffe, Projector.Apply(0x0001, 0x7ffe, 0, CcbControl.PlutPosition, (uint)B15Position.One, 0, 0));
        Assert.Equal(0xfffe, Projector.Apply(0x8001, 0x7ffe, 0, CcbControl.PlutPosition, (uint)B15Position.PixelDecoder, 0, 0));
    }

    [Fact]
    public void Apply_B0ModesOverrideBit0()
    {
        Assert.Equal(0x7ffe, Projector.Apply(0x8001, 0x7ffe, 0, CcbControl.PlutPosition, (uint)B0Position.Zero, 0, 0));
        Assert.Equal(0x7fff, Projector.Apply(0x8000, 0x7ffe, 0, CcbControl.PlutPosition, (uint)B0Position.One, 0, 0));
        Assert.Equal(0x7ffe, Projector.Apply(0x8000, 0x7ffe, 0, CcbControl.PlutPosition, (uint)B0Position.PixelProcessor, 0, 0));
        Assert.Equal(0x7fff, Projector.Apply(0x8000, 0x7fff, 0, CcbControl.PlutPosition, (uint)B0Position.PixelProcessor, 0, 0));
        Assert.Equal(0x7ffe, Projector.Apply(0x8000, 0x7ffe, 0, CcbControl.PlutPosition, (uint)B0Position.PixelDecoder, 0, 0));
    }

    [Fact]
    public void Apply_MatchesReferenceGridHash()
    {
        // Differential hash over the FreeDO PPROJ_OUTPUT input grid.
        var hash = 2166136261u;
        for (uint flags = 0; flags < 0x1000; flags += 0x1f)
        {
            for (uint ccbControl0 = 0; ccbControl0 < 0x40000000; ccbControl0 += 0x09111111)
            {
                for (uint pre1 = 0; pre1 < 0x10000; pre1 += 0x1111)
                {
                    var output = Projector.Apply(
                        0x8001,
                        0x7ffe,
                        0x1234,
                        (CcbControl)flags,
                        ccbControl0,
                        pre1,
                        0);
                    hash = PixelProcessorTests.Mix(hash, output);
                }
            }
        }

        Assert.Equal(0x6018c2c5u, hash);
    }
}
