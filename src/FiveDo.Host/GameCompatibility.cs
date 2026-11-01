using System;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>
/// Maps 4DO game IDs to the per-game compatibility flags FreeDOCore expects.
/// </summary>
/// <remarks>
/// <para>
/// 4DO applies these fixes from <c>GameConsole.OpenGameSource</c> after the game source
/// reports its ID. The mapping is deliberately kept in one place so the CLI and desktop
/// shell apply the same behavior. Unknown IDs get no fixes.
/// </para>
/// <para>
/// A host may override the automatic result (see <see cref="AppSettings.ResolveFixMode"/>);
/// the core never guesses a fix from disc contents.
/// </para>
/// </remarks>
public static class GameCompatibility
{
    /// <summary>Gets the fixes for a 4DO game ID (the first eight hex characters of the two-sector MD5).</summary>
    /// <param name="gameId">Game ID, case-insensitive. A full MD5 is accepted and truncated to eight characters.</param>
    /// <returns>The matching quirks, or <see cref="GameQuirks.None"/>.</returns>
    public static GameQuirks ForGameId(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return GameQuirks.None;
        }

        var normalized = gameId.Trim().ToUpperInvariant();
        if (normalized.Length > 8)
        {
            normalized = normalized[..8];
        }

        return normalized switch
        {
            // Crash 'n Burn (JP/US): LDM value/PC timing.
            "F3AF1B13" or "217344B0" => new GameQuirks(FixMode.BitTiming1),

            // Street Fighter II: intro sync (flag retained for the 4DO database mapping).
            "DBB419FA" or "07C32F10" or "5282889F" or "7340307E" => new GameQuirks(FixMode.BitTiming2),

            // Dinopark Tycoon: scale Y * 5.
            "CB8EE795" => new GameQuirks(FixMode.BitTiming3),

            // Microcosm: VDLP current/previous bitmap offsets.
            "1A370EBA" or "B35C911D" => new GameQuirks(FixMode.BitTiming5),

            // Alone in the Dark and Samurai Shodown no longer need per-title fixes:
            // the generic XYPOSH/XYPOSL CEL origin model and the corrected literal
            // CEL read row cover both cases. The old BitTiming6/GraphicsStepY bits
            // remain defined for old save states but are no longer assigned.
            _ => GameQuirks.None,
        };
    }

    /// <summary>Computes the game ID and returns its fixes.</summary>
    /// <param name="disc">Disc source.</param>
    /// <returns>The matching quirks, or <see cref="GameQuirks.None"/>.</returns>
    public static GameQuirks ForDisc(IDiscSource disc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        return ForGameId(GameId.Compute(disc));
    }

    /// <summary>Applies fixes to a machine, replacing the current fix-mode bits.</summary>
    /// <param name="machine">Machine to update.</param>
    /// <param name="gameId">4DO game ID, or <see langword="null"/> for none.</param>
    /// <param name="overrideMode">Explicit fix-mode override, or <see langword="null"/> to use the database.</param>
    /// <returns>The applied fix-mode bits.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="machine"/> is null.</exception>
    public static FixMode ApplyTo(FiveDoMachine machine, string? gameId, FixMode? overrideMode = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        FixMode mode = overrideMode ?? ForGameId(gameId).FixMode;
        machine.Compatibility.FixMode = mode;
        return mode;
    }
}
