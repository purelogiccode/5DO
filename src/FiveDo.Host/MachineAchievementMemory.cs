using System;
using FiveDo.Achievements;
using FiveDo.Memory;

namespace FiveDo.Host;

/// <summary>
/// Exposes the raw 3DO DRAM byte view used by Opera and RetroArch to the achievement
/// runtime. The rcheevos 3DO memory map covers the first 2 MB of DRAM.
/// </summary>
public sealed class MachineAchievementMemory(FiveDoMemory memory) : IAchievementMemory
{
    private const uint VisibleRamSize = 2 * 1024 * 1024;

    private readonly FiveDoMemory _memory = memory ?? throw new ArgumentNullException(nameof(memory));

    /// <inheritdoc />
    public uint Peek(uint address, uint numBytes)
    {
        if (numBytes is 0 or > 4 || address >= VisibleRamSize)
        {
            return 0;
        }

        ReadOnlySpan<uint> words = _memory.Words;
        uint value = 0;
        for (uint index = 0; index < numBytes; index++)
        {
            ulong byteIndex = (ulong)address + index;
            if (byteIndex >= VisibleRamSize || byteIndex >= (ulong)words.Length * sizeof(uint))
            {
                return 0;
            }

            uint word = words[(int)(byteIndex >> 2)];
            byte lane = (byte)(word >> (int)((byteIndex & 3) * 8));
            value |= (uint)lane << (int)(index * 8);
        }

        return value;
    }
}
