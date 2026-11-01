using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace FiveDo.Tas;

/// <summary>
/// Reads and writes the versioned 5DO movie container.
/// </summary>
/// <remarks>
/// Layout (little-endian):
/// <code>
/// 0   magic "5DOMOVIE" (8)
/// 8   format version (int32)
/// 12  header JSON length (int32)
/// 16  compressed payload length (int64)
/// 24  uncompressed payload length (int64)
/// 32  state length (int64)
/// 40  header CRC-32 (uint32)
/// 44  payload CRC-32 (uint32)
/// 48  state CRC-32 (uint32)
/// 52  header JSON
///     Brotli-compressed input payload
///     optional raw save state
/// </code>
/// The input payload starts with a frame count (int32), followed by a 16-bit packet
/// length and packet bytes for every frame.
/// </remarks>
public static class MovieFile
{
    /// <summary>Loads and validates a movie file.</summary>
    /// <param name="path">Movie path.</param>
    /// <returns>The parsed movie.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is malformed or corrupt.</exception>
    /// <exception cref="NotSupportedException">Thrown when the format version is unsupported.</exception>
    public static Movie Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    /// <summary>Reads a movie from a stream.</summary>
    /// <param name="stream">Readable stream positioned at the start of the movie.</param>
    /// <returns>The parsed movie.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is malformed or corrupt.</exception>
    /// <exception cref="NotSupportedException">Thrown when the format version is unsupported.</exception>
    public static Movie Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Span<byte> fileHeader = stackalloc byte[MovieFormat.FileHeaderSize];
        ReadExactly(stream, fileHeader);

        if (!fileHeader[..8].SequenceEqual(MovieFormat.Magic))
        {
            throw new InvalidDataException("The file is not a 5DO movie.");
        }

        var version = BinaryPrimitives.ReadInt32LittleEndian(fileHeader[8..]);
        if (!MovieFormat.IsSupportedVersion(version))
        {
            throw new NotSupportedException(
                $"Movie format version {version} is not supported by this build "
                + $"(supported {MovieFormat.MinSupportedVersion}-{MovieFormat.Version}).");
        }

        var headerLength = BinaryPrimitives.ReadInt32LittleEndian(fileHeader[12..]);
        var payloadLength = BinaryPrimitives.ReadInt64LittleEndian(fileHeader[16..]);
        var payloadUncompressedLength = BinaryPrimitives.ReadInt64LittleEndian(fileHeader[24..]);
        var stateLength = BinaryPrimitives.ReadInt64LittleEndian(fileHeader[32..]);
        var headerCrc = BinaryPrimitives.ReadUInt32LittleEndian(fileHeader[40..]);
        var payloadCrc = BinaryPrimitives.ReadUInt32LittleEndian(fileHeader[44..]);
        var stateCrc = BinaryPrimitives.ReadUInt32LittleEndian(fileHeader[48..]);

        if (headerLength <= 0 || headerLength > 16 * 1024 * 1024)
        {
            throw new InvalidDataException("The movie header length is invalid.");
        }

        if (payloadLength < 0 || payloadLength > MovieFormat.MaxUncompressedPayloadLength
            || payloadUncompressedLength < 0
            || payloadUncompressedLength > MovieFormat.MaxUncompressedPayloadLength)
        {
            throw new InvalidDataException("The movie input payload length is invalid.");
        }

        if (stateLength < 0 || stateLength > MovieFormat.MaxStateLength)
        {
            throw new InvalidDataException("The movie state length is invalid.");
        }

        var headerBytes = new byte[headerLength];
        ReadExactly(stream, headerBytes);
        if (MovieHash.Crc32(headerBytes) != headerCrc)
        {
            throw new InvalidDataException("The movie header checksum does not match.");
        }

        var payloadBytes = new byte[checked((int)payloadLength)];
        ReadExactly(stream, payloadBytes);
        if (MovieHash.Crc32(payloadBytes) != payloadCrc)
        {
            throw new InvalidDataException("The movie input checksum does not match.");
        }

        byte[]? state = null;
        if (stateLength > 0)
        {
            state = new byte[checked((int)stateLength)];
            ReadExactly(stream, state);
            if (MovieHash.Crc32(state) != stateCrc)
            {
                throw new InvalidDataException("The movie state checksum does not match.");
            }
        }

        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("The movie file has trailing data.");
        }

        MovieHeader? header;
        try
        {
            header = JsonSerializer.Deserialize(headerBytes, MovieJsonContext.Default.MovieHeader);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The movie header JSON is malformed.", exception);
        }

        if (header is null)
        {
            throw new InvalidDataException("The movie header is missing.");
        }

        if (header.FormatVersion != version)
        {
            throw new InvalidDataException("The movie header version does not match the file header.");
        }

        byte[] uncompressed = Decompress(payloadBytes, checked((int)payloadUncompressedLength));
        byte[][] frames = ParseFrames(uncompressed, header.FrameCount);
        if (frames.Length != header.FrameCount)
        {
            throw new InvalidDataException("The movie frame count does not match its header.");
        }

        if (header.HasState != (state is not null))
        {
            throw new InvalidDataException("The movie state flag does not match the payload.");
        }

        return new Movie(header, frames, state);
    }

    /// <summary>Writes a movie atomically to a file.</summary>
    /// <param name="path">Destination path.</param>
    /// <param name="movie">Movie to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="movie"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
    public static void Save(string path, Movie movie)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(movie);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = fullPath + ".tmp";
        try
        {
            using (FileStream stream = File.Create(temporaryPath))
            {
                Write(stream, movie);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>Writes a movie to a stream.</summary>
    /// <param name="stream">Writable stream.</param>
    /// <param name="movie">Movie to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void Write(Stream stream, Movie movie)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(movie);
        MovieHeader header = movie.Header with
        {
            FormatVersion = MovieFormat.Version,
            FrameCount = movie.FrameCount,
            HasState = movie.HasState,
        };

        byte[] headerBytes = JsonSerializer.SerializeToUtf8Bytes(
            header,
            MovieJsonContext.Default.MovieHeader);
        byte[] payload = BuildPayload(movie.Frames);
        byte[] compressed = Compress(payload);
        byte[]? state = movie.State;

        Span<byte> fileHeader = stackalloc byte[MovieFormat.FileHeaderSize];
        MovieFormat.Magic.CopyTo(fileHeader);
        BinaryPrimitives.WriteInt32LittleEndian(fileHeader[8..], MovieFormat.Version);
        BinaryPrimitives.WriteInt32LittleEndian(fileHeader[12..], headerBytes.Length);
        BinaryPrimitives.WriteInt64LittleEndian(fileHeader[16..], compressed.Length);
        BinaryPrimitives.WriteInt64LittleEndian(fileHeader[24..], payload.Length);
        BinaryPrimitives.WriteInt64LittleEndian(fileHeader[32..], state?.Length ?? 0);
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader[40..], MovieHash.Crc32(headerBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(fileHeader[44..], MovieHash.Crc32(compressed));
        BinaryPrimitives.WriteUInt32LittleEndian(
            fileHeader[48..],
            state is null ? 0 : MovieHash.Crc32(state));

        stream.Write(fileHeader);
        stream.Write(headerBytes);
        stream.Write(compressed);
        if (state is not null)
        {
            stream.Write(state);
        }
    }

    private static byte[] BuildPayload(IReadOnlyList<byte[]> frames)
    {
        var length = 4;
        foreach (byte[] frame in frames)
        {
            MovieFormat.ValidatePacketLength(frame.Length);
            length = checked(length + 2 + frame.Length);
        }

        var payload = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(payload, frames.Count);
        var offset = 4;
        foreach (byte[] frame in frames)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), (ushort)frame.Length);
            offset += 2;
            frame.CopyTo(payload, offset);
            offset += frame.Length;
        }

        return payload;
    }

    private static byte[][] ParseFrames(byte[] payload, int expectedCount)
    {
        if (payload.Length < 4)
        {
            throw new InvalidDataException("The movie input payload is truncated.");
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(payload);
        if (count < 0 || count != expectedCount)
        {
            throw new InvalidDataException("The movie input frame count is invalid.");
        }

        var frames = new byte[count][];
        var offset = 4;
        for (var i = 0; i < count; i++)
        {
            if (payload.Length - offset < 2)
            {
                throw new InvalidDataException("The movie input payload is truncated.");
            }

            var packetLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset));
            offset += 2;
            if (packetLength > MovieFormat.MaxPacketLength || payload.Length - offset < packetLength)
            {
                throw new InvalidDataException("The movie input packet length is invalid.");
            }

            frames[i] = payload.AsSpan(offset, packetLength).ToArray();
            offset += packetLength;
        }

        if (offset != payload.Length)
        {
            throw new InvalidDataException("The movie input payload has trailing data.");
        }

        return frames;
    }

    private static byte[] Compress(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var compressor = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressor.Write(payload);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] compressed, int expectedLength)
    {
        try
        {
            using var input = new MemoryStream(compressed);
            using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
            var payload = new byte[expectedLength];
            var read = 0;
            while (read < payload.Length)
            {
                var count = decompressor.Read(payload, read, payload.Length - read);
                if (count == 0)
                {
                    throw new InvalidDataException("The movie input payload is truncated.");
                }

                read += count;
            }

            if (decompressor.ReadByte() != -1)
            {
                throw new InvalidDataException("The movie input payload is longer than its header.");
            }

            return payload;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            throw new InvalidDataException("The movie input payload could not be decompressed.", exception);
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        var read = 0;
        while (read < destination.Length)
        {
            var count = stream.Read(destination[read..]);
            if (count == 0)
            {
                throw new InvalidDataException("The movie file is truncated.");
            }

            read += count;
        }
    }
}
