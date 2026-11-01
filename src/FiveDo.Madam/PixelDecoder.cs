using System;
using System.Runtime.CompilerServices;

namespace FiveDo.Madam;

/// <summary>
/// Decodes CEL pixels for 1/2/4/6/8/16 bpp. FreeDO: <c>Madam.cpp: PDEC</c>.
/// </summary>
/// <remarks>
/// The lookup tables are built exactly as in <c>_madam_Init</c>. The decoder is stateless
/// apart from those tables, so the caller supplies the current PLUT span and CCB flags.
/// </remarks>
public sealed class PixelDecoder
{
    private const uint Pre0Linear = 0x10;
    private const uint Pre0BppMask = 0x7;
    private const uint CcbBackground = 0x20;
    private const ushort DefaultAmv = 0x49;

    private readonly ushort[] _mapU8B = new ushort[256];
    private readonly ushort[] _mapC8BAmv = new ushort[256];
    private readonly ushort[] _mapC16BAmv = new ushort[8 * 8 * 8];

    /// <summary>Initializes a new instance of the <see cref="PixelDecoder"/> class.</summary>
    public PixelDecoder()
    {
        BuildTables();
    }

    /// <summary>Decodes one CEL pixel. FreeDO: <c>PDEC</c>.</summary>
    /// <param name="pixel">Raw pixel bits from the CEL bit reader.</param>
    /// <param name="pre0">First preamble word.</param>
    /// <param name="ccbFlags">CCB flags word.</param>
    /// <param name="plut">Current 32-entry PLUT.</param>
    /// <returns>The decoded pixel, AMV, and transparency state.</returns>
    /// <exception cref="ArgumentException">Thrown when the PLUT is too small.</exception>
    public PixelDecodeResult Decode(uint pixel, uint pre0, uint ccbFlags, ReadOnlySpan<ushort> plut)
    {
        if (plut.Length < 32)
        {
            throw new ArgumentException("The PLUT must contain 32 entries.", nameof(plut));
        }

        return DecodeFast(pixel, pre0, ccbFlags, plut);
    }

    /// <summary>
    /// Decodes one CEL pixel without the public-API PLUT length check.
    /// </summary>
    /// <remarks>
    /// The CEL rasterizer owns a 32-entry PLUT and calls this path once per texel, so
    /// the length check is hoisted to its per-CEL pipeline setup. Do not call this
    /// method with a shorter span.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PixelDecodeResult DecodeFast(
        uint pixel,
        uint pre0,
        uint ccbFlags,
        ReadOnlySpan<ushort> plut,
        uint pre1 = 0,
        uint control0 = 0)
    {
        ushort pres;
        ushort amv;

        switch (pre0 & Pre0BppMask)
        {
            case 4:
                pres = (ushort)(plut[(int)(pixel & 0x1f)] & 0x7fff);
                pres = (ushort)(pres + (((pixel >> 5) & 1) << 15));
                amv = DefaultAmv;
                break;
            case 5:
                if ((pre0 & Pre0Linear) != 0)
                {
                    pres = _mapU8B[pixel & 0xff];
                    amv = DefaultAmv;
                }
                else
                {
                    pres = plut[(int)(pixel & 0x1f)];
                    amv = _mapC8BAmv[pixel & 0xff];
                }

                break;
            case 6:
            case 7:
                if ((pre0 & Pre0Linear) != 0)
                {
                    pres = (ushort)pixel;
                    amv = DefaultAmv;
                }
                else
                {
                    pres = (ushort)(plut[(int)(pixel & 0x1f)] & 0x7fff);
                    pres = (ushort)(pres | (pixel & 0x8000));
                    amv = _mapC16BAmv[(pixel >> 5) & 0x1ff];
                }

                break;
            default:
                var pixelBitsMask = (pre0 & Pre0BppMask) switch
                {
                    0 or 1 => 1u,
                    2 => 3u,
                    _ => 15u,
                };
                var plutBits = (pre0 & Pre0BppMask) switch
                {
                    0 or 1 => (ccbFlags & 0xf) * 4,
                    2 => (ccbFlags & 0xe) * 4,
                    _ => (ccbFlags & 0x8) * 4,
                };
                pres = plut[(int)((plutBits + ((pixel & pixelBitsMask) * 2)) >> 1)];
                amv = DefaultAmv;
                break;
        }

        // Opera: transparency is decided before blue-LSB substitution; the
        // projector receives the pre-substitution bit 15/bit 0 pair.
        var rawVhBits = (ushort)(pres & 0x8001);
        var transparent = (pres & 0x7fff) == 0 && (ccbFlags & CcbBackground) == 0;

        var lsbMode = (ccbFlags & (uint)CcbControl.Packed) != 0
            ? (control0 >> 20) & 3u
            : (pre1 >> 12) & 3u;
        pres = SubstituteBlueLsb(pres, lsbMode);

        return new PixelDecodeResult(pres, amv, transparent, rawVhBits);
    }

    private static ushort SubstituteBlueLsb(ushort pixel, uint mode)
    {
        var lsb = (mode & 3) switch
        {
            1 => pixel & 1u,
            2 => (pixel >> 4) & 1u,
            3 => (pixel >> 5) & 1u,
            _ => 0u,
        };

        return (ushort)((pixel & ~1u) | lsb);
    }

    private void BuildTables()
    {
        for (var i = 0; i < 256; i++)
        {
            var b = i & 3;
            var g = (i >> 2) & 7;
            var r = (i >> 5) & 7;
            var blue = (b << 3) + (b << 1) + (b >> 1);
            var green = (g << 2) + (g >> 1);
            var red = (r << 2) + (r >> 1);
            _mapU8B[i] = (ushort)(blue | (green << 5) | (red << 10));

            var m = (i >> 6) & 3;
            var mpw = (i >> 5) & 1;
            var amv = (m << 1) + mpw;
            _mapC8BAmv[i] = (ushort)((amv << 6) + (amv << 3) + amv);
        }

        for (var i = 0; i < 8 * 8 * 8; i++)
        {
            var mb = i & 7;
            var mg = (i >> 3) & 7;
            var mr = (i >> 6) & 7;
            _mapC16BAmv[i] = (ushort)((mr << 6) + (mg << 3) + mb);
        }
    }
}
