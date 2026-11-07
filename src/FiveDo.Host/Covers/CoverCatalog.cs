using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FiveDo.Host;

/// <summary>Result of loading a cover catalog.</summary>
/// <param name="Catalog">Parsed catalog, or <see langword="null"/> on failure.</param>
/// <param name="Index">Built lookup index, or <see langword="null"/> on failure.</param>
/// <param name="Error">Failure detail, or <see langword="null"/> on success.</param>
public sealed record CoverCatalogLoadResult(
    CoverCatalog? Catalog,
    CoverCatalogIndex? Index,
    string? Error)
{
    /// <summary>Gets a value indicating whether the catalog loaded.</summary>
    public bool Success => Catalog is not null && Index is not null;
}

/// <summary>One catalog match.</summary>
/// <param name="Entry">Matched cover entry.</param>
/// <param name="MatchedAlias">Alias or name that produced the match.</param>
/// <param name="Score">Match confidence from 0.0 to 1.0.</param>
/// <param name="Exact">Whether the match was exact or token/no-space exact.</param>
public sealed record CoverCatalogMatch(
    CoverCatalogEntry Entry,
    string MatchedAlias,
    double Score,
    bool Exact);

/// <summary>
/// Loads and validates cover catalogs and provides exact and fuzzy lookups.
/// </summary>
[SuppressMessage(
    "ReSharper",
    "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract",
    Justification = "System.Text.Json can deserialize null despite nullable annotations.")]
public sealed class CoverCatalogIndex
{
    private static readonly string[] ImageExtensions = [".webp"];

    private readonly bool _stripAnnotations;
    private readonly Dictionary<string, List<Candidate>> _byNormalized =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Candidate>> _byToken =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Candidate>> _byNoSpace =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, CoverCatalogEntry> _byGameId =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Candidate> _candidates = [];

    private CoverCatalogIndex(CoverCatalog catalog, bool stripAnnotations)
    {
        Catalog = catalog;
        _stripAnnotations = stripAnnotations;
    }

    /// <summary>Gets the loaded catalog.</summary>
    public CoverCatalog Catalog { get; }

    /// <summary>Gets the cover entries.</summary>
    public IReadOnlyList<CoverCatalogEntry> Entries =>
        Catalog.Covers as IReadOnlyList<CoverCatalogEntry> ?? Catalog.Covers.ToList();

    /// <summary>Builds an index and validates the catalog.</summary>
    /// <param name="catalog">Catalog to index.</param>
    /// <param name="stripAnnotations">Whether aliases are annotation-stripped before indexing.</param>
    /// <returns>The index.</returns>
    /// <exception cref="InvalidDataException">Thrown when the catalog is invalid.</exception>
    public static CoverCatalogIndex Build(CoverCatalog catalog, bool stripAnnotations = true)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SchemaVersion != CoverDefaults.SchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported cover catalog schema version {catalog.SchemaVersion}; expected {CoverDefaults.SchemaVersion}.");
        }

        catalog.PackVersion ??= string.Empty;
        catalog.NoticeUrl ??= string.Empty;
        catalog.ImagesBase ??= "images/";
        catalog.PackFile ??= string.Empty;
        catalog.PackSha256 ??= string.Empty;
        catalog.Covers ??= [];
        if (catalog.Covers.Count == 0)
        {
            throw new InvalidDataException("The cover catalog contains no covers.");
        }

        var index = new CoverCatalogIndex(catalog, stripAnnotations);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CoverCatalogEntry entry in catalog.Covers)
        {
            ValidateEntry(entry, seenIds);
            var candidate = index.CreateCandidate(entry);
            index._candidates.Add(candidate);
            foreach (CoverNameKeys key in candidate.Keys)
            {
                Add(index._byNormalized, key.Normalized, candidate);
                Add(index._byToken, key.TokenKey, candidate);
                if (key.NoSpaceKey.Length >= 6)
                {
                    Add(index._byNoSpace, key.NoSpaceKey, candidate);
                }
            }

            foreach (var gameId in entry.GameIds)
            {
                var normalizedId = NormalizeGameId(gameId);
                if (normalizedId.Length == 0)
                {
                    throw new InvalidDataException(
                        $"Cover '{entry.Name}' contains an invalid game ID '{gameId}'.");
                }

                if (!index._byGameId.TryAdd(normalizedId, entry))
                {
                    Serilog.Log.Debug(
                        "Cover catalog maps game ID {GameId} to more than one cover; keeping the first.",
                        normalizedId);
                }
            }
        }

        return index;
    }

    /// <summary>Finds a cover by 4DO game ID.</summary>
    /// <param name="gameId">Full or short 4DO game ID.</param>
    /// <returns>The cover entry, or <see langword="null"/>.</returns>
    public CoverCatalogEntry? FindByGameId(string? gameId)
    {
        var normalized = NormalizeGameId(gameId);
        return normalized.Length > 0 && _byGameId.TryGetValue(normalized, out CoverCatalogEntry? entry)
            ? entry
            : null;
    }

    /// <summary>Finds the best cover by title and optional filename.</summary>
    /// <param name="title">Database title or display name.</param>
    /// <param name="fileName">Optional disc filename without extension.</param>
    /// <param name="threshold">Fuzzy-match threshold.</param>
    /// <param name="fuzzy">Whether fuzzy matching is enabled.</param>
    /// <param name="gameRegions">Game region codes used for tie-breaking.</param>
    /// <param name="gameYear">Game release year used for tie-breaking.</param>
    /// <returns>The best match, or <see langword="null"/> when no candidate is confident.</returns>
    public CoverCatalogMatch? FindByTitle(
        string title,
        string? fileName,
        double threshold,
        bool fuzzy,
        string? gameRegions = null,
        string? gameYear = null)
    {
        var queryAliases = BuildQueryAliases(title, fileName);
        if (queryAliases.Count == 0)
        {
            return null;
        }

        // Exact normalized and token/no-space lookups are cheap and deterministic.
        List<Candidate>? exact = FindExact(queryAliases);
        if (exact is not null)
        {
            Candidate? chosen = Choose(exact, gameRegions, gameYear, allowAmbiguous: false);
            return chosen is null
                ? null
                : new CoverCatalogMatch(chosen.Entry, chosen.MatchedAlias, 1.0, Exact: true);
        }

        if (!fuzzy)
        {
            return null;
        }

        var bestScore = 0.0;
        var bestCandidates = new List<Candidate>(4);
        foreach (Candidate candidate in _candidates)
        {
            double score = ScoreCandidate(queryAliases, candidate);
            if (score < threshold)
            {
                continue;
            }

            if (score > bestScore + 0.0001)
            {
                bestScore = score;
                bestCandidates.Clear();
                bestCandidates.Add(candidate);
            }
            else if (Math.Abs(score - bestScore) <= 0.0001)
            {
                bestCandidates.Add(candidate);
            }
        }

        if (bestCandidates.Count == 0)
        {
            return null;
        }

        Candidate? winner = Choose(
            bestCandidates,
            gameRegions,
            gameYear,
            allowAmbiguous: bestScore < 0.995);
        if (winner is null)
        {
            return null;
        }

        return new CoverCatalogMatch(winner.Entry, winner.MatchedAlias, bestScore, Exact: false);
    }

    /// <summary>Resolves an entry image URI.</summary>
    /// <param name="catalogUri">Catalog document URI.</param>
    /// <param name="catalog">Catalog document.</param>
    /// <param name="entry">Cover entry.</param>
    /// <returns>The absolute image URI.</returns>
    public static Uri ResolveImageUri(Uri catalogUri, CoverCatalog catalog, CoverCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(catalogUri);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(entry);
        var relative = string.IsNullOrWhiteSpace(catalog.ImagesBase)
            ? entry.File
            : catalog.ImagesBase.TrimEnd('/') + "/" + entry.File;
        var uri = new Uri(catalogUri, relative);
        if (!catalogUri.IsFile && uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException($"Cover image '{entry.File}' resolves to an unsupported URI scheme.");
        }

        return uri;
    }

    /// <summary>Resolves the optional bulk-pack URI.</summary>
    /// <param name="catalogUri">Catalog document URI.</param>
    /// <param name="catalog">Catalog.</param>
    /// <returns>The absolute pack URI, or <see langword="null"/> when no pack is published.</returns>
    public static Uri? ResolvePackUri(Uri catalogUri, CoverCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalogUri);
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(catalog.PackFile))
        {
            return null;
        }

        var uri = new Uri(catalogUri, catalog.PackFile);
        if (!catalogUri.IsFile && uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException("The cover pack resolves to an unsupported URI scheme.");
        }

        return uri;
    }

    /// <summary>Normalizes a 4DO game ID to its 8-character uppercase form.</summary>
    /// <param name="gameId">Full checksum or 8-character ID.</param>
    /// <returns>The normalized ID, or an empty string.</returns>
    public static string NormalizeGameId(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return string.Empty;
        }

        var text = gameId.Trim();
        if (text.Length > 8)
        {
            text = text[..8];
        }

        if (text.Length != 8 || !text.All(IsHex))
        {
            return string.Empty;
        }

        return text.ToUpperInvariant();
    }

    private List<string> BuildQueryAliases(string title, string? fileName)
    {
        var result = new List<string>(4);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddQueryAlias(title);
        AddQueryAlias(fileName);
        if (!string.IsNullOrWhiteSpace(title))
        {
            foreach (var alias in CoverMatcher.ExpandAliases(title, _stripAnnotations))
            {
                AddQueryAlias(alias);
            }
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            foreach (var alias in CoverMatcher.ExpandAliases(fileName, _stripAnnotations))
            {
                AddQueryAlias(alias);
            }
        }

        return result;

        void AddQueryAlias(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = CoverMatcher.Normalize(value, _stripAnnotations);
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }
    }

    private List<Candidate>? FindExact(List<string> queryAliases)
    {
        foreach (var alias in queryAliases)
        {
            if (TryGet(_byNormalized, alias, out List<Candidate>? exact))
            {
                return exact;
            }
        }

        foreach (var alias in queryAliases)
        {
            var token = CoverMatcher.BuildTokenKey(alias);
            if (TryGet(_byToken, token, out List<Candidate>? exact))
            {
                return exact;
            }
        }

        foreach (var alias in queryAliases)
        {
            var noSpace = CoverMatcher.BuildNoSpaceKey(alias);
            if (noSpace.Length >= 6 && TryGet(_byNoSpace, noSpace, out List<Candidate>? exact))
            {
                return exact;
            }
        }

        return null;
    }

    private static double ScoreCandidate(List<string> queryAliases, Candidate candidate)
    {
        var best = 0.0;
        foreach (var candidateKey in candidate.Keys)
        {
            foreach (var candidateAlias in candidateKey.NormalizedAliases)
            {
                foreach (var query in queryAliases)
                {
                    if (string.Equals(query, candidateAlias, StringComparison.Ordinal))
                    {
                        return 1.0;
                    }

                    if (string.Equals(
                            CoverMatcher.BuildTokenKey(query),
                            CoverMatcher.BuildTokenKey(candidateAlias),
                            StringComparison.Ordinal))
                    {
                        best = Math.Max(best, 0.99);
                        continue;
                    }

                    if (query.Length >= 6
                        && string.Equals(
                            CoverMatcher.BuildNoSpaceKey(query),
                            CoverMatcher.BuildNoSpaceKey(candidateAlias),
                            StringComparison.Ordinal))
                    {
                        best = Math.Max(best, 0.98);
                        continue;
                    }

                    var similarity = CoverMatcher.CalculateJaroWinklerSimilarity(query, candidateAlias);
                    if (CoverMatcher.HasNumberConflict(query, candidateAlias))
                    {
                        similarity = Math.Min(similarity, 0.84);
                    }

                    best = Math.Max(best, similarity);
                }
            }
        }

        return best;
    }

    private static Candidate? Choose(
        List<Candidate> candidates,
        string? gameRegions,
        string? gameYear,
        bool allowAmbiguous)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        var ordered = candidates
            .OrderBy(candidate => candidate.Entry, CoverTieBreaker.Create(gameRegions, gameYear))
            .ToList();
        if (!allowAmbiguous)
        {
            return ordered[0];
        }

        var first = ordered[0].Entry;
        var second = ordered[1].Entry;
        var comparison = CoverTieBreaker.Create(gameRegions, gameYear).Compare(first, second);
        return comparison == 0 ? null : ordered[0];
    }

    private Candidate CreateCandidate(CoverCatalogEntry entry)
    {
        var aliases = new List<string>(entry.Aliases.Count + 1) { entry.Name };
        aliases.AddRange(entry.Aliases);
        var keys = new List<CoverNameKeys>(aliases.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            CoverNameKeys key = CoverMatcher.BuildKeys(alias, _stripAnnotations);
            if (key.Normalized.Length > 0 && seen.Add(key.Normalized))
            {
                keys.Add(key);
            }
        }

        if (keys.Count == 0)
        {
            throw new InvalidDataException($"Cover '{entry.Name}' has no usable aliases.");
        }

        return new Candidate(entry, keys, keys[0].Alias);
    }

    private static void Add(
        Dictionary<string, List<Candidate>> index,
        string key,
        Candidate candidate)
    {
        if (key.Length == 0)
        {
            return;
        }

        if (!index.TryGetValue(key, out List<Candidate>? values))
        {
            values = [];
            index[key] = values;
        }

        if (!values.Contains(candidate))
        {
            values.Add(candidate);
        }
    }

    private static bool TryGet(
        Dictionary<string, List<Candidate>> index,
        string key,
        out List<Candidate>? values)
    {
        if (index.TryGetValue(key, out values))
        {
            return true;
        }

        return false;
    }

    private static bool IsHex(char character)
    {
        return character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';
    }

    private static void ValidateEntry(CoverCatalogEntry entry, HashSet<string> seenIds)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Id ??= string.Empty;
        entry.File ??= string.Empty;
        entry.Name ??= string.Empty;
        entry.Aliases ??= [];
        entry.GameIds ??= [];
        entry.Regions ??= [];
        var id = entry.Id.Trim().ToLowerInvariant();
        if (id.Length != 64 || !id.All(IsHex))
        {
            throw new InvalidDataException($"Cover '{entry.Name}' has an invalid SHA-256 identifier.");
        }

        if (!seenIds.Add(id))
        {
            throw new InvalidDataException($"Cover catalog repeats the image identifier '{id}'.");
        }

        var file = entry.File.Trim();
        if (file.Length == 0
            || file.Contains("..", StringComparison.Ordinal)
            || file.Contains('/')
            || file.Contains('\\')
            || Path.IsPathRooted(file))
        {
            throw new InvalidDataException($"Cover '{entry.Name}' has an unsafe image file name.");
        }

        var extension = Path.GetExtension(file);
        if (!ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Cover '{entry.Name}' has an unsupported image extension.");
        }

        if (!string.Equals(Path.GetFileNameWithoutExtension(file), id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Cover '{entry.Name}' file name does not match its content identifier.");
        }

        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            throw new InvalidDataException("A cover entry has an empty display name.");
        }

        foreach (var gameId in entry.GameIds)
        {
            if (NormalizeGameId(gameId).Length == 0)
            {
                throw new InvalidDataException(
                    $"Cover '{entry.Name}' contains an invalid game ID '{gameId}'.");
            }
        }
    }

    private sealed record Candidate(
        CoverCatalogEntry Entry,
        IReadOnlyList<CoverNameKeys> Keys,
        string MatchedAlias);
}

/// <summary>Loads cover catalogs from JSON.</summary>
public static class CoverCatalogLoader
{
    /// <summary>Loads and validates a catalog from JSON text.</summary>
    /// <param name="json">Catalog JSON.</param>
    /// <param name="stripAnnotations">Whether aliases are annotation-stripped before indexing.</param>
    /// <returns>The load result.</returns>
    public static CoverCatalogLoadResult LoadJson(string json, bool stripAnnotations = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        json = json.TrimStart('\uFEFF');
        try
        {
            CoverCatalog? catalog = JsonSerializer.Deserialize(
                json,
                CoverCatalogJsonContext.Default.CoverCatalog);
            if (catalog is null)
            {
                return new CoverCatalogLoadResult(null, null, "The cover catalog is empty.");
            }

            CoverCatalogIndex index = CoverCatalogIndex.Build(catalog, stripAnnotations);
            return new CoverCatalogLoadResult(catalog, index, null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
        {
            return new CoverCatalogLoadResult(null, null, exception.Message);
        }
    }

    /// <summary>Loads and validates a catalog from a file.</summary>
    /// <param name="path">Catalog file path.</param>
    /// <param name="stripAnnotations">Whether aliases are annotation-stripped before indexing.</param>
    /// <returns>The load result.</returns>
    public static CoverCatalogLoadResult LoadFile(string path, bool stripAnnotations = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return LoadJson(File.ReadAllText(path), stripAnnotations);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new CoverCatalogLoadResult(null, null, exception.Message);
        }
    }
}

/// <summary>Compares catalog entries for region, year, and retail preference.</summary>
internal sealed class CoverTieBreaker : IComparer<CoverCatalogEntry>
{
    private readonly HashSet<string> _gameRegions;
    private readonly string _gameYear;

    private CoverTieBreaker(string? gameRegions, string? gameYear)
    {
        _gameRegions = ParseRegions(gameRegions);
        _gameYear = gameYear?.Trim() ?? string.Empty;
    }

    internal static CoverTieBreaker Create(string? gameRegions, string? gameYear)
    {
        return new CoverTieBreaker(gameRegions, gameYear);
    }

    public int Compare(CoverCatalogEntry? x, CoverCatalogEntry? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        var region = RegionScore(y).CompareTo(RegionScore(x));
        if (region != 0)
        {
            return region;
        }

        var year = YearScore(y).CompareTo(YearScore(x));
        if (year != 0)
        {
            return year;
        }

        var rank = x.Rank.CompareTo(y.Rank);
        if (rank != 0)
        {
            return rank;
        }

        var length = x.Name.Length.CompareTo(y.Name.Length);
        if (length != 0)
        {
            return length;
        }

        return string.Compare(x.Id, y.Id, StringComparison.Ordinal);
    }

    private int RegionScore(CoverCatalogEntry entry)
    {
        if (_gameRegions.Count == 0 || entry.Regions.Count == 0)
        {
            return 0;
        }

        foreach (var region in entry.Regions)
        {
            if (_gameRegions.Contains(region))
            {
                return 1;
            }
        }

        return 0;
    }

    private int YearScore(CoverCatalogEntry entry)
    {
        return _gameYear.Length > 0
               && string.Equals(entry.Year, _gameYear, StringComparison.Ordinal)
            ? 1
            : 0;
    }

    private static HashSet<string> ParseRegions(string? regions)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(regions))
        {
            return result;
        }

        foreach (var token in regions.Split(
                     ['-', ',', '/', ' ', ';'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result.Add(token.ToUpperInvariant());
        }

        return result;
    }
}
