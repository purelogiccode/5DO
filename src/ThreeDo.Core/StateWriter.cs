using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace ThreeDo.Core;

/// <summary>
/// A growable, explicit little-endian state writer with per-section CRC validation.
/// </summary>
/// <remarks>
/// A section is framed as:
/// <c>[id:u32][version:u16][reserved:u16][length:u32][payload][crc32:u32]</c>.
/// The outer state-file header is added by the machine in M8.
/// </remarks>
public sealed class StateWriter : IStateWriter
{
    private readonly ArrayBufferWriter<byte> _buffer = new();

    /// <summary>Gets the number of bytes written so far.</summary>
    public int Length => _buffer.WrittenCount;

    /// <summary>Gets the written bytes.</summary>
    public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    /// <inheritdoc />
    public void WriteByte(byte value)
    {
        var span = _buffer.GetSpan(1);
        span[0] = value;
        _buffer.Advance(1);
    }

    /// <inheritdoc />
    public void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.GetSpan(2), value);
        _buffer.Advance(2);
    }

    /// <inheritdoc />
    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.GetSpan(4), value);
        _buffer.Advance(4);
    }

    /// <inheritdoc />
    public void WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.GetSpan(8), value);
        _buffer.Advance(8);
    }

    /// <inheritdoc />
    public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

    /// <inheritdoc />
    public void WriteInt64(long value) => WriteUInt64(unchecked((ulong)value));

    /// <inheritdoc />
    public void WriteSingle(float value) => WriteUInt32(BitConverter.SingleToUInt32Bits(value));

    /// <inheritdoc />
    public void WriteDouble(double value) => WriteUInt64(BitConverter.DoubleToUInt64Bits(value));

    /// <inheritdoc />
    public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    /// <inheritdoc />
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        value.CopyTo(_buffer.GetSpan(value.Length));
        _buffer.Advance(value.Length);
    }

    /// <inheritdoc />
    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteInt32(byteCount);
        var span = _buffer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(value, span);
        _buffer.Advance(byteCount);
    }

    /// <summary>Writes one section with its header and CRC trailer.</summary>
    /// <param name="section">Section to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="section"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the section identifier is zero.</exception>
    public void WriteSection(IStateSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (section.Id == 0)
        {
            throw new ArgumentException("A state section identifier must not be zero.", nameof(section));
        }

        var payloadWriter = new StateWriter();
        section.Save(payloadWriter);
        var payload = payloadWriter.WrittenSpan;

        WriteUInt32(section.Id);
        WriteUInt16(section.Version);
        WriteUInt16(0);
        WriteUInt32(checked((uint)payload.Length));
        WriteBytes(payload);
        WriteUInt32(StateCrc32.Compute(payload));
    }

    /// <summary>Returns the written bytes as a new array.</summary>
    /// <returns>A copy of the written state.</returns>
    public byte[] ToArray() => _buffer.WrittenSpan.ToArray();
}
