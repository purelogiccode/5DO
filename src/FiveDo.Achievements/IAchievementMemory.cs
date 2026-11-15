namespace FiveDo.Achievements;

/// <summary>
/// A little-endian view of the 3DO main RAM exposed to RetroAchievements.
/// </summary>
/// <remarks>
/// The addresses are the raw byte offsets of Opera's <c>DRAM</c> array (the same
/// bytes RetroArch exposes as <c>RETRO_MEMORY_SYSTEM_RAM</c>). Implementations
/// must be safe to call from the emulator thread and should return zero for
/// out-of-range reads.
/// </remarks>
public interface IAchievementMemory
{
    /// <summary>Reads one to four bytes and packs them in little-endian order.</summary>
    /// <param name="address">Byte address in the 3DO DRAM image.</param>
    /// <param name="numBytes">Number of bytes to read (1-4).</param>
    /// <returns>The packed value, or zero when the address is outside the RAM image.</returns>
    uint Peek(uint address, uint numBytes);
}
