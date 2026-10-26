using System.Collections.Generic;
using FiveDo.Madam;

namespace FiveDo.Tests.Madam;

public sealed class PixelProcessorTests
{
    // Expected values generated from FreeDO's PPROC with signed-char component arithmetic.
    public static IEnumerable<object[]> ReferenceVectors()
    {
        yield return new object[] { 0x0000u, 0x0000u, 0x0049u, 0x00000000u, CcbControl.None, 0x0400u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000000u, CcbControl.None, 0x0421u };
        yield return new object[] { 0x7fffu, 0x7fffu, 0x0049u, 0x00000000u, CcbControl.None, 0x0421u };
        yield return new object[] { 0x1234u, 0x5678u, 0x0049u, 0x00000000u, CcbControl.None, 0x0021u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000000u, CcbControl.PixelXor, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000000u, CcbControl.NoBlock, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000000u, CcbControl.Background, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000000u, CcbControl.PixelOverMask, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000000u, (CcbControl)0x180, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000000u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000002u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000004u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000008u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000010u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000020u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000040u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000080u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000100u, CcbControl.UseAv, 0x3defu };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000200u, CcbControl.UseAv, 0x1ce7u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000400u, CcbControl.UseAv, 0x0c63u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00000800u, CcbControl.UseAv, 0x14a5u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00001000u, CcbControl.UseAv, 0x2529u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00002000u, CcbControl.UseAv, 0x0c63u };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00004000u, CcbControl.UseAv, 0x7fffu };
        yield return new object[] { 0x7fffu, 0x0000u, 0x0049u, 0x00008000u, CcbControl.UseAv, 0x0400u };
        yield return new object[] { 0x0000u, 0x7fffu, 0x0049u, 0x00008000u, CcbControl.UseAv, 0x0421u };
        yield return new object[] { 0xffffu, 0xffffu, 0xffffu, 0xffffffffu, CcbControl.UseAv, 0x2108u };
        yield return new object[] { 0x5555u, 0xaaaau, 0x003fu, 0x0000ffffu, CcbControl.UseAv, 0x08c2u };
        yield return new object[] { 0xaaaau, 0x5555u, 0x007fu, 0xffff0000u, CcbControl.UseAv, 0x1846u };
    }

    [Theory]
    [MemberData(nameof(ReferenceVectors))]
    public void Process_MatchesFreeDoReference(
        uint pixel,
        uint framePixel,
        uint amv,
        uint pixelControl,
        CcbControl ccbFlags,
        uint expected)
    {
        var actual = PixelProcessor.Process(
            (ushort)pixel,
            (ushort)framePixel,
            (ushort)amv,
            pixelControl,
            ccbFlags);

        Assert.Equal((ushort)expected, actual);
    }

    [Fact]
    public void Process_NoBlockTurnsBlackIntoRedLsb()
    {
        var withBlock = PixelProcessor.Process(0, 0, 0x49, 0, CcbControl.None);
        var withoutBlock = PixelProcessor.Process(0, 0, 0x49, 0, CcbControl.NoBlock);

        Assert.Equal(0x0400, withBlock);
        Assert.Equal(0x0000, withoutBlock);
    }

    [Fact]
    public void Process_PixelOverZeroClearsBit15BeforeControlSelection()
    {
        // PIXC high half is zero; the low half selects the AV colour source.
        const uint pixelControl = 0x0000_0050;
        var flags = CcbControl.UseAv | (CcbControl)0x100;

        var normal = PixelProcessor.Process(0x8000, 0, 0x49, pixelControl, CcbControl.UseAv);
        var overZero = PixelProcessor.Process(0x8000, 0, 0x49, pixelControl, flags);

        Assert.Equal(0x0400, normal);
        Assert.Equal(0x1084, overZero);
    }

    [Fact]
    public void Process_PixelOverOneSetsBit15BeforeControlSelection()
    {
        const uint pixelControl = 0x0050_0000;
        var flags = CcbControl.UseAv | (CcbControl)0x180;

        var normal = PixelProcessor.Process(0x0000, 0, 0x49, pixelControl, CcbControl.UseAv);
        var overOne = PixelProcessor.Process(0x0000, 0, 0x49, pixelControl, flags);

        Assert.Equal(0x0400, normal);
        Assert.Equal(0x1084, overOne);
    }

    [Fact]
    public void Process_MatchesReferenceGridHash()
    {
        // Differential hash over the FreeDO PPROC input grid. The expected value was
        // generated from the reference C++ implementation with signed-char arithmetic.
        var hash = 2166136261u;
        for (uint flags = 0; flags < 0x1000; flags += 0x1f)
        {
            for (uint pixelControl = 0; pixelControl < 0x10000; pixelControl += 0x3d)
            {
                for (uint pixel = 0; pixel < 0x10000; pixel += 0x1111)
                {
                    var framePixel = (pixel * 37 + 0x1234) & 0xffff;
                    var amv = (pixel * 13) & 0xffff;
                    var output = PixelProcessor.Process(
                        (ushort)pixel,
                        (ushort)framePixel,
                        (ushort)amv,
                        pixelControl,
                        (CcbControl)flags);
                    hash = Mix(hash, output);
                }
            }
        }

        Assert.Equal(0x87bbce2fu, hash);
    }

    internal static uint Mix(uint hash, uint value)
    {
        for (var i = 0; i < 4; i++)
        {
            hash ^= (value >> (i * 8)) & 0xff;
            hash *= 16777619u;
        }

        return hash;
    }
}
