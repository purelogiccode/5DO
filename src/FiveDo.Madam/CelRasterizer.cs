using System;
using FiveDo.Core;

namespace FiveDo.Madam;

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
/// <c>RESSCALE</c> is the runtime high-resolution mode flag (0 or 1). When it is set,
/// the CEL engine addresses the four 1 MB VRAM planes that make up a 640x480 frame.
/// </para>
/// </remarks>
internal sealed class CelRasterizer
{
    private static readonly int[] BppTable = { 1, 1, 2, 4, 6, 8, 16, 1 };

    private readonly CelEngine _engine;

    // Per-CEL copies of the engine registers and rasterizer settings. The CPU cannot
    // change these while a CCB is being rasterized, so caching them removes repeated
    // Engine/Madam property reads and modulo recomputation from the per-pixel loops.
    private IRam _ram = null!;
    private uint _pixelSource;
    private uint _frameBufferTarget;
    private int _readModulo;
    private int _writeModulo;
    private CcbControl _ccbFlags;
    private uint _control0;
    private uint _pre1;
    private uint _celOriginVh;
    private uint _pixelControl;
    private uint _pre0;
    private uint _flagsRaw;
    private ushort[] _plut = null!;
    private bool _resScale;
    private bool _trackWork;

    internal CelRasterizer(CelEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <summary>Gets the high-resolution scale: 0 for 320x240, 1 for 640x480.</summary>
    private int ResScale => _engine.Compatibility.HighResolution ? 1 : 0;

    private void PreparePipeline()
    {
        _ram = _engine.Ram;
        _pixelSource = _engine.PixelSource;
        _frameBufferTarget = _engine.FrameBufferTarget;
        _readModulo = _engine.ReadModulo;
        _writeModulo = _engine.WriteModulo;
        _ccbFlags = _engine.Flags;
        _control0 = _engine.Control0;
        _pre1 = _engine.Pre1;
        _celOriginVh = _engine.CelOriginVh;
        _pixelControl = _engine.PixelControl;
        _pre0 = _engine.Pre0;
        _flagsRaw = (uint)_engine.Flags;
        _plut = _engine.Madam.PlutEntries;
        _resScale = ResScale != 0;
        _trackWork = _engine.TrackWork;
    }

    /// <summary>Renders a packed CEL. FreeDO: <c>DrawPackedCel_New</c>.</summary>
    internal void DrawPackedCel()
    {
        CompatibilityState compatibility = _engine.Compatibility;
        compatibility.Sf = 100_000;

        CelGeometry geometry = _engine.Geometry;
        var start = unchecked((int)_engine.DataAddress);
        var rowCount = (int)((_engine.Pre0 & 0xffc0u) >> 6);
        var bpp = BppTable[(int)(_engine.Pre0 & 0x7u)];
        var offsetLength = bpp < 8 ? 1 : 2;
        var skipX = (int)((_engine.Pre0 >> 24) & 0xfu);
        int pixcount;

        geometry.SpriteHeight = rowCount + 1;
        var originX = geometry.XPosition1616;
        var originY = geometry.YPosition1616;
        var originVdx = geometry.Vdx1616;
        var originVdy = geometry.Vdy1616;
        var originHeight = geometry.SpriteHeight;

        if (!PrepareGeometry(true))
        {
            _engine.StoreXYPosFromDrawHeight(originX, originY, originVdx, originVdy, originHeight);
            return;
        }

        PreparePipeline();
        _engine.TraceCel("packed", geometry.SpriteWidth, geometry.SpriteHeight, bpp);

        var xvert = geometry.XPosition1616;
        var yvert = geometry.YPosition1616;
        int xcur;
        int ycur;

        if (geometry.TexelFunction == TexelFunction.Line)
        {
            for (var currentRow = 0; currentRow < geometry.TextureHeightLimit; currentRow++)
            {
                _engine.BitReader.AttachBuffer(unchecked((uint)start));
                var sourceSkip = skipX;
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

                    if (sourceSkip != 0)
                    {
                        pixcount = SkipPackedPixels(type, pixcount, ref sourceSkip, bpp);
                        if (pixcount == 0)
                        {
                            continue;
                        }
                    }

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
                                PixelDecodeResult decoded = DecodePixel(bpp);
                                if (!decoded.Transparent)
                                {
                                    DrawPixel(decoded, xcur, ycur);
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
                            PixelDecodeResult repeat = DecodePixel(bpp);
                            ApplyPackedRepeatSpeedFix(repeat.Pixel);
                            if (!repeat.Transparent)
                            {
                                TexelDrawLine(repeat.Pixel, repeat.RawVhBits, repeat.Amv, xcur, ycur, pixcount);
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

            for (var currentRow = 0; currentRow < geometry.SpriteHeight; currentRow++)
            {
                _engine.BitReader.AttachBuffer(unchecked((uint)start));
                var sourceSkip = skipX;
                var offset = (int)_engine.BitReader.Read((byte)(offsetLength << 3));
                var lastaddr = unchecked(start + ((offset + 2) << 2));
                var eor = false;

                xcur = xvert;
                ycur = yvert;
                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);

                int remaining;
                while (!eor)
                {
                    var type = (int)_engine.BitReader.Read(2);
                    if (unchecked((int)(_engine.BitReader.BytePosition + (uint)start)) >= lastaddr)
                    {
                        type = 0;
                    }

                    remaining = (int)_engine.BitReader.Read(6) + 1;

                    if (sourceSkip != 0)
                    {
                        remaining = SkipPackedPixels(type, remaining, ref sourceSkip, bpp);
                        if (remaining == 0)
                        {
                            continue;
                        }
                    }

                    switch (type)
                    {
                        case 0:
                            eor = true;
                            break;
                        case 1:
                            while (remaining != 0)
                            {
                                remaining--;
                                PixelDecodeResult decoded = DecodePixel(bpp);
                                if (!decoded.Transparent)
                                {
                                    if (TexelDrawScale(
                                            decoded.Pixel,
                                            decoded.RawVhBits,
                                            decoded.Amv,
                                            xcur >> 16,
                                            ycur >> 16,
                                            (xcur + geometry.Hdx1616 + geometry.Vdx1616) >> 16,
                                            (ycur + geometry.Hdy1616 + geometry.Vdy1616) >> 16) != 0)
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
                            PixelDecodeResult repeat = DecodePixel(bpp);
                            if (!repeat.Transparent)
                            {
                                if (TexelDrawScale(
                                        repeat.Pixel,
                                        repeat.RawVhBits,
                                        repeat.Amv,
                                        xcur >> 16,
                                        ycur >> 16,
                                        (xcur + (geometry.Hdx1616 * remaining) + geometry.Vdx1616) >> 16,
                                        (ycur + (geometry.Hdy1616 * remaining) + geometry.Vdy1616) >> 16) != 0)
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
                var sourceSkip = skipX;
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
                int remaining;

                while (!eor)
                {
                    var type = (int)_engine.BitReader.Read(2);
                    if (unchecked((int)(_engine.BitReader.BytePosition + (uint)start)) >= lastaddr)
                    {
                        type = 0;
                    }

                    remaining = (int)_engine.BitReader.Read(6) + 1;

                    if (sourceSkip != 0)
                    {
                        remaining = SkipPackedPixels(type, remaining, ref sourceSkip, bpp);
                        if (remaining == 0)
                        {
                            continue;
                        }
                    }

                    switch (type)
                    {
                        case 0:
                            eor = true;
                            break;
                        case 1:
                            while (remaining != 0)
                            {
                                PixelDecodeResult decoded = DecodePixel(bpp);
                                remaining--;
                                if (!decoded.Transparent)
                                {
                                    if (TexelDrawArbitrary(
                                            decoded.Pixel,
                                            decoded.RawVhBits,
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
                            PixelDecodeResult repeat = DecodePixel(bpp);
                            ApplyArbitraryRepeatSpeedFix(repeat.Pixel);
                            if (!repeat.Transparent)
                            {
                                while (remaining != 0)
                                {
                                    remaining--;
                                    if (TexelDrawArbitrary(
                                            repeat.Pixel,
                                            repeat.RawVhBits,
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
        _engine.StoreXYPosFromDrawHeight(originX, originY, originVdx, originVdy, originHeight);
    }

    /// <summary>Renders a literal CEL. FreeDO: <c>DrawLiteralCel_New</c>.</summary>
    internal void DrawLiteralCel()
    {
        CompatibilityState compatibility = _engine.Compatibility;
        compatibility.Sf = 100_000;

        CelGeometry geometry = _engine.Geometry;
        var bpp = BppTable[(int)(_engine.Pre0 & 0x7u)];
        var offsetLength = bpp < 8 ? 1 : 2;
        var offset = offsetLength == 1
            ? (int)((_engine.Pre1 & 0xff000000u) >> 24)
            : (int)((_engine.Pre1 & 0x03ff0000u) >> 16);

        var spriteWidth = 1 + (int)(_engine.Pre1 & 0x7ffu);
        var lrForm = (_engine.Pre1 & (uint)CcbPreamble1.LrForm) != 0;
        var skipX = (int)((_engine.Pre0 >> 24) & 0xfu);
        geometry.SpriteWidth = spriteWidth;
        geometry.SpriteHeight = (int)((_engine.Pre0 & 0xffc0u) >> 6) + 1;
        if (lrForm)
        {
            // Opera: LRFORM describes a source row-pair layout. A 1-bpp LRFORM
            // CEL is used as a priority mask over LR-format bitmap data.
            geometry.SpriteHeight <<= 1;
        }

        var originX = geometry.XPosition1616;
        var originY = geometry.YPosition1616;
        var originVdx = geometry.Vdx1616;
        var originVdy = geometry.Vdy1616;
        var originHeight = geometry.SpriteHeight;

        if (skipX >= spriteWidth)
        {
            // Opera: SKIPX that exhausts the span draws nothing but still
            // advances the CEL origin by the full sprite height.
            var height = geometry.SpriteHeight;
            geometry.Hdx1616 = unchecked(geometry.Hdx1616 + (geometry.Hddx1616 * height));
            geometry.Hdy1616 = unchecked(geometry.Hdy1616 + (geometry.Hddy1616 * height));
            _engine.StoreXYPosFromDrawHeight(originX, originY, originVdx, originVdy, originHeight);
            return;
        }

        if (!PrepareGeometry(false))
        {
            _engine.StoreXYPosFromDrawHeight(originX, originY, originVdx, originVdy, originHeight);
            return;
        }

        PreparePipeline();
        _engine.TraceCel("literal", spriteWidth, geometry.SpriteHeight, bpp);
        var xvert = geometry.XPosition1616;
        var yvert = geometry.YPosition1616;
        int xcur;
        int ycur;
        var rowBytes = (offset + 2) << 2;
        var sourceBase = unchecked((int)_engine.DataAddress);

        if (geometry.TexelFunction == TexelFunction.Line)
        {
            compatibility.Sdf = 100_000;
            spriteWidth -= skipX;
            geometry.SpriteWidth = spriteWidth;
            xvert = unchecked(xvert + (geometry.TextureHeightStart * geometry.Vdx1616));
            yvert = unchecked(yvert + (geometry.TextureHeightStart * geometry.Vdy1616));
            if (spriteWidth > geometry.TextureWidthLimit)
            {
                spriteWidth = geometry.TextureWidthLimit;
                geometry.SpriteWidth = spriteWidth;
            }

            for (var i = geometry.TextureHeightStart; i < geometry.TextureHeightLimit; i++)
            {
                var rowAddress = lrForm
                    ? sourceBase + Xy2Off(0, i, rowBytes)
                    : sourceBase + (rowBytes * i);
                _engine.BitReader.AttachBuffer(unchecked((uint)rowAddress));
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
                    PixelDecodeResult decoded = lrForm ? DecodePixelLr(bpp) : DecodePixel(bpp);
                    if (!decoded.Transparent)
                    {
                        DrawPixel(decoded, xcur, ycur);
                    }

                    xcur = unchecked(xcur + geometry.Hdx1616);
                    ycur = unchecked(ycur + geometry.Hdy1616);
                }
            }
        }
        else if (geometry.TexelFunction == TexelFunction.Scale)
        {
            spriteWidth -= skipX;
            geometry.SpriteWidth = spriteWidth;

            for (var i = 0; i < geometry.SpriteHeight; i++)
            {
                var rowAddress = lrForm
                    ? sourceBase + Xy2Off(0, i, rowBytes)
                    : sourceBase + (rowBytes * i);
                _engine.BitReader.AttachBuffer(unchecked((uint)rowAddress));
                xcur = xvert;
                ycur = yvert;
                xvert = unchecked(xvert + geometry.Vdx1616);
                yvert = unchecked(yvert + geometry.Vdy1616);
                _engine.BitReader.Skip((uint)(bpp * skipX));

                for (var j = 0; j < spriteWidth; j++)
                {
                    PixelDecodeResult decoded = lrForm ? DecodePixelLr(bpp) : DecodePixel(bpp);
                    if (!decoded.Transparent)
                    {
                        if (TexelDrawScale(
                                decoded.Pixel,
                                decoded.RawVhBits,
                                decoded.Amv,
                                xcur >> 16,
                                ycur >> 16,
                                (xcur + geometry.Hdx1616 + geometry.Vdx1616) >> 16,
                                (ycur + geometry.Hdy1616 + geometry.Vdy1616) >> 16) != 0)
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
            spriteWidth -= skipX;
            geometry.SpriteWidth = spriteWidth;

            var texels = 0L;
            var trace = _engine.TraceWriter is not null;
            for (var i = 0; i < geometry.SpriteHeight; i++)
            {
                var rowAddress = lrForm
                    ? sourceBase + Xy2Off(0, i, rowBytes)
                    : sourceBase + (rowBytes * i);
                _engine.BitReader.AttachBuffer(unchecked((uint)rowAddress));
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
                    if (trace && (texels++ & 0x3ffff) == 0)
                    {
                        _engine.TraceProgress(texels, i, j);
                    }

                    PixelDecodeResult decoded = lrForm ? DecodePixelLr(bpp) : DecodePixel(bpp);
                    if (!decoded.Transparent)
                    {
                        if (TexelDrawArbitrary(
                                decoded.Pixel,
                                decoded.RawVhBits,
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
            }
        }

        _engine.StoreXYPosFromDrawHeight(originX, originY, originVdx, originVdy, originHeight);
    }

    /// <summary>Renders a 16-bit LR-form CEL. FreeDO: <c>DrawLRCel_New</c>.</summary>
    internal void DrawLrCel()
    {
        CompatibilityState compatibility = _engine.Compatibility;
        compatibility.Sf = 100_000;

        CelGeometry geometry = _engine.Geometry;
        var bpp = BppTable[(int)(_engine.Pre0 & 0x7u)];
        var offset = bpp < 8
            ? (int)((_engine.Pre1 & 0xff000000u) >> 24)
            : (int)((_engine.Pre1 & 0x03ff0000u) >> 16);
        offset += 2;

        var spriteWidth = 1 + (int)(_engine.Pre1 & 0x7ffu);
        geometry.SpriteWidth = spriteWidth;
        geometry.SpriteHeight = (((int)((_engine.Pre0 & 0xffc0u) >> 6)) << 1) + 2;
        var originX = geometry.XPosition1616;
        var originY = geometry.YPosition1616;
        var originVdx = geometry.Vdx1616;
        var originVdy = geometry.Vdy1616;
        var originHeight = geometry.SpriteHeight;

        if (!PrepareGeometry(false))
        {
            _engine.StoreXYPosFromDrawHeight(originX, originY, originVdx, originVdy, originHeight);
            return;
        }

        PreparePipeline();
        _engine.TraceCel("lr", spriteWidth, geometry.SpriteHeight, bpp);
        var data = unchecked((int)_engine.DataAddress);
        var xvert = geometry.XPosition1616;
        var yvert = geometry.YPosition1616;
        int xcur;
        int ycur;

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
                    PixelDecodeResult decoded = _engine.Decoder.DecodeFast(raw, _pre0, _flagsRaw, _plut, _pre1, _control0);
                    if (!decoded.Transparent)
                    {
                        DrawPixel(decoded, xcur, ycur);
                    }

                    xcur = unchecked(xcur + geometry.Hdx1616);
                    ycur = unchecked(ycur + geometry.Hdy1616);
                }
            }
        }
        else if (geometry.TexelFunction == TexelFunction.Scale)
        {
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
                    PixelDecodeResult decoded = _engine.Decoder.DecodeFast(raw, _pre0, _flagsRaw, _plut, _pre1, _control0);
                    if (!decoded.Transparent)
                    {
                        if (TexelDrawScale(
                                decoded.Pixel,
                                decoded.RawVhBits,
                                decoded.Amv,
                                xcur >> 16,
                                ycur >> 16,
                                (xcur + geometry.Hdx1616 + geometry.Vdx1616) >> 16,
                                (ycur + geometry.Hdy1616 + geometry.Vdy1616) >> 16) != 0)
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
                    PixelDecodeResult decoded = _engine.Decoder.DecodeFast(raw, _pre0, _flagsRaw, _plut, _pre1, _control0);
                    if (!decoded.Transparent)
                    {
                        if (TexelDrawArbitrary(
                                decoded.Pixel,
                                decoded.RawVhBits,
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

        _engine.StoreXYPosFromDrawHeight(originX, originY, originVdx, originVdy, originHeight);
    }

    private int SkipPackedPixels(int type, int pixelCount, ref int skipPixels, int bpp)
    {
        if (skipPixels == 0 || pixelCount == 0 || type == 0)
        {
            return pixelCount;
        }

        var skipped = Math.Min(skipPixels, pixelCount);
        if (type == 1)
        {
            _engine.BitReader.Skip((uint)(bpp * skipped));
        }
        else if (type == 3 && skipped == pixelCount)
        {
            // A repeat packet stores one source pixel for the whole run.
            _engine.BitReader.Skip((uint)bpp);
        }

        skipPixels -= skipped;
        return pixelCount - skipped;
    }

    /// <summary>Draws one texel row segment. FreeDO: <c>TexelDraw_Line</c>.</summary>
    internal int TexelDrawLine(ushort curPix, ushort rawVhBits, ushort lamv, int xcur, int ycur, int count)
    {
        CelGeometry geometry = _engine.Geometry;
        xcur >>= 16;
        ycur >>= 16;

        uint currentFramePixel = 0xffffffffu;
        uint pixel = 0;
        for (var i = 0; i < count; i++, xcur = unchecked(xcur + (geometry.Hdx1616 >> 16)), ycur = unchecked(ycur + (geometry.Hdy1616 >> 16)))
        {
            var next = _resScale
                ? ReadPixel(_pixelSource, xcur, ycur, _readModulo)
                : _engine.Memory.ReadHalfword(Address(
                    _pixelSource,
                    Xy2Off(unchecked(xcur << 2), ycur, _readModulo)));
            if (next != currentFramePixel)
            {
                currentFramePixel = next;
                pixel = ProcessPixel(curPix, lamv, next);
            }

            pixel = Projector.Apply(
                rawVhBits,
                (ushort)pixel,
                next,
                _ccbFlags,
                _control0,
                _pre1,
                _celOriginVh);

            if (_resScale)
            {
                WritePixel(_frameBufferTarget, xcur, ycur, (ushort)pixel, _writeModulo);
            }
            else
            {
                _engine.Memory.WriteHalfword(
                    Address(
                        _frameBufferTarget,
                        Xy2Off(unchecked(xcur << 2), ycur, _writeModulo)),
                    (ushort)pixel);
            }
        }

        return 0;
    }

    /// <summary>Draws a scaled texel rectangle. FreeDO: <c>TexelDraw_Scale</c>.</summary>
    internal int TexelDrawScale(ushort curPix, ushort rawVhBits, ushort lamv, int xcur, int ycur, int deltax, int deltay)
    {
        CelGeometry geometry = _engine.Geometry;

        if ((_engine.Compatibility.FixMode & FixMode.BitTiming3) != FixMode.None)
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

        var maxX = _resScale ? ((_engine.ClipX + 1) << 1) - 1 : _engine.ClipX;
        var maxY = _resScale ? ((_engine.ClipY + 1) << 1) - 1 : _engine.ClipY;

        if (geometry.Hdx1616 > 0 && deltax > maxX && xcur > maxX)
        {
            return -1;
        }

        if (geometry.Hdy1616 > 0 && deltay > maxY && ycur > maxY)
        {
            return -1;
        }

        if (xcur == deltax)
        {
            return 0;
        }

        for (var i = ycur; i != deltay; i = unchecked(i + geometry.TexelIncrementY))
        {
            if (!_resScale)
            {
                var readAddress = Xy2Off(xcur << 2, i, _readModulo);
                var writeAddress = Xy2Off(xcur << 2, i, _writeModulo);
                var pixels = 0;
                for (var j = unchecked(xcur << 2); j != unchecked(deltax << 2); j = unchecked(j + geometry.TexelIncrementX))
                {
                    if (TestClip(unchecked(j << 14), unchecked(i << 16)))
                    {
                        var framePixel = _ram.Read16(unchecked(_pixelSource + (uint)readAddress) ^ 2);
                        var pixel = ProcessPixel(curPix, lamv, framePixel);
                        pixel = Projector.Apply(
                            rawVhBits,
                            pixel,
                            framePixel,
                            _ccbFlags,
                            _control0,
                            _pre1,
                            _celOriginVh);
                        _ram.Write16(unchecked(_frameBufferTarget + (uint)writeAddress) ^ 2, pixel);
                        if (_trackWork)
                        {
                            _engine.AddPixels(1);
                        }
                        pixels++;

                        if ((_ccbFlags & CcbControl.Maria) != CcbControl.None)
                        {
                            // The early return still consumed one read/write pair.
                            _engine.Memory.AddCycles(3);
                            return 0;
                        }
                    }

                    readAddress = unchecked(readAddress + geometry.TexelIncrementX);
                    writeAddress = unchecked(writeAddress + geometry.TexelIncrementX);
                }

                // One halfword read plus one halfword write per pixel: 1 + 2 CEL cycles.
                _engine.Memory.AddCycles(pixels * 3);
                continue;
            }

            for (var j = unchecked(xcur << 2); j != unchecked(deltax << 2); j = unchecked(j + geometry.TexelIncrementX))
            {
                if (!TestClip(unchecked(j << 14), unchecked(i << 16)))
                {
                    continue;
                }

                var framePixel = ReadPixel(_pixelSource, j >> 2, i, _readModulo);
                var pixel = ProcessPixel(curPix, lamv, framePixel);
                pixel = Projector.Apply(
                    rawVhBits,
                    pixel,
                    framePixel,
                    _ccbFlags,
                    _control0,
                    _pre1,
                    _celOriginVh);
                WritePixel(_frameBufferTarget, j >> 2, i, pixel, _writeModulo);
                if ((_ccbFlags & CcbControl.Maria) != CcbControl.None)
                {
                    return 0;
                }
            }
        }

        return 0;
    }

    /// <summary>Fills an arbitrary texel quad. FreeDO: <c>TexelDraw_Arbitrary</c>.</summary>
    internal int TexelDrawArbitrary(
        ushort curPix,
        ushort rawVhBits,
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
        CelGeometry geometry = _engine.Geometry;
        if (_trackWork)
        {
            _engine.AddTexel();
        }
        if (_engine.TraceWriter is not null)
        {
            _engine.TraceArbitrary(xA, yA, xB, yB, xC, yC, xD, yD);
        }

        var scaleShift = _resScale ? 15 : 16;
        xA >>= scaleShift;
        xB >>= scaleShift;
        xC >>= scaleShift;
        xD >>= scaleShift;
        yA >>= scaleShift;
        yB >>= scaleShift;
        yC >>= scaleShift;
        yD >>= scaleShift;

        if (xA == xB && xB == xC && xC == xD)
        {
            return 0;
        }

        var maxXt = _resScale ? (_engine.ClipX + 1) << 1 : _engine.ClipX + 1;
        var maxYt = _resScale ? (_engine.ClipY + 1) << 1 : _engine.ClipY + 1;

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
        int maxx;

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
                && (((_ccbFlags & CcbControl.Clockwise) != CcbControl.None && updowns[2] == 0)
                    || ((_ccbFlags & CcbControl.CounterClockwise) != CcbControl.None && updowns[2] == 1)))
            {
                var j = xpoints[2];
                if (j < 0)
                {
                    j = 0;
                }

                // Opera: the span endpoint is inclusive. FreeDO stopped one
                // texel short, which clipped the last column of arbitrary CELs.
                maxx = xpoints[3] + 1;
                if (maxx > maxXt)
                {
                    maxx = maxXt;
                }

                FillArbitrarySpan(curPix, rawVhBits, lamv, i, j, maxx, ref currentFramePixel, ref pixel);
                if ((_ccbFlags & CcbControl.Maria) != CcbControl.None)
                {
                    return 0;
                }
            }

            if (((_ccbFlags & CcbControl.Clockwise) != CcbControl.None && updowns[0] == 0)
                || ((_ccbFlags & CcbControl.CounterClockwise) != CcbControl.None && updowns[0] == 1))
            {
                var j = xpoints[0];
                if (j < 0)
                {
                    j = 0;
                }

                maxx = xpoints[1] + 1;
                if (maxx > maxXt)
                {
                    maxx = maxXt;
                }

                FillArbitrarySpan(curPix, rawVhBits, lamv, i, j, maxx, ref currentFramePixel, ref pixel);
                if ((_ccbFlags & CcbControl.Maria) != CcbControl.None)
                {
                    return 0;
                }
            }
        }

        return 0;
    }

    private void FillArbitrarySpan(
        ushort curPix,
        ushort rawVhBits,
        ushort lamv,
        int y,
        int startX,
        int endX,
        ref uint currentFramePixel,
        ref uint pixel)
    {
        if (!_resScale)
        {
            // Consecutive x pixels are four bytes apart in VRAM. Keep the row base
            // addresses as counters so the inner loop is a load/blend/store sequence
            // without a multiply, shifts, or engine property reads per pixel.
            var readAddress = Xy2Off(unchecked(startX << 2), y, _writeModulo);
            var writeAddress = readAddress;
            for (var x = startX; x < endX; x++)
            {
                var next = _ram.Read16(unchecked(_pixelSource + (uint)readAddress) ^ 2);
                if (next != currentFramePixel)
                {
                    currentFramePixel = next;
                    pixel = ProcessPixel(curPix, lamv, next);
                    pixel = Projector.Apply(
                        rawVhBits,
                        (ushort)pixel,
                        next,
                        _ccbFlags,
                        _control0,
                        _pre1,
                        _celOriginVh);
                }

                _ram.Write16(unchecked(_frameBufferTarget + (uint)writeAddress) ^ 2, (ushort)pixel);
                if (_trackWork)
                {
                    _engine.AddPixels(1);
                }

                if ((_ccbFlags & CcbControl.Maria) != CcbControl.None)
                {
                    return;
                }

                readAddress += 4;
                writeAddress += 4;
            }

            return;
        }

        for (var x = startX; x < endX; x++)
        {
            var next = ReadPixel(_pixelSource, x, y);
            if (next != currentFramePixel)
            {
                currentFramePixel = next;
                pixel = ProcessPixel(curPix, lamv, next);
                pixel = Projector.Apply(
                    rawVhBits,
                    (ushort)pixel,
                    next,
                    _ccbFlags,
                    _control0,
                    _pre1,
                    _celOriginVh);
            }

            WritePixel(_frameBufferTarget, x, y, (ushort)pixel);
            if ((_ccbFlags & CcbControl.Maria) != CcbControl.None)
            {
                return;
            }
        }
    }

    private void ApplyPackedRepeatSpeedFix(ushort curPix)
    {
        CompatibilityState compatibility = _engine.Compatibility;
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
        CompatibilityState compatibility = _engine.Compatibility;
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
        CelGeometry geometry = _engine.Geometry;
        geometry.Flags = _engine.Flags;
        geometry.ClipX = _engine.ClipX;
        geometry.ClipY = _engine.ClipY;
        return geometry.TryInitVisual(packed);
    }

    private PixelDecodeResult DecodePixel(int bpp)
    {
        var raw = _engine.BitReader.Read((byte)bpp);
        return _engine.Decoder.DecodeFast(raw, _pre0, _flagsRaw, _plut, _pre1, _control0);
    }

    private PixelDecodeResult DecodePixelLr(int bpp)
    {
        var raw = _engine.BitReader.ReadLrForm((byte)bpp);
        return _engine.Decoder.DecodeFast(raw, _pre0, _flagsRaw, _plut, _pre1, _control0);
    }

    private ushort ProcessPixel(ushort pixel, ushort amv, ushort framePixel)
    {
        return PixelProcessor.Process(
            pixel,
            framePixel,
            amv,
            _pixelControl,
            _ccbFlags);
    }

    private void DrawPixel(PixelDecodeResult decoded, int xcur, int ycur)
    {
        var x = xcur >> 16;
        var readY = ycur >> 16;
        var writeY = ycur >> 16;

        ushort framePixel;
        if (_resScale)
        {
            framePixel = ReadPixel(_pixelSource, x, readY, _readModulo);
        }
        else
        {
            framePixel = _engine.Memory.ReadHalfword(Address(
                _pixelSource,
                Xy2Off(unchecked(x << 2), readY, _readModulo)));
        }

        var pixel = ProcessPixel(decoded.Pixel, decoded.Amv, framePixel);
        pixel = Projector.Apply(
            decoded.RawVhBits,
            pixel,
            framePixel,
            _ccbFlags,
            _control0,
            _pre1,
            _celOriginVh);

        if (_resScale)
        {
            WritePixel(_frameBufferTarget, x, writeY, pixel, _writeModulo);
        }
        else
        {
            _engine.Memory.WriteHalfword(
                Address(
                    _frameBufferTarget,
                    Xy2Off(unchecked(x << 2), writeY, _writeModulo)),
                pixel);
        }
    }

    private ushort ReadPixel(uint source, int x, int y)
    {
        return ReadPixel(source, x, y, _writeModulo);
    }

    private ushort ReadPixel(uint source, int x, int y, int modulo)
    {
        var offset = Xy2Off((x >> ResScale) << 2, y, modulo);
        if (_resScale)
        {
            // Field 0 uses planes 0/1; the odd X column is in plane 1.
            var address = unchecked((source + (uint)offset) ^ 2);
            return _ram.Read16(address + (uint)((x & 1) * EmulatorConstants.VramSize));
        }

        return _ram.Read16(unchecked(source + (uint)offset) ^ 2);
    }

    private void WritePixel(uint target, int x, int y, ushort value)
    {
        WritePixel(target, x, y, value, _writeModulo);
    }

    private void WritePixel(uint target, int x, int y, ushort value, int modulo)
    {
        if (_trackWork)
        {
            _engine.AddPixels(1);
        }

        var offset = Xy2Off((x >> ResScale) << 2, y, modulo);
        if (_resScale)
        {
            // X is already in 640-wide space; Y is a 240-line scanline that the VDLP
            // line-doubles. Write both interlace fields so both output lines match.
            var plane = x & 1;
            var address = unchecked((target + (uint)offset) ^ 2);
            _ram.Write16(address + (uint)(plane * EmulatorConstants.VramSize), value);
            _ram.Write16(address + (uint)((plane + 2) * EmulatorConstants.VramSize), value);
            return;
        }

        _ram.Write16(unchecked(target + (uint)offset) ^ 2, value);
    }

    private bool TestClip(int clipX, int clipY)
    {
        var maxX = _resScale ? ((_engine.ClipX + 1) << 1) - 1 : _engine.ClipX;
        var maxY = _resScale ? ((_engine.ClipY + 1) << 1) - 1 : _engine.ClipY;
        return clipX >= 0
            && clipX <= (maxX << 16)
            && clipY >= 0
            && clipY <= (maxY << 16);
    }

    private static int Xy2Off(int a, int b, int c)
    {
        // FreeDO: (((int)b>>1)*c) + (((int)b&1)<<1) + a.
        return unchecked(((b >> 1) * c) + ((b & 1) << 1) + a);
    }

    private static int Divide(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static uint Address(uint baseAddress, int offset)
    {
        return unchecked(baseAddress + (uint)offset);
    }
}
