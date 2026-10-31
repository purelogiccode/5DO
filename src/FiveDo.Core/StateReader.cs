using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FiveDo.Core;

/// <summary>
/// A bounded, explicit little-endian state reader with per-section CRC validation.
/// </summary>
public class StateReader : IStateReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ReadOnlyMemory<byte> _data;
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="StateReader"/> class.</summary>
    /// <param name="data">State bytes to read.</param>
    public StateReader(ReadOnlyMemory<byte> data)
    {
        _data = data;
    }

    /// <inheritdoc />
    public int Remaining => _data.Length - _position;

    /// <summary>Attempts to read the next section.</summary>
    /// <param name="section">Receives the bounded section reader when one was read.</param>
    /// <returns><see langword="true"/> when a section was read; <see langword="false"/> at a clean end of input.</returns>
    /// <exception cref="InvalidStateException">Thrown when the section header, length, or CRC is invalid.</exception>
    public bool TryReadSection([NotNullWhen(true)] out StateSectionReader? section)
    {
        if (Remaining == 0)
        {
            section = null;
            return false;
        }

        EnsureAvailable(StateFormat.SectionHeaderSize, "section header");
        var id = ReadUInt32();
        var version = ReadUInt16();
        var flags = ReadUInt16();
        var length = ReadUInt32();

        if (length > int.MaxValue)
        {
            throw new InvalidStateException($"Section 0x{id:X8} has an invalid payload length.");
        }

        if ((flags & ~StateFormat.SectionFlagCompressed) != 0)
        {
            throw new InvalidStateException($"Section 0x{id:X8} has unsupported flags 0x{flags:X4}.");
        }

        var storedLength = (int)length;
        if (Remaining < storedLength + StateFormat.SectionCrcSize)
        {
            throw new InvalidStateException($"Section 0x{id:X8} is truncated.");
        }

        ReadOnlyMemory<byte> storedPayload = _data.Slice(_position, storedLength);
        _position += storedLength;
        var expectedCrc = ReadUInt32();
        var compressed = (flags & StateFormat.SectionFlagCompressed) != 0;
        ReadOnlyMemory<byte> payload;
        if (compressed)
        {
            payload = StateCompression.DecompressSection(storedPayload.Span, id);
        }
        else
        {
            payload = storedPayload;
        }

        var actualCrc = StateCrc32.Compute(payload.Span);
        if (expectedCrc != actualCrc)
        {
            throw new InvalidStateException($"Section 0x{id:X8} failed its CRC check.");
        }

        section = new StateSectionReader(id, version, payload, storedLength, compressed);
        return true;
    }

    /// <summary>Reads the next section or throws when no section remains.</summary>
    /// <returns>The section reader.</returns>
    /// <exception cref="InvalidStateException">Thrown when no section remains or the section is invalid.</exception>
    public StateSectionReader ReadSection()
    {
        if (!TryReadSection(out StateSectionReader? section))
        {
            throw new InvalidStateException("The state ended before the expected section.");
        }

        return section;
    }

    /// <inheritdoc />
    public byte ReadByte()
    {
        EnsureAvailable(1, "byte");
        return _data.Span[_position++];
    }

    /// <inheritdoc />
    public ushort ReadUInt16()
    {
        EnsureAvailable(2, "UInt16");
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Span.Slice(_position, 2));
        _position += 2;
        return value;
    }

    /// <inheritdoc />
    public uint ReadUInt32()
    {
        EnsureAvailable(4, "UInt32");
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Span.Slice(_position, 4));
        _position += 4;
        return value;
    }

    /// <inheritdoc />
    public ulong ReadUInt64()
    {
        EnsureAvailable(8, "UInt64");
        var value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Span.Slice(_position, 8));
        _position += 8;
        return value;
    }

    /// <inheritdoc />
    public int ReadInt32()
    {
        return unchecked((int)ReadUInt32());
    }

    /// <inheritdoc />
    public long ReadInt64()
    {
        return unchecked((long)ReadUInt64());
    }

    /// <inheritdoc />
    public float ReadSingle()
    {
        return BitConverter.UInt32BitsToSingle(ReadUInt32());
    }

    /// <inheritdoc />
    public double ReadDouble()
    {
        return BitConverter.UInt64BitsToDouble(ReadUInt64());
    }

    /// <inheritdoc />
    public bool ReadBoolean()
    {
        var value = ReadByte();
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidStateException($"Invalid boolean value 0x{value:X2} in state data."),
        };
    }

    /// <inheritdoc />
    public void ReadBytes(Span<byte> destination)
    {
        EnsureAvailable(destination.Length, "byte block");
        _data.Span.Slice(_position, destination.Length).CopyTo(destination);
        _position += destination.Length;
    }

    /// <inheritdoc />
    public string ReadString()
    {
        var byteCount = ReadInt32();
        if (byteCount < 0)
        {
            throw new InvalidStateException("A state string has a negative length.");
        }

        EnsureAvailable(byteCount, "string");
        ReadOnlySpan<byte> bytes = _data.Span.Slice(_position, byteCount);
        _position += byteCount;

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidStateException("A state string is not valid UTF-8.", exception);
        }
    }

    private void EnsureAvailable(int count, string description)
    {
        if (count < 0 || Remaining < count)
        {
            throw new InvalidStateException($"The state is truncated while reading {description}.");
        }
    }
}
