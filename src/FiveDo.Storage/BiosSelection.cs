using System;

namespace FiveDo.Storage;

/// <summary>
/// Options that influence automatic BIOS selection.
/// </summary>
/// <param name="AllowPatched">Allow patched/arcade BIOS images for non-arcade discs.</param>
/// <param name="RequireKanji">Only consider BIOS images with Kanji support.</param>
/// <param name="PreferAnvil">Prefer an Anvil image when the region match is otherwise equal.</param>
/// <param name="PreferredMd5">A per-game BIOS override; takes precedence over region matching.</param>
/// <param name="DiscName">Optional disc file name used for known-title requirements.</param>
/// <param name="AllowUnsignedDiscs">
/// Allow No-RSA BIOS images and prefer them when choosing a BIOS for an unsigned disc.
/// </param>
public sealed record BiosSelectionOptions(
    bool AllowPatched = false,
    bool RequireKanji = false,
    bool PreferAnvil = false,
    string? PreferredMd5 = null,
    string? DiscName = null,
    bool AllowUnsignedDiscs = false);

/// <summary>
/// The result of choosing a BIOS set for a game.
/// </summary>
/// <param name="Primary">The selected 1 MB BIOS, or <see langword="null"/> when none is usable.</param>
/// <param name="Secondary">The optional BIOS2/Kanji font paired with <paramref name="Primary"/>.</param>
/// <param name="Reason">A human-readable explanation of the choice.</param>
public sealed record BiosSelection(BiosProfile? Primary, BiosProfile? Secondary, string Reason)
{
    /// <summary>Gets a value indicating whether a primary BIOS was selected.</summary>
    public bool HasBios => Primary is not null;

    /// <summary>Creates an empty selection with an explanation.</summary>
    /// <param name="reason">Why no BIOS could be selected.</param>
    /// <returns>An empty selection.</returns>
    public static BiosSelection None(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new BiosSelection(null, null, reason);
    }
}
