namespace FiveDo.Madam;

/// <summary>
/// The CEL pixel processor. FreeDO: <c>Madam.cpp: PPROC</c>.
/// </summary>
/// <remarks>
/// <para>
/// The processor combines the pixel decoder output with the existing frame pixel. It
/// selects the input and secondary colour sources from the 16-bit pixel-processor control
/// word (<c>PIXC</c>), applies the scale table (<c>PSCALAR</c>), the optional AV signal,
/// XOR mode, and clipping, and returns a 16-bit RGB555 pixel.
/// </para>
/// <para>
/// The reference keeps the processor mode (<c>PIXC</c>) and the per-CCB pixel-over mode in
/// global state. This port keeps the scale table immutable and takes both values as
/// arguments, so it is safe for the rasterizer and for tests.
/// </para>
/// </remarks>
public static class PixelProcessor
{
    private const uint PixelOverMask = (uint)CcbControl.PixelOverMask;
    private const uint PixelOverZero = 0x00000100;
    private const uint PixelOverOne = 0x00000180;

    private const int ScaleMxfCount = 8;
    private const int ScaleDvCount = 4;
    private const int ScaleValueCount = 32;

    private static readonly byte[] ScaleTable = BuildScaleTable();

    /// <summary>
    /// Processes one CEL pixel. FreeDO: <c>PPROC(pixel, fpix, amv)</c>.
    /// </summary>
    /// <param name="pixel">The pixel decoder output (<c>CURPIX</c>).</param>
    /// <param name="framePixel">The current frame-buffer pixel (<c>fpix</c>).</param>
    /// <param name="amv">The attribute/AMV value from the pixel decoder.</param>
    /// <param name="pixelControl">The 32-bit pixel-processor control register (<c>PIXC</c>).</param>
    /// <param name="ccbFlags">The current CCB flags.</param>
    /// <returns>The processed 16-bit RGB555 pixel.</returns>
    public static ushort Process(ushort pixel, ushort framePixel, ushort amv, uint pixelControl, CcbControl ccbFlags)
    {
        // FreeDO: pproj.pmode is derived from CCBFLAGS before PPROC is called.
        var pixelOverMode = (uint)ccbFlags & PixelOverMask;
        var orMask = pixelOverMode == PixelOverOne ? 0x8000u : 0u;
        var andMask = pixelOverMode != PixelOverZero ? 0xffffu : 0x7fffu;
        var adjustedPixel = (pixel | orMask) & andMask;

        // FreeDO selects the high half of PIXC when the adjusted pixel has bit 15 set.
        var control = (adjustedPixel & 0x8000) != 0
            ? (ushort)(pixelControl >> 16)
            : (ushort)pixelControl;
        var decodedPixel = (ushort)adjustedPixel;
        var dv2 = control & 1;
        var avField = (uint)((control >> 1) & 0x1f);
        var avRaw = ((uint)ccbFlags & (uint)CcbControl.UseAv) != 0
            ? avField
            : 0u;

        var neg = (int)(avRaw & 1);
        var xtend = (avRaw & 2) != 0;
        var noClip = (avRaw & 4) != 0;
        var dv3 = (int)(avRaw >> 3) & 3;

        var s2 = (control >> 6) & 3;
        var dv1 = (control >> 8) & 3;
        var mxf = (control >> 10) & 7;
        var ms = (control >> 13) & 3;
        var s1 = (control >> 15) & 1;

        var input1 = s1 == 0 ? decodedPixel : framePixel;

        var color2 = SelectColor2(s2, avField, dv3, decodedPixel, framePixel);

        var color1 = SelectColor1(ms, mxf, dv1, input1, decodedPixel, amv);

        // FreeDO: AOP.raw = color1.raw & PXOR1; color1.raw &= PXOR2.
        uint aop;
        uint color1Raw;
        if ((ccbFlags & CcbControl.PixelXor) != 0)
        {
            aop = 0;
            color1Raw = color1 & 0x1f1f1f1f;
        }
        else
        {
            aop = color1;
            color1Raw = 0;
        }

        var bop = neg != 0 ? color2 ^ 0x00ffffffu : color2 ^ color1Raw;

        var aopR = (sbyte)(byte)(aop & 0xff);
        var aopB = (sbyte)(byte)((aop >> 8) & 0xff);
        var aopG = (sbyte)(byte)((aop >> 16) & 0xff);

        var bopR = (sbyte)(byte)(bop & 0xff);
        var bopB = (sbyte)(byte)((bop >> 8) & 0xff);
        var bopG = (sbyte)(byte)((bop >> 16) & 0xff);

        if (xtend)
        {
            // FreeDO uses signed char arithmetic: (x << 3) >> 3 sign-extends the low 3 bits.
            bopR = (sbyte)((bopR << 3) >> 3);
            bopB = (sbyte)((bopB << 3) >> 3);
            bopG = (sbyte)((bopG << 3) >> 3);
        }

        // FreeDO stores each result back into a signed char before clipping.
        var outR = (int)(sbyte)((aopR + bopR + neg) >> dv2);
        var outB = (int)(sbyte)((aopB + bopB + neg) >> dv2);
        var outG = (int)(sbyte)((aopG + bopG + neg) >> dv2);

        if (!noClip)
        {
            outR = Clamp5(outR);
            outB = Clamp5(outB);
            outG = Clamp5(outG);
        }

        var output = (uint)(outB & 0x1f)
            | ((uint)(outG & 0x1f) << 5)
            | ((uint)(outR & 0x1f) << 10);

        // FreeDO: if (!(CCBFLAGS & CCB_NOBLK) && out.raw == 0) out.raw = 1 << 10;
        if ((ccbFlags & CcbControl.NoBlock) == 0 && output == 0)
        {
            output = 1u << 10;
        }

        return (ushort)output;
    }

    private static int Clamp5(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 31 ? 31 : value;
    }

    private static uint SelectColor2(int s2, uint avField, int dv3, ushort pixel, ushort framePixel)
    {
        switch (s2)
        {
            case 1:
                var value = (int)(avField >> dv3);
                return (uint)value | ((uint)value << 8) | ((uint)value << 16);
            case 2:
                return ((uint)((framePixel >> 10) & 0x1f) >> dv3)
                    | (((uint)(framePixel & 0x1f) >> dv3) << 8)
                    | (((uint)((framePixel >> 5) & 0x1f) >> dv3) << 16);
            case 3:
                return ((uint)((pixel >> 10) & 0x1f) >> dv3)
                    | (((uint)(pixel & 0x1f) >> dv3) << 8)
                    | (((uint)((pixel >> 5) & 0x1f) >> dv3) << 16);
            default:
                return 0;
        }
    }

    private static uint SelectColor1(int ms, int mxf, int dv1, ushort input1, ushort pixel, ushort amv)
    {
        var r = (input1 >> 10) & 0x1f;
        var g = (input1 >> 5) & 0x1f;
        var b = input1 & 0x1f;

        switch (ms)
        {
            case 0:
                return Scale(mxf, dv1, r)
                    | ((uint)Scale(mxf, dv1, b) << 8)
                    | ((uint)Scale(mxf, dv1, g) << 16);
            case 1:
                return Scale((amv >> 6) & 7, dv1, r)
                    | ((uint)Scale(amv & 7, dv1, b) << 8)
                    | ((uint)Scale((amv >> 3) & 7, dv1, g) << 16);
            case 2:
                var pixelR = (pixel >> 10) & 0x1f;
                var pixelG = (pixel >> 5) & 0x1f;
                var pixelB = pixel & 0x1f;
                return Scale(pixelR >> 2, pixelR & 3, r)
                    | ((uint)Scale(pixelB >> 2, pixelB & 3, b) << 8)
                    | ((uint)Scale(pixelG >> 2, pixelG & 3, g) << 16);
            default:
                return Scale(4, dv1, r)
                    | ((uint)Scale(4, dv1, b) << 8)
                    | ((uint)Scale(4, dv1, g) << 16);
        }
    }

    private static byte Scale(int mxf, int dv1, int value)
    {
        return ScaleTable[((mxf * ScaleDvCount) + dv1) * ScaleValueCount + value];
    }

    private static byte[] BuildScaleTable()
    {
        // FreeDO: for (i = 0; i < 32; i++)
        //             for (j = 0; j < 8; j++)
        //                 for (n = 0; n < 4; n++)
        //                     PSCALAR[j][n][i] = ((i * (j + 1)) >> PDV(n));
        var table = new byte[ScaleMxfCount * ScaleDvCount * ScaleValueCount];
        for (var j = 0; j < ScaleMxfCount; j++)
        {
            for (var n = 0; n < ScaleDvCount; n++)
            {
                var shift = ((n - 1) & 3) + 1;
                for (var i = 0; i < ScaleValueCount; i++)
                {
                    table[((j * ScaleDvCount) + n) * ScaleValueCount + i] = (byte)((i * (j + 1)) >> shift);
                }
            }
        }

        return table;
    }
}
