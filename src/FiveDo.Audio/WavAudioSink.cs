using System;
using System.Buffers.Binary;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Audio;

/// <summary>
/// Writes DSP samples to a 44.1 kHz, 16-bit stereo WAV file.
/// </summary>
/// <remarks>
/// Used by the CLI and tests for offline audio verification. The header sizes are patched
/// when the sink is disposed; callers should dispose it (or use <c>using</c>) before reading
/// the file.
/// </remarks>
public sealed class WavAudioSink : IAudioSink, IDisposable
{
    private const int HeaderSize = 44;
    private const short Channels = 2;
    private const short BitsPerSample = 16;
    private const int SampleRate = 44100;
    private const int BlockAlign = Channels * (BitsPerSample / 8);

    private readonly Stream _stream;
    private readonly byte[] _sampleBytes = new byte[4];
    private long _dataBytes;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="WavAudioSink"/> class.</summary>
    /// <param name="path">Destination WAV path.</param>
    public WavAudioSink(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _stream = File.Create(path);
        WriteHeader();
    }

    /// <summary>Gets the number of stereo samples written.</summary>
    public long SampleCount => _dataBytes / BlockAlign;

    /// <summary>Gets the number of bytes of PCM data written.</summary>
    public long DataBytes => _dataBytes;

    /// <inheritdoc />
    public void PushSample(uint packedSample)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var sample = AudioSample.FromPacked(packedSample);
        BinaryPrimitives.WriteInt16LittleEndian(_sampleBytes, sample.Left);
        BinaryPrimitives.WriteInt16LittleEndian(_sampleBytes.AsSpan(2), sample.Right);
        _stream.Write(_sampleBytes);
        _dataBytes += _sampleBytes.Length;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], SampleRate * BlockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], BlockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], BitsPerSample);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..], 0);
        _stream.Write(header);
    }
}
