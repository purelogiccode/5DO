using System;
using ThreeDo.Core;

namespace ThreeDo.Madam;

/// <summary>
/// The software CEL rasterizers and texel primitives. FreeDO: <c>Madam.cpp:
/// DrawPackedCel_New</c>, <c>DrawLiteralCel_New</c>, <c>DrawLRCel_New</c>,
/// <c>TexelDraw_Line</c>, <c>TexelDraw_Scale</c>, and <c>TexelDraw_Arbitrary</c>.
/// </summary>
/// <remarks>
/// <para>
/// The port keeps the reference's integer arithmetic, signed shifts, and clipping order.
/// The <c>speedfixes</c>/<c>unknownflag11</c>/<c>sdf</c> heuristics are compatibility state
/// and are applied exactly as the reference applies them.
/// </para>
/// <para>
/// <c>RESSCALE</c> is zero in v1 (see M4.16); the high-resolution mirror paths are
/// deliberately omitted until M11.2.
/// </para>
/// </remarks>
internal sealed class CelRasterizer
{
    private const int ResScale = 0;
    private const int FixedOne = 0x10000;
    private static readonly int[] BppTable = { 1, 1, 2, 4, 6, 8, 16, 1 };

    private readonly CelEngine _engine;

    internal CelRasterizer(CelEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <summary>Renders a packed CEL. FreeDO: <c>DrawPackedCel_New</c>.</summary>
    internal void DrawPackedCel()
    {
        var compatibility = _engine.Compatibility;
        compatibility.Sf = 100_000;

        var geometry = _engine.Geometry;
        var start = unchecked((int)_engine.DataAddress);
        var rowCount = (int)((_engine.Pre0 & 0xffc0u) >> 6);
        var bpp = BppTable[(int)(_engine.Pre0 & 0x7u)];
        var offsetLength = bpp < 8 ? 1 : 2;
        var pixcount = 0;

        geometry.SpriteHeight = rowCount + 1;

        if (!PrepareGeometry(true))
        {
            return;
        }

        _engine.TraceCel("packed", geometry.SpriteWidth, geometry.SpriteHeight, bpp);

        var xvert = geometry.XPosition1616;
        var yvert = geometry.YPosition1616;
        var xcur = xvert;
        var ycur = yvert;

        if (geometry.TexelFunction == TexelFunction.Line)
        {
            for (var currentRow = 0; currentRow < geometry.TextureHeightLimit; currentRow++)
            {
                _engine.BitReader.AttachBuffer(unchecked((uint)start));
                var offset = (int)_engine.BitReader.Read((byte)(offsetLength << 3));
                var lastaddr = unchecked(start + ((offset + 2) << 2));
                var eor = false;

                xcur = xvert;
                ycur = yvert;
                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);

                if (geometry.ConsumeHeightStart())
                {
                    start = lastaddr;
                    continue;
                }

                var scipw = geometry.TextureWidthStart;
                var wcnt = scipw;
                while (!eor)
                {
                    var type = (int)_engine.BitReader.Read(2);
                    if (unchecked((int)(_engine.BitReader.BytePosition + (uint)start)) >= lastaddr)
                    {
                        type = 0;
                    }

                    pixcount = (int)_engine.BitReader.Read(6) + 1;

                    if (scipw != 0)
                    {
                        if (type == 0)
                        {
                            break;
                        }

                        if (scipw >= pixcount)
                        {
                            scipw -= pixcount;
                            if (geometry.Hdx1616 != 0)
                            {
                                xcur = unchecked(xcur + (geometry.Hdx1616 * pixcount));
                            }

                            if (geometry.Hdy1616 != 0)
                            {
                                ycur = unchecked(ycur + (geometry.Hdy1616 * pixcount));
                            }

                            if (type == 1)
                            {
                                _engine.BitReader.Skip((uint)(bpp * pixcount));
                            }
                            else if (type == 3)
                            {
                                _engine.BitReader.Skip((uint)bpp);
                            }

                            continue;
                        }

                        if (geometry.Hdx1616 != 0)
                        {
                            xcur = unchecked(xcur + (geometry.Hdx1616 * scipw));
                        }

                        if (geometry.Hdy1616 != 0)
                        {
                            ycur = unchecked(ycur + (geometry.Hdy1616 * scipw));
                        }

                        pixcount -= scipw;
                        if (type == 1)
                        {
                            _engine.BitReader.Skip((uint)(bpp * scipw));
                        }

                        scipw = 0;
                    }

                    wcnt += pixcount;
                    if (wcnt > geometry.TextureWidthLimit)
                    {
                        pixcount -= wcnt - geometry.TextureWidthLimit;
                    }

                    switch (type)
                    {
                        case 0:
                            eor = true;
                            break;
                        case 1:
                            for (var pix = 0; pix < pixcount; pix++)
                            {
                                var decoded = DecodePixel(bpp);
                                if (!decoded.Transparent)
                                {
                                    DrawPixel(decoded, xcur, ycur, false);
                                }

                                xcur = unchecked(xcur + geometry.Hdx1616);
                                ycur = unchecked(ycur + geometry.Hdy1616);
                            }

                            break;
                        case 2:
                            if (geometry.Hdx1616 != 0)
                            {
                                xcur = unchecked(xcur + (geometry.Hdx1616 * pixcount));
                            }

                            if (geometry.Hdy1616 != 0)
                            {
                                ycur = unchecked(ycur + (geometry.Hdy1616 * pixcount));
                            }

                            break;
                        case 3:
                            var repeat = DecodePixel(bpp);
                            ApplyPackedRepeatSpeedFix(repeat.Pixel);
                            if (!repeat.Transparent)
                            {
                                TexelDrawLine(repeat.Pixel, repeat.Amv, xcur, ycur, pixcount);
                            }

                            if (geometry.Hdx1616 != 0)
                            {
                                xcur = unchecked(xcur + (geometry.Hdx1616 * pixcount));
                            }

                            if (geometry.Hdy1616 != 0)
                            {
                                ycur = unchecked(ycur + (geometry.Hdy1616 * pixcount));
                            }

                            break;
                    }

                    if (wcnt >= geometry.TextureWidthLimit)
                    {
                        break;
                    }
                }

                start = lastaddr;
            }
        }
        else if (geometry.TexelFunction == TexelFunction.Scale)
        {
            compatibility.UnknownFlag11 = 100_000;

            var drawHeight = geometry.Vdy1616;
            if ((_engine.Flags & CcbControl.Maria) != 0 && drawHeight > FixedOne)
            {
                drawHeight = FixedOne;
            }

            for (var currentRow = 0; currentRow < geometry.SpriteHeight; currentRow++)
            {
                _engine.BitReader.AttachBuffer(unchecked((uint)start));
                var offset = (int)_engine.BitReader.Read((byte)(offsetLength << 3));
                var lastaddr = unchecked(start + ((offset + 2) << 2));
                var eor = false;

                xcur = xvert;
                ycur = yvert;
                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);

                var remaining = 0;
                while (!eor)
                {
                    var type = (int)_engine.BitReader.Read(2);
                    if (unchecked((int)(_engine.BitReader.BytePosition + (uint)start)) >= lastaddr)
                    {
                        type = 0;
                    }

                    remaining = (int)_engine.BitReader.Read(6) + 1;

                    switch (type)
                    {
                        case 0:
                            eor = true;
                            break;
                        case 1:
                            while (remaining != 0)
                            {
                                remaining--;
                                var decoded = DecodePixel(bpp);
                                if (!decoded.Transparent)
                                {
                                    if (TexelDrawScale(
                                            decoded.Pixel,
                                            decoded.Amv,
                                            xcur >> 16,
                                            ycur >> 16,
                                            (xcur + geometry.Hdx1616 + geometry.Vdx1616) >> 16,
                                            (ycur + geometry.Hdy1616 + drawHeight) >> 16) != 0)
                                    {
                                        break;
                                    }
                                }

                                xcur = unchecked(xcur + geometry.Hdx1616);
                                ycur = unchecked(ycur + geometry.Hdy1616);
                            }

                            break;
                        case 2:
                            xcur = unchecked(xcur + (geometry.Hdx1616 * remaining));
                            ycur = unchecked(ycur + (geometry.Hdy1616 * remaining));
                            remaining = 0;
                            break;
                        case 3:
                            var repeat = DecodePixel(bpp);
                            if (!repeat.Transparent)
                            {
                                if (TexelDrawScale(
                                        repeat.Pixel,
                                        repeat.Amv,
                                        xcur >> 16,
                                        ycur >> 16,
                                        (xcur + (geometry.Hdx1616 * remaining) + geometry.Vdx1616) >> 16,
                                        (ycur + (geometry.Hdy1616 * remaining) + drawHeight) >> 16) != 0)
                                {
                                    break;
                                }
                            }

                            xcur = unchecked(xcur + (geometry.Hdx1616 * remaining));
                            ycur = unchecked(ycur + (geometry.Hdy1616 * remaining));
                            remaining = 0;
                            break;
                    }

                    if (remaining != 0)
                    {
                        break;
                    }
                }

                start = lastaddr;
            }
        }
        else
        {
            if (compatibility.SpeedFixes >= 0 && compatibility.SpeedFixes <= 100_001)
            {
                compatibility.SpeedFixes = 100_000;
            }

            for (var currentRow = 0; currentRow < geometry.SpriteHeight; currentRow++)
            {
                _engine.BitReader.AttachBuffer(unchecked((uint)start));
                var offset = (int)_engine.BitReader.Read((byte)(offsetLength << 3));
                var lastaddr = unchecked(start + ((offset + 2) << 2));
                var eor = false;

                xcur = xvert;
                ycur = yvert;
                var hdx = geometry.Hdx1616;
                var hdy = geometry.Hdy1616;

                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);
                geometry.Hdx1616 = unchecked(geometry.Hdx1616 + geometry.Hddx1616);
                geometry.Hdy1616 = unchecked(geometry.Hdy1616 + geometry.Hddy1616);

                var xdown = xvert;
                var ydown = yvert;
                var remaining = 0;

                while (!eor)
                {
                    var type = (int)_engine.BitReader.Read(2);
                    if (unchecked((int)(_engine.BitReader.BytePosition + (uint)start)) >= lastaddr)
                    {
                        type = 0;
                    }

                    remaining = (int)_engine.BitReader.Read(6) + 1;

                    switch (type)
                    {
                        case 0:
                            eor = true;
                            break;
                        case 1:
                            while (remaining != 0)
                            {
                                var decoded = DecodePixel(bpp);
                                remaining--;
                                if (!decoded.Transparent)
                                {
                                    if (TexelDrawArbitrary(
                                            decoded.Pixel,
                                            decoded.Amv,
                                            xcur,
                                            ycur,
                                            unchecked(xcur + hdx),
                                            unchecked(ycur + hdy),
                                            unchecked(xdown + geometry.Hdx1616),
                                            unchecked(ydown + geometry.Hdy1616),
                                            xdown,
                                            ydown) != 0)
                                    {
                                        break;
                                    }
                                }

                                xcur = unchecked(xcur + hdx);
                                ycur = unchecked(ycur + hdy);
                                xdown = unchecked(xdown + geometry.Hdx1616);
                                ydown = unchecked(ydown + geometry.Hdy1616);
                            }

                            break;
                        case 2:
                            if (compatibility.SpeedFixes >= 0 && compatibility.Sdf > 0)
                            {
                                compatibility.SpeedFixes = 300_000;
                            }

                            xcur = unchecked(xcur + (hdx * remaining));
                            ycur = unchecked(ycur + (hdy * remaining));
                            xdown = unchecked(xdown + (geometry.Hdx1616 * remaining));
                            ydown = unchecked(ydown + (geometry.Hdy1616 * remaining));
                            remaining = 0;
                            break;
                        case 3:
                            var repeat = DecodePixel(bpp);
                            ApplyArbitraryRepeatSpeedFix(repeat.Pixel);
                            if (!repeat.Transparent)
                            {
                                while (remaining != 0)
                                {
                                    remaining--;
                                    if (TexelDrawArbitrary(
                                            repeat.Pixel,
                                            repeat.Amv,
                                            xcur,
                                            ycur,
                                            unchecked(xcur + hdx),
                                            unchecked(ycur + hdy),
                                            unchecked(xdown + geometry.Hdx1616),
                                            unchecked(ydown + geometry.Hdy1616),
                                            xdown,
                                            ydown) != 0)
                                    {
                                        break;
                                    }

                                    xcur = unchecked(xcur + hdx);
                                    ycur = unchecked(ycur + hdy);
                                    xdown = unchecked(xdown + geometry.Hdx1616);
                                    ydown = unchecked(ydown + geometry.Hdy1616);
                                }
                            }
                            else
                            {
                                xcur = unchecked(xcur + (hdx * remaining));
                                ycur = unchecked(ycur + (hdy * remaining));
                                xdown = unchecked(xdown + (geometry.Hdx1616 * remaining));
                                ydown = unchecked(ydown + (geometry.Hdy1616 * remaining));
                                remaining = 0;
                            }

                            break;
                    }

                    if (remaining != 0)
                    {
                        break;
                    }
                }

                start = lastaddr;
            }
        }

        geometry.SpriteWidth++;
        FinalizeVisualPosition(xcur, ycur);
    }

    /// <summary>Renders a literal CEL. FreeDO: <c>DrawLiteralCel_New</c>.</summary>
    internal void DrawLiteralCel()
    {
        var compatibility = _engine.Compatibility;
        compatibility.Sf = 100_000;

        var geometry = _engine.Geometry;
        var bpp = BppTable[(int)(_engine.Pre0 & 0x7u)];
        var offsetLength = bpp < 8 ? 1 : 2;
        var offset = offsetLength == 1
            ? (int)((_engine.Pre1 & 0xff000000u) >> 24)
            : (int)((_engine.Pre1 & 0x03ff0000u) >> 16);

        var spriteWidth = 1 + (int)(_engine.Pre1 & 0x7ffu);
        geometry.SpriteWidth = spriteWidth;
        geometry.SpriteHeight = (int)((_engine.Pre0 & 0xffc0u) >> 6) + 1;

        if (!PrepareGeometry(false))
        {
            return;
        }

        _engine.TraceCel("literal", spriteWidth, geometry.SpriteHeight, bpp);
        var xvert = geometry.XPosition1616;
        var yvert = geometry.YPosition1616;
        var xcur = xvert;
        var ycur = yvert;
        var skipX = (int)((_engine.Pre0 >> 24) & 0xfu);
        var data = unchecked((int)_engine.DataAddress);

        if (geometry.TexelFunction == TexelFunction.Line)
        {
            compatibility.Sdf = 100_000;
            spriteWidth -= skipX;
            geometry.SpriteWidth = spriteWidth;
            xvert = unchecked(xvert + (geometry.TextureHeightStart * geometry.Vdx1616));
            yvert = unchecked(yvert + (geometry.TextureHeightStart * geometry.Vdy1616));
            data = unchecked(data + (((offset + 2) << 2) * geometry.TextureHeightStart));

            if (spriteWidth > geometry.TextureWidthLimit)
            {
                spriteWidth = geometry.TextureWidthLimit;
                geometry.SpriteWidth = spriteWidth;
            }

            for (var i = geometry.TextureHeightStart; i < geometry.TextureHeightLimit; i++)
            {
                _engine.BitReader.AttachBuffer(unchecked((uint)data));
                xcur = unchecked(xvert + (geometry.TextureWidthStart * geometry.Hdx1616));
                ycur = unchecked(yvert + (geometry.TextureWidthStart * geometry.Hdy1616));
                _engine.BitReader.Skip((uint)(bpp * skipX));
                if (geometry.TextureWidthStart != 0)
                {
                    _engine.BitReader.Skip((uint)(bpp * geometry.TextureWidthStart));
                }

                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);

                for (var j = geometry.TextureWidthStart; j < spriteWidth; j++)
                {
                    var decoded = DecodePixel(bpp);
                    if (!decoded.Transparent)
                    {
                        DrawPixel(decoded, xcur, ycur, (compatibility.FixMode & FixMode.BitTiming6) != 0);
                    }

                    xcur = unchecked(xcur + geometry.Hdx1616);
                    ycur = unchecked(ycur + geometry.Hdy1616);
                }

                data = unchecked(data + ((offset + 2) << 2));
            }
        }
        else if (geometry.TexelFunction == TexelFunction.Scale)
        {
            spriteWidth -= skipX;
            geometry.SpriteWidth = spriteWidth;

            var drawHeight = geometry.Vdy1616;
            if ((_engine.Flags & CcbControl.Maria) != 0 && drawHeight > FixedOne)
            {
                drawHeight = FixedOne;
            }

            for (var i = 0; i < geometry.SpriteHeight; i++)
            {
                _engine.BitReader.AttachBuffer(unchecked((uint)data));
                xcur = xvert;
                ycur = yvert;
                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);
                _engine.BitReader.Skip((uint)(bpp * skipX));

                for (var j = 0; j < spriteWidth; j++)
                {
                    var decoded = DecodePixel(bpp);
                    if (!decoded.Transparent)
                    {
                        if (TexelDrawScale(
                                decoded.Pixel,
                                decoded.Amv,
                                xcur >> 16,
                                ycur >> 16,
                                (xcur + geometry.Hdx1616 + geometry.Vdx1616) >> 16,
                                (ycur + geometry.Hdy1616 + drawHeight) >> 16) != 0)
                        {
                            break;
                        }
                    }

                    xcur = unchecked(xcur + geometry.Hdx1616);
                    ycur = unchecked(ycur + geometry.Hdy1616);
                }

                data = unchecked(data + ((offset + 2) << 2));
            }
        }
        else
        {
            spriteWidth -= skipX;
            geometry.SpriteWidth = spriteWidth;

            var texels = 0L;
            for (var i = 0; i < geometry.SpriteHeight; i++)
            {
                _engine.BitReader.AttachBuffer(unchecked((uint)data));
                xcur = xvert;
                ycur = yvert;
                var hdx = geometry.Hdx1616;
                var hdy = geometry.Hdy1616;

                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);
                geometry.Hdx1616 = unchecked(geometry.Hdx1616 + geometry.Hddx1616);
                geometry.Hdy1616 = unchecked(geometry.Hdy1616 + geometry.Hddy1616);
                _engine.BitReader.Skip((uint)(bpp * skipX));

                var xdown = xvert;
                var ydown = yvert;

                for (var j = 0; j < spriteWidth; j++)
                {
                    if ((texels++ & 0x3ffff) == 0)
                    {
                        _engine.TraceProgress(texels, i, j);
                    }

                    var decoded = DecodePixel(bpp);
                    if (!decoded.Transparent)
                    {
                        if (TexelDrawArbitrary(
                                decoded.Pixel,
                                decoded.Amv,
                                xcur,
                                ycur,
                                unchecked(xcur + hdx),
                                unchecked(ycur + hdy),
                                unchecked(xdown + geometry.Hdx1616),
                                unchecked(ydown + geometry.Hdy1616),
                                xdown,
                                ydown) != 0)
                        {
                            break;
                        }

                        if (compatibility.SpeedFixes < 1
                            || (compatibility.SpeedFixes >= 0 && compatibility.SpeedFixes < 200_001))
                        {
                            compatibility.SpeedFixes = decoded.Pixel > 30000 && decoded.Pixel < 40000
                                ? 0
                                : -100_000;
                        }
                    }

                    xcur = unchecked(xcur + hdx);
                    ycur = unchecked(ycur + hdy);
                    xdown = unchecked(xdown + geometry.Hdx1616);
                    ydown = unchecked(ydown + geometry.Hdy1616);
                }

                data = unchecked(data + ((offset + 2) << 2));
            }
        }

        FinalizeVisualPosition(xcur, ycur);
    }

    /// <summary>Renders a 16-bit LR-form CEL. FreeDO: <c>DrawLRCel_New</c>.</summary>
    internal void DrawLrCel()
    {
        var compatibility = _engine.Compatibility;
        compatibility.Sf = 100_000;

        var geometry = _engine.Geometry;
        var bpp = BppTable[(int)(_engine.Pre0 & 0x7u)];
        var offset = bpp < 8
            ? (int)((_engine.Pre1 & 0xff000000u) >> 24)
            : (int)((_engine.Pre1 & 0x03ff0000u) >> 16);
        offset += 2;

        var spriteWidth = 1 + (int)(_engine.Pre1 & 0x7ffu);
        geometry.SpriteWidth = spriteWidth;
        geometry.SpriteHeight = (((int)((_engine.Pre0 & 0xffc0u) >> 6)) << 1) + 2;

        if (!PrepareGeometry(false))
        {
            return;
        }

        _engine.TraceCel("lr", spriteWidth, geometry.SpriteHeight, bpp);
        var data = unchecked((int)_engine.DataAddress);
        var xvert = geometry.XPosition1616;
        var yvert = geometry.YPosition1616;
        var xcur = xvert;
        var ycur = yvert;

        if (geometry.TexelFunction == TexelFunction.Line)
        {
            xvert = unchecked(xvert + (geometry.TextureHeightStart * geometry.Vdx1616));
            yvert = unchecked(yvert + (geometry.TextureHeightStart * geometry.Vdy1616));
            if (spriteWidth > geometry.TextureWidthLimit)
            {
                spriteWidth = geometry.TextureWidthLimit;
                geometry.SpriteWidth = spriteWidth;
            }

            for (var i = geometry.TextureHeightStart; i < geometry.TextureHeightLimit; i++)
            {
                xcur = unchecked(xvert + (geometry.TextureWidthStart * geometry.Hdx1616));
                ycur = unchecked(yvert + (geometry.TextureWidthStart * geometry.Hdy1616));
                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);

                for (var j = geometry.TextureWidthStart; j < spriteWidth; j++)
                {
                    var raw = _engine.Memory.ReadHalfword(Address(
                        unchecked((uint)data),
                        Xy2Off(j << 2, i, offset << 2)));
                    var decoded = _engine.Decoder.Decode(
                        raw,
                        _engine.Pre0,
                        (uint)_engine.Flags,
                        _engine.Madam.Plut);
                    if (!decoded.Transparent)
                    {
                        DrawPixel(decoded, xcur, ycur, false);
                    }

                    xcur = unchecked(xcur + geometry.Hdx1616);
                    ycur = unchecked(ycur + geometry.Hdy1616);
                }
            }
        }
        else if (geometry.TexelFunction == TexelFunction.Scale)
        {
            var drawHeight = geometry.Vdy1616;
            if ((_engine.Flags & CcbControl.Maria) != 0 && drawHeight > FixedOne)
            {
                drawHeight = FixedOne;
            }

            for (var i = 0; i < geometry.SpriteHeight; i++)
            {
                xcur = xvert;
                ycur = yvert;
                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);

                for (var j = 0; j < spriteWidth; j++)
                {
                    var raw = _engine.Memory.ReadHalfword(Address(
                        unchecked((uint)data),
                        Xy2Off(j << 2, i, offset << 2)));
                    var decoded = _engine.Decoder.Decode(
                        raw,
                        _engine.Pre0,
                        (uint)_engine.Flags,
                        _engine.Madam.Plut);
                    if (!decoded.Transparent)
                    {
                        if (TexelDrawScale(
                                decoded.Pixel,
                                decoded.Amv,
                                xcur >> 16,
                                ycur >> 16,
                                (xcur + geometry.Hdx1616 + geometry.Vdx1616) >> 16,
                                (ycur + geometry.Hdy1616 + drawHeight) >> 16) != 0)
                        {
                            break;
                        }
                    }

                    xcur = unchecked(xcur + geometry.Hdx1616);
                    ycur = unchecked(ycur + geometry.Hdy1616);
                }
            }
        }
        else
        {
            for (var i = 0; i < geometry.SpriteHeight; i++)
            {
                xcur = xvert;
                ycur = yvert;
                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);
                var xdown = xvert;
                var ydown = yvert;
                var hdx = geometry.Hdx1616;
                var hdy = geometry.Hdy1616;
                geometry.Hdx1616 = unchecked(geometry.Hdx1616 + geometry.Hddx1616);
                geometry.Hdy1616 = unchecked(geometry.Hdy1616 + geometry.Hddy1616);

                for (var j = 0; j < spriteWidth; j++)
                {
                    var raw = _engine.Memory.ReadHalfword(Address(
                        unchecked((uint)data),
                        Xy2Off(j << 2, i, offset << 2)));
                    var decoded = _engine.Decoder.Decode(
                        raw,
                        _engine.Pre0,
                        (uint)_engine.Flags,
                        _engine.Madam.Plut);
                    if (!decoded.Transparent)
                    {
                        if (TexelDrawArbitrary(
                                decoded.Pixel,
                                decoded.Amv,
                                xcur,
                                ycur,
                                unchecked(xcur + hdx),
                                unchecked(ycur + hdy),
                                unchecked(xdown + geometry.Hdx1616),
                                unchecked(ydown + geometry.Hdy1616),
                                xdown,
                                ydown) != 0)
                        {
                            break;
                        }
                    }

                    xcur = unchecked(xcur + hdx);
                    ycur = unchecked(ycur + hdy);
                    xdown = unchecked(xdown + geometry.Hdx1616);
                    ydown = unchecked(ydown + geometry.Hdy1616);
                }
            }
        }

        FinalizeVisualPosition(xcur, ycur);
    }

    /// <summary>Draws one texel row segment. FreeDO: <c>TexelDraw_Line</c>.</summary>
    internal int TexelDrawLine(ushort curPix, ushort lamv, int xcur, int ycur, int count)
    {
        var geometry = _engine.Geometry;
        xcur >>= 16;
        ycur >>= 16;

        uint currentFramePixel = 0xffffffffu;
        uint pixel = 0;
        for (var i = 0; i < count; i++, xcur = unchecked(xcur + (geometry.Hdx1616 >> 16)), ycur = unchecked(ycur + (geometry.Hdy1616 >> 16)))
        {
            var next = _engine.Memory.ReadHalfword(Address(
                _engine.PixelSource,
                Xy2Off(unchecked(xcur << 2), ycur, _engine.ReadModulo)));
            if (next != currentFramePixel)
            {
                currentFramePixel = next;
                pixel = ProcessPixel(curPix, lamv, next);
            }

            pixel = Projector.Apply(
                curPix,
                (ushort)pixel,
                next,
                _engine.Flags,
                _engine.Control0,
                _engine.Pre1,
                _engine.CelOriginVh);

            _engine.Memory.WriteHalfword(
                Address(
                    _engine.FrameBufferTarget,
                    Xy2Off(unchecked(xcur << 2), ycur, _engine.WriteModulo)),
                (ushort)pixel);
        }

        return 0;
    }

    /// <summary>Draws a scaled texel rectangle. FreeDO: <c>TexelDraw_Scale</c>.</summary>
    internal int TexelDrawScale(ushort curPix, ushort lamv, int xcur, int ycur, int deltax, int deltay)
    {
        var geometry = _engine.Geometry;

        if ((_engine.Compatibility.FixMode & FixMode.BitTiming3) != 0)
        {
            deltay *= 5;
            ycur *= 5;
        }

        if (geometry.Hdx1616 < 0 && deltax < 0 && xcur < 0)
        {
            return -1;
        }

        if (geometry.Hdy1616 < 0 && deltay < 0 && ycur < 0)
        {
            return -1;
        }

        if (geometry.Hdx1616 > 0 && deltax > _engine.ClipX && xcur > _engine.ClipX)
        {
            return -1;
        }

        if (geometry.Hdy1616 > 0 && deltay > _engine.ClipY && ycur > _engine.ClipY)
        {
            return -1;
        }

        if (xcur == deltax)
        {
            return 0;
        }

        for (var i = ycur; i != deltay; i = unchecked(i + geometry.TexelIncrementY))
        {
            for (var j = unchecked(xcur << 2); j != unchecked(deltax << 2); j = unchecked(j + geometry.TexelIncrementX))
            {
                if (!TestClip(unchecked(j << 14), unchecked(i << 16)))
                {
                    continue;
                }

                var framePixel = _engine.Memory.ReadHalfword(Address(
                    _engine.PixelSource,
                    Xy2Off(j, i, _engine.ReadModulo)));
                var pixel = ProcessPixel(curPix, lamv, framePixel);
                pixel = Projector.Apply(
                    curPix,
                    pixel,
                    framePixel,
                    _engine.Flags,
                    _engine.Control0,
                    _engine.Pre1,
                    _engine.CelOriginVh);
                _engine.Memory.WriteHalfword(
                    Address(
                        _engine.FrameBufferTarget,
                        Xy2Off(j, i, _engine.WriteModulo)),
                    pixel);
            }
        }

        return 0;
    }

    /// <summary>Fills an arbitrary texel quad. FreeDO: <c>TexelDraw_Arbitrary</c>.</summary>
    internal int TexelDrawArbitrary(
        ushort curPix,
        ushort lamv,
        int xA,
        int yA,
        int xB,
        int yB,
        int xC,
        int yC,
        int xD,
        int yD)
    {
        var geometry = _engine.Geometry;
        _engine.TraceArbitrary(xA, yA, xB, yB, xC, yC, xD, yD);
        xA >>= 16 - ResScale;
        xB >>= 16 - ResScale;
        xC >>= 16 - ResScale;
        xD >>= 16 - ResScale;
        yA >>= 16 - ResScale;
        yB >>= 16 - ResScale;
        yC >>= 16 - ResScale;
        yD >>= 16 - ResScale;

        if (xA == xB && xB == xC && xC == xD)
        {
            return 0;
        }

        var maxXt = (_engine.ClipX + 1) << ResScale;
        var maxYt = (_engine.ClipY + 1) << ResScale;

        if (geometry.Hdx1616 < 0 && geometry.Hddx1616 < 0
            && xA < 0 && xB < 0 && xC < 0 && xD < 0)
        {
            return -1;
        }

        if (geometry.Hdx1616 > 0 && geometry.Hddx1616 > 0
            && xA >= maxXt && xB >= maxXt && xC >= maxXt && xD >= maxXt)
        {
            return -1;
        }

        if (geometry.Hdy1616 < 0 && geometry.Hddy1616 < 0
            && yA < 0 && yB < 0 && yC < 0 && yD < 0)
        {
            return -1;
        }

        if (geometry.Hdy1616 > 0 && geometry.Hddy1616 > 0
            && yA >= maxYt && yB >= maxYt && yC >= maxYt && yD >= maxYt)
        {
            return -1;
        }

        var miny = yA;
        var maxy = yA;
        if (miny > yB)
        {
            miny = yB;
        }

        if (miny > yC)
        {
            miny = yC;
        }

        if (miny > yD)
        {
            miny = yD;
        }

        if (maxy < yB)
        {
            maxy = yB;
        }

        if (maxy < yC)
        {
            maxy = yC;
        }

        if (maxy < yD)
        {
            maxy = yD;
        }

        var i = miny;
        if (i < 0)
        {
            i = 0;
        }

        if (maxy < maxYt)
        {
            maxYt = maxy;
        }

        Span<int> xpoints = stackalloc int[4];
        Span<int> updowns = stackalloc int[4];
        var currentFramePixel = 0xffffffffu;
        uint pixel = 0;
        var maxx = 0;

        for (; i < maxYt; i++)
        {
            var cntCross = 0;
            if (i < yB && i >= yA)
            {
                xpoints[cntCross] = Divide((xB - xA) * (i - yA), yB - yA) + xA;
                updowns[cntCross++] = 1;
            }
            else if (i >= yB && i < yA)
            {
                xpoints[cntCross] = Divide((xA - xB) * (i - yB), yA - yB) + xB;
                updowns[cntCross++] = 0;
            }

            if (i < yC && i >= yB)
            {
                xpoints[cntCross] = Divide((xC - xB) * (i - yB), yC - yB) + xB;
                updowns[cntCross++] = 1;
            }
            else if (i >= yC && i < yB)
            {
                xpoints[cntCross] = Divide((xB - xC) * (i - yC), yB - yC) + xC;
                updowns[cntCross++] = 0;
            }

            if (i < yD && i >= yC)
            {
                xpoints[cntCross] = Divide((xD - xC) * (i - yC), yD - yC) + xC;
                updowns[cntCross++] = 1;
            }
            else if (i >= yD && i < yC)
            {
                xpoints[cntCross] = Divide((xC - xD) * (i - yD), yC - yD) + xD;
                updowns[cntCross++] = 0;
            }

            if ((cntCross & 1) != 0)
            {
                if (i < yA && i >= yD)
                {
                    xpoints[cntCross] = Divide((xA - xD) * (i - yD), yA - yD) + xD;
                    updowns[cntCross] = 1;
                }
                else if (i >= yA && i < yD)
                {
                    xpoints[cntCross] = Divide((xD - xA) * (i - yA), yD - yA) + xA;
                    updowns[cntCross] = 0;
                }
            }

            if (cntCross == 0)
            {
                continue;
            }

            if (xpoints[0] > xpoints[1])
            {
                var swap = xpoints[0];
                xpoints[0] = xpoints[1];
                xpoints[1] = swap;

                var swapUpDown = updowns[0];
                updowns[0] = updowns[1];
                updowns[1] = swapUpDown;
            }

            if (cntCross > 2)
            {
                if (xpoints[1] > xpoints[2])
                {
                    (xpoints[1], xpoints[2]) = (xpoints[2], xpoints[1]);
                    (updowns[1], updowns[2]) = (updowns[2], updowns[1]);

                    if (xpoints[0] > xpoints[1])
                    {
                        (xpoints[0], xpoints[1]) = (xpoints[1], xpoints[0]);
                        (updowns[0], updowns[1]) = (updowns[1], updowns[0]);
                    }
                }

                if (xpoints[2] > xpoints[3])
                {
                    (xpoints[2], xpoints[3]) = (xpoints[3], xpoints[2]);
                    (updowns[2], updowns[3]) = (updowns[3], updowns[2]);

                    if (xpoints[1] > xpoints[2])
                    {
                        (xpoints[1], xpoints[2]) = (xpoints[2], xpoints[1]);
                        (updowns[1], updowns[2]) = (updowns[2], updowns[1]);

                        if (xpoints[0] > xpoints[1])
                        {
                            (xpoints[0], xpoints[1]) = (xpoints[1], xpoints[0]);
                            (updowns[0], updowns[1]) = (updowns[1], updowns[0]);
                        }
                    }
                }
            }

            if (cntCross > 2
                && (((_engine.Flags & CcbControl.Clockwise) != 0 && updowns[2] == 0)
                    || ((_engine.Flags & CcbControl.CounterClockwise) != 0 && updowns[2] == 1)))
            {
                var j = xpoints[2];
                if (j < 0)
                {
                    j = 0;
                }

                maxx = xpoints[3];
                if (maxx > maxXt)
                {
                    maxx = maxXt;
                }

                for (; j < maxx; j++)
                {
                    var next = ReadPixel(_engine.PixelSource, i, j);
                    if (next != currentFramePixel)
                    {
                        currentFramePixel = next;
                        pixel = ProcessPixel(curPix, lamv, next);
                        pixel = Projector.Apply(
                            curPix,
                            (ushort)pixel,
                            next,
                            _engine.Flags,
                            _engine.Control0,
                            _engine.Pre1,
                            _engine.CelOriginVh);
                    }

                    WritePixel(_engine.FrameBufferTarget, i, j, (ushort)pixel);
                }
            }

            if (((_engine.Flags & CcbControl.Clockwise) != 0 && updowns[0] == 0)
                || ((_engine.Flags & CcbControl.CounterClockwise) != 0 && updowns[0] == 1))
            {
                var j = xpoints[0];
                if (j < 0)
                {
                    j = 0;
                }

                maxx = xpoints[1];
                if (maxx > maxXt)
                {
                    maxx = maxXt;
                }

                for (; j < maxx; j++)
                {
                    var next = ReadPixel(_engine.PixelSource, i, j);
                    if (next != currentFramePixel)
                    {
                        currentFramePixel = next;
                        pixel = ProcessPixel(curPix, lamv, next);
                        pixel = Projector.Apply(
                            curPix,
                            (ushort)pixel,
                            next,
                            _engine.Flags,
                            _engine.Control0,
                            _engine.Pre1,
                            _engine.CelOriginVh);
                    }

                    WritePixel(_engine.FrameBufferTarget, i, j, (ushort)pixel);
                }
            }
        }

        return 0;
    }

    private void ApplyPackedRepeatSpeedFix(ushort curPix)
    {
        var compatibility = _engine.Compatibility;
        if (curPix > 32300 && curPix < 33500 && (curPix > 32760 || curPix < 32750))
        {
            if (compatibility.SpeedFixes >= 0 && compatibility.Sdf == 0
                && compatibility.SpeedFixes <= 200_001 && compatibility.UnknownFlag11 == 0)
            {
                compatibility.SpeedFixes = 200_000;
            }
        }

        if (compatibility.UnknownFlag11 > 0 && compatibility.Sdf == 0
            && curPix < 30000 && curPix > 29000)
        {
            compatibility.SpeedFixes = -200_000;
        }
    }

    private void ApplyArbitraryRepeatSpeedFix(ushort curPix)
    {
        var compatibility = _engine.Compatibility;
        if (compatibility.SpeedFixes >= 0
            && compatibility.SpeedFixes < 200_001
            && curPix > 10000
            && curPix < 11000
            && compatibility.Sdf == 0)
        {
            compatibility.SpeedFixes = 200_000;
        }
    }

    private bool PrepareGeometry(bool packed)
    {
        var geometry = _engine.Geometry;
        geometry.Flags = _engine.Flags;
        geometry.ClipX = _engine.ClipX;
        geometry.ClipY = _engine.ClipY;
        return geometry.TryInitVisual(packed);
    }

    private PixelDecodeResult DecodePixel(int bpp)
    {
        var raw = _engine.BitReader.Read((byte)bpp);
        return _engine.Decoder.Decode(raw, _engine.Pre0, (uint)_engine.Flags, _engine.Madam.Plut);
    }

    private ushort ProcessPixel(ushort pixel, ushort amv, ushort framePixel)
    {
        return PixelProcessor.Process(
            pixel,
            framePixel,
            amv,
            _engine.PixelControl,
            _engine.Flags);
    }

    private void DrawPixel(PixelDecodeResult decoded, int xcur, int ycur, bool doubleY)
    {
        var y = doubleY ? unchecked((ycur >> 16) << 1) : ycur >> 16;
        var framePixel = _engine.Memory.ReadHalfword(Address(
            _engine.PixelSource,
            Xy2Off(unchecked((xcur >> 16) << 2), y, _engine.ReadModulo)));
        var pixel = ProcessPixel(decoded.Pixel, decoded.Amv, framePixel);
        pixel = Projector.Apply(
            decoded.Pixel,
            pixel,
            framePixel,
            _engine.Flags,
            _engine.Control0,
            _engine.Pre1,
            _engine.CelOriginVh);
        _engine.Memory.WriteHalfword(
            Address(
                _engine.FrameBufferTarget,
                Xy2Off(unchecked((xcur >> 16) << 2), ycur >> 16, _engine.WriteModulo)),
            pixel);
    }

    private ushort ReadPixel(uint source, int x, int y)
    {
        var offset = Xy2Off((x >> ResScale) << 2, y >> ResScale, _engine.WriteModulo);
        return _engine.Ram.Read16(unchecked(source + (uint)offset) ^ 2);
    }

    private void WritePixel(uint target, int x, int y, ushort value)
    {
        var offset = Xy2Off((x >> ResScale) << 2, y >> ResScale, _engine.WriteModulo);
        _engine.Ram.Write16(unchecked(target + (uint)offset) ^ 2, value);
    }

    private bool TestClip(int clipX, int clipY)
    {
        return clipX >= 0
            && clipX <= (_engine.ClipX << 16)
            && clipY >= 0
            && clipY <= (_engine.ClipY << 16);
    }

    private void FinalizeVisualPosition(int xcur, int ycur)
    {
        var geometry = _engine.Geometry;
        if ((_engine.Compatibility.FixMode & FixMode.GraphicsStepY) != 0)
        {
            geometry.YPosition1616 = ycur;
        }
        else
        {
            geometry.XPosition1616 = xcur;
        }
    }

    private static int Xy2Off(int a, int b, int c)
    {
        // FreeDO: (((int)b>>1)*c) + (((int)b&1)<<1) + a.
        return unchecked(((b >> 1) * c) + ((b & 1) << 1) + a);
    }

    private static int Divide(int numerator, int denominator) => denominator == 0 ? 0 : numerator / denominator;

    private static uint Address(uint baseAddress, int offset) => unchecked(baseAddress + (uint)offset);
}
