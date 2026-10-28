using System;
using System.Buffers.Binary;
using System.IO;

namespace FiveDo.Audio;

/// <summary>
/// Streams 16-bit stereo PCM into a RIFF/WAVE file and patches the sizes on disposal.
/// </summary>
/// <remarks>
/// Samples are buffered so callers can write one frame at a time without a file write per
/// sample. <see cref="PatchHeader"/> makes a partially written file readable after a crash.
/// </remarks>
internal sealed class Pcm16WavWriter : IDisposable
{
    private const int HeaderSize = 44;
    private const int BufferSize = 64 * 1024;
    private const short Channels = 2;
    private const short BitsPerSample = 16;
    private const int BlockAlign = Channels * (BitsPerSample / 8);

    private readonly Stream _stream;
    private readonly byte[] _buffer = new byte[BufferSize];
    private readonly int _sampleRate;
    private int _fill;
    private long _dataBytes;
    private bool _disposed;

    /// <summary>Initializes a writer that creates or truncates the file at <paramref name="path"/>.</summary>
    /// <param name="path">Destination WAV path. Missing parent directories are created.</param>
    /// <param name="sampleRate">Sample rate written to the format chunk.</param>
    internal Pcm16WavWriter(string path, int sampleRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _sampleRate = sampleRate;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _stream = File.Create(path);
        WriteHeader();
    }

    /// <summary>Gets the number of PCM bytes written so far.</summary>
    internal long DataBytes => _dataBytes;

    /// <summary>Writes one interleaved stereo frame.</summary>
    /// <param name="left">Left channel sample.</param>
    /// <param name="right">Right channel sample.</param>
    internal void WriteSample(short left, short right)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_fill + BlockAlign > _buffer.Length)
        {
            Flush();
        }

        BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_fill), left);
        BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_fill + 2), right);
        _fill += BlockAlign;
        _dataBytes += BlockAlign;
    }

    /// <summary>Writes interleaved stereo samples.</summary>
    /// <param name="samples">Interleaved left/right samples.</param>
    internal void Write(ReadOnlySpan<short> samples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var sample in samples)
        {
            if (_fill + sizeof(short) > _buffer.Length)
            {
                Flush();
            }

            BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_fill), sample);
            _fill += sizeof(short);
            _dataBytes += sizeof(short);
        }
    }

    /// <summary>Flushes buffered samples and rewrites the RIFF and data sizes.</summary>
    internal void PatchHeader()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var position = _stream.Position;
        Flush();
        _stream.Seek(4, SeekOrigin.Begin);
        Span<byte> riffSize = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(riffSize, checked((int)(HeaderSize - 8 + _dataBytes)));
        _stream.Write(riffSize);

        _stream.Seek(40, SeekOrigin.Begin);
        Span<byte> dataSize = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(dataSize, checked((int)_dataBytes));
        _stream.Write(dataSize);
        _stream.Seek(position, SeekOrigin.Begin);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Flush();
        _stream.Flush();
        _stream.Seek(4, SeekOrigin.Begin);
        Span<byte> riffSize = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(riffSize, checked((int)(HeaderSize - 8 + _dataBytes)));
        _stream.Write(riffSize);

        _stream.Seek(40, SeekOrigin.Begin);
        Span<byte> dataSize = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(dataSize, checked((int)_dataBytes));
        _stream.Write(dataSize);
        _stream.Dispose();
    }

    private void Flush()
    {
        if (_fill == 0)
        {
            return;
        }

        _stream.Write(_buffer, 0, _fill);
        _fill = 0;
    }

    private void WriteHeader()
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], 0);
        "WAVE"u8.CopyTo(header[8..]);
        "fmt "u8.CopyTo(header[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..], Channels);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], _sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], _sampleRate * BlockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], BlockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], BitsPerSample);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..], 0);
        _stream.Write(header);
    }
}
