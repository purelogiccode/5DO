using System;

namespace FiveDo.Core;

/// <summary>
/// CRC-32 (IEEE 802.3, reflected polynomial 0xEDB88320) used to validate state sections.
/// </summary>
internal static class StateCrc32
{
    private static readonly uint[] Table = CreateTable();

    /// <summary>Computes the CRC-32 of a byte span.</summary>
    /// <param name="data">Data to checksum.</param>
    /// <returns>The checksum.</returns>
    internal static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] CreateTable()
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
