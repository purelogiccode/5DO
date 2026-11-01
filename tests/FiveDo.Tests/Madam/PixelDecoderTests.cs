using FiveDo.Madam;

namespace FiveDo.Tests.Madam;

public sealed class PixelDecoderTests
{
    [Fact]
    public void Decode_OneBppUsesPlutEntries()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[0] = 0x1234;
        plut[1] = 0x5678;

        PixelDecodeResult first = decoder.Decode(0, 1, 0, plut);
        PixelDecodeResult second = decoder.Decode(1, 1, 0, plut);

        Assert.Equal(0x1234, first.Pixel);
        Assert.Equal(0x49, first.Amv);
        Assert.False(first.Transparent);
        Assert.Equal(0x5678, second.Pixel);
    }

    [Fact]
    public void Decode_RawVhBitsSurviveBlueLsbSubstitution()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[0] = 0x8001;

        PixelDecodeResult result = decoder.Decode(0, 1, 0, plut);

        Assert.Equal(0x8001, result.RawVhBits);
        Assert.Equal(0x8000, result.Pixel);
    }

    [Fact]
    public void Decode_ZeroBppUsesTheOneBppGroup()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[0] = 0x1234;
        plut[1] = 0x5678;

        PixelDecodeResult first = decoder.Decode(0, 0, 0, plut);
        PixelDecodeResult second = decoder.Decode(1, 0, 0, plut);

        Assert.Equal(0x1234, first.Pixel);
        Assert.Equal(0x5678, second.Pixel);
        Assert.Equal(0x49, first.Amv);
    }

    [Fact]
    public void Decode_TwoAndFourBppUsePlutEntries()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[2] = 0x2222;
        plut[15] = 0x3333;

        Assert.Equal(0x2222, decoder.Decode(2, 2, 0, plut).Pixel);
        Assert.Equal(0x3332, decoder.Decode(15, 3, 0, plut).Pixel);
    }

    [Fact]
    public void Decode_SixBppUsesColorAndPaletteWriteBit()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[5] = 0x1234;

        PixelDecodeResult result = decoder.Decode(5 | (1u << 5), 4, 0, plut);

        Assert.Equal(0x9234, result.Pixel);
        Assert.Equal(0x49, result.Amv);
    }

    [Fact]
    public void Decode_EightBppLinearExpandsComponents()
    {
        var decoder = new PixelDecoder();

        PixelDecodeResult result = decoder.Decode(0x12, 5 | 0x10, 0, CreatePlut());

        Assert.Equal(0x254, result.Pixel);
        Assert.Equal(0x49, result.Amv);
    }

    [Fact]
    public void Decode_EightBppCodedUsesPlutAndAmvTable()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[0] = 0x2222;

        PixelDecodeResult result = decoder.Decode(0x60, 5, 0, plut);

        Assert.Equal(0x2222, result.Pixel);
        Assert.Equal(219, result.Amv);
    }

    [Fact]
    public void Decode_SixteenBppLinearReturnsRawPixel()
    {
        var decoder = new PixelDecoder();

        PixelDecodeResult result = decoder.Decode(0xabcd, 6 | 0x10, 0, CreatePlut());

        Assert.Equal(0xabcc, result.Pixel);
        Assert.Equal(0x49, result.Amv);
    }

    [Fact]
    public void Decode_SixteenBppCodedUsesPlutBit15AndAmvTable()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[5] = 0x1234;
        var pixel = 0x8000u | (1u << 5) | (2u << 8) | (3u << 11) | 5u;

        PixelDecodeResult result = decoder.Decode(pixel, 6, 0, plut);

        Assert.Equal(0x9234, result.Pixel);
        Assert.Equal(209, result.Amv);
    }

    [Fact]
    public void Decode_TransparencyUsesBackgroundFlag()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[0] = 0;

        Assert.True(decoder.Decode(0, 1, 0, plut).Transparent);
        Assert.False(decoder.Decode(0, 1, (uint)CcbControl.Background, plut).Transparent);
    }

    [Fact]
    public void Decode_PlutOffsetBitsSelectHighEntries()
    {
        var decoder = new PixelDecoder();
        var plut = CreatePlut();
        plut[16] = 0x4444;
        plut[31] = 0x5555;

        Assert.Equal(0x4444, decoder.Decode(0, 3, 0x8, plut).Pixel);
        Assert.Equal(0x5554, decoder.Decode(3, 2, 0xe, plut).Pixel);
        Assert.Equal(0x5554, decoder.Decode(1, 1, 0xf, plut).Pixel);
    }

    private static ushort[] CreatePlut()
    {
        var plut = new ushort[32];
        for (var i = 0; i < plut.Length; i++)
        {
            plut[i] = (ushort)(i * 0x100);
        }

        return plut;
    }
}
