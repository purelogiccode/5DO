namespace ThreeDo.Core;

/// <summary>
/// Immutable per-game compatibility information sourced from the game database.
/// </summary>
/// <param name="FixMode">The FreeDO fix-mode bits to apply.</param>
/// <param name="SpeedFixes">Whether the CEL timing heuristics are enabled.</param>
/// <param name="UnknownFlag11">Whether the undocumented CEL flag 11 heuristic is enabled.</param>
/// <param name="Jw">Whether the <c>jw</c> compatibility quirk is enabled.</param>
public readonly record struct GameQuirks(
    FixMode FixMode,
    bool SpeedFixes = false,
    bool UnknownFlag11 = false,
    bool Jw = false)
{
    /// <summary>Gets a <see cref="GameQuirks"/> value with no fixes enabled.</summary>
    public static GameQuirks None => default;
}
