using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace FiveDo.Core;

/// <summary>
/// A growable, explicit little-endian state writer with per-section CRC validation.
/// </summary>
/// <remarks>
/// A section is framed as:
/// <c>[id:u32][version:u16][reserved:u16][length:u32][payload][crc32:u32]</c>.
/// The outer state-file header is added by the machine in M8.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The reusable compression scratch is a MemoryStream over managed memory; it holds no unmanaged resources.")]
public sealed class StateWriter : IStateWriter
{
    private readonly ArrayBufferWriter<byte> _buffer;
    private StateWriter? _payloadWriter;
    private MemoryStream? _compressionScratch;

    /// <summary>Initializes a new instance of the <see cref="StateWriter"/> class.</summary>
    /// <param name="initialCapacity">
    /// Optional initial buffer size in bytes. Pre-sizing avoids repeated large copies when the
    /// caller already knows the expected state size (for example a rewind snapshot).
    /// </param>
    public StateWriter(int initialCapacity = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
        _buffer = initialCapacity > 0
            ? new ArrayBufferWriter<byte>(initialCapacity)
            : new ArrayBufferWriter<byte>();
    }

    /// <summary>
    /// Gets or sets a value indicating whether large section payloads are Brotli-compressed.
    /// </summary>
    public bool CompressionEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Brotli compression level used for large section payloads.
    /// <see cref="CompressionLevel.Fastest"/> trades a slightly larger state for a much
    /// shorter capture, which matters for the in-memory rewind history.
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// Gets or sets a value indicating whether each section trailer contains a CRC-32 of the
    /// uncompressed payload. Disable only for transient snapshots that are never persisted.
    /// </summary>
    public bool ChecksumsEnabled { get; set; } = true;

    /// <summary>Gets the number of bytes written so far.</summary>
    public int Length => _buffer.WrittenCount;

    /// <summary>Gets the written bytes.</summary>
    public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    /// <summary>
    /// Gets the written bytes as memory. The memory is valid until the next reset or write.
    /// </summary>
    public ReadOnlyMemory<byte> WrittenMemory => _buffer.WrittenMemory;

    /// <inheritdoc />
    public void WriteByte(byte value)
    {
        Span<byte> span = _buffer.GetSpan(1);
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
    public void WriteInt32(int value)
    {
        WriteUInt32(unchecked((uint)value));
    }

    /// <inheritdoc />
    public void WriteInt64(long value)
    {
        WriteUInt64(unchecked((ulong)value));
    }

    /// <inheritdoc />
    public void WriteSingle(float value)
    {
        WriteUInt32(BitConverter.SingleToUInt32Bits(value));
    }

    /// <inheritdoc />
    public void WriteDouble(double value)
    {
        WriteUInt64(BitConverter.DoubleToUInt64Bits(value));
    }

    /// <inheritdoc />
    public void WriteBoolean(bool value)
    {
        WriteByte(value ? (byte)1 : (byte)0);
    }

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
        Span<byte> span = _buffer.GetSpan(byteCount);
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

        // Reuse the payload writer across sections and captures. The memory payload is
        // several megabytes, so allocating a fresh buffer for every section on every
        // rewind snapshot creates avoidable large-object-heap churn.
        StateWriter payloadWriter = _payloadWriter ??= new StateWriter();
        payloadWriter.Reset();
        section.Save(payloadWriter);
        ReadOnlySpan<byte> payload = payloadWriter.WrittenSpan;
        MemoryStream? compressedBuffer = null;
        var storedLength = 0;
        var compressed = false;
        if (CompressionEnabled && payload.Length >= StateFormat.CompressionThreshold)
        {
            // Reuse the compression scratch buffer across sections and captures. This is
            // what keeps a multi-megabyte rewind snapshot from allocating on the emulation
            // thread every couple of seconds.
            compressedBuffer = _compressionScratch ??= new MemoryStream();
            compressed = StateCompression.TryCompress(
                payload,
                CompressionLevel,
                compressedBuffer,
                out storedLength);
        }

        var frameLength = compressed ? storedLength : payload.Length;
        var flags = (ushort)((compressed ? StateFormat.SectionFlagCompressed : (ushort)0)
            | (ChecksumsEnabled ? (ushort)0 : StateFormat.SectionFlagUnchecked));
        WriteUInt32(section.Id);
        WriteUInt16(section.Version);
        WriteUInt16(flags);
        WriteUInt32(checked((uint)frameLength));
        WriteBytes(compressed
            ? compressedBuffer!.GetBuffer().AsSpan(0, storedLength)
            : payload);
        WriteUInt32(ChecksumsEnabled ? StateCrc32.Compute(payload) : 0);
    }

    /// <summary>Returns the written bytes as a new array.</summary>
    /// <returns>A copy of the written state.</returns>
    public byte[] ToArray()
    {
        return _buffer.WrittenSpan.ToArray();
    }

    /// <summary>Clears the written data while keeping the allocated capacity.</summary>
    public void Reset()
    {
        _buffer.ResetWrittenCount();
    }
}
