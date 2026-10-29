using System;

namespace FiveDo.Core;

/// <summary>
/// Writes save-state primitives in explicit little-endian form.
/// </summary>
public interface IStateWriter
{
    /// <summary>Gets the number of bytes written so far.</summary>
    int Length { get; }

    /// <summary>Writes one byte.</summary>
    /// <param name="value">Value to write.</param>
    void WriteByte(byte value);

    /// <summary>Writes one unsigned 16-bit integer.</summary>
    /// <param name="value">Value to write.</param>
    void WriteUInt16(ushort value);

    /// <summary>Writes one unsigned 32-bit integer.</summary>
    /// <param name="value">Value to write.</param>
    void WriteUInt32(uint value);

    /// <summary>Writes one unsigned 64-bit integer.</summary>
    /// <param name="value">Value to write.</param>
    void WriteUInt64(ulong value);

    /// <summary>Writes one signed 32-bit integer.</summary>
    /// <param name="value">Value to write.</param>
    void WriteInt32(int value);

    /// <summary>Writes one signed 64-bit integer.</summary>
    /// <param name="value">Value to write.</param>
    void WriteInt64(long value);

    /// <summary>Writes one single-precision value in IEEE 754 little-endian form.</summary>
    /// <param name="value">Value to write.</param>
    void WriteSingle(float value);

    /// <summary>Writes one double-precision value in IEEE 754 little-endian form.</summary>
    /// <param name="value">Value to write.</param>
    void WriteDouble(double value);

    /// <summary>Writes one boolean as a single byte (0 or 1).</summary>
    /// <param name="value">Value to write.</param>
    void WriteBoolean(bool value);

    /// <summary>Writes a byte block.</summary>
    /// <param name="value">Bytes to write.</param>
    void WriteBytes(ReadOnlySpan<byte> value);

    /// <summary>Writes a length-prefixed UTF-8 string.</summary>
    /// <param name="value">String to write.</param>
    void WriteString(string value);
}
