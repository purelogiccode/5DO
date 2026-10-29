using System;

namespace FiveDo.Core;

/// <summary>
/// A bounded reader for one validated state section.
/// </summary>
public sealed class StateSectionReader : StateReader
{
    internal StateSectionReader(
        uint id,
        ushort version,
        ReadOnlyMemory<byte> payload,
        int storedLength,
        bool compressed)
        : base(payload)
    {
        Id = id;
        Version = version;
        PayloadLength = payload.Length;
        StoredLength = storedLength;
        Compressed = compressed;
    }

    /// <summary>Gets the section identifier.</summary>
    public uint Id { get; }

    /// <summary>Gets the section format version.</summary>
    public ushort Version { get; }

    /// <summary>Gets the uncompressed payload length in bytes.</summary>
    public int PayloadLength { get; }

    /// <summary>Gets the stored payload length in bytes before decompression.</summary>
    public int StoredLength { get; }

    /// <summary>Gets a value indicating whether the payload was Brotli-compressed in the file.</summary>
    public bool Compressed { get; }
}
