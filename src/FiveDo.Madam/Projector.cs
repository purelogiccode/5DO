
namespace FiveDo.Madam;

/// <summary>
/// The CEL pixel projector. FreeDO: <c>Madam.cpp: PPROJ_OUTPUT</c>.
/// </summary>
/// <remarks>
/// The projector takes the low H/V bits from the pixel decoder, the CEL origin, or the
/// pixel processor, applies the optional H/V swap, and then overwrites bit 15 and bit 0
/// according to the <c>B15POS</c> and <c>B0POS</c> fields of <c>CCBCTL0</c>. All other
/// bits come from the pixel-processor output. The reference's <c>CFBDSUB</c> path is
/// disabled and is deliberately not implemented.
/// </remarks>
public static class Projector
{
    /// <summary>
    /// Projects one processed pixel. FreeDO: <c>PPROJ_OUTPUT(pdec_output, pproc_output, pframe_input)</c>.
    /// </summary>
    /// <param name="decoderOutput">The pixel decoder output (<c>CURPIX</c>).</param>
    /// <param name="processorOutput">The pixel processor output.</param>
    /// <param name="framePixel">The current frame-buffer pixel (<c>pframe_input</c>).</param>
    /// <param name="ccbFlags">The current CCB flags.</param>
    /// <param name="ccbControl0">The <c>CCBCTL0</c> register value.</param>
    /// <param name="pre1">The second CEL preamble word.</param>
    /// <param name="celOriginVh">The CEL origin's V/H bits.</param>
    /// <returns>The projected 16-bit pixel.</returns>
    public static ushort Apply(
        ushort decoderOutput,
        ushort processorOutput,
        ushort framePixel,
        CcbControl ccbFlags,
        uint ccbControl0,
        uint pre1,
        uint celOriginVh)
    {
        // FreeDO: the CFBDSUB path is disabled in the reference; keep the parameter for
        // signature parity with PPROJ_OUTPUT without changing the result.
        _ = framePixel;

        var vhOutput = (ccbFlags & CcbControl.PlutPosition) != 0
            ? (uint)(decoderOutput & 0x8001)
            : celOriginVh;

        if ((ccbControl0 & (uint)CcbControl0.SwapHV) != 0
            && (pre1 & (uint)CcbPreamble1.NoSwap) == 0)
        {
            vhOutput = (vhOutput >> 15) | ((vhOutput & 1) << 15);
        }

        var b15Mode = ccbControl0 & (uint)CcbControl0.B15PositionMask;
        if (b15Mode == (uint)B15Position.Zero)
        {
            vhOutput &= ~0x8000u;
        }
        else if (b15Mode == (uint)B15Position.One)
        {
            vhOutput |= 0x8000u;
        }

        var b0Mode = ccbControl0 & (uint)CcbControl0.B0PositionMask;
        if (b0Mode == (uint)B0Position.PixelProcessor)
        {
            vhOutput = (vhOutput & ~1u) | (processorOutput & 1u);
        }
        else if (b0Mode == (uint)B0Position.Zero)
        {
            vhOutput &= ~1u;
        }
        else if (b0Mode == (uint)B0Position.One)
        {
            vhOutput |= 1u;
        }

        return (ushort)((uint)(processorOutput & 0x7ffe) | vhOutput);
    }
}
