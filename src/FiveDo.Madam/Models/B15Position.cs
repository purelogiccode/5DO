namespace FiveDo.Madam;

/// <summary>
/// Source of the projector's V bit (bit 15). FreeDO: the <c>B15POS_*</c> defines.
/// </summary>
public enum B15Position : uint
{
    /// <summary>Force the V bit to zero. FreeDO: <c>B15POS_0</c>.</summary>
    Zero = 0x00000000,

    /// <summary>Force the V bit to one. FreeDO: <c>B15POS_1</c>.</summary>
    One = 0x40000000,

    /// <summary>Use the pixel decoder's V bit. FreeDO: <c>B15POS_PDC</c>.</summary>
    PixelDecoder = 0xC0000000,
}
