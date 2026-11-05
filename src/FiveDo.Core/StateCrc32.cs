using System;
using System.Buffers.Binary;

namespace FiveDo.Core;

/// <summary>
/// CRC-32 (IEEE 802.3, reflected polynomial 0xEDB88320) used to validate state sections.
/// </summary>
/// <remarks>
/// Uses a slicing-by-8 lookup so large sections (the memory section is several megabytes)
/// do not dominate save/rewind time. The result is identical to the byte-at-a-time
/// algorithm defined by the state format.
/// </remarks>
internal static class StateCrc32
{
    private static readonly uint[][] Tables = CreateTables();

    /// <summary>Computes the CRC-32 of a byte span.</summary>
    /// <param name="data">Data to checksum.</param>
    /// <returns>The checksum.</returns>
    internal static uint Compute(ReadOnlySpan<byte> data)
    {
        uint[] table = Tables[0];
        var crc = 0xFFFFFFFFu;
        var index = 0;

        // Slice by eight while a full eight-byte block is available.
        while (data.Length - index >= 8)
        {
            crc ^= BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(index, 4));
            var next = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(index + 4, 4));
            crc = Tables[7][crc & 0xFF]
                ^ Tables[6][(crc >> 8) & 0xFF]
                ^ Tables[5][(crc >> 16) & 0xFF]
                ^ Tables[4][(crc >> 24) & 0xFF]
                ^ Tables[3][next & 0xFF]
                ^ Tables[2][(next >> 8) & 0xFF]
                ^ Tables[1][(next >> 16) & 0xFF]
                ^ table[next >> 24];
            index += 8;
        }

        while (index < data.Length)
        {
            crc = table[(crc ^ data[index]) & 0xFF] ^ (crc >> 8);
            index++;
        }

        return ~crc;
    }

    private static uint[][] CreateTables()
    {
        var tables = new uint[8][];
        tables[0] = new uint[256];
        for (uint i = 0; i < tables[0].Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            tables[0][i] = value;
        }

        for (var table = 1; table < tables.Length; table++)
        {
            tables[table] = new uint[256];
            for (var i = 0; i < 256; i++)
            {
                var previous = tables[table - 1][i];
                tables[table][i] = (previous >> 8) ^ tables[0][previous & 0xFF];
            }
        }

        return tables;
    }
}
