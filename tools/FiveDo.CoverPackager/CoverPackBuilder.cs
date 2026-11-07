using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FiveDo.Host;
using FiveDo.Storage;
using SkiaSharp;

namespace FiveDo.CoverPackager;

/// <summary>Builds the deduplicated WebP cover catalog, ZIP pack, checksums, and report.</summary>
internal sealed class CoverPackBuilder
{
    private const string ChecksumPlaceholder = "00000000000000000000000000000000";
    private const string PackFileName = "covers-v1.zip";

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CoverPackOptions _options;

    internal CoverPackBuilder(CoverPackOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    internal async Task<CoverPackReport> BuildAsync()
    {
        var report = new CoverPackReport { PackVersion = _options.PackVersion };
        List<string> files = Directory
            .EnumerateFiles(_options.SourceDirectory)
            .Where(IsImageFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        report.InputFiles = files.Count;
        if (files.Count == 0)
        {
            throw new InvalidDataException($"No image files were found in {_options.SourceDirectory}.");
        }

        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var hash = HashFile(file);
            if (!groups.TryGetValue(hash, out List<string>? group))
            {
                group = [];
                groups[hash] = group;
            }

            group.Add(file);
            report.InputBytes += new FileInfo(file).Length;
        }

        report.UniqueInputImages = groups.Count;
        Console.WriteLine($"Input: {report.InputFiles} files, {groups.Count} unique images, {FormatBytes(report.InputBytes)}.");

        var sourcesById = new Dictionary<string, CoverSource>(StringComparer.OrdinalIgnoreCase);
        var encoded = 0;
        foreach (List<string> group in groups.Values.OrderBy(value => value[0], StringComparer.OrdinalIgnoreCase))
        {
            var aliases = group
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var display = ChooseDisplay(aliases);
            var image = EncodeImage(group[0]);
            var id = HashBytes(image.Bytes);
            if (!sourcesById.TryGetValue(id, out CoverSource? source))
            {
                source = new CoverSource
                {
                    Id = id,
                    DisplayName = display.Name,
                    Regions = display.Regions,
                    Year = display.Year,
                    Rank = display.Rank,
                    Width = image.Width,
                    Height = image.Height,
                    Size = image.Bytes.Length,
                    WebpBytes = image.Bytes,
                };
                sourcesById[id] = source;
            }

            foreach (var alias in aliases)
            {
                if (!source.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                {
                    source.Aliases.Add(alias);
                }
            }

            encoded++;
            if (encoded % 50 == 0 || encoded == groups.Count)
            {
                Console.WriteLine($"Encoded {encoded}/{groups.Count} unique images...");
            }
        }

        var sources = sourcesById.Values.ToList();
        var temporaryCatalog = BuildCatalog(sources, packFile: string.Empty, packSize: 0, packSha256: string.Empty);
        CoverCatalogIndex index = CoverCatalogIndex.Build(temporaryCatalog, stripAnnotations: true);
        GameDatabase database = LoadGameDatabase();
        report.DatabaseGames = database.Records.Count;
        MapGameIds(index, database, sourcesById, report);
        ApplyOverrides(sourcesById, report);

        foreach (CoverSource source in sources)
        {
            if (source.Aliases.Count > 1)
            {
                var duplicate = new DuplicateReport { CoverId = source.Id };
                duplicate.Aliases.AddRange(source.Aliases);
                report.Duplicates.Add(duplicate);
            }
        }

        report.OutputCovers = sources.Count;
        report.OutputImageBytes = sources.Sum(source => source.Size);
        Console.WriteLine(
            $"Duplicates: {report.Duplicates.Count} groups, "
            + $"{report.Duplicates.Sum(group => group.Aliases.Count)} aliases.");
        sources.Sort(static (left, right) =>
        {
            var name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        });

        await WriteOutputAsync(sources, report).ConfigureAwait(false);
        Console.WriteLine(
            $"Output: {sources.Count} WebP covers, {FormatBytes(report.OutputImageBytes)} images, "
            + $"{FormatBytes(report.OutputPackBytes)} pack.");
        Console.WriteLine(
            $"Mapped {report.MappedGameIds}/{report.DatabaseGames} game IDs "
            + $"({report.WeakMappings} weak, {report.OverridesApplied} overrides).");
        return report;
    }

    private async Task WriteOutputAsync(List<CoverSource> sources, CoverPackReport report)
    {
        if (_options.Clean && Directory.Exists(_options.OutputDirectory))
        {
            Directory.Delete(_options.OutputDirectory, recursive: true);
        }

        var imagesDirectory = Path.Combine(_options.OutputDirectory, "images");
        var packsDirectory = Path.Combine(_options.OutputDirectory, "packs");
        Directory.CreateDirectory(imagesDirectory);
        Directory.CreateDirectory(packsDirectory);

        foreach (CoverSource source in sources)
        {
            await File.WriteAllBytesAsync(
                    Path.Combine(imagesDirectory, source.Id + ".webp"),
                    source.WebpBytes)
                .ConfigureAwait(false);
        }

        var zipPath = Path.Combine(packsDirectory, PackFileName);
        if (_options.CreateZip)
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (CoverSource source in sources)
            {
                ZipArchiveEntry entry = archive.CreateEntry(
                    "images/" + source.Id + ".webp",
                    CompressionLevel.Fastest);
                using Stream stream = entry.Open();
                await stream.WriteAsync(source.WebpBytes).ConfigureAwait(false);
            }
        }
        else if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        var packSize = _options.CreateZip ? new FileInfo(zipPath).Length : 0;
        var packHash = _options.CreateZip ? HashFile(zipPath) : string.Empty;
        report.OutputPackBytes = packSize;
        CoverCatalog catalog = BuildCatalog(
            sources,
            _options.CreateZip ? "packs/" + PackFileName : string.Empty,
            packSize,
            packHash);
        var catalogJson = JsonSerializer.Serialize(catalog, JsonOptions);
        await File.WriteAllTextAsync(
                Path.Combine(_options.OutputDirectory, "catalog.json"),
                catalogJson,
                Utf8NoBom)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(_options.OutputDirectory, "report.json"),
                JsonSerializer.Serialize(report, JsonOptions),
                Utf8NoBom)
            .ConfigureAwait(false);
        await WriteChecksumsAsync(sources, zipPath, packSize > 0).ConfigureAwait(false);
        VerifyOutput(imagesDirectory, zipPath, packSize > 0);
    }

    private void VerifyOutput(
        string imagesDirectory,
        string zipPath,
        bool includePack)
    {
        CoverCatalogLoadResult loaded = CoverCatalogLoader.LoadFile(
            Path.Combine(_options.OutputDirectory, "catalog.json"));
        if (!loaded.Success)
        {
            throw new InvalidDataException($"Generated catalog did not reload: {loaded.Error}");
        }

        var gameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CoverCatalogEntry entry in loaded.Catalog!.Covers)
        {
            var imagePath = Path.Combine(imagesDirectory, entry.File);
            if (!File.Exists(imagePath))
            {
                throw new InvalidDataException($"Generated image is missing: {entry.File}");
            }

            if (!string.Equals(HashFile(imagePath), entry.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Generated image hash mismatch: {entry.File}");
            }

            foreach (var gameId in entry.GameIds)
            {
                if (!gameIds.Add(gameId))
                {
                    throw new InvalidDataException($"Game ID {gameId} is mapped more than once.");
                }
            }
        }

        if (includePack)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count != loaded.Catalog.Covers.Count)
            {
                throw new InvalidDataException(
                    $"The cover pack contains {archive.Entries.Count} entries; "
                    + $"the catalog has {loaded.Catalog.Covers.Count} covers.");
            }
        }
    }

    private async Task WriteChecksumsAsync(
        List<CoverSource> sources,
        string zipPath,
        bool includePack)
    {
        var lines = new List<string>(sources.Count + 2);
        foreach (CoverSource source in sources)
        {
            lines.Add($"{source.Id}  images/{source.Id}.webp");
        }

        var catalogPath = Path.Combine(_options.OutputDirectory, "catalog.json");
        lines.Add($"{HashFile(catalogPath)}  catalog.json");
        if (includePack)
        {
            lines.Add($"{HashFile(zipPath)}  packs/{PackFileName}");
        }

        lines.Sort(StringComparer.Ordinal);
        await File.WriteAllLinesAsync(
                Path.Combine(_options.OutputDirectory, "SHA256SUMS.txt"),
                lines,
                Utf8NoBom)
            .ConfigureAwait(false);
    }

    private CoverCatalog BuildCatalog(
        List<CoverSource> sources,
        string packFile,
        long packSize,
        string packSha256)
    {
        var catalog = new CoverCatalog
        {
            SchemaVersion = CoverDefaults.SchemaVersion,
            PackVersion = _options.PackVersion,
            GeneratedUtc = DateTimeOffset.UtcNow,
            NoticeUrl = _options.NoticeUrl,
            ImagesBase = "images/",
            PackFile = packFile,
            PackSize = packSize,
            PackSha256 = packSha256,
        };
        foreach (CoverSource source in sources)
        {
            catalog.Covers.Add(new CoverCatalogEntry
            {
                Id = source.Id,
                File = source.Id + ".webp",
                Name = source.DisplayName,
                Aliases = [.. source.Aliases],
                GameIds = [.. source.GameIds],
                Width = source.Width,
                Height = source.Height,
                Size = source.Size,
                Regions = [.. source.Regions],
                Year = source.Year,
                Rank = source.Rank,
            });
        }

        return catalog;
    }

    private void MapGameIds(
        CoverCatalogIndex index,
        GameDatabase database,
        Dictionary<string, CoverSource> sourcesById,
        CoverPackReport report)
    {
        foreach (GameRecord game in database.Records.OrderBy(record => record.Name, StringComparer.OrdinalIgnoreCase))
        {
            CoverCatalogMatch? best = index.FindByTitle(
                game.Name,
                fileName: null,
                threshold: 0.0,
                fuzzy: true,
                game.Regions,
                game.ReleaseYear);
            if (best is null)
            {
                report.UnmappedGames.Add(new UnmappedGameReport
                {
                    GameId = game.Id,
                    GameName = game.Name,
                });
                continue;
            }

            var mapping = new CoverMappingReport
            {
                GameId = game.Id,
                GameName = game.Name,
                CoverId = best.Entry.Id,
                CoverName = best.Entry.Name,
                Score = best.Score,
                Exact = best.Exact,
            };
            if (best.Score < _options.MapThreshold)
            {
                report.UnmappedGames.Add(new UnmappedGameReport
                {
                    GameId = game.Id,
                    GameName = game.Name,
                    BestScore = best.Score,
                    BestCoverName = best.Entry.Name,
                });
                if (_options.Verbose)
                {
                    Console.WriteLine($"  unmapped {game.Id} '{game.Name}' best={best.Score:0.000} '{best.Entry.Name}'");
                }

                continue;
            }

            sourcesById[best.Entry.Id].GameIds.Add(game.Id);
            report.Mappings.Add(mapping);
            if (!best.Exact && best.Score < 0.95)
            {
                report.WeakMappings++;
            }

            if (_options.Verbose)
            {
                Console.WriteLine(
                    $"  mapped {game.Id} '{game.Name}' -> '{best.Entry.Name}' {best.Score:0.000}");
            }
        }

        report.MappedGameIds = report.Mappings.Count;
    }

    private void ApplyOverrides(
        Dictionary<string, CoverSource> sourcesById,
        CoverPackReport report)
    {
        if (_options.OverridesPath is null || !File.Exists(_options.OverridesPath))
        {
            return;
        }

        CoverOverrides overrides = JsonSerializer.Deserialize<CoverOverrides>(
                File.ReadAllText(_options.OverridesPath),
                JsonOptions)
            ?? throw new InvalidDataException("The override file is empty.");
        if (overrides.Version != 1)
        {
            throw new InvalidDataException($"Unsupported override schema version {overrides.Version}.");
        }

        // JSON can deserialize null despite the non-nullable annotation.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (overrides.GameIds is null)
        {
            return;
        }

        foreach ((var rawGameId, var coverReference) in overrides.GameIds)
        {
            var gameId = CoverCatalogIndex.NormalizeGameId(rawGameId);
            if (gameId.Length == 0)
            {
                throw new InvalidDataException($"Override game ID '{rawGameId}' is invalid.");
            }

            CoverSource? target = FindSource(sourcesById.Values, coverReference);
            if (target is null)
            {
                throw new InvalidDataException(
                    $"Override cover '{coverReference}' for game {gameId} was not found.");
            }

            foreach (CoverSource source in sourcesById.Values)
            {
                source.GameIds.Remove(gameId);
            }

            target.GameIds.Add(gameId);
            report.Mappings.RemoveAll(mapping =>
                string.Equals(mapping.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            report.UnmappedGames.RemoveAll(unmapped =>
                string.Equals(unmapped.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            report.Mappings.Add(new CoverMappingReport
            {
                GameId = gameId,
                GameName = gameId,
                CoverId = target.Id,
                CoverName = target.DisplayName,
                Score = 1.0,
                Exact = true,
                Source = "override",
            });
            report.OverridesApplied++;
        }

        report.MappedGameIds = report.Mappings.Count;
    }

    private static CoverSource? FindSource(
        IEnumerable<CoverSource> sources,
        string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var trimmed = reference.Trim();
        if (trimmed.Length == 64 && trimmed.All(IsHex))
        {
            return sources.FirstOrDefault(source =>
                string.Equals(source.Id, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        var matches = sources
            .Where(source => source.Aliases.Any(alias =>
                    string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase))
                || string.Equals(source.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Override cover reference '{reference}' matched {matches.Count} images; use the SHA-256 id.");
    }

    private GameDatabase LoadGameDatabase()
    {
        if (_options.GameDatabasePath is null)
        {
            return GameDatabase.Default;
        }

        using var stream = File.OpenRead(_options.GameDatabasePath);
        return GameDatabase.Load(stream);
    }

    private static (string Name, List<string> Regions, string Year, int Rank) ChooseDisplay(
        List<string> aliases)
    {
        var candidates = new List<DisplayCandidate>(aliases.Count);
        foreach (var alias in aliases)
        {
            GameRecord parsed = ParseMetadata(alias + ".png");
            candidates.Add(new DisplayCandidate(
                parsed.Name,
                SplitRegions(parsed.Regions),
                parsed.ReleaseYear,
                RankFromRevision(parsed.Revision)));
        }

        if (candidates.Count == 0)
        {
            return (aliases.Count > 0 ? aliases[0] : "Unknown", [], string.Empty, 3);
        }

        DisplayCandidate best = candidates
            .OrderBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Name.Length)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .First();
        return (best.Name, best.Regions, best.Year, best.Rank);
    }

    private (byte[] Bytes, int Width, int Height) EncodeImage(string path)
    {
        using SKBitmap? decoded = SKBitmap.Decode(path);
        if (decoded is null)
        {
            throw new InvalidDataException($"Could not decode cover image {path}.");
        }

        SKBitmap bitmap = decoded;
        SKBitmap? resized = null;
        try
        {
            if (bitmap.Height > _options.MaxHeight)
            {
                var width = Math.Max(
                    1,
                    (int)Math.Round(bitmap.Width * (double)_options.MaxHeight / bitmap.Height));
                var info = new SKImageInfo(width, _options.MaxHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
                resized = bitmap.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell))
                    ?? throw new InvalidDataException($"Could not resize cover image {path}.");
                bitmap = resized;
            }

            using SKImage image = SKImage.FromBitmap(bitmap)
                ?? throw new InvalidDataException($"Could not create an image from {path}.");
            using SKData? data = image.Encode(SKEncodedImageFormat.Webp, _options.Quality);
            if (data is null)
            {
                throw new InvalidDataException($"Could not encode cover image {path}.");
            }

            return (data.ToArray(), bitmap.Width, bitmap.Height);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static GameRecord ParseMetadata(string path)
    {
        return GameDatabaseNameParser.Describe(path, ChecksumPlaceholder);
    }

    private static List<string> SplitRegions(string value)
    {
        return value
            .Split(['-', ',', '/', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(region => region.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static bool IsImageFile(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsHex(char character)
    {
        return character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string HashBytes(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FormatBytes(long bytes)
    {
        return bytes >= 1024 * 1024
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.0} MB")
            : string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.0} KB");
    }

    private sealed record DisplayCandidate(
        string Name,
        List<string> Regions,
        string Year,
        int Rank);
}
