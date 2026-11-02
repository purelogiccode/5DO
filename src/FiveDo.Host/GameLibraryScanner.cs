using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>An initial library listing plus files whose metadata still needs inspection.</summary>
/// <param name="Entries">Entries available immediately, including pending placeholders.</param>
/// <param name="PendingPaths">Disc paths that still need a game ID and sector count.</param>
public sealed record GameLibraryScanResult(
    IReadOnlyList<GameLibraryEntry> Entries,
    IReadOnlyList<string> PendingPaths);

/// <summary>
/// Scans folders for ISO/BIN/CUE/CHD images and resolves titles through the 4DO game database.
/// </summary>
public static class GameLibraryScanner
{
    private static readonly string[] Extensions = [".iso", ".bin", ".cue", ".chd"];

    private static readonly string[] CoverExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    /// <summary>Gets the default persistent cache path.</summary>
    public static string DefaultCachePath => LibraryCacheStore.DefaultPath;

    /// <summary>
    /// Lists discs immediately, reusing cached metadata for unchanged files.
    /// </summary>
    /// <param name="folders">Folders to scan. Missing folders are skipped.</param>
    /// <param name="maximumDepth">Maximum recursion depth (0 scans only the top level).</param>
    /// <param name="cachePath">Optional persistent cache path.</param>
    /// <param name="cancellationToken">Cancellation token for a superseded scan.</param>
    /// <param name="coverDirectory">
    /// Optional directory containing cover images named after the disc files.
    /// </param>
    /// <returns>
    /// Cached/full entries plus placeholders for files that need <see cref="InspectPending"/>.
    /// </returns>
    public static GameLibraryScanResult BeginScan(
        IEnumerable<string> folders,
        int maximumDepth = 4,
        string? cachePath = null,
        string? coverDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);

        HashSet<string> files = CollectFolders(folders, maximumDepth, cancellationToken);
        LibraryCache cache = cachePath is null ? new LibraryCache() : LibraryCacheStore.Load(cachePath);
        var cachedByPath = new Dictionary<string, CachedLibraryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (CachedLibraryEntry cached in cache.Entries)
        {
            if (!string.IsNullOrWhiteSpace(cached.Path))
            {
                cachedByPath[cached.Path] = cached;
            }
        }

        var entries = new List<GameLibraryEntry>(files.Count);
        var pending = new List<string>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                if (cachedByPath.TryGetValue(file, out CachedLibraryEntry? cached) && cached.Matches(info))
                {
                    entries.Add(CreateEntry(cached, coverDirectory));
                }
                else
                {
                    entries.Add(CreatePlaceholder(file, coverDirectory));
                    pending.Add(file);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Serilog.Log.Debug(exception, "Could not inspect library entry {Path}.", file);
            }
        }

        SortEntries(entries);
        return new GameLibraryScanResult(Deduplicate(entries), pending);
    }

    /// <summary>
    /// Computes game IDs and sector counts for the entries returned by <see cref="BeginScan"/>.
    /// </summary>
    /// <param name="paths">Pending disc paths.</param>
    /// <param name="cachePath">Optional persistent cache path.</param>
    /// <param name="progress">Optional progress receiver for the number of inspected entries.</param>
    /// <param name="cancellationToken">Cancellation token for a superseded scan.</param>
    /// <param name="coverDirectory">
    /// Optional directory containing cover images named after the disc files.
    /// </param>
    /// <returns>The enriched entries, sorted by title then path.</returns>
    public static IReadOnlyList<GameLibraryEntry> InspectPending(
        IReadOnlyList<string> paths,
        string? cachePath = null,
        IProgress<int>? progress = null,
        string? coverDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return [];
        }

        var entries = new ConcurrentBag<GameLibraryEntry>();
        var inspected = new ConcurrentDictionary<string, CachedLibraryEntry>(StringComparer.OrdinalIgnoreCase);
        var found = 0;
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4),
        };
        Parallel.ForEach(paths, options, file =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                GameLibraryEntry? entry = Inspect(file, chdReadAheadHunks: 0, coverDirectory: coverDirectory);
                if (entry is null)
                {
                    return;
                }

                entries.Add(entry);
                if (entry.Error.Length == 0)
                {
                    inspected[file] = CachedLibraryEntry.From(entry, info);
                }

                var count = Interlocked.Increment(ref found);
                if ((count & 15) == 0)
                {
                    progress?.Report(count);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Serilog.Log.Debug(exception, "Could not inspect library entry {Path}.", file);
            }
        });

        progress?.Report(found);

        if (cachePath is not null && !cancellationToken.IsCancellationRequested)
        {
            LibraryCache cache = LibraryCacheStore.Load(cachePath);
            var merged = new Dictionary<string, CachedLibraryEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (CachedLibraryEntry cached in cache.Entries)
            {
                if (!string.IsNullOrWhiteSpace(cached.Path))
                {
                    merged[cached.Path] = cached;
                }
            }

            // Re-inspect unreadable or deleted files on the next scan.
            foreach (var path in paths)
            {
                merged.Remove(path);
            }

            foreach (KeyValuePair<string, CachedLibraryEntry> pair in inspected)
            {
                merged[pair.Key] = pair.Value;
            }

            LibraryCacheStore.Save(cachePath, new LibraryCache { Entries = merged.Values.ToList() });
        }

        var result = entries.ToList();
        SortEntries(result);
        return Deduplicate(result);
    }

    /// <summary>Scans one or more folders, recursively, and returns fully inspected entries.</summary>
    /// <param name="folders">Folders to scan. Missing folders are skipped.</param>
    /// <param name="maximumDepth">Maximum recursion depth (0 scans only the top level).</param>
    /// <param name="cachePath">
    /// Optional cache path. When supplied, unchanged files reuse their previous metadata
    /// instead of reopening and hashing every disc.
    /// </param>
    /// <param name="progress">Optional progress receiver for the number of entries inspected.</param>
    /// <param name="cancellationToken">Cancellation token for a superseded scan.</param>
    /// <param name="coverDirectory">
    /// Optional directory containing cover images named after the disc files.
    /// </param>
    /// <returns>Entries sorted by title then path.</returns>
    public static IReadOnlyList<GameLibraryEntry> Scan(
        IEnumerable<string> folders,
        int maximumDepth = 4,
        string? cachePath = null,
        IProgress<int>? progress = null,
        string? coverDirectory = null,
        CancellationToken cancellationToken = default)
    {
        GameLibraryScanResult initial = BeginScan(
            folders,
            maximumDepth,
            cachePath,
            coverDirectory,
            cancellationToken);
        if (initial.PendingPaths.Count == 0)
        {
            return initial.Entries;
        }

        IReadOnlyList<GameLibraryEntry> inspected = InspectPending(
            initial.PendingPaths,
            cachePath,
            progress,
            coverDirectory,
            cancellationToken);
        var all = initial.Entries
            .Where(entry => !entry.Pending)
            .Concat(inspected)
            .ToList();
        SortEntries(all);
        return Deduplicate(all);
    }

    /// <summary>
    /// Removes duplicate copies of the same disc. Two folders can contain the same game
    /// (for example the same library on two drives); the 4DO game ID identifies those
    /// copies, so the library lists the game once.
    /// </summary>
    /// <param name="entries">Entries to deduplicate.</param>
    /// <returns>The entries in their original order with one entry per known game ID.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
    public static IReadOnlyList<GameLibraryEntry> Deduplicate(IReadOnlyList<GameLibraryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var byGameId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<GameLibraryEntry>(entries.Count);
        var duplicates = 0;
        foreach (GameLibraryEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.GameId))
            {
                result.Add(entry);
                continue;
            }

            if (!byGameId.TryGetValue(entry.GameId, out var index))
            {
                byGameId[entry.GameId] = result.Count;
                result.Add(entry);
                continue;
            }

            duplicates++;
            if (Prefer(entry, result[index]))
            {
                result[index] = entry;
            }
        }

        if (duplicates > 0)
        {
            Serilog.Log.Debug(
                "Library deduplicated {DuplicateCount} disc(s) that share a 3DO game ID.",
                duplicates);
        }

        return result;
    }

    private static bool Prefer(GameLibraryEntry candidate, GameLibraryEntry existing)
    {
        if (candidate.Error.Length == 0 && existing.Error.Length > 0)
        {
            return true;
        }

        if (candidate.Error.Length > 0 && existing.Error.Length == 0)
        {
            return false;
        }

        if (!candidate.Pending && existing.Pending)
        {
            return true;
        }

        if (candidate.Pending && !existing.Pending)
        {
            return false;
        }

        if (candidate.CoverPath.Length > 0 && existing.CoverPath.Length == 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>Builds a library entry for one disc, or returns null for an unsupported file.</summary>
    /// <param name="path">Disc path.</param>
    /// <param name="chdReadAheadHunks">
    /// CHD hunks to pre-decompress. Metadata scans pass zero to avoid background work.
    /// </param>
    /// <param name="coverDirectory">
    /// Optional directory containing cover images named after the disc files.
    /// </param>
    /// <returns>The entry, or null when the extension is not a supported disc container.</returns>
    public static GameLibraryEntry? Inspect(
        string path,
        int chdReadAheadHunks = 0,
        string? coverDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsDiscPath(path))
        {
            return null;
        }

        var coverPath = FindCover(path, coverDirectory);
        try
        {
            using IDiscSource disc = DiscImageFactory.Open(path, chdReadAheadHunks: chdReadAheadHunks);
            var gameId = disc.SectorCount >= 2 ? GameId.Compute(disc) : string.Empty;
            GameRecord? record = gameId.Length == 0 ? null : GameDatabase.Default.FindByChecksum(gameId);
            return new GameLibraryEntry(
                path,
                FormatFor(path),
                disc.SectorCount,
                gameId,
                record?.Name ?? Path.GetFileNameWithoutExtension(path),
                record is not null,
                string.Empty,
                coverPath)
            {
                ReleaseYear = record?.ReleaseYear ?? string.Empty,
                Publisher = record?.Publisher ?? string.Empty,
                Regions = record?.Regions ?? string.Empty,
            };
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or DiscImageException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Skipped unreadable library entry {Path}.", path);
            return new GameLibraryEntry(
                path,
                FormatFor(path),
                0,
                string.Empty,
                Path.GetFileNameWithoutExtension(path),
                false,
                exception.Message,
                coverPath);
        }
    }

    /// <summary>
    /// Finds cover art for a disc. A sibling image with the same base name next to the
    /// disc takes precedence (for example <c>game.png</c> beside <c>game.iso</c>);
    /// otherwise the configured cover directory is searched for the same base name.
    /// </summary>
    /// <param name="discPath">Disc path.</param>
    /// <param name="coverDirectory">
    /// Optional directory containing cover images named after the disc files.
    /// </param>
    /// <returns>The cover path, or an empty string when none exists.</returns>
    public static string FindCover(string discPath, string? coverDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discPath);
        var baseName = Path.GetFileNameWithoutExtension(discPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(discPath));
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        var sibling = FindCoverIn(directory, baseName);
        if (sibling.Length > 0)
        {
            return sibling;
        }

        if (string.IsNullOrWhiteSpace(coverDirectory))
        {
            return string.Empty;
        }

        try
        {
            var covers = Path.GetFullPath(coverDirectory);
            return Directory.Exists(covers) ? FindCoverIn(covers, baseName) : string.Empty;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Cover directory {Directory} could not be searched.", coverDirectory);
            return string.Empty;
        }
    }

    private static string FindCoverIn(string directory, string baseName)
    {
        foreach (var extension in CoverExtensions)
        {
            var candidate = Path.Combine(directory, baseName + extension);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static HashSet<string> CollectFolders(
        IEnumerable<string> folders,
        int maximumDepth,
        CancellationToken cancellationToken)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            CollectFiles(folder, maximumDepth, files, cancellationToken);
        }

        return files;
    }

    private static GameLibraryEntry CreateEntry(CachedLibraryEntry cached, string? coverDirectory)
    {
        GameRecord? record = cached.GameId.Length == 0
            ? null
            : GameDatabase.Default.FindByChecksum(cached.GameId);
        return new GameLibraryEntry(
            cached.Path,
            cached.Format,
            cached.SectorCount,
            cached.GameId,
            record?.Name ?? Path.GetFileNameWithoutExtension(cached.Path),
            record is not null,
            cached.Error,
            FindCover(cached.Path, coverDirectory))
        {
            ReleaseYear = record?.ReleaseYear ?? string.Empty,
            Publisher = record?.Publisher ?? string.Empty,
            Regions = record?.Regions ?? string.Empty,
        };
    }

    private static GameLibraryEntry CreatePlaceholder(string path, string? coverDirectory)
    {
        return new GameLibraryEntry(
            path,
            FormatFor(path),
            0,
            string.Empty,
            Path.GetFileNameWithoutExtension(path),
            false,
            string.Empty,
            FindCover(path, coverDirectory),
            Pending: true);
    }

    private static string FormatFor(string path)
    {
        return Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
    }

    private static void SortEntries(List<GameLibraryEntry> entries)
    {
        entries.Sort(static (left, right) =>
        {
            var title = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return title != 0
                ? title
                : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void CollectFiles(
        string folder,
        int depth,
        HashSet<string> files,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsDiscPath(file))
                {
                    files.Add(Path.GetFullPath(file));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Could not enumerate files in {Folder}.", folder);
            return;
        }

        if (depth == 0)
        {
            return;
        }

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                CollectFiles(directory, depth - 1, files, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Could not enumerate subdirectories in {Folder}.", folder);
        }
    }

    private static bool IsDiscPath(string path)
    {
        var extension = Path.GetExtension(path);
        foreach (var candidate in Extensions)
        {
            if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
