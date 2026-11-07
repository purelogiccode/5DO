using System;
using System.Collections.Generic;

// These properties are populated by the packager and consumed by the JSON report.
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FiveDo.CoverPackager;

/// <summary>One deduplicated source image and its encoded WebP bytes.</summary>
internal sealed class CoverSource
{
    /// <summary>Gets the SHA-256 identifier of the encoded WebP.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets the original cover names.</summary>
    public List<string> Aliases { get; } = [];

    /// <summary>Gets the mapped 4DO game IDs.</summary>
    public SortedSet<string> GameIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the parsed region codes.</summary>
    public List<string> Regions { get; init; } = [];

    /// <summary>Gets the parsed release year.</summary>
    public string Year { get; init; } = string.Empty;

    /// <summary>Gets the release rank.</summary>
    public int Rank { get; init; }

    /// <summary>Gets the encoded width.</summary>
    public int Width { get; init; }

    /// <summary>Gets the encoded height.</summary>
    public int Height { get; init; }

    /// <summary>Gets the encoded size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Gets the encoded WebP bytes.</summary>
    public byte[] WebpBytes { get; init; } = [];
}

/// <summary>Optional manual game-ID overrides.</summary>
internal sealed class CoverOverrides
{
    /// <summary>Gets or sets the override schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets the game-ID to cover-alias/ID overrides.</summary>
    public Dictionary<string, string> GameIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One automatically or manually mapped game ID.</summary>
internal sealed class CoverMappingReport
{
    /// <summary>Gets or sets the game ID.</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>Gets or sets the game name.</summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>Gets or sets the cover ID.</summary>
    public string CoverId { get; set; } = string.Empty;

    /// <summary>Gets or sets the cover display name.</summary>
    public string CoverName { get; set; } = string.Empty;

    /// <summary>Gets or sets the match score.</summary>
    public double Score { get; set; }

    /// <summary>Gets or sets a value indicating whether the match was exact.</summary>
    public bool Exact { get; set; }

    /// <summary>Gets or sets the mapping source.</summary>
    public string Source { get; set; } = "automatic";
}

/// <summary>One game ID without a confident cover.</summary>
internal sealed class UnmappedGameReport
{
    /// <summary>Gets or sets the game ID.</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>Gets or sets the game name.</summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>Gets or sets the best score seen.</summary>
    public double BestScore { get; set; }

    /// <summary>Gets or sets the best cover name seen.</summary>
    public string BestCoverName { get; set; } = string.Empty;
}

/// <summary>One duplicate alias group.</summary>
internal sealed class DuplicateReport
{
    /// <summary>Gets or sets the cover ID.</summary>
    public string CoverId { get; set; } = string.Empty;

    /// <summary>Gets the duplicate source names.</summary>
    public List<string> Aliases { get; } = [];
}

/// <summary>Build report written beside the catalog.</summary>
internal sealed class CoverPackReport
{
    /// <summary>Gets or sets the pack version.</summary>
    public string PackVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the source file count.</summary>
    public int InputFiles { get; set; }

    /// <summary>Gets or sets the unique source image count.</summary>
    public int UniqueInputImages { get; set; }

    /// <summary>Gets or sets the output cover count.</summary>
    public int OutputCovers { get; set; }

    /// <summary>Gets or sets the input bytes read.</summary>
    public long InputBytes { get; set; }

    /// <summary>Gets or sets the encoded image bytes written.</summary>
    public long OutputImageBytes { get; set; }

    /// <summary>Gets or sets the bulk-pack bytes written.</summary>
    public long OutputPackBytes { get; set; }

    /// <summary>Gets or sets the database game count.</summary>
    public int DatabaseGames { get; set; }

    /// <summary>Gets or sets the mapped game-ID count.</summary>
    public int MappedGameIds { get; set; }

    /// <summary>Gets or sets the weak-mapping count.</summary>
    public int WeakMappings { get; set; }

    /// <summary>Gets or sets the applied override count.</summary>
    public int OverridesApplied { get; set; }

    /// <summary>Gets the per-game mappings.</summary>
    public List<CoverMappingReport> Mappings { get; } = [];

    /// <summary>Gets the unmapped games.</summary>
    public List<UnmappedGameReport> UnmappedGames { get; } = [];

    /// <summary>Gets the duplicate alias groups.</summary>
    public List<DuplicateReport> Duplicates { get; } = [];
}
