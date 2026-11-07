using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>One local cover match.</summary>
/// <param name="Path">Cover file path.</param>
/// <param name="MatchedAlias">File name or alias that matched.</param>
/// <param name="Score">Match confidence.</param>
internal sealed record LocalCoverMatch(
    string Path,
    string MatchedAlias,
    double Score);

/// <summary>
/// Indexes a local cover directory for exact and fuzzy name matching. The index is
/// read-only after construction and can be shared by concurrent library resolution.
/// </summary>
internal sealed class LocalCoverIndex
{
    private const string ChecksumPlaceholder = "00000000000000000000000000000000";

    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp"];

    private readonly Dictionary<string, LocalCandidate> _byBaseName =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<LocalCandidate>> _byNormalized =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<LocalCandidate>> _byToken =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<LocalCandidate>> _byNoSpace =
        new(StringComparer.Ordinal);
    private readonly List<LocalCandidate> _candidates = [];
    private readonly bool _stripAnnotations;

    private LocalCoverIndex(string directory, bool stripAnnotations)
    {
        DirectoryPath = directory;
        _stripAnnotations = stripAnnotations;
    }

    /// <summary>Gets the indexed directory.</summary>
    public string DirectoryPath { get; }

    /// <summary>Gets the indexed file count.</summary>
    public int Count => _candidates.Count;

    /// <summary>Builds an index for one directory.</summary>
    /// <param name="directory">Directory to index.</param>
    /// <param name="stripAnnotations">Whether annotations are stripped before matching.</param>
    /// <returns>The index, which may be empty.</returns>
    public static LocalCoverIndex Build(string directory, bool stripAnnotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var index = new LocalCoverIndex(Path.GetFullPath(directory), stripAnnotations);
        try
        {
            foreach (var file in Directory.EnumerateFiles(index.DirectoryPath))
            {
                if (!IsCoverFile(file))
                {
                    continue;
                }

                index.Add(file);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Cover directory {Directory} could not be indexed.", directory);
        }

        return index;
    }

    /// <summary>Finds a cover by exact file base name.</summary>
    /// <param name="baseName">Disc base name.</param>
    /// <returns>The cover path, or <see langword="null"/>.</returns>
    public string? FindExact(string? baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return null;
        }

        return _byBaseName.TryGetValue(baseName, out LocalCandidate? candidate)
            ? candidate.Path
            : null;
    }

    /// <summary>Finds the best local cover by title and optional disc file name.</summary>
    /// <param name="title">Database title or display name.</param>
    /// <param name="fileName">Optional disc file name without extension.</param>
    /// <param name="threshold">Fuzzy-match threshold.</param>
    /// <param name="fuzzy">Whether fuzzy matching is enabled.</param>
    /// <param name="gameRegions">Game region codes for tie-breaking.</param>
    /// <param name="gameYear">Game release year for tie-breaking.</param>
    /// <returns>The best match, or <see langword="null"/>.</returns>
    public LocalCoverMatch? Find(
        string? title,
        string? fileName,
        double threshold,
        bool fuzzy,
        string? gameRegions,
        string? gameYear)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var exact = FindExact(fileName);
            if (exact is not null)
            {
                return new LocalCoverMatch(exact, Path.GetFileNameWithoutExtension(exact), 1.0);
            }
        }

        var queryAliases = BuildQueryAliases(title, fileName);
        if (queryAliases.Count == 0)
        {
            return null;
        }

        List<LocalCandidate>? exactCandidates = FindExactCandidates(queryAliases);
        if (exactCandidates is not null)
        {
            LocalCandidate? chosen = Choose(exactCandidates, gameRegions, gameYear, allowAmbiguous: false);
            return chosen is null
                ? null
                : new LocalCoverMatch(chosen.Path, chosen.Name, 1.0);
        }

        if (!fuzzy)
        {
            return null;
        }

        var bestScore = 0.0;
        var best = new List<LocalCandidate>(4);
        foreach (LocalCandidate candidate in _candidates)
        {
            double score = ScoreCandidate(queryAliases, candidate);
            if (score < threshold)
            {
                continue;
            }

            if (score > bestScore + 0.0001)
            {
                bestScore = score;
                best.Clear();
                best.Add(candidate);
            }
            else if (Math.Abs(score - bestScore) <= 0.0001)
            {
                best.Add(candidate);
            }
        }

        if (best.Count == 0)
        {
            return null;
        }

        LocalCandidate? winner = Choose(
            best,
            gameRegions,
            gameYear,
            allowAmbiguous: bestScore < 0.995);
        return winner is null
            ? null
            : new LocalCoverMatch(winner.Path, winner.Name, bestScore);
    }

    private void Add(string file)
    {
        var baseName = Path.GetFileNameWithoutExtension(file);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return;
        }

        var parsed = ParseMetadata(file);
        var aliases = new List<string>(4) { baseName };
        if (!string.IsNullOrWhiteSpace(parsed.Name)
            && !string.Equals(parsed.Name, baseName, StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add(parsed.Name);
        }

        foreach (var alias in CoverMatcher.ExpandAliases(baseName, _stripAnnotations))
        {
            if (!aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
            {
                aliases.Add(alias);
            }
        }

        var keys = new List<CoverNameKeys>(aliases.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            CoverNameKeys key = CoverMatcher.BuildKeys(alias, _stripAnnotations);
            if (key.Normalized.Length > 0 && seen.Add(key.Normalized))
            {
                keys.Add(key);
            }
        }

        if (keys.Count == 0)
        {
            return;
        }

        var candidate = new LocalCandidate(
            file,
            baseName,
            keys,
            parsed.Regions,
            parsed.ReleaseYear,
            RankFromRevision(parsed.Revision),
            aliases);
        _candidates.Add(candidate);
        _byBaseName.TryAdd(baseName, candidate);
        foreach (var key in keys)
        {
            Add(_byNormalized, key.Normalized, candidate);
            Add(_byToken, key.TokenKey, candidate);
            if (key.NoSpaceKey.Length >= 6)
            {
                Add(_byNoSpace, key.NoSpaceKey, candidate);
            }
        }
    }

    private List<string> BuildQueryAliases(string? title, string? fileName)
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

    private List<LocalCandidate>? FindExactCandidates(List<string> queryAliases)
    {
        foreach (var alias in queryAliases)
        {
            if (_byNormalized.TryGetValue(alias, out List<LocalCandidate>? values))
            {
                return values;
            }
        }

        foreach (var alias in queryAliases)
        {
            if (_byToken.TryGetValue(CoverMatcher.BuildTokenKey(alias), out List<LocalCandidate>? values))
            {
                return values;
            }
        }

        foreach (var alias in queryAliases)
        {
            var noSpace = CoverMatcher.BuildNoSpaceKey(alias);
            if (noSpace.Length >= 6
                && _byNoSpace.TryGetValue(noSpace, out List<LocalCandidate>? values))
            {
                return values;
            }
        }

        return null;
    }

    private static double ScoreCandidate(List<string> queryAliases, LocalCandidate candidate)
    {
        var best = 0.0;
        foreach (CoverNameKeys key in candidate.Keys)
        {
            foreach (var alias in key.NormalizedAliases)
            {
                foreach (var query in queryAliases)
                {
                    if (string.Equals(query, alias, StringComparison.Ordinal))
                    {
                        return 1.0;
                    }

                    if (string.Equals(
                            CoverMatcher.BuildTokenKey(query),
                            CoverMatcher.BuildTokenKey(alias),
                            StringComparison.Ordinal))
                    {
                        best = Math.Max(best, 0.99);
                        continue;
                    }

                    if (query.Length >= 6
                        && string.Equals(
                            CoverMatcher.BuildNoSpaceKey(query),
                            CoverMatcher.BuildNoSpaceKey(alias),
                            StringComparison.Ordinal))
                    {
                        best = Math.Max(best, 0.98);
                        continue;
                    }

                    var similarity = CoverMatcher.CalculateJaroWinklerSimilarity(query, alias);
                    if (CoverMatcher.HasNumberConflict(query, alias))
                    {
                        similarity = Math.Min(similarity, 0.84);
                    }

                    best = Math.Max(best, similarity);
                }
            }
        }

        return best;
    }

    private static LocalCandidate? Choose(
        List<LocalCandidate> candidates,
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

        var regions = ParseRegions(gameRegions);
        var year = gameYear?.Trim() ?? string.Empty;
        var ordered = candidates
            .OrderByDescending(candidate => RegionScore(candidate, regions))
            .ThenByDescending(candidate => string.Equals(candidate.Year, year, StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Name.Length)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!allowAmbiguous)
        {
            return ordered[0];
        }

        var first = ordered[0];
        var second = ordered[1];
        var tied = RegionScore(first, regions) == RegionScore(second, regions)
                   && string.Equals(first.Year, second.Year, StringComparison.Ordinal)
                   && first.Rank == second.Rank;
        return tied ? null : first;
    }

    private static int RegionScore(LocalCandidate candidate, HashSet<string> regions)
    {
        if (regions.Count == 0 || candidate.Regions.Length == 0)
        {
            return 0;
        }

        var candidateRegions = ParseRegions(candidate.Regions);
        return candidateRegions.Overlaps(regions) ? 1 : 0;
    }

    private static HashSet<string> ParseRegions(string? value)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        foreach (var token in value.Split(
                     ['-', ',', '/', ' ', ';'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result.Add(token.ToUpperInvariant());
        }

        return result;
    }

    private static GameRecord ParseMetadata(string path)
    {
        try
        {
            return GameDatabaseNameParser.Describe(path, ChecksumPlaceholder);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "Cover file {Path} metadata could not be parsed.", path);
            return new GameRecord(string.Empty, ChecksumPlaceholder, Path.GetFileNameWithoutExtension(path), string.Empty, string.Empty, string.Empty);
        }
    }

    private static int RankFromRevision(string revision)
    {
        if (revision.Length == 0)
        {
            return 0;
        }

        if (revision.Contains("demo", StringComparison.OrdinalIgnoreCase)
            || revision.Contains("sample", StringComparison.OrdinalIgnoreCase)
            || revision.Contains("preview", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return revision.Contains("beta", StringComparison.OrdinalIgnoreCase)
               || revision.Contains("alpha", StringComparison.OrdinalIgnoreCase)
               || revision.Contains("proto", StringComparison.OrdinalIgnoreCase)
            ? 2
            : 0;
    }

    private static void Add(
        Dictionary<string, List<LocalCandidate>> index,
        string key,
        LocalCandidate candidate)
    {
        if (key.Length == 0)
        {
            return;
        }

        if (!index.TryGetValue(key, out List<LocalCandidate>? values))
        {
            values = [];
            index[key] = values;
        }

        if (!values.Contains(candidate))
        {
            values.Add(candidate);
        }
    }

    private static bool IsCoverFile(string path)
    {
        var extension = Path.GetExtension(path);
        return Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record LocalCandidate(
        string Path,
        string Name,
        IReadOnlyList<CoverNameKeys> Keys,
        string Regions,
        string Year,
        int Rank,
        IReadOnlyList<string> Aliases);
}
