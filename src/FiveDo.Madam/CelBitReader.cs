using System;
using FiveDo.Core;

namespace FiveDo.Madam;

/// <summary>
/// The big-endian bit reader used by the CEL engine. FreeDO: <c>bitop.cpp: BitReaderBig</c>.
/// </summary>
/// <remarks>
/// The reader addresses bytes through the reference's word-swapped raw RAM convention:
/// byte <c>point</c> is read from <c>buffer + (point ^ 3)</c>. A buffer offset of zero
/// disables reading and returns zero, matching <c>if (!buf) return 0</c>.
/// </remarks>
public sealed class CelBitReader
{
    private static readonly uint[] Masks = { 0, 1, 3, 7, 15, 31, 63, 127, 255 };

    private readonly IRam _ram;

    private uint _buffer;
    private uint _point;
    private int _bitPoint;
    private int _bitSet = 1;

    /// <summary>Initializes a new instance of the <see cref="CelBitReader"/> class.</summary>
    /// <param name="ram">Raw RAM/VRAM accessor.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ram"/> is null.</exception>
    public CelBitReader(IRam ram)
    {
        ArgumentNullException.ThrowIfNull(ram);
        _ram = ram;
    }

    /// <summary>Gets the current byte position. FreeDO: <c>GetBytePose</c>.</summary>
    public uint BytePosition => _point;

    /// <summary>Gets the current bit position within the current byte.</summary>
    public int BitPosition => _bitPoint;

    /// <summary>Gets the configured bit rate.</summary>
    public int BitRate => _bitSet;

    /// <summary>Attaches a buffer and resets the position. FreeDO: <c>AttachBuffer</c>.</summary>
    /// <param name="buffer">Byte offset of the buffer in RAM.</param>
    public void AttachBuffer(uint buffer)
    {
        _buffer = buffer;
        _point = 0;
        _bitPoint = 0;
    }

    /// <summary>Sets the bit rate, clamped to 1-32. FreeDO: <c>SetBitRate</c>.</summary>
    /// <param name="bits">Bits per read.</param>
    public void SetBitRate(byte bits)
    {
        _bitSet = bits > 32 ? 32 : bits;
        if (_bitSet == 0)
        {
            _bitSet = 1;
        }
    }

    /// <summary>Sets the byte and bit position. FreeDO: <c>SetPosition</c>.</summary>
    /// <param name="bytePosition">Byte position.</param>
    /// <param name="bitPosition">Bit position within the byte.</param>
    public void SetPosition(uint bytePosition, byte bitPosition)
    {
        _point = bytePosition;
        _bitPoint = bitPosition;
    }

    /// <summary>Sets the absolute bit position. FreeDO: <c>SetPos</c>.</summary>
    /// <param name="bitPosition">Absolute bit position.</param>
    public void SetPos(uint bitPosition) => SetPosition(bitPosition >> 3, (byte)(bitPosition & 7));

    /// <summary>Skips bits. FreeDO: <c>Skip</c>.</summary>
    /// <param name="bits">Number of bits to skip.</param>
    public void Skip(uint bits)
    {
        bits += (uint)_bitPoint;
        _point += bits >> 3;
        _bitPoint = (int)(bits & 7);
    }

    /// <summary>Reads the configured number of bits. FreeDO: <c>BitReaderBig::Read</c>.</summary>
    /// <returns>The bit value.</returns>
    public uint Read()
    {
        uint result = 0;
        if (_buffer == 0)
        {
            return result;
        }

        var bitCount = _bitSet;
        if ((8 - _bitPoint) > _bitSet)
        {
            result = ReadByte(_point);
            result >>= 8 - _bitPoint - _bitSet;
            result &= Masks[_bitSet];
            _bitPoint += _bitSet;
            return result;
        }

        if (_bitPoint != 0)
        {
            result = ReadByte(_point) & Masks[8 - _bitPoint];
            _point++;
            bitCount -= 8 - _bitPoint;
        }

        while (bitCount >= 8)
        {
            result = unchecked((result << 8) | ReadByte(_point));
            _point++;
            bitCount -= 8;
        }

        if (bitCount != 0)
        {
            result <<= bitCount;
            result |= (uint)(ReadByte(_point) >> (8 - bitCount));
        }

        _bitPoint = bitCount;
        return result;
    }

    /// <summary>Sets the bit rate and reads. FreeDO: <c>BitReaderBig::Read(bits)</c>.</summary>
    /// <param name="bits">Bits to read.</param>
    /// <returns>The bit value.</returns>
    public uint Read(byte bits)
    {
        SetBitRate(bits);
        return Read();
    }

    private byte ReadByte(uint point) => _ram.Read8(_buffer + (point ^ 3));
}
