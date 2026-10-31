namespace FiveDo.Core;

/// <summary>
/// A named bundle of machine timing options.
/// </summary>
/// <remarks>
/// Profiles are a convenience over <see cref="EmulatorTuning"/>: the individual options
/// remain the source of truth and are recorded in save states. <see cref="Custom"/> leaves
/// the current options unchanged. Selecting a profile never changes per-game fixes,
/// high-resolution rendering, or the random seed.
/// </remarks>
public enum EmulationProfile
{
    /// <summary>The safe FreeDO-compatible timing baseline (the default).</summary>
    Compatible = 0,

    /// <summary>ARM60 datasheet N-cycles plus the Opera timer, CEL, and CD timing models.</summary>
    Accuracy = 1,

    /// <summary>FreeDO-compatible timing with a modest 1.5x CPU overclock.</summary>
    Performance = 2,

    /// <summary>Keep the current individual options.</summary>
    Custom = 3,
}
