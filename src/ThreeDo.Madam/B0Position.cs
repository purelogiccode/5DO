namespace ThreeDo.Madam;

/// <summary>
/// Source of the projector's H bit (bit 0). FreeDO: the <c>B0POS_*</c> defines.
/// </summary>
public enum B0Position : uint
{
    /// <summary>Force the H bit to zero. FreeDO: <c>B0POS_0</c>.</summary>
    Zero = 0x00000000,

    /// <summary>Force the H bit to one. FreeDO: <c>B0POS_1</c>.</summary>
    One = 0x10000000,

    /// <summary>Use the pixel processor's H bit. FreeDO: <c>B0POS_PPMP</c>.</summary>
    PixelProcessor = 0x20000000,

    /// <summary>Use the pixel decoder's H bit. FreeDO: <c>B0POS_PDC</c>.</summary>
    PixelDecoder = 0x30000000,
}
