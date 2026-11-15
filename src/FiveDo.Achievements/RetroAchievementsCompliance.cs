using FiveDo.Core;

namespace FiveDo.Achievements;

/// <summary>
/// RetroAchievements client-recognition state. RetroAchievements injects the zero-point
/// "Warning: Unknown Emulator" achievement until the client's User-Agent is approved for
/// hardcore credit. Flip <see cref="HardcoreApproved"/> only after the server recognizes
/// 5DO and the hardcore validation suite has passed.
/// </summary>
public static class RetroAchievementsCompliance
{
    /// <summary>Gets a value indicating whether 5DO is recognized for hardcore credit.</summary>
    public static bool HardcoreApproved => IsHardcoreApproved();

    /// <summary>Gets the stable User-Agent submitted with every request.</summary>
    public static string UserAgent => $"5DO/{FiveDoVersion.Version}";

    private static bool IsHardcoreApproved() => false;
}
