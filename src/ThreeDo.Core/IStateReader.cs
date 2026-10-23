using System;

namespace ThreeDo.Core;

/// <summary>
/// Reads save-state primitives in explicit little-endian form.
/// </summary>
public interface IStateReader
{
    /// <summary>Gets the number of bytes remaining in the current section or buffer.</summary>
    int Remaining { get; }

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    byte ReadByte();

    /// <summary>Reads one unsigned 16-bit integer.</summary>
    /// <returns>The value.</returns>
    ushort ReadUInt16();

    /// <summary>Reads one unsigned 32-bit integer.</summary>
    /// <returns>The value.</returns>
    uint ReadUInt32();

    /// <summary>Reads one unsigned 64-bit integer.</summary>
    /// <returns>The value.</returns>
    ulong ReadUInt64();

    /// <summary>Reads one signed 32-bit integer.</summary>
    /// <returns>The value.</returns>
    int ReadInt32();

    /// <summary>Reads one signed 64-bit integer.</summary>
    /// <returns>The value.</returns>
    long ReadInt64();

    /// <summary>Reads one single-precision value.</summary>
    /// <returns>The value.</returns>
    float ReadSingle();

    /// <summary>Reads one double-precision value.</summary>
    /// <returns>The value.</returns>
    double ReadDouble();

    /// <summary>Reads one boolean.</summary>
    /// <returns>The value.</returns>
    bool ReadBoolean();

    /// <summary>Reads a byte block into <paramref name="destination"/>.</summary>
    /// <param name="destination">Destination buffer.</param>
    void ReadBytes(Span<byte> destination);

    /// <summary>Reads a length-prefixed UTF-8 string.</summary>
    /// <returns>The string.</returns>
    string ReadString();
}
