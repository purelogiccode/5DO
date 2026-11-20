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
    internal static CachedLibraryEntry From(GameLibraryEntry entry, FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(info);
        return new CachedLibraryEntry
        {
            Path = entry.Path,
            Length = info.Length,

            // Some removable and network file systems report a FILETIME that cannot
            // become a DateTime. Such discs stay in the library with an unknown stamp.
            LastWriteTimeUtcTicks = FileStamp.GetLastWriteTimeUtcTicks(info),
            Format = entry.Format,
            SectorCount = entry.SectorCount,
            GameId = entry.GameId,
            Error = entry.Error,
        };
    }

    /// <summary>Returns whether this record still matches a file's identity.</summary>
    /// <param name="info">Current file information.</param>
    /// <returns><see langword="true"/> when the size and timestamp match.</returns>
    internal bool Matches(FileInfo info)
    {
        return FileStamp.Matches(info, Length, LastWriteTimeUtcTicks);
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
    internal static LibraryCache Load(string path)
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
            if (cache.Version != LibraryCache.CurrentVersion)
            {
                return new LibraryCache();
            }

            // JSON can deserialize null entries and null strings despite the
            // non-nullable annotations. Drop unusable records before the scanner
            // calls Path/Error/GameId on them.
            // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            cache.Entries ??= [];
            for (var index = cache.Entries.Count - 1; index >= 0; index--)
            {
                CachedLibraryEntry? entry = cache.Entries[index];
                if (entry is null)
                {
                    cache.Entries.RemoveAt(index);
                    continue;
                }

                entry.Path ??= string.Empty;
                entry.Format ??= string.Empty;
                entry.GameId ??= string.Empty;
                entry.Error ??= string.Empty;
                // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                // ReSharper restore NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                if (entry.Path.Length == 0)
                {
                    cache.Entries.RemoveAt(index);
                }
            }

            return cache;
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
    internal static void Save(string path, LibraryCache cache)
    {
        try
        {
            var json = JsonSerializer.Serialize(cache, LibraryCacheJsonContext.Default.LibraryCache);
            if (!AtomicFile.WriteAllText(path, json, "library cache"))
            {
                Serilog.Log.Debug("Library cache {Path} could not be written.", path);
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "Library cache {Path} could not be serialized.", path);
        }
    }
}
