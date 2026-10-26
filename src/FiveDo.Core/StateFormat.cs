namespace FiveDo.Core;

/// <summary>
/// Save-state format constants.
/// </summary>
public static class StateFormat
{
    /// <summary>ASCII magic <c>TDO1</c> stored as a little-endian 32-bit value.</summary>
    public const uint Magic = 0x314F4454;

    /// <summary>Current save-state format version.</summary>
    public const ushort Version = 1;

    /// <summary>Size of a section header in bytes.</summary>
    public const int SectionHeaderSize = 12;

    /// <summary>Size of the CRC trailer that follows each section payload.</summary>
    public const int SectionCrcSize = 4;
}
