namespace FiveDo.Core;

/// <summary>
/// Save-state format constants.
/// </summary>
public static class StateFormat
{
    /// <summary>ASCII magic <c>TDO1</c> stored as a little-endian 32-bit value.</summary>
    public const uint Magic = 0x314F4454;

    /// <summary>Current save-state format version.</summary>
    public const ushort Version = 2;

    /// <summary>Oldest save-state format version this build can read.</summary>
    public const ushort MinSupportedVersion = 1;

    /// <summary>Size of a section header in bytes.</summary>
    public const int SectionHeaderSize = 12;

    /// <summary>Size of the CRC trailer that follows each section payload.</summary>
    public const int SectionCrcSize = 4;

    /// <summary>Section flag that marks a Brotli-compressed payload.</summary>
    public const ushort SectionFlagCompressed = 1;

    /// <summary>Payload size at or above which the writer attempts compression.</summary>
    public const int CompressionThreshold = 4096;

    /// <summary>Bytes of uncompressed-length prefix stored ahead of a compressed section payload.</summary>
    public const int CompressedSectionPrefixSize = 4;

    /// <summary>Largest uncompressed section payload accepted by the reader.</summary>
    public const int MaxUncompressedSectionLength = 64 * 1024 * 1024;

    /// <summary>Checks whether a state-file version is readable by this build.</summary>
    /// <param name="version">Format version from the file header.</param>
    /// <returns><see langword="true"/> when the version is supported.</returns>
    public static bool IsSupportedVersion(ushort version)
    {
        return version >= MinSupportedVersion && version <= Version;
    }
}
