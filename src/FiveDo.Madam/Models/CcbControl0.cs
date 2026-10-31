namespace FiveDo.Madam;

/// <summary>
/// CEL control-block word 0 flags. FreeDO: the <c>CCBCTL0</c> defines in <c>Madam.cpp</c>.
/// </summary>
/// <remarks>
/// This word is loaded from MADAM register <c>0x110</c> and is not part of the CCB
/// header itself. Values are persisted in guest memory and must not be renumbered.
/// The word is a packed bitfield rather than a set of independent flags, so it is not
/// marked with <see cref="System.FlagsAttribute"/>.
/// </remarks>
public enum CcbControl0 : uint
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>B15 source mask. FreeDO: <c>B15POS_MASK</c>.</summary>
    B15PositionMask = 0xC0000000,

    /// <summary>B0 source mask. FreeDO: <c>B0POS_MASK</c>.</summary>
    B0PositionMask = 0x30000000,

    /// <summary>Swap the H and V bits. FreeDO: <c>SWAPHV</c>.</summary>
    SwapHv = 0x08000000,

    /// <summary>All-subcode flag. FreeDO: <c>ASCALL</c>.</summary>
    AscAll = 0x04000000,

    /// <summary>Reserved bit 25. FreeDO: <c>_CCBCTL0_u25</c>.</summary>
    Reserved25 = 0x02000000,

    /// <summary>Substitute frame-buffer VH data. FreeDO: <c>CFBDSUB</c>.</summary>
    FrameBufferDataSubstitute = 0x01000000,

    /// <summary>Frame-buffer data LSB mask. FreeDO: <c>CFBDLSB_MASK</c>.</summary>
    FrameBufferDataLsbMask = 0x00C00000,

    /// <summary>Pixel-decoder LSB mask. FreeDO: <c>PDCLSB_MASK</c>.</summary>
    PixelDecoderLsbMask = 0x00300000,
}
