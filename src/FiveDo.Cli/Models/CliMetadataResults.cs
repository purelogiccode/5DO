using System.Collections.Generic;

namespace FiveDo.Cli;

/// <summary>One metadata record in a CLI response.</summary>
internal sealed record CliMetadataRecord(
    string GameId,
    string Path,
    string Title,
    string Developer,
    string Publisher,
    string ReleaseYear,
    string Genre,
    string Regions,
    int Players,
    string Description,
    IReadOnlyList<string> Media);

/// <summary>Result of an external metadata lookup.</summary>
internal sealed record CliMetadataLookupResult(
    bool Ok,
    string Query,
    string? GameId,
    bool FromCache,
    string? Warning,
    CliMetadataRecord? Record,
    IReadOnlyList<CliMetadataRecord> Matches);

/// <summary>Result of a manual media attachment operation.</summary>
internal sealed record CliMetadataMediaResult(
    bool Ok,
    string DiscPath,
    string GameId,
    IReadOnlyList<string> Media,
    string? Error);
