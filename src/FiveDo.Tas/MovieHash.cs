using System;

namespace FiveDo.Tas;

/// <summary>
/// Hash helpers used by the movie container and verification.
/// </summary>
/// <remarks>
/// <see cref="Fnv1a"/> matches <c>FiveDo.Video.FrameHasher.HashBytes</c> and the CLI's
/// state hash, so movie evidence and checkpoint evidence use the same contract.
/// </remarks>
public static class MovieHash
{
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>Computes the FNV-1a 32-bit hash of a byte span.</summary>
    /// <param name="bytes">Bytes to hash.</param>
    /// <returns>The hash.</returns>
    public static uint Fnv1a(ReadOnlySpan<byte> bytes)
    {
        var hash = FnvOffsetBasis;
        foreach (var value in bytes)
        {
            hash = unchecked((hash ^ value) * FnvPrime);
        }

        return hash;
    }

    /// <summary>Computes the CRC-32 (IEEE 802.3) of a byte span.</summary>
    /// <param name="bytes">Bytes to checksum.</param>
    /// <returns>The checksum.</returns>
    public static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
