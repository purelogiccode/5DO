using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FiveDo.Host;

/// <summary>
/// Persistent metadata for the game library so unchanged disc images do not need to be
/// reopened and hashed on every scan.
/// </summary>
internal sealed class LibraryCache
{
    /// <summary>Current cache schema version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the cache schema version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets the cached entries.</summary>
    public List<CachedLibraryEntry> Entries { get; set; } = [];
}

/// <summary>One cached disc-image record.</summary>
internal sealed class CachedLibraryEntry
{
    /// <summary>Gets or sets the full disc path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the file length at the time of the scan.</summary>
    public long Length { get; set; }

    /// <summary>Gets or sets the last-write time in UTC ticks at the time of the scan.</summary>
    public long LastWriteTimeUtcTicks { get; set; }

    /// <summary>Gets or sets the container format label.</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of 2048-byte sectors.</summary>
    public int SectorCount { get; set; }

    /// <summary>Gets or sets the 4DO game identifier.</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>Gets or sets the read error, or an empty string for a readable image.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>Creates a cache record from a scanner entry and its file identity.</summary>
    /// <param name="entry">Scanner result.</param>
    /// <param name="info">File information captured during the scan.</param>
    /// <returns>The cache record.</returns>
    public static CachedLibraryEntry From(GameLibraryEntry entry, FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(info);
        return new CachedLibraryEntry
        {
            Path = entry.Path,
            Length = info.Length,
            LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks,
            Format = entry.Format,
            SectorCount = entry.SectorCount,
            GameId = entry.GameId,
            Error = entry.Error,
        };
    }

    /// <summary>Returns whether this record still matches a file's identity.</summary>
    /// <param name="info">Current file information.</param>
    /// <returns><see langword="true"/> when the size and timestamp match.</returns>
    public bool Matches(FileInfo info)
    {
        return info.Length == Length
               && info.LastWriteTimeUtc.Ticks == LastWriteTimeUtcTicks;
    }
}

/// <summary>Loads and saves <see cref="LibraryCache"/> atomically.</summary>
internal static class LibraryCacheStore
{
    /// <summary>Gets the default cache file path.</summary>
    public static string DefaultPath => Path.Combine(AppSettingsStore.DefaultDirectory, "library-cache.json");

    /// <summary>Loads a cache, returning an empty cache when it is missing or invalid.</summary>
    /// <param name="path">Cache path.</param>
    /// <returns>The loaded cache.</returns>
    public static LibraryCache Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new LibraryCache();
            }

            LibraryCache cache = JsonSerializer.Deserialize(
                File.ReadAllText(path),
                LibraryCacheJsonContext.Default.LibraryCache) ?? new LibraryCache();
            return cache.Version == LibraryCache.CurrentVersion ? cache : new LibraryCache();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Library cache {Path} is unreadable; rebuilding it.", path);
            return new LibraryCache();
        }
    }

    /// <summary>Saves a cache atomically.</summary>
    /// <param name="path">Cache path.</param>
    /// <param name="cache">Cache to save.</param>
    public static void Save(string path, LibraryCache cache)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = path + ".tmp";
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(cache, LibraryCacheJsonContext.Default.LibraryCache));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Library cache {Path} could not be written.", path);
        }
    }
}
