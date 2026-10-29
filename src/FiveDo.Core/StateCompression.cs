using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace FiveDo.Core;

/// <summary>
/// Brotli compression for large state-section payloads.
/// </summary>
/// <remarks>
/// A compressed section payload is stored as a little-endian uncompressed length
/// followed by the Brotli stream. The section CRC is always computed over the
/// uncompressed payload, so a state can be validated without trusting the compressor.
/// </remarks>
internal static class StateCompression
{
    /// <summary>Compresses a payload when it is large enough and the result is smaller.</summary>
    /// <param name="payload">Uncompressed section payload.</param>
    /// <param name="stored">Receives the compressed framing when the method returns true.</param>
    /// <returns><see langword="true"/> when the payload should be stored compressed.</returns>
    internal static bool TryCompress(ReadOnlySpan<byte> payload, out byte[] stored)
    {
        stored = [];
        if (payload.Length < StateFormat.CompressionThreshold)
        {
            return false;
        }

        using var compressed = new MemoryStream();
        using (var brotli = new BrotliStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(payload);
        }

        if (compressed.Length + StateFormat.CompressedSectionPrefixSize >= payload.Length)
        {
            return false;
        }

        stored = new byte[StateFormat.CompressedSectionPrefixSize + (int)compressed.Length];
        BinaryPrimitives.WriteInt32LittleEndian(stored, payload.Length);
        compressed.Position = 0;
        compressed.ReadExactly(stored.AsSpan(StateFormat.CompressedSectionPrefixSize));
        return true;
    }

    /// <summary>Decompresses a section payload written by <see cref="TryCompress"/>.</summary>
    /// <param name="stored">Compressed framing, including the uncompressed-length prefix.</param>
    /// <param name="sectionId">Section identifier used in error messages.</param>
    /// <returns>The uncompressed payload.</returns>
    /// <exception cref="InvalidStateException">Thrown when the framing or Brotli stream is invalid.</exception>
    internal static byte[] DecompressSection(ReadOnlySpan<byte> stored, uint sectionId)
    {
        if (stored.Length < StateFormat.CompressedSectionPrefixSize)
        {
            throw new InvalidStateException($"Section 0x{sectionId:X8} has a truncated compressed payload.");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(stored);
        if (length < 0 || length > StateFormat.MaxUncompressedSectionLength)
        {
            throw new InvalidStateException($"Section 0x{sectionId:X8} has an invalid uncompressed length {length}.");
        }

        if (length == 0)
        {
            if (stored.Length != StateFormat.CompressedSectionPrefixSize)
            {
                throw new InvalidStateException($"Section 0x{sectionId:X8} has trailing compressed data.");
            }

            return [];
        }

        try
        {
            using var input = new MemoryStream(
                stored.ToArray(),
                StateFormat.CompressedSectionPrefixSize,
                stored.Length - StateFormat.CompressedSectionPrefixSize,
                writable: false);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);
            var payload = new byte[length];
            brotli.ReadExactly(payload);
            if (brotli.ReadByte() != -1)
            {
                throw new InvalidStateException($"Section 0x{sectionId:X8} has trailing compressed data.");
            }

            return payload;
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidStateException($"Section 0x{sectionId:X8} could not be decompressed.", exception);
        }
        catch (InvalidOperationException exception)
        {
            // BrotliStream reports malformed input as InvalidOperationException on
            // some runtimes ("Decoder ran into invalid data").
            throw new InvalidStateException($"Section 0x{sectionId:X8} could not be decompressed.", exception);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidStateException($"Section 0x{sectionId:X8} is truncated after decompression.", exception);
        }
    }
}
