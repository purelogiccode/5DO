namespace FiveDo.Core;

/// <summary>
/// Save-state format constants.
/// </summary>
public static class StateFormat
{
    /// <summary>ASCII magic <c>TDO1</c> stored as a little-endian 32-bit value.</summary>
    public const uint Magic = 0x314F4454;

    /// <summary>Current save-state format version.</summary>
    /// <remarks>
    /// Version 3 adds header feature flags and an up-front section table. Versions 1 and 2
    /// remain readable; their reserved header word is zero and they have no table.
    /// </remarks>
    public const ushort Version = 3;

    /// <summary>Oldest save-state format version this build can read.</summary>
    public const ushort MinSupportedVersion = 1;

    /// <summary>Size of a section header in bytes.</summary>
    public const int SectionHeaderSize = 12;

    /// <summary>Size of the CRC trailer that follows each section payload.</summary>
    public const int SectionCrcSize = 4;

    /// <summary>Header flag that marks a v3 feature-flags word after the frame count.</summary>
    public const ushort HeaderFlagFeatureFlags = 1;

    /// <summary>Header flag that marks a v3 section table after the frame count.</summary>
    public const ushort HeaderFlagSectionTable = 2;

    /// <summary>All header flags understood by this build.</summary>
    public const ushort HeaderFlagAll = HeaderFlagFeatureFlags | HeaderFlagSectionTable;

    /// <summary>The state records the machine-options section.</summary>
    public const uint FeatureOptionsSnapshot = 1;

    /// <summary>The state declares its section table in the header.</summary>
    public const uint FeatureSectionTable = 2;

    /// <summary>Large section payloads were eligible for Brotli compression.</summary>
    public const uint FeatureCompressedSections = 4;

    /// <summary>The options section records the video region.</summary>
    public const uint FeatureVideoRegion = 8;

    /// <summary>All feature flags understood by this build.</summary>
    public const uint FeatureAll =
        FeatureOptionsSnapshot | FeatureSectionTable | FeatureCompressedSections | FeatureVideoRegion;

    /// <summary>Upper bound on declared sections accepted from a state header.</summary>
    public const int MaxSectionTableEntries = 4096;

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
