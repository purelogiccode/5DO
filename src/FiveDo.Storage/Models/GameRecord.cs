namespace FiveDo.Storage;


/// <summary>
/// One entry from the 4DO game database.
/// </summary>
/// <param name="Id">The 4DO game identifier.</param>
/// <param name="CheckSum">MD5 of the first two disc sectors, uppercase hex.</param>
/// <param name="Name">Game title.</param>
/// <param name="ReleaseYear">Release year.</param>
/// <param name="Publisher">Publisher.</param>
/// <param name="Regions">Region codes.</param>
public sealed record GameRecord(
    string Id,
    string CheckSum,
    string Name,
    string ReleaseYear,
    string Publisher,
    string Regions)
{
    /// <summary>Gets the revision label (for example <c>Rev 1</c>), or an empty string.</summary>
    public string Revision { get; init; } = string.Empty;

    /// <summary>Gets the disc number for a multi-disc title, or <see langword="null"/>.</summary>
    public int? DiscNumber { get; init; }

    /// <summary>Gets the total number of discs in the set, or <see langword="null"/>.</summary>
    public int? DiscTotal { get; init; }

    /// <summary>Gets the preferred primary BIOS basename, or an empty string for automatic selection.</summary>
    public string Bios { get; init; } = string.Empty;

    /// <summary>Gets the preferred secondary BIOS basename (for example a Kanji font), or an empty string.</summary>
    public string Bios2 { get; init; } = string.Empty;

    /// <summary>Gets the per-game fix mask, or <see langword="null"/> for the built-in compatibility table.</summary>
    public uint? FixMode { get; init; }

    /// <summary>Gets the per-game PBUS device list (for example <c>1=joypad,2=mouse</c>), or an empty string.</summary>
    public string InputDevices { get; init; } = string.Empty;

    /// <summary>Gets the preferred NVRAM storage mode (<c>auto</c>, <c>persistent</c>, or <c>disabled</c>), or an empty string.</summary>
    public string Nvram { get; init; } = string.Empty;

    /// <summary>Gets the per-game cheat bundle name, or an empty string.</summary>
    public string Cheats { get; init; } = string.Empty;
}
