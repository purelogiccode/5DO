using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FiveDo.Host;

/// <summary>Result of checking a disc image's readability and DAT match.</summary>
public enum DiscHealthStatus
{
    /// <summary>The disc has not been checked.</summary>
    Unknown,

    /// <summary>Every track matched the configured DAT.</summary>
    Verified,

    /// <summary>The disc is readable but does not match the configured DAT.</summary>
    Mismatch,

    /// <summary>At least one sector or track could not be read.</summary>
    ReadError,

    /// <summary>The disc is readable and no DAT is configured.</summary>
    NoDat,
}

/// <summary>
/// Cached result of verifying one disc image. The file length and timestamp make the
/// record self-invalidating when the image is replaced.
/// </summary>
public sealed class DiscHealthRecord
{
    /// <summary>Gets or sets the full disc path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the file length at the time of verification.</summary>
    public long Length { get; set; }

    /// <summary>Gets or sets the last-write time in UTC ticks at the time of verification.</summary>
    public long LastWriteTimeUtcTicks { get; set; }

    /// <summary>Gets or sets the verification result.</summary>
    public DiscHealthStatus Status { get; set; }

    /// <summary>Gets or sets the matched DAT game name, or an empty string.</summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>Gets or sets the matched DAT ROM name, or an empty string.</summary>
    public string RomName { get; set; } = string.Empty;

    /// <summary>Gets or sets the first track's CRC-32, or an empty string.</summary>
    public string Crc32 { get; set; } = string.Empty;

    /// <summary>Gets or sets the first track's MD5, or an empty string.</summary>
    public string Md5 { get; set; } = string.Empty;

    /// <summary>Gets or sets the first track's SHA-1, or an empty string.</summary>
    public string Sha1 { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of tracks that were hashed.</summary>
    public int TrackCount { get; set; }

    /// <summary>Gets or sets the number of tracks that matched the DAT.</summary>
    public int MatchedTracks { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the check.</summary>
    public long VerifiedUtcTicks { get; set; }

    /// <summary>Gets or sets the human-readable detail (read error, DAT note, or hashes).</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Creates a record for a disc file.</summary>
    /// <param name="path">Disc path.</param>
    /// <param name="info">File information captured before verification.</param>
    /// <param name="status">Verification result.</param>
    /// <returns>The record.</returns>
    public static DiscHealthRecord Create(string path, FileInfo info, DiscHealthStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(info);
        long length = 0;
        long ticks = 0;
        try
        {
            if (info.Exists)
            {
                length = info.Length;
                ticks = FileStamp.GetLastWriteTimeUtcTicks(info);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            // A missing or unreadable file still gets a record so the library can show why.
        }

        return new DiscHealthRecord
        {
            Path = path,
            Length = length,
            LastWriteTimeUtcTicks = ticks,
            Status = status,
            VerifiedUtcTicks = DateTime.UtcNow.Ticks,
        };
    }

    /// <summary>Returns whether this record still describes the file on disk.</summary>
    /// <param name="info">Current file information.</param>
    /// <returns><see langword="true"/> when the size and timestamp match.</returns>
    public bool Matches(FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return FileStamp.Matches(info, Length, LastWriteTimeUtcTicks);
    }
}

/// <summary>Counts of each disc-health status for a library view.</summary>
/// <param name="Verified">Discs that matched the DAT.</param>
/// <param name="Mismatch">Readable discs that did not match the DAT.</param>
/// <param name="ReadError">Discs with at least one read error.</param>
/// <param name="NoDat">Readable discs checked without a DAT.</param>
/// <param name="Unchecked">Discs that have no current health record.</param>
public sealed record DiscHealthSummary(
    int Verified,
    int Mismatch,
    int ReadError,
    int NoDat,
    int Unchecked)
{
    /// <summary>Gets the total number of discs counted.</summary>
    public int Total => Verified + Mismatch + ReadError + NoDat + Unchecked;

    /// <summary>Gets a value indicating whether every disc has a current record.</summary>
    public bool Complete => Unchecked == 0;

    /// <summary>Gets a compact status line for the library view.</summary>
    public string Text
    {
        get
        {
            if (Total == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(4);
            if (Verified > 0)
            {
                parts.Add($"{Verified} known dump(s)");
            }

            if (Mismatch > 0)
            {
                parts.Add($"{Mismatch} not in DAT");
            }

            if (ReadError > 0)
            {
                parts.Add($"{ReadError} read error(s)");
            }

            if (NoDat > 0)
            {
                parts.Add($"{NoDat} readable without a DAT");
            }

            if (Unchecked > 0)
            {
                parts.Add($"{Unchecked} unchecked");
            }

            return string.Join(" · ", parts);
        }
    }
}

/// <summary>
/// Persistent per-disc verification results so the library can show a known-dump badge
/// without re-reading every image on each scan.
/// </summary>
public sealed class DiscHealthCache
{
    /// <summary>Current cache schema version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the cache schema version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets the cached records.</summary>
    public IList<DiscHealthRecord> Entries { get; set; } = [];

    /// <summary>Finds the current record for a disc, if the file identity still matches.</summary>
    /// <param name="path">Disc path.</param>
    /// <returns>The record, or <see langword="null"/> when it is missing or stale.</returns>
    public DiscHealthRecord? Find(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        DiscHealthRecord? record = FindRecord(path);
        if (record is null)
        {
            return null;
        }

        try
        {
            var info = new FileInfo(path);
            return info.Exists && record.Matches(info) ? record : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Stores a record, replacing an older record for the same path.</summary>
    /// <param name="record">Record to store.</param>
    public void Set(DiscHealthRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Path, nameof(record));
        for (var i = 0; i < Entries.Count; i++)
        {
            if (string.Equals(Entries[i].Path, record.Path, StringComparison.OrdinalIgnoreCase))
            {
                Entries[i] = record;
                return;
            }
        }

        Entries.Add(record);
    }

    /// <summary>Removes records whose files no longer exist.</summary>
    public void RemoveMissingFiles()
    {
        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            if (!File.Exists(Entries[i].Path))
            {
                Entries.RemoveAt(i);
            }
        }
    }

    /// <summary>Applies the cached status to a library entry.</summary>
    /// <param name="entry">Scanner entry.</param>
    /// <returns>The entry with dump status fields populated.</returns>
    public GameLibraryEntry Apply(GameLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        DiscHealthRecord? record = Find(entry.Path);
        return record is null
            ? entry
            : entry with
            {
                DumpStatus = record.Status,
                DumpGameName = record.GameName,
                DumpDetail = record.Detail,
            };
    }

    /// <summary>Counts the current statuses for a set of library entries.</summary>
    /// <param name="entries">Library entries.</param>
    /// <returns>The status summary.</returns>
    public DiscHealthSummary Summarize(IEnumerable<GameLibraryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        int verified = 0;
        int mismatch = 0;
        int readError = 0;
        int noDat = 0;
        int unknown = 0;
        foreach (GameLibraryEntry entry in entries)
        {
            switch (Find(entry.Path)?.Status)
            {
                case DiscHealthStatus.Verified:
                    verified++;
                    break;
                case DiscHealthStatus.Mismatch:
                    mismatch++;
                    break;
                case DiscHealthStatus.ReadError:
                    readError++;
                    break;
                case DiscHealthStatus.NoDat:
                    noDat++;
                    break;
                default:
                    unknown++;
                    break;
            }
        }

        return new DiscHealthSummary(verified, mismatch, readError, noDat, unknown);
    }

    private DiscHealthRecord? FindRecord(string path)
    {
        foreach (DiscHealthRecord record in Entries)
        {
            if (string.Equals(record.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                return record;
            }
        }

        return null;
    }
}

/// <summary>Loads and saves <see cref="DiscHealthCache"/> atomically.</summary>
public static class DiscHealthCacheStore
{
    /// <summary>Gets the default cache file path.</summary>
    public static string DefaultPath => Path.Combine(AppSettingsStore.DefaultDirectory, "library-health.json");

    /// <summary>Loads the cache, returning an empty cache when it is missing or invalid.</summary>
    /// <param name="path">Optional cache path. Defaults to <see cref="DefaultPath"/>.</param>
    /// <returns>The loaded cache.</returns>
    public static DiscHealthCache Load(string? path = null)
    {
        var resolved = path ?? DefaultPath;
        try
        {
            if (!File.Exists(resolved))
            {
                return new DiscHealthCache();
            }

            DiscHealthCache cache = JsonSerializer.Deserialize(
                File.ReadAllText(resolved),
                DiscHealthCacheJsonContext.Default.DiscHealthCache) ?? new DiscHealthCache();
            return cache.Version == DiscHealthCache.CurrentVersion ? cache : new DiscHealthCache();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Disc-health cache {Path} is unreadable; rebuilding it.", resolved);
            return new DiscHealthCache();
        }
    }

    /// <summary>Saves the cache atomically.</summary>
    /// <param name="cache">Cache to save.</param>
    /// <param name="path">Optional cache path. Defaults to <see cref="DefaultPath"/>.</param>
    public static void Save(DiscHealthCache cache, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var resolved = path ?? DefaultPath;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(resolved));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = resolved + ".tmp";
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(cache, DiscHealthCacheJsonContext.Default.DiscHealthCache));
            File.Move(temporary, resolved, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Disc-health cache {Path} could not be written.", resolved);
        }
    }
}
