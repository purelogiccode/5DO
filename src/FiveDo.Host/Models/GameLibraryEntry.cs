
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FiveDo.Host;


/// <summary>One disc found by the library scanner.</summary>
/// <param name="Path">Disc file path.</param>
/// <param name="Format">Container format label.</param>
/// <param name="SectorCount">Number of 2048-byte sectors.</param>
/// <param name="GameId">MD5 game identifier, or empty when the image is unreadable.</param>
/// <param name="Name">Database title, or the file name for unknown discs.</param>
/// <param name="Known">Whether the 4DO game database recognized the disc.</param>
/// <param name="Error">Error message when the image could not be opened.</param>
/// <param name="CoverPath">Cover-art image path, or empty when none was found.</param>
/// <param name="Pending">
/// Whether the entry was listed before its game ID and sector count were computed.
/// </param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "NotAccessedPositionalProperty.Global",
    Justification = "The game identifier is part of the scanner's public result for host tooling.")]
public sealed record GameLibraryEntry(
    string Path,
    string Format,
    int SectorCount,
    string GameId,
    string Name,
    bool Known,
    string Error,
    string CoverPath = "",
    bool Pending = false)
{
    /// <summary>Gets the release year from the game database or an offline metadata overlay.</summary>
    public string ReleaseYear { get; init; } = string.Empty;

    /// <summary>Gets the publisher from the game database or an offline metadata overlay.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>Gets the region codes from the game database or an offline metadata overlay.</summary>
    public string Regions { get; init; } = string.Empty;

    /// <summary>Gets the developer from an offline metadata overlay.</summary>
    public string Developer { get; init; } = string.Empty;

    /// <summary>Gets the genre from an offline metadata overlay.</summary>
    public string Genre { get; init; } = string.Empty;

    /// <summary>Gets the supported player count from an offline metadata overlay.</summary>
    public int Players { get; init; }

    /// <summary>Gets the description from an offline metadata overlay.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the cover-resolution source label, or an empty string.</summary>
    public string CoverSource { get; init; } = string.Empty;

    /// <summary>Gets the cover-match confidence from 0.0 to 1.0.</summary>
    public double CoverConfidence { get; init; }

    /// <summary>Gets the cover alias or file name that matched.</summary>
    public string CoverMatchName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the user marked this disc as a favorite.</summary>
    public bool Favorite { get; init; }

    /// <summary>Gets the accumulated play time in seconds.</summary>
    public long PlaytimeSeconds { get; init; }

    /// <summary>Gets the UTC timestamp of the last launch or play session.</summary>
    public DateTime? LastPlayedUtc { get; init; }

    /// <summary>Gets the number of times the disc was opened.</summary>
    public int LaunchCount { get; init; }

    /// <summary>Gets the favorite indicator shown by the desktop shell.</summary>
    public string FavoriteGlyph => Favorite ? "★" : "☆";

    /// <summary>Gets a compact metadata line for the library views.</summary>
    public string MetadataText
    {
        get
        {
            var parts = new List<string>(4);
            Add(parts, ReleaseYear);
            Add(parts, Publisher);
            Add(parts, Developer);
            Add(parts, Genre);
            Add(parts, Regions);
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Gets the accumulated play time formatted for the library views.</summary>
    public string PlaytimeText => PlaytimeSeconds <= 0
        ? string.Empty
        : FormatPlaytime(PlaytimeSeconds);

    /// <summary>Gets the last-played timestamp formatted for the library views.</summary>
    public string LastPlayedText => LastPlayedUtc is null
        ? string.Empty
        : LastPlayedUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    /// <summary>Gets a detailed tooltip line for the library views.</summary>
    public string DetailText
    {
        get
        {
            var parts = new List<string>(5);
            Add(parts, MetadataText);
            Add(parts, Players > 0 ? string.Create(CultureInfo.InvariantCulture, $"{Players} players") : string.Empty);
            Add(parts, PlaytimeText.Length == 0 ? string.Empty : $"Played {PlaytimeText}");
            Add(parts, LaunchCount > 0 ? string.Create(CultureInfo.InvariantCulture, $"Launched {LaunchCount} times") : string.Empty);
            Add(parts, Description);
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Gets the cached disc-health status, or <see cref="DiscHealthStatus.Unknown"/>.</summary>
    public DiscHealthStatus DumpStatus { get; init; }

    /// <summary>Gets the matched DAT game name, or an empty string.</summary>
    public string DumpGameName { get; init; } = string.Empty;

    /// <summary>Gets the disc-health detail (hashes, DAT match, or read error).</summary>
    public string DumpDetail { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the disc has a current health result.</summary>
    public bool HasDumpBadge => DumpStatus != DiscHealthStatus.Unknown;

    /// <summary>Gets the compact disc-health badge text.</summary>
    public string DumpBadge => DumpStatus switch
    {
        DiscHealthStatus.Verified => "Known dump",
        DiscHealthStatus.Mismatch => "Not in DAT",
        DiscHealthStatus.ReadError => "Read error",
        DiscHealthStatus.NoDat => "Checked",
        _ => string.Empty,
    };

    /// <summary>Gets the disc-health tooltip text.</summary>
    public string DumpTip
    {
        get
        {
            if (DumpStatus == DiscHealthStatus.Unknown)
            {
                return string.Empty;
            }

            var parts = new List<string>(2) { DumpBadge };
            Add(parts, DumpGameName);
            Add(parts, DumpDetail);
            return string.Join(" · ", parts);
        }
    }

    private static void Add(List<string> parts, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
    }

    private static string FormatPlaytime(long seconds)
    {
        if (seconds >= 3600)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{seconds / 3600}h {seconds % 3600 / 60:00}m");
        }

        return seconds >= 60
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}m")
            : string.Create(CultureInfo.InvariantCulture, $"{seconds}s");
    }
}
