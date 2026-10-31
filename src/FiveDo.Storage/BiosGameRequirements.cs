using System;
using System.IO;

namespace FiveDo.Storage;

/// <summary>
/// Known per-title BIOS requirements that cannot be inferred from the game database region
/// alone. The table is intentionally small and evidence-based; unknown titles fall back to
/// region matching and per-game overrides.
/// </summary>
public static class BiosGameRequirements
{
    /// <summary>Gets the preferred primary BIOS MD5 for a disc, if one is known.</summary>
    /// <param name="game">Game metadata, or <see langword="null"/>.</param>
    /// <param name="discName">Disc path or file name, or <see langword="null"/>.</param>
    /// <returns>A lowercase MD5, or <see langword="null"/> when no title-specific rule applies.</returns>
    /// <remarks>
    /// Evidence: Slayer Europe renders its credits with <c>panafz1</c>, while Slayer USA
    /// needs <c>panafz10</c> for its title menu at 60,000 frames; Powers Kingdom (Europe)
    /// reaches its live-action intro with the Anvil FZ-10E. See docs/Compatibility.md.
    /// </remarks>
    public static string? PreferredMd5(GameRecord? game, string? discName)
    {
        var fileName = string.IsNullOrWhiteSpace(discName)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(discName);

        if (fileName.Contains("Slayer (Europe)", StringComparison.OrdinalIgnoreCase))
        {
            return "f47264dd47fe30f73ab3c010015c155b";
        }

        if (fileName.Contains("Slayer (USA)", StringComparison.OrdinalIgnoreCase))
        {
            return "51f2f43ae2f3508a14d9f56597e2d3ce";
        }

        if (fileName.Contains("Powers Kingdom (Europe)", StringComparison.OrdinalIgnoreCase))
        {
            return "a48e6746bd7edec0f40cff078f0bb19f";
        }

        if (game?.Name.Contains("Arcade", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Arcade discs use the patched image; region scoring selects the patched
            // US/EU image and the selector never uses a patch for a non-arcade disc.
            return null;
        }

        return null;
    }
}
