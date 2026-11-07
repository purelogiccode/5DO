using System;
using System.Collections.Generic;

namespace FiveDo.Host;

/// <summary>
/// One online cover-art catalog. The document is published as
/// <c>catalog.json</c> and references content-addressed WebP images.
/// </summary>
public sealed class CoverCatalog
{
    /// <summary>Gets or sets the catalog schema version.</summary>
    public int SchemaVersion { get; set; } = CoverDefaults.SchemaVersion;

    /// <summary>Gets or sets the cover-pack version label.</summary>
    public string PackVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC generation timestamp.</summary>
    public DateTimeOffset? GeneratedUtc { get; set; }

    /// <summary>Gets or sets the optional rights/takedown notice URL.</summary>
    public string NoticeUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the image path prefix, relative to the catalog URL.</summary>
    public string ImagesBase { get; set; } = "images/";

    /// <summary>Gets or sets the optional bulk-pack path, relative to the catalog URL.</summary>
    public string PackFile { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional bulk-pack size in bytes.</summary>
    public long PackSize { get; set; }

    /// <summary>Gets or sets the optional bulk-pack SHA-256 hash.</summary>
    public string PackSha256 { get; set; } = string.Empty;

    /// <summary>Gets the cover entries.</summary>
    public IList<CoverCatalogEntry> Covers { get; set; } = [];
}

/// <summary>One cover image in the online catalog.</summary>
public sealed class CoverCatalogEntry
{
    /// <summary>Gets or sets the SHA-256 identifier of the encoded WebP image.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the image file name, relative to <see cref="CoverCatalog.ImagesBase"/>.</summary>
    public string File { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets the original cover-file names that resolve to this image.</summary>
    public IList<string> Aliases { get; set; } = [];

    /// <summary>Gets the 4DO game IDs mapped to this cover by the packager.</summary>
    public IList<string> GameIds { get; set; } = [];

    /// <summary>Gets or sets the encoded image width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Gets or sets the encoded image height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Gets or sets the encoded image size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Gets the region codes parsed from the source file names.</summary>
    public IList<string> Regions { get; set; } = [];

    /// <summary>Gets or sets the release year parsed from the source file names.</summary>
    public string Year { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release rank: 0 retail, 1 demo/sample, 2 beta/proto, 3 unknown.
    /// </summary>
    public int Rank { get; set; }
}

/// <summary>Sidecar metadata for the cached catalog.</summary>
public sealed class CoverCatalogCacheMetadata
{
    /// <summary>Gets or sets the catalog schema version.</summary>
    public int SchemaVersion { get; set; } = CoverDefaults.SchemaVersion;

    /// <summary>Gets or sets the source catalog URL.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTTP ETag returned by the server.</summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC fetch timestamp.</summary>
    public DateTimeOffset FetchedUtc { get; set; }
}

/// <summary>Explicit options for one cover service instance.</summary>
public sealed class CoverServiceOptions
{
    /// <summary>Gets the catalog URI.</summary>
    public Uri CatalogUri { get; init; } = new(CoverDefaults.CatalogUrl);

    /// <summary>Gets a value indicating whether the online catalog may be used.</summary>
    public bool UseOnlineCovers { get; init; } = true;

    /// <summary>Gets a value indicating whether fuzzy title matching is enabled.</summary>
    public bool FuzzyMatching { get; init; } = true;

    /// <summary>Gets a value indicating whether annotations are stripped before matching.</summary>
    public bool AnnotationStripping { get; init; } = true;

    /// <summary>Gets the fuzzy-match threshold.</summary>
    public double FuzzyThreshold { get; init; } = CoverDefaults.DefaultFuzzyThreshold;

    /// <summary>Gets the optional user cover directory.</summary>
    public string CoverDirectory { get; init; } = string.Empty;

    /// <summary>Gets the optional cache directory override.</summary>
    public string CacheDirectory { get; init; } = string.Empty;

    /// <summary>Gets how long a cached catalog is considered fresh for matching.</summary>
    public TimeSpan CatalogCacheDuration { get; init; } = TimeSpan.FromHours(6);

    /// <summary>Creates options from persisted settings.</summary>
    /// <param name="settings">Application settings.</param>
    /// <returns>Cover-service options.</returns>
    public static CoverServiceOptions FromSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new CoverServiceOptions
        {
            CatalogUri = new Uri(CoverDefaults.NormalizeCatalogUrl(settings.CoverCatalogUrl)),
            UseOnlineCovers = settings.UseOnlineCovers,
            FuzzyMatching = settings.CoverFuzzyMatching,
            AnnotationStripping = settings.CoverAnnotationStripping,
            FuzzyThreshold = CoverDefaults.ClampFuzzyThreshold(settings.CoverFuzzyThreshold),
            CoverDirectory = settings.CoverDirectory,
        };
    }
}

/// <summary>Result of one cover-catalog refresh.</summary>
/// <param name="Success">Whether a usable catalog is available.</param>
/// <param name="FromCache">Whether the cached catalog was used.</param>
/// <param name="NotModified">Whether the server returned HTTP 304.</param>
/// <param name="PackVersion">Catalog pack version.</param>
/// <param name="CoverCount">Number of cover entries.</param>
/// <param name="Error">Optional failure detail.</param>
public sealed record CoverCatalogRefreshResult(
    bool Success,
    bool FromCache,
    bool NotModified,
    string PackVersion,
    int CoverCount,
    string? Error);

/// <summary>One resolved cover for a library entry.</summary>
/// <param name="EntryPath">Disc path that was resolved.</param>
/// <param name="CoverPath">Local cover path, including a not-yet-downloaded cache path.</param>
/// <param name="CoverSource">Resolution source label.</param>
/// <param name="Score">Match confidence from 0.0 to 1.0.</param>
/// <param name="MatchedName">Cover alias or catalog name that matched.</param>
/// <param name="CoverId">Catalog image identifier, or empty for local files.</param>
/// <param name="NeedsDownload">Whether the local cache file still needs to be downloaded.</param>
public sealed record CoverResolution(
    string EntryPath,
    string? CoverPath,
    string CoverSource,
    double Score,
    string MatchedName,
    string CoverId = "",
    bool NeedsDownload = false)
{
    /// <summary>Gets a value indicating whether a cover path was resolved.</summary>
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverPath);
}

/// <summary>Progress for a batch cover download.</summary>
/// <param name="Completed">Completed items.</param>
/// <param name="Total">Total items.</param>
/// <param name="Bytes">Bytes written.</param>
/// <param name="CurrentName">Current cover name.</param>
public sealed record CoverDownloadProgress(
    int Completed,
    int Total,
    long Bytes,
    string CurrentName);

/// <summary>One failed cover download.</summary>
/// <param name="EntryPath">Disc path, or empty for a catalog-wide download.</param>
/// <param name="CoverId">Catalog image identifier.</param>
/// <param name="Message">Failure detail.</param>
public sealed record CoverDownloadFailure(string EntryPath, string CoverId, string Message);

/// <summary>Result of a batch cover download.</summary>
/// <param name="Downloaded">Downloaded items.</param>
/// <param name="Skipped">Already-cached items.</param>
/// <param name="Failed">Failed items.</param>
/// <param name="Bytes">Bytes written.</param>
/// <param name="UsedPack">Whether the bulk pack was used.</param>
/// <param name="Failures">Failed items.</param>
/// <param name="CoverPaths">Resolved cover paths keyed by disc path.</param>
public sealed record CoverDownloadResult(
    int Downloaded,
    int Skipped,
    int Failed,
    long Bytes,
    bool UsedPack,
    IReadOnlyList<CoverDownloadFailure> Failures,
    IReadOnlyDictionary<string, string> CoverPaths);

/// <summary>Progress for a cache verification.</summary>
/// <param name="Completed">Completed items.</param>
/// <param name="Total">Total items.</param>
/// <param name="CurrentName">Current cover name.</param>
public sealed record CoverVerifyProgress(int Completed, int Total, string CurrentName);

/// <summary>One failed cache verification.</summary>
/// <param name="CoverId">Catalog image identifier.</param>
/// <param name="Path">Cached file path.</param>
/// <param name="Message">Failure detail.</param>
public sealed record CoverVerifyFailure(string CoverId, string Path, string Message);

/// <summary>Result of a cache verification.</summary>
/// <param name="Ok">Valid cached images.</param>
/// <param name="Missing">Missing images.</param>
/// <param name="Corrupt">Corrupt images.</param>
/// <param name="Bytes">Valid bytes.</param>
/// <param name="Failures">Failed items.</param>
public sealed record CoverVerifyResult(
    int Ok,
    int Missing,
    int Corrupt,
    long Bytes,
    IReadOnlyList<CoverVerifyFailure> Failures);

/// <summary>Current cover-cache statistics.</summary>
/// <param name="ImageCount">Number of cached images.</param>
/// <param name="Bytes">Total cached image bytes.</param>
/// <param name="CatalogPresent">Whether a cached catalog exists.</param>
/// <param name="PackPresent">Whether a cached pack exists.</param>
public sealed record CoverCacheStats(int ImageCount, long Bytes, bool CatalogPresent, bool PackPresent);
