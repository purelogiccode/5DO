using System;
using System.Collections.Generic;

namespace FiveDo.Host;

/// <summary>
/// Titles where the experimental true 2x CEL path has been visually validated.
/// </summary>
/// <remarks>
/// The true 2x path still has presentation work outstanding for bitmap/SPORT content and
/// the bottom VDL rows. Road Rash, in particular, shows stale/rainbow rows at scene
/// transitions. The host therefore enables the mode only for the titles below and falls
/// back to native 320x240 everywhere else.
/// </remarks>
public static class TrueHighResolutionCompatibility
{
    private static readonly HashSet<string> ValidatedGameIdPrefixes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "18036597", // Jurassic Park Interactive (US)
            "55f8ccf1", // Family Feud (US)
        };

    /// <summary>Gets the human-readable names of the validated titles.</summary>
    public static IReadOnlyList<string> ValidatedTitles { get; } =
        Array.AsReadOnly(["Jurassic Park Interactive", "Family Feud"]);

    /// <summary>Tests whether a 3DO game ID has a validated true 2x result.</summary>
    /// <param name="gameId">Full or eight-character game ID.</param>
    /// <returns><see langword="true"/> when the true 2x path may be used.</returns>
    public static bool IsValidated(string? gameId)
    {
        return gameId is { Length: >= 8 }
            && ValidatedGameIdPrefixes.Contains(gameId[..8]);
    }
}
