using System;
using System.Collections.Generic;

namespace FiveDo.Host;

/// <summary>
/// Titles where the experimental true 2x CEL path has been visually validated.
/// </summary>
/// <remarks>
/// The true 2x path is available for every title; this list is used for validation
/// reporting and warnings, not to gate the option. Bitmap/SPORT sampling and
/// framebuffer-read semantics are still incomplete, so unvalidated titles may misrender
/// and are expected to be tested individually.
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
    /// <returns><see langword="true"/> when the title has a validated true 2x result.</returns>
    public static bool IsValidated(string? gameId)
    {
        return gameId is { Length: >= 8 }
            && ValidatedGameIdPrefixes.Contains(gameId[..8]);
    }
}
