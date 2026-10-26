using System;

namespace FiveDo.Core;

/// <summary>
/// A bounded reader for one validated state section.
/// </summary>
public sealed class StateSectionReader : StateReader
{
    internal StateSectionReader(uint id, ushort version, ReadOnlyMemory<byte> payload)
        : base(payload)
    {
        Id = id;
        Version = version;
        PayloadLength = payload.Length;
    }

    /// <summary>Gets the section identifier.</summary>
    public uint Id { get; }

    /// <summary>Gets the section format version.</summary>
    public ushort Version { get; }

    /// <summary>Gets the payload length in bytes.</summary>
    public int PayloadLength { get; }
}
