using System;

namespace FiveDo.Storage;

/// <summary>
/// The role of a file in a 3DO BIOS set.
/// </summary>
public enum BiosImageKind
{
    /// <summary>A 1 MB primary BIOS image loaded into ROM bank 0.</summary>
    Primary,

    /// <summary>A smaller optional BIOS2, Kanji font, or arcade support ROM.</summary>
    Secondary,
}

/// <summary>
/// The territory a BIOS image was intended for.
/// </summary>
public enum BiosTerritory
{
    /// <summary>The territory could not be determined.</summary>
    Unknown,

    /// <summary>North America.</summary>
    Usa,

    /// <summary>Japan.</summary>
    Japan,

    /// <summary>Europe.</summary>
    Europe,

    /// <summary>Korea.</summary>
    Korea,

    /// <summary>Arcade CD-ROM hardware.</summary>
    Arcade,

    /// <summary>Region-free or world hardware.</summary>
    World,
}

/// <summary>
/// The video standard a BIOS image is designed for.
/// </summary>
public enum BiosVideoStandard
{
    /// <summary>The video standard could not be determined.</summary>
    Unknown,

    /// <summary>NTSC 320x240 at 60 Hz.</summary>
    Ntsc,

    /// <summary>PAL 320x288 or 384x288 at 50 Hz.</summary>
    Pal,
}

/// <summary>
/// Metadata for one file discovered by <see cref="BiosLibrary"/>.
/// </summary>
/// <param name="Path">Full path to the file.</param>
/// <param name="FileName">File name without a directory.</param>
/// <param name="Md5">Lowercase MD5 of the file.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="Kind">Primary or secondary role.</param>
/// <param name="Territory">Target territory.</param>
/// <param name="VideoStandard">Target video standard.</param>
/// <param name="IsKanji">Whether the file provides Kanji font data.</param>
/// <param name="IsAnvil">Whether the Anvil workaround is required.</param>
/// <param name="IsPatched">Whether the image carries an arcade/unsigned-disc patch.</param>
/// <param name="Name">Human-readable profile name.</param>
/// <param name="Notes">Selection and compatibility notes.</param>
/// <param name="Preference">Higher values win when two profiles are otherwise equivalent.</param>
/// <param name="IsRsaDisabled">Whether the image has the media RSA signature check disabled.</param>
public sealed record BiosProfile(
    string Path,
    string FileName,
    string Md5,
    long Size,
    BiosImageKind Kind,
    BiosTerritory Territory,
    BiosVideoStandard VideoStandard,
    bool IsKanji,
    bool IsAnvil,
    bool IsPatched,
    string Name,
    string Notes,
    int Preference,
    bool IsRsaDisabled = false)
{
    /// <summary>Gets a value indicating whether this is a primary BIOS image.</summary>
    public bool IsPrimary => Kind == BiosImageKind.Primary;

    /// <summary>Gets a value indicating whether this is an optional secondary ROM.</summary>
    public bool IsSecondary => Kind == BiosImageKind.Secondary;

    /// <summary>Gets a compact region label such as <c>USA (NTSC)</c>.</summary>
    public string RegionLabel
    {
        get
        {
            var territory = Territory switch
            {
                BiosTerritory.Usa => "USA",
                BiosTerritory.Japan => "Japan",
                BiosTerritory.Europe => "Europe",
                BiosTerritory.Korea => "Korea",
                BiosTerritory.Arcade => "Arcade",
                BiosTerritory.World => "World",
                _ => "Unknown",
            };

            var standard = VideoStandard switch
            {
                BiosVideoStandard.Ntsc => "NTSC",
                BiosVideoStandard.Pal => "PAL",
                _ => "unknown video",
            };

            return $"{territory} ({standard})";
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Name} [{Md5[..Math.Min(8, Md5.Length)]}]";
    }
}
