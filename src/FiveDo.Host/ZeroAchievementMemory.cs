using FiveDo.Achievements;

namespace FiveDo.Host;

/// <summary>An all-zero achievement memory used when no machine has been created yet.</summary>
public sealed class ZeroAchievementMemory : IAchievementMemory
{
    /// <inheritdoc />
    public uint Peek(uint address, uint numBytes) => 0;
}
