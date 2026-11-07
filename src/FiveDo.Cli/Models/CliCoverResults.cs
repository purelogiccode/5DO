using System.Collections.Generic;

namespace FiveDo.Cli;

/// <summary>Result of <c>covers update</c>.</summary>
internal sealed record CliCoverUpdateResult(
    bool Ok,
    string CatalogUrl,
    string CacheDirectory,
    string PackVersion,
    int CoverCount,
    bool FromCache,
    bool NotModified,
    string? Error);

/// <summary>Result of <c>covers match</c>.</summary>
internal sealed record CliCoverMatchResult(
    bool Ok,
    string Query,
    IReadOnlyList<CliCoverMatchEntry> Matches,
    string? Error);

/// <summary>One candidate cover returned by <c>covers match</c>.</summary>
internal sealed record CliCoverMatchEntry(
    string CoverId,
    string Name,
    string MatchedAlias,
    double Score,
    bool Exact,
    IReadOnlyList<string> GameIds);

/// <summary>Result of <c>covers download</c>.</summary>
internal sealed record CliCoverDownloadResult(
    bool Ok,
    int Matched,
    int Downloaded,
    int Skipped,
    int Pending,
    int Failed,
    long Bytes,
    bool UsedPack,
    IReadOnlyList<CliCoverDownloadFailure> Failures,
    string? Error);

/// <summary>One failed cover download.</summary>
internal sealed record CliCoverDownloadFailure(string EntryPath, string CoverId, string Message);

/// <summary>Result of <c>covers verify</c>.</summary>
internal sealed record CliCoverVerifyResult(
    bool Ok,
    int Valid,
    int Missing,
    int Corrupt,
    long Bytes,
    IReadOnlyList<CliCoverVerifyFailure> Failures,
    string? Error);

/// <summary>One failed cache verification.</summary>
internal sealed record CliCoverVerifyFailure(string CoverId, string Path, string Message);

/// <summary>Result of a <c>covers cache</c> action.</summary>
internal sealed record CliCoverCacheResult(
    bool Ok,
    string Command,
    string Directory,
    int ImageCount,
    long Bytes,
    bool CatalogPresent,
    bool PackPresent,
    string? Error);
