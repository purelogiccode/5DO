
namespace ThreeDo.Madam;

/// <summary>
/// CEL geometry setup and clipping. FreeDO: <c>Madam.cpp: TestInitVisual</c> and the
/// <c>Init_*_Map</c> helpers.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the CEL origin, deltas, sprite size, clip window, and selected
/// texel function in file-scope globals. This class groups that mutable state so the
/// rasterizer can own one instance and tests can drive it directly.
/// </para>
/// <para>
/// The arithmetic intentionally mirrors the reference, including integer overflow,
/// arithmetic right shifts of negative coordinates, and the retained
/// <see cref="TextureWidthLimit"/> value on line-map paths that do not assign it.
/// </para>
/// </remarks>
public sealed class CelGeometry
{
    private const int FixedHalf = 0x8000;
    private const int FixedOne = 0x10000;

    /// <summary>Gets or sets the CEL X origin in 16.16 fixed point. FreeDO: <c>XPOS1616</c>.</summary>
    public int XPosition1616 { get; set; }

    /// <summary>Gets or sets the CEL Y origin in 16.16 fixed point. FreeDO: <c>YPOS1616</c>.</summary>
    public int YPosition1616 { get; set; }

    /// <summary>Gets or sets the horizontal delta in 16.16 fixed point. FreeDO: <c>HDX1616</c>.</summary>
    public int Hdx1616 { get; set; }

    /// <summary>Gets or sets the horizontal delta Y in 16.16 fixed point. FreeDO: <c>HDY1616</c>.</summary>
    public int Hdy1616 { get; set; }

    /// <summary>Gets or sets the vertical delta X in 16.16 fixed point. FreeDO: <c>VDX1616</c>.</summary>
    public int Vdx1616 { get; set; }

    /// <summary>Gets or sets the vertical delta Y in 16.16 fixed point. FreeDO: <c>VDY1616</c>.</summary>
    public int Vdy1616 { get; set; }

    /// <summary>Gets or sets the perspective horizontal delta. FreeDO: <c>HDDX1616</c>.</summary>
    public int Hddx1616 { get; set; }

    /// <summary>Gets or sets the perspective vertical delta. FreeDO: <c>HDDY1616</c>.</summary>
    public int Hddy1616 { get; set; }

    /// <summary>Gets or sets the sprite width. FreeDO: <c>SPRWI</c>.</summary>
    public int SpriteWidth { get; set; }

    /// <summary>Gets or sets the sprite height. FreeDO: <c>SPRHI</c>.</summary>
    public int SpriteHeight { get; set; }

    /// <summary>Gets or sets the horizontal clip value. FreeDO: <c>CLIPXVAL</c>.</summary>
    public int ClipX { get; set; }

    /// <summary>Gets or sets the vertical clip value. FreeDO: <c>CLIPYVAL</c>.</summary>
    public int ClipY { get; set; }

    /// <summary>Gets or sets the current CCB flags. FreeDO: <c>CCBFLAGS</c>.</summary>
    public CcbControl Flags { get; set; }

    /// <summary>Gets the selected texel function. FreeDO: <c>TEXEL_FUN_NUMBER</c>.</summary>
    public TexelFunction TexelFunction { get; private set; }

    /// <summary>Gets the horizontal start offset. FreeDO: <c>TEXTURE_WI_START</c>.</summary>
    public int TextureWidthStart { get; private set; }

    /// <summary>Gets the vertical start offset. FreeDO: <c>TEXTURE_HI_START</c>.</summary>
    public int TextureHeightStart { get; private set; }

    /// <summary>Gets the horizontal limit. FreeDO: <c>TEXTURE_WI_LIM</c>.</summary>
    public int TextureWidthLimit { get; private set; }

    /// <summary>Gets the vertical limit. FreeDO: <c>TEXTURE_HI_LIM</c>.</summary>
    public int TextureHeightLimit { get; private set; }

    /// <summary>Gets the texel X increment. FreeDO: <c>TEXEL_INCX</c>.</summary>
    public int TexelIncrementX { get; private set; }

    /// <summary>Gets the texel Y increment. FreeDO: <c>TEXEL_INCY</c>.</summary>
    public int TexelIncrementY { get; private set; }

    /// <summary>
    /// Sets up the CEL geometry and selects the rasterizer path. FreeDO: <c>TestInitVisual</c>.
    /// </summary>
    /// <param name="packed">Whether the CEL is packed.</param>
    /// <returns><see langword="true"/> when the CEL is visible; <see langword="false"/> when culled.</returns>
    public bool TryInitVisual(bool packed)
    {
        if ((Flags & (CcbControl.Clockwise | CcbControl.CounterClockwise)) == 0)
        {
            return false;
        }

        if (!packed)
        {
            var x0 = XPosition1616 >> 16;
            var x1 = (XPosition1616 + (Hdx1616 * SpriteWidth)) >> 16;
            var x2 = (XPosition1616 + (Vdx1616 * SpriteHeight)) >> 16;
            var x3 = (XPosition1616 + (Vdx1616 * SpriteHeight)
                + ((Hdx1616 + (Hddx1616 * SpriteHeight)) * SpriteWidth)) >> 16;
            if (x0 < 0 && x1 < 0 && x2 < 0 && x3 < 0)
            {
                return false;
            }

            if (x0 > ClipX && x1 > ClipX && x2 > ClipX && x3 > ClipX)
            {
                return false;
            }

            var y0 = YPosition1616 >> 16;
            var y1 = (YPosition1616 + (Hdy1616 * SpriteWidth)) >> 16;
            var y2 = (YPosition1616 + (Vdy1616 * SpriteHeight)) >> 16;
            var y3 = (YPosition1616 + (Vdy1616 * SpriteHeight)
                + ((Hdy1616 + (Hddy1616 * SpriteHeight)) * SpriteWidth)) >> 16;
            if (y0 < 0 && y1 < 0 && y2 < 0 && y3 < 0)
            {
                return false;
            }

            if (y0 > ClipY && y1 > ClipY && y2 > ClipY && y3 > ClipY)
            {
                return false;
            }
        }
        else
        {
            var x0 = XPosition1616 >> 16;
            var x1 = (XPosition1616 + (Vdx1616 * SpriteHeight)) >> 16;
            if (x0 < 0 && x1 < 0 && Hdx1616 <= 0 && Hddx1616 <= 0)
            {
                return false;
            }

            if (x0 > ClipX && x1 > ClipX && Hdx1616 >= 0 && Hddx1616 >= 0)
            {
                return false;
            }

            var y0 = YPosition1616 >> 16;
            var y1 = (YPosition1616 + (Vdy1616 * SpriteHeight)) >> 16;
            if (y0 < 0 && y1 < 0 && Hdy1616 <= 0 && Hddy1616 <= 0)
            {
                return false;
            }

            if (y0 > ClipY && y1 > ClipY && Hdy1616 >= 0 && Hddy1616 >= 0)
            {
                return false;
            }
        }

        if (Hddx1616 == 0 && Hddy1616 == 0)
        {
            if (Hdx1616 == 0 && Vdy1616 == 0)
            {
                if ((Hdy1616 < 0 && Vdx1616 > 0) || (Hdy1616 > 0 && Vdx1616 < 0))
                {
                    if ((Flags & CcbControl.Clockwise) != 0)
                    {
                        if (Abs(Hdy1616) == FixedOne
                            && Abs(Vdx1616) == FixedOne
                            && ((YPosition1616 | XPosition1616) & 0xffff) == 0)
                        {
                            return InitLineMap();
                        }

                        InitScaleMap();
                        return true;
                    }
                }
                else if ((Flags & CcbControl.CounterClockwise) != 0)
                {
                    if (Abs(Hdy1616) == FixedOne
                        && Abs(Vdx1616) == FixedOne
                        && ((YPosition1616 | XPosition1616) & 0xffff) == 0)
                    {
                        return InitLineMap();
                    }

                    InitScaleMap();
                    return true;
                }

                return false;
            }

            if (Hdy1616 == 0 && Vdx1616 == 0)
            {
                if ((Hdx1616 < 0 && Vdy1616 > 0) || (Hdx1616 > 0 && Vdy1616 < 0))
                {
                    if ((Flags & CcbControl.CounterClockwise) != 0)
                    {
                        if (Abs(Hdx1616) == FixedOne
                            && Abs(Vdy1616) == FixedOne
                            && ((YPosition1616 | XPosition1616) & 0xffff) == 0)
                        {
                            return InitLineMap();
                        }

                        InitScaleMap();
                        return true;
                    }
                }
                else if ((Flags & CcbControl.Clockwise) != 0)
                {
                    if (Abs(Hdx1616) == FixedOne
                        && Abs(Vdy1616) == FixedOne
                        && ((YPosition1616 | XPosition1616) & 0xffff) == 0)
                    {
                        return InitLineMap();
                    }

                    InitScaleMap();
                    return true;
                }

                return false;
            }
        }

        if (QuadCcwTest(packed ? 2048 : SpriteWidth))
        {
            return false;
        }

        InitArbitraryMap();
        return true;
    }

    /// <summary>
    /// Consumes one skipped texture row. FreeDO: the packed rasterizer's
    /// <c>if (TEXTURE_HI_START) { TEXTURE_HI_START--; ... }</c>.
    /// </summary>
    /// <returns><see langword="true"/> when a row was skipped.</returns>
    internal bool ConsumeHeightStart()
    {
        if (TextureHeightStart == 0)
        {
            return false;
        }

        TextureHeightStart--;
        return true;
    }

    private bool InitLineMap()
    {
        TexelFunction = TexelFunction.Line;
        TextureWidthStart = 0;
        TextureHeightStart = 0;
        TextureHeightLimit = SpriteHeight;

        if (Hdx1616 < 0)
        {
            XPosition1616 -= FixedHalf;
        }
        else if (Vdx1616 < 0)
        {
            XPosition1616 -= FixedHalf;
        }

        if (Hdy1616 < 0)
        {
            YPosition1616 -= FixedHalf;
        }
        else if (Vdy1616 < 0)
        {
            YPosition1616 -= FixedHalf;
        }

        if (Vdx1616 < 0)
        {
            if (((XPosition1616 - ((SpriteHeight - 1) << 16)) >> 16) < 0)
            {
                TextureHeightLimit = (XPosition1616 >> 16) + 1;
            }

            if (TextureHeightLimit > SpriteHeight)
            {
                TextureHeightLimit = SpriteHeight;
            }
        }
        else if (Vdx1616 > 0)
        {
            if (((XPosition1616 + (SpriteHeight << 16)) >> 16) > ClipX)
            {
                TextureHeightLimit = ClipX - (XPosition1616 >> 16) + 1;
            }
        }

        if (Vdy1616 < 0)
        {
            if (((YPosition1616 - ((SpriteHeight - 1) << 16)) >> 16) < 0)
            {
                TextureHeightLimit = (YPosition1616 >> 16) + 1;
            }

            if (TextureHeightLimit > SpriteHeight)
            {
                TextureHeightLimit = SpriteHeight;
            }
        }
        else if (Vdy1616 > 0)
        {
            if (((YPosition1616 + (SpriteHeight << 16)) >> 16) > ClipY)
            {
                TextureHeightLimit = ClipY - (YPosition1616 >> 16) + 1;
            }
        }

        if (Hdx1616 < 0)
        {
            TextureWidthLimit = (XPosition1616 >> 16) + 1;
        }
        else if (Hdx1616 > 0)
        {
            TextureWidthLimit = ClipX - (XPosition1616 >> 16) + 1;
        }

        if (Hdy1616 < 0)
        {
            TextureWidthLimit = (YPosition1616 >> 16) + 1;
        }
        else if (Hdy1616 > 0)
        {
            TextureWidthLimit = ClipY - (YPosition1616 >> 16) + 1;
        }

        if (XPosition1616 < 0)
        {
            if (Hdx1616 < 0)
            {
                return false;
            }

            if (Hdx1616 > 0)
            {
                TextureWidthStart = -(XPosition1616 >> 16);
            }

            if (Vdx1616 < 0)
            {
                return false;
            }

            if (Vdx1616 > 0)
            {
                TextureHeightStart = -(XPosition1616 >> 16);
            }
        }
        else if ((XPosition1616 >> 16) > ClipX)
        {
            if (Hdx1616 > 0)
            {
                return false;
            }

            if (Hdx1616 < 0)
            {
                TextureWidthStart = (XPosition1616 >> 16) - ClipX;
            }

            if (Vdx1616 > 0)
            {
                return false;
            }

            if (Vdx1616 < 0)
            {
                TextureHeightStart = (XPosition1616 >> 16) - ClipX;
            }
        }

        if (YPosition1616 < 0)
        {
            if (Hdy1616 < 0)
            {
                return false;
            }

            if (Hdy1616 > 0)
            {
                TextureWidthStart = -(YPosition1616 >> 16);
            }

            if (Vdy1616 < 0)
            {
                return false;
            }

            if (Vdy1616 > 0)
            {
                TextureHeightStart = -(YPosition1616 >> 16);
            }
        }
        else if ((YPosition1616 >> 16) > ClipY)
        {
            if (Hdy1616 > 0)
            {
                return false;
            }

            if (Hdy1616 < 0)
            {
                TextureWidthStart = (YPosition1616 >> 16) - ClipY;
            }

            if (Vdy1616 > 0)
            {
                return false;
            }

            if (Vdy1616 < 0)
            {
                TextureHeightStart = (YPosition1616 >> 16) - ClipY;
            }
        }

        return TextureWidthLimit > 0;
    }

    private void InitScaleMap()
    {
        TexelFunction = TexelFunction.Scale;

        if (Hdx1616 < 0)
        {
            XPosition1616 -= FixedHalf;
        }
        else if (Vdx1616 < 0)
        {
            XPosition1616 -= FixedHalf;
        }

        if (Hdy1616 < 0)
        {
            YPosition1616 -= FixedHalf;
        }
        else if (Vdy1616 < 0)
        {
            YPosition1616 -= FixedHalf;
        }

        var deltaX = Hdx1616 + Vdx1616;
        var deltaY = Hdy1616 + Vdy1616;
        TexelIncrementX = deltaX < 0 ? -1 : 1;
        TexelIncrementY = deltaY < 0 ? -1 : 1;
        TexelIncrementX <<= 2;

        TextureWidthStart = 0;
        TextureHeightStart = 0;
    }

    private void InitArbitraryMap()
    {
        TexelFunction = TexelFunction.Arbitrary;
        TextureWidthStart = 0;
        TextureHeightStart = 0;
    }

    private bool QuadCcwTest(int width)
    {
        if ((Flags & (CcbControl.CounterClockwise | CcbControl.Clockwise))
            == (CcbControl.CounterClockwise | CcbControl.Clockwise))
        {
            return false;
        }

        var tmp = TexelCcwTest(Hdx, Hdy, Vdx, Vdy);
        if (tmp != TexelCcwTest(Hdx, Hdy, Vdx + (Hddx * width), Vdy + (Hddy * width)))
        {
            return false;
        }

        if (tmp != TexelCcwTest(
                Hdx + (Hddx * SpriteHeight),
                Hdy + (Hddy * SpriteHeight),
                Vdx,
                Vdy))
        {
            return false;
        }

        if (tmp != TexelCcwTest(
                Hdx + (Hddx * SpriteHeight),
                Hdy + (Hddy * SpriteHeight),
                Vdx + (Hddx * SpriteHeight * (float)width),
                Vdy + (Hddy * SpriteHeight * (float)width)))
        {
            return false;
        }

        return tmp == (Flags & (CcbControl.CounterClockwise | CcbControl.Clockwise));
    }

    private static CcbControl TexelCcwTest(double hdx, double hdy, double vdx, double vdy)
    {
        return ((hdx + vdx) * (hdy - vdy)) + (vdx * vdy) - (hdx * hdy) < 0.0
            ? CcbControl.CounterClockwise
            : CcbControl.Clockwise;
    }

    private static int Abs(int value)
    {
        return value > 0 ? value : unchecked(-value);
    }

    private double Hdx => Hdx1616 / 65536.0;

    private double Hdy => Hdy1616 / 65536.0;

    private double Vdx => Vdx1616 / 65536.0;

    private double Vdy => Vdy1616 / 65536.0;

    private double Hddx => Hddx1616 / 65536.0;

    private double Hddy => Hddy1616 / 65536.0;
}
