using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace FiveDo.Audio;

/// <summary>
/// Validates and analyzes PCM WAV files without loading the complete file into memory.
/// </summary>
/// <remarks>
/// The CLI uses this to compare captures between runs and against the Opera reference.
/// Supported input is 16-bit signed PCM, mono or stereo, at any sample rate.
/// </remarks>
public static class AudioAnalyzer
{
    private const int RiffHeaderSize = 12;
    private const int ChunkHeaderSize = 8;

    /// <summary>Analyzes a WAV file.</summary>
    /// <param name="path">WAV path.</param>
    /// <returns>Format information and statistics.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file is not a supported WAV.</exception>
    public static AudioAnalysis Analyze(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);

        if (stream.Length < RiffHeaderSize)
        {
            throw new InvalidDataException("The WAV file is too short.");
        }

        Span<byte> header = stackalloc byte[RiffHeaderSize];
        stream.ReadExactly(header);
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("The file is not a RIFF/WAVE file.");
        }

        var format = default(WavFormat);
        var hasFormat = false;
        var hasData = false;
        var statistics = new AudioStatistics();
        long dataBytes = 0;

        Span<byte> chunkHeader = stackalloc byte[ChunkHeaderSize];
        while (stream.Position + ChunkHeaderSize <= stream.Length)
        {
            stream.ReadExactly(chunkHeader);
            var id = chunkHeader[..4];
            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);

            if (id.SequenceEqual("fmt "u8))
            {
                format = ReadFormat(stream, size);
                hasFormat = true;
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (!hasFormat)
                {
                    throw new InvalidDataException("The WAV data chunk appears before the fmt chunk.");
                }

                var available = stream.Length - stream.Position;
                var requested = size == uint.MaxValue ? available : Math.Min(size, (uint)available);
                if (size != uint.MaxValue && size > available)
                {
                    throw new InvalidDataException("The WAV data chunk is truncated.");
                }

                hasData = true;
                dataBytes += ReadData(stream, (long)requested, format, statistics);
                if (((long)requested & 1) != 0 && stream.Position < stream.Length)
                {
                    stream.Seek(1, SeekOrigin.Current);
                }
            }
            else
            {
                if (size > stream.Length - stream.Position)
                {
                    throw new InvalidDataException("A WAV chunk is truncated.");
                }

                var skip = (long)size + (size & 1);
                stream.Seek(Math.Min(skip, stream.Length - stream.Position), SeekOrigin.Current);
            }
        }

        if (!hasFormat)
        {
            throw new InvalidDataException("The WAV file has no fmt chunk.");
        }

        if (!hasData)
        {
            throw new InvalidDataException("The WAV file has no data chunk.");
        }

        var info = new AudioFileInfo(
            path,
            format.SampleRate,
            format.Channels,
            format.BitsPerSample,
            statistics.Frames,
            dataBytes);
        return new AudioAnalysis(info, statistics);
    }

    private static WavFormat ReadFormat(FileStream stream, uint size)
    {
        if (size < 16 || size > 4096 || size > stream.Length - stream.Position)
        {
            throw new InvalidDataException("The WAV fmt chunk is invalid.");
        }

        var bytes = new byte[(int)size];
        stream.ReadExactly(bytes);
        if ((size & 1) != 0 && stream.Position < stream.Length)
        {
            stream.Seek(1, SeekOrigin.Current);
        }

        var formatTag = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2));
        var sampleRateValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        if (sampleRateValue > int.MaxValue)
        {
            throw new InvalidDataException("The WAV fmt chunk has an invalid sample rate.");
        }

        var sampleRate = (int)sampleRateValue;
        var blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12));
        var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(14));

        if (formatTag == 0xFFFE)
        {
            // WAVE_FORMAT_EXTENSIBLE: the first two bytes of the subformat GUID are the
            // effective format tag. Only PCM is accepted.
            if (size < 40)
            {
                throw new InvalidDataException("The WAV extensible fmt chunk is too short.");
            }

            formatTag = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(24));
        }

        if (formatTag != 1)
        {
            throw new NotSupportedException($"Unsupported WAV format tag 0x{formatTag:X4}; only PCM is supported.");
        }

        if (channels is not 1 and not 2)
        {
            throw new NotSupportedException($"Unsupported WAV channel count {channels}; only mono and stereo are supported.");
        }

        if (bitsPerSample != 16)
        {
            throw new NotSupportedException($"Unsupported WAV sample size {bitsPerSample}; only 16-bit PCM is supported.");
        }

        if (sampleRate <= 0 || blockAlign != channels * 2)
        {
            throw new InvalidDataException("The WAV fmt chunk has an invalid sample rate or block alignment.");
        }

        return new WavFormat(channels, sampleRate, bitsPerSample);
    }

    private static long ReadData(FileStream stream, long byteCount, WavFormat format, AudioStatistics statistics)
    {
        // A buffer multiple of the frame size keeps partial frames from leaking between
        // chunks. The carry is exactly one frame so mono files are handled as well as stereo.
        var frameBytes = format.Channels * 2;
        var bufferSize = 64 * 1024;
        var buffer = new byte[bufferSize];
        var consumed = 0L;
        var carry = new byte[frameBytes];
        var carryCount = 0;

        while (consumed < byteCount)
        {
            var requested = (int)Math.Min(bufferSize, byteCount - consumed);
            var read = stream.Read(buffer, 0, requested);
            if (read <= 0)
            {
                throw new InvalidDataException("The WAV data chunk is truncated.");
            }

            consumed += read;
            var offset = 0;
            if (carryCount > 0)
            {
                var need = frameBytes - carryCount;
                var copy = Math.Min(need, read);
                buffer.AsSpan(0, copy).CopyTo(carry.AsSpan(carryCount));
                carryCount += copy;
                offset = copy;
                if (carryCount < frameBytes)
                {
                    continue;
                }

                AddFrame(carry, format, statistics);
                carryCount = 0;
            }

            var end = offset + (((read - offset) / frameBytes) * frameBytes);
            for (var i = offset; i < end; i += frameBytes)
            {
                AddFrame(buffer.AsSpan(i), format, statistics);
            }

            var remaining = read - end;
            if (remaining > 0)
            {
                buffer.AsSpan(end, remaining).CopyTo(carry);
                carryCount = remaining;
            }
        }

        if (carryCount > 0)
        {
            throw new InvalidDataException("The WAV data chunk ends in a partial sample frame.");
        }

        return consumed;
    }

    private static void AddFrame(ReadOnlySpan<byte> frame, WavFormat format, AudioStatistics statistics)
    {
        var left = BinaryPrimitives.ReadInt16LittleEndian(frame);
        var right = format.Channels == 2
            ? BinaryPrimitives.ReadInt16LittleEndian(frame[2..])
            : left;
        statistics.Add(left, right);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct WavFormat(int Channels, int SampleRate, int BitsPerSample);
}
