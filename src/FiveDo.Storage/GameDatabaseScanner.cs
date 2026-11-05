using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>One disc discovered by <see cref="GameDatabaseScanner"/>.</summary>
/// <param name="Path">Disc path.</param>
/// <param name="CheckSum">Uppercase MD5 of the first two sectors.</param>
/// <param name="Record">Existing database record, or a record derived from the filename.</param>
/// <param name="Known">True when the embedded database already contains the checksum.</param>
public sealed record GameDatabaseScanEntry(
    string Path,
    string CheckSum,
    GameRecord Record,
    bool Known);

/// <summary>Result of scanning a folder for 3DO discs.</summary>
/// <param name="Entries">Successfully scanned discs, ordered by path.</param>
/// <param name="Failures">Files that could not be read, as <c>path: message</c>.</param>
public sealed record GameDatabaseScanResult(
    IReadOnlyList<GameDatabaseScanEntry> Entries,
    IReadOnlyList<string> Failures)
{
    /// <summary>Gets the number of successfully scanned discs.</summary>
    public int DiscCount => Entries.Count;

    /// <summary>Gets the number of distinct checksums.</summary>
    public int UniqueCount => Entries.Select(entry => entry.CheckSum).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>Gets the number of distinct checksums already in the database.</summary>
    public int KnownCount => Entries.Where(entry => entry.Known)
        .Select(entry => entry.CheckSum)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    /// <summary>Gets the number of disc images whose checksum is already in the database.</summary>
    public int KnownImageCount => Entries.Count(entry => entry.Known);

    /// <summary>Gets the number of disc images whose checksum is missing from the database.</summary>
    public int MissingImageCount => DiscCount - KnownImageCount;

    /// <summary>Gets paths that produced more than one scan entry (diagnostic).</summary>
    public IReadOnlyList<string> DuplicatePaths => Entries
        .GroupBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .Take(10)
        .ToList();

    /// <summary>Gets the number of distinct entry paths (diagnostic).</summary>
    public int UniqueEntryPaths => Entries
        .Select(entry => entry.Path)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    /// <summary>Gets the number of distinct checksums missing from the database.</summary>
    public int MissingCount => UniqueCount - KnownCount;
}

/// <summary>Scans disc images and derives database records.</summary>
public static class GameDatabaseScanner
{
    private static readonly string[] Extensions = [".chd", ".cue", ".iso"];

    /// <summary>Enumerates supported disc images below a folder.</summary>
    /// <param name="folder">Folder to scan.</param>
    /// <param name="recursive">Whether to recurse into subdirectories.</param>
    /// <returns>Disc paths in stable order.</returns>
    public static IReadOnlyList<string> FindDiscs(string folder, bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"Disc folder not found: '{folder}'.");
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };
        return Directory.EnumerateFiles(folder, "*", options)
            .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Scans every disc image in a folder.</summary>
    /// <param name="folder">Folder to scan.</param>
    /// <param name="database">Existing database used to keep curated records.</param>
    /// <param name="recursive">Whether to recurse into subdirectories.</param>
    /// <returns>Scan results.</returns>
    public static GameDatabaseScanResult ScanFolder(
        string folder,
        GameDatabase database,
        bool recursive = true)
    {
        return Scan(FindDiscs(folder, recursive), database);
    }

    /// <summary>Scans disc images and derives database records.</summary>
    /// <param name="paths">Disc paths.</param>
    /// <param name="database">Existing database used to keep curated records.</param>
    /// <param name="maxParallelism">Maximum concurrent disc reads; zero uses the processor count.</param>
    /// <returns>Scan results.</returns>
    public static GameDatabaseScanResult Scan(
        IEnumerable<string> paths,
        GameDatabase database,
        int maxParallelism = 0)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(database);

        var entries = new ConcurrentDictionary<string, GameDatabaseScanEntry>(StringComparer.OrdinalIgnoreCase);
        var failures = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, GameRecord> byName = database.Records
            .GroupBy(record => NormalizeName(record.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallelism > 0
                ? maxParallelism
                : Math.Min(Environment.ProcessorCount, 8),
        };

        void ScanOne(string path)
        {
            try
            {
                using IDiscSource disc = DiscImageFactory.Open(path);
                string checkSum = GameId.Compute(disc).ToUpperInvariant();
                GameRecord? known = database.FindByChecksum(checkSum);
                GameRecord record = known ?? Enrich(GameDatabaseNameParser.Describe(path, checkSum), byName);
                string fullPath = Path.GetFullPath(path);
                entries[fullPath] = new GameDatabaseScanEntry(fullPath, checkSum, record, known is not null);
                failures.TryRemove(path, out _);
            }
            catch (Exception exception) when (exception is IOException
                                                  or UnauthorizedAccessException
                                                  or InvalidDataException
                                                  or DiscImageException
                                                  or ArgumentException
                                                  or NotSupportedException)
            {
                failures[path] = $"{path}: {exception.Message}";
            }
        }

        Parallel.ForEach(paths, options, ScanOne);

        // CHDSharp occasionally reports a transient "parameter is incorrect" when many
        // CHDs are opened at once. Retry failures serially so a large scan is stable.
        foreach (string path in failures.Keys.ToArray())
        {
            ScanOne(path);
        }

        return new GameDatabaseScanResult(
            entries.Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList(),
            failures.Values.OrderBy(failure => failure, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static GameRecord Enrich(GameRecord derived, Dictionary<string, GameRecord> byName)
    {
        if (!byName.TryGetValue(NormalizeName(derived.Name), out GameRecord? curated))
        {
            return derived;
        }

        return derived with
        {
            ReleaseYear = derived.ReleaseYear.Length == 0 ? curated.ReleaseYear : derived.ReleaseYear,
            Publisher = derived.Publisher.Length == 0 ? curated.Publisher : derived.Publisher,
            Regions = derived.Regions.Length == 0 ? curated.Regions : derived.Regions,
            Bios = derived.Bios.Length == 0 ? curated.Bios : derived.Bios,
            Bios2 = derived.Bios2.Length == 0 ? curated.Bios2 : derived.Bios2,
            FixMode = derived.FixMode ?? curated.FixMode,
            InputDevices = derived.InputDevices.Length == 0 ? curated.InputDevices : derived.InputDevices,
            Nvram = derived.Nvram.Length == 0 ? curated.Nvram : derived.Nvram,
            Cheats = derived.Cheats.Length == 0 ? curated.Cheats : derived.Cheats,
        };
    }

    private static string NormalizeName(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }
}

/// <summary>
/// Derives a <see cref="GameRecord"/> from a Redump/GoodTools/No-Intro-style filename.
/// </summary>
/// <remarks>
/// The parser intentionally keeps unknown parenthetical groups in the title. Only the
/// well-known release tags (year, publisher position, region, language, disc, revision,
/// and dump/edition markers) are consumed. Missing fields are left empty rather than
/// guessed, and a curated database record always wins over a derived one.
/// </remarks>
public static class GameDatabaseNameParser
{
    private static readonly Dictionary<string, string> RegionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["usa"] = "US",
        ["us"] = "US",
        ["u.s.a."] = "US",
        ["america"] = "US",
        ["north america"] = "US",
        ["europe"] = "EU",
        ["eu"] = "EU",
        ["japan"] = "JP",
        ["jp"] = "JP",
        ["korea"] = "KR",
        ["kr"] = "KR",
        ["brazil"] = "BR",
        ["br"] = "BR",
        ["germany"] = "DE",
        ["de"] = "DE",
        ["france"] = "FR",
        ["fr"] = "FR",
        ["spain"] = "ES",
        ["es"] = "ES",
        ["italy"] = "IT",
        ["it"] = "IT",
        ["australia"] = "AU",
        ["au"] = "AU",
        ["canada"] = "CA",
        ["ca"] = "CA",
        ["china"] = "CN",
        ["cn"] = "CN",
        ["taiwan"] = "TW",
        ["tw"] = "TW",
        ["russia"] = "RU",
        ["ru"] = "RU",
        ["world"] = "WORLD",
        ["asia"] = "ASIA",
        ["hong kong"] = "HK",
        ["hk"] = "HK",
        ["netherlands"] = "NL",
        ["nl"] = "NL",
        ["sweden"] = "SE",
        ["se"] = "SE",
        ["finland"] = "FI",
        ["fi"] = "FI",
        ["denmark"] = "DK",
        ["dk"] = "DK",
        ["norway"] = "NO",
        ["no"] = "NO",
        ["poland"] = "PL",
        ["pl"] = "PL",
        ["portugal"] = "PT",
        ["pt"] = "PT",
        ["mexico"] = "MX",
        ["mx"] = "MX",
        ["argentina"] = "AR",
        ["ar"] = "AR",
    };

    private static readonly HashSet<string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "fr", "de", "es", "it", "nl", "pt", "ru", "ja", "jp", "ko", "kr", "zh", "cn",
        "tw", "sv", "no", "da", "fi", "pl", "cs", "hu", "tr", "ar", "he", "el", "th", "vi",
    };

    private static readonly HashSet<string> EditionTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "!", "b", "a", "f", "h", "o", "p", "t", "u", "v",
        "unl", "alt", "m4", "m5", "r18", "sample", "demo", "beta", "alpha", "proto", "prototype",
        "promo", "rental", "not for resale", "ntsc version", "pal version", "ntsc", "pal",
        "nfr", "rev a", "rev b", "rev c", "rev 1", "rev 2", "rev 3", "ver 1.0", "ver 1.1",
        "en,f r", "preview", "kiosk", "debug", "bootleg", "pirate", "translation",
        "unknown", "mail-in disc", "bsd2", "unencrypted", "req. rsa disable",
        "re1", "re2", "re3", "release 1", "release 2", "release 3",
    };

    /// <summary>Derives a record from a disc path and checksum.</summary>
    /// <param name="path">Disc path.</param>
    /// <param name="checkSum">Uppercase checksum of the first two sectors.</param>
    /// <returns>A record with the id, checksum, name, region, and any parsed tags.</returns>
    public static GameRecord Describe(string path, string checkSum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkSum);

        string name = Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Trim();
        string year = string.Empty;
        string publisher = string.Empty;
        string regions = string.Empty;
        string revision = string.Empty;
        int? discNumber = null;
        int? discTotal = null;

        List<Segment> segments = Segmentize(name);
        var consumed = new bool[segments.Count];
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].IsTag)
            {
                // Bracketed dump/edition tags never belong to the title.
                consumed[i] = true;
            }
        }

        // Year (or full date) and the publisher immediately following it. GoodTools
        // compound discs repeat the year/publisher/region sequence for each title.
        for (var i = 0; i < segments.Count; i++)
        {
            if (!segments[i].IsParenthetical
                || !TryParseYear(segments[i].Text, out string parsedYear))
            {
                continue;
            }

            if (year.Length == 0)
            {
                year = parsedYear;
            }

            consumed[i] = true;
            if (i + 1 < segments.Count
                && segments[i + 1].IsParenthetical
                && !consumed[i + 1]
                && !IsRecognizedTag(segments[i + 1].Text))
            {
                if (publisher.Length == 0)
                {
                    publisher = segments[i + 1].Text.Trim();
                }

                consumed[i + 1] = true;
            }
        }

        for (var i = 0; i < segments.Count; i++)
        {
            if (consumed[i] || !segments[i].IsParenthetical)
            {
                continue;
            }

            string text = segments[i].Text.Trim();
            if (TryParseRegion(text, out string parsedRegions))
            {
                regions = regions.Length == 0 ? parsedRegions : $"{regions}-{parsedRegions}";
                consumed[i] = true;
                continue;
            }

            if (TryParseDisc(text, out int number, out int total))
            {
                discNumber ??= number;
                if (total > 0)
                {
                    discTotal ??= total;
                }

                consumed[i] = true;
                continue;
            }

            if (TryParseRevision(text, out string parsedRevision))
            {
                revision = parsedRevision;
                consumed[i] = true;
                continue;
            }

            if (IsLanguageList(text))
            {
                consumed[i] = true;
                continue;
            }

            if (IsEditionTag(text))
            {
                if (revision.Length == 0 && TryNormalizeReleaseType(text, out string releaseType))
                {
                    revision = releaseType;
                }

                consumed[i] = true;
            }
        }

        regions = NormalizeRegions(regions);

        var title = string.Concat(
            segments.Where((_, index) => !consumed[index]).Select(segment => segment.Text));
        title = CleanTitle(title);
        if (title.Length == 0)
        {
            title = Path.GetFileNameWithoutExtension(path).Trim();
        }

        return new GameRecord(
            checkSum[..Math.Min(8, checkSum.Length)].ToUpperInvariant(),
            checkSum.ToUpperInvariant(),
            title,
            year,
            publisher,
            regions)
        {
            Revision = revision,
            DiscNumber = discNumber,
            DiscTotal = discTotal,
        };
    }

    private static List<Segment> Segmentize(string name)
    {
        var segments = new List<Segment>();
        var literal = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c is '(' or '[')
            {
                if (literal.Length > 0)
                {
                    segments.Add(new Segment(literal.ToString(), IsParenthetical: false, IsTag: false));
                    literal.Clear();
                }

                char close = c == '(' ? ')' : ']';
                int end = name.IndexOf(close, i + 1);
                if (end < 0)
                {
                    literal.Append(c);
                    continue;
                }

                segments.Add(new Segment(name[(i + 1)..end], IsParenthetical: true, IsTag: c == '['));
                i = end;
            }
            else
            {
                literal.Append(c);
            }
        }

        if (literal.Length > 0)
        {
            segments.Add(new Segment(literal.ToString(), IsParenthetical: false, IsTag: false));
        }

        return segments;
    }

    private static bool IsRecognizedTag(string text)
    {
        return TryParseYear(text, out _)
               || TryParseRegion(text, out _)
               || TryParseDisc(text, out _, out _)
               || TryParseRevision(text, out _)
               || IsLanguageList(text)
               || IsEditionTag(text);
    }

    private static bool TryParseYear(string text, out string year)
    {
        year = string.Empty;
        if (YearRegex.IsMatch(text))
        {
            year = text;
            return true;
        }

        Match date = DateRegex.Match(text);
        if (date.Success)
        {
            year = date.Groups["year"].Value;
            return true;
        }

        return false;
    }

    private static string NormalizeRegions(string regions)
    {
        if (regions.Length == 0)
        {
            return regions;
        }

        var unique = new List<string>();
        foreach (string code in regions.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!unique.Contains(code, StringComparer.Ordinal))
            {
                unique.Add(code);
            }
        }

        return string.Join('-', unique);
    }

    private static bool TryNormalizeReleaseType(string text, out string releaseType)
    {
        releaseType = string.Empty;
        string normalized = text.Trim().ToLowerInvariant();
        if (normalized is "beta" or "alpha" or "proto" or "prototype" or "demo" or "sample"
            or "preview" or "kiosk" or "debug")
        {
            releaseType = char.ToUpperInvariant(normalized[0]) + normalized[1..];
            return true;
        }

        if (normalized is "re1" or "re2" or "re3")
        {
            releaseType = normalized.ToUpperInvariant();
            return true;
        }

        return false;
    }

    private static bool TryParseRegion(string text, out string regions)
    {
        regions = string.Empty;
        string[] tokens = text.Split([',', '/', '&', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var mapped = new List<string>(tokens.Length);
        foreach (string token in tokens)
        {
            string key = token.Trim();
            if (key.Equals("PAL", StringComparison.OrdinalIgnoreCase))
            {
                mapped.Add("EU");
                continue;
            }

            if (key.Equals("NTSC", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!RegionNames.TryGetValue(key, out string? code))
            {
                return false;
            }

            if (!mapped.Contains(code, StringComparer.Ordinal))
            {
                mapped.Add(code);
            }
        }

        if (mapped.Count == 0)
        {
            return false;
        }

        regions = string.Join('-', mapped);
        return true;
    }

    private static bool TryParseDisc(string text, out int number, out int total)
    {
        number = 0;
        total = 0;
        Match match = DiscRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        string value = match.Groups["number"].Value.Trim();
        number = value.Length == 1 && char.IsLetter(value[0])
            ? char.ToUpperInvariant(value[0]) - 'A' + 1
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        if (number <= 0)
        {
            return false;
        }

        total = int.TryParse(match.Groups["total"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTotal)
            ? parsedTotal
            : 0;
        return true;
    }

    private static bool TryParseRevision(string text, out string revision)
    {
        revision = string.Empty;
        Match match = RevisionRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        revision = text.Trim();
        return true;
    }

    private static bool IsLanguageList(string text)
    {
        string[] tokens = text.Split([',', '/', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length > 0 && tokens.All(token => LanguageNames.Contains(token));
    }

    private static bool IsEditionTag(string text)
    {
        string normalized = text.Trim().TrimStart('!').Trim();
        return EditionTags.Contains(normalized)
               || normalized.StartsWith("tr ", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("translation", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanTitle(string title)
    {
        string cleaned = WhitespaceRegex.Replace(title, " ");
        cleaned = cleaned.Trim(' ', '-', ',', ';', ':');
        return cleaned;
    }

    private static readonly Regex YearRegex = new(
        @"^(?:19|20)\d{2}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking);

    private static readonly Regex DateRegex = new(
        @"^(?<year>(?:19|20)\d{2})-\d{2}-\d{2}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking);

    private static readonly Regex DiscRegex = new(
        @"^(?:Disc|Disk|CD|Side)\s*(?<number>\d+|[A-Za-z])(?:\s*(?:of|/)\s*(?<total>\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking);

    private static readonly Regex RevisionRegex = new(
        @"^(?:(?:Rev(?:ision)?|Ver(?:sion)?|RE|v)\s*[0-9A-Za-z.]+|\d+\.\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private sealed record Segment(string Text, bool IsParenthetical, bool IsTag);
}

