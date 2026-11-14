using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FiveDo.Host;

/// <summary>
/// Offline metadata for one disc. Records can be keyed by 4DO game ID, by full path, or both.
/// </summary>
public sealed class LibraryMetadataRecord
{
    /// <summary>Gets or sets the 4DO game ID this record describes.</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>Gets or sets the full disc path this record describes.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional display title that overrides the database or file name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the developer name.</summary>
    public string Developer { get; set; } = string.Empty;

    /// <summary>Gets or sets the publisher name.</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>Gets or sets the release year.</summary>
    public string ReleaseYear { get; set; } = string.Empty;

    /// <summary>Gets or sets the genre.</summary>
    public string Genre { get; set; } = string.Empty;

    /// <summary>Gets or sets the region codes.</summary>
    public string Regions { get; set; } = string.Empty;

    /// <summary>Gets or sets the supported player count.</summary>
    public int Players { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets user-attached media paths (manual, screenshots, music, or video files).
    /// </summary>
    /// <remarks>
    /// Attachments are metadata only. The emulator stores the absolute paths and never
    /// copies or modifies the referenced files.
    /// </remarks>
    public IList<string> Media { get; set; } = [];
}

/// <summary>Offline library metadata document.</summary>
public sealed class LibraryMetadata
{
    /// <summary>Current metadata schema version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the schema version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets the metadata records.</summary>
    public IList<LibraryMetadataRecord> Records { get; set; } = [];
}

/// <summary>
/// Applies optional offline metadata (Redump/No-Intro/IGDB/LaunchBox-style facts) to library
/// entries. The catalog is a plain JSON file so it works without a network connection and can be
/// generated or curated by external tools.
/// </summary>
public sealed class LibraryMetadataCatalog
{
    private readonly Dictionary<string, LibraryMetadataRecord> _byGameId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LibraryMetadataRecord> _byPath = new(StringComparer.OrdinalIgnoreCase);

    private LibraryMetadataCatalog()
    {
    }

    /// <summary>Gets an empty catalog.</summary>
    public static LibraryMetadataCatalog Empty { get; } = new();

    /// <summary>Loads a catalog from a JSON file.</summary>
    /// <param name="path">Metadata path. Defaults to <see cref="LibraryMetadataStore.DefaultPath"/>.</param>
    /// <returns>The loaded catalog, or an empty catalog when the file is missing or invalid.</returns>
    public static LibraryMetadataCatalog Load(string? path = null)
    {
        var resolved = path ?? LibraryMetadataStore.DefaultPath;
        try
        {
            if (!File.Exists(resolved))
            {
                return Empty;
            }

            LibraryMetadata metadata = JsonSerializer.Deserialize(
                File.ReadAllText(resolved),
                LibraryMetadataJsonContext.Default.LibraryMetadata) ?? new LibraryMetadata();
            if (metadata.Version != LibraryMetadata.CurrentVersion)
            {
                Serilog.Log.Debug(
                    "Library metadata {Path} has unsupported version {Version}; ignoring it.",
                    resolved,
                    metadata.Version);
                return Empty;
            }

            var catalog = new LibraryMetadataCatalog();
            // JSON can deserialize null despite the non-nullable annotation.
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            foreach (LibraryMetadataRecord record in metadata.Records ?? [])
            {
                // JSON can deserialize null despite the non-nullable annotation.
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (record is null)
                {
                    continue;
                }

                // JSON can deserialize a null list despite the non-nullable annotation.
                // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                record.Media ??= [];
                catalog.Add(record);
            }

            return catalog;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Library metadata {Path} is unreadable; ignoring it.", resolved);
            return Empty;
        }
    }

    /// <summary>Adds a record to the catalog. Later records replace earlier records with the same key.</summary>
    /// <param name="record">Metadata record.</param>
    public void Add(LibraryMetadataRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var gameId = NormalizeGameId(record.GameId);
        if (gameId.Length > 0)
        {
            _byGameId[gameId] = record;
        }

        if (!string.IsNullOrWhiteSpace(record.Path))
        {
            _byPath[NormalizePath(record.Path)] = record;
        }
    }

    /// <summary>Finds metadata for a disc, preferring the game ID over the path.</summary>
    /// <param name="gameId">4DO game ID.</param>
    /// <param name="path">Full disc path.</param>
    /// <returns>The matching record, or <see langword="null"/>.</returns>
    public LibraryMetadataRecord? Find(string? gameId, string path)
    {
        var normalizedId = NormalizeGameId(gameId);
        if (normalizedId.Length > 0 && _byGameId.TryGetValue(normalizedId, out LibraryMetadataRecord? byId))
        {
            return byId;
        }

        return _byPath.GetValueOrDefault(NormalizePath(path));
    }

    /// <summary>Applies metadata to a scanner entry.</summary>
    /// <param name="entry">Scanner entry.</param>
    /// <returns>The entry with any matching metadata applied.</returns>
    public GameLibraryEntry Apply(GameLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        LibraryMetadataRecord? record = Find(entry.GameId, entry.Path);
        if (record is null)
        {
            return entry;
        }

        return entry with
        {
            Name = string.IsNullOrWhiteSpace(record.Title) ? entry.Name : record.Title.Trim(),
            Developer = FirstNonEmpty(record.Developer, entry.Developer),
            Publisher = FirstNonEmpty(record.Publisher, entry.Publisher),
            ReleaseYear = FirstNonEmpty(record.ReleaseYear, entry.ReleaseYear),
            Genre = FirstNonEmpty(record.Genre, entry.Genre),
            Regions = FirstNonEmpty(record.Regions, entry.Regions),
            Players = record.Players > 0 ? record.Players : entry.Players,
            Description = FirstNonEmpty(record.Description, entry.Description),
        };
    }

    private static string FirstNonEmpty(string preferred, string fallback)
    {
        return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();
    }

    private static string NormalizeGameId(string? gameId)
    {
        var normalized = gameId?.Trim().ToUpperInvariant() ?? string.Empty;
        return normalized.Length > 8 ? normalized[..8] : normalized;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}

/// <summary>Loads and saves offline library metadata.</summary>
public static class LibraryMetadataStore
{
    /// <summary>Gets the default metadata file path.</summary>
    public static string DefaultPath => Path.Combine(AppSettingsStore.DefaultDirectory, "library-metadata.json");

    /// <summary>Loads a metadata document.</summary>
    /// <param name="path">Metadata path. Defaults to <see cref="DefaultPath"/>.</param>
    /// <returns>The loaded document, or an empty document when the file is missing or invalid.</returns>
    public static LibraryMetadata Load(string? path = null)
    {
        var resolved = path ?? DefaultPath;
        try
        {
            if (!File.Exists(resolved))
            {
                return new LibraryMetadata();
            }

            LibraryMetadata metadata = JsonSerializer.Deserialize(
                File.ReadAllText(resolved),
                LibraryMetadataJsonContext.Default.LibraryMetadata) ?? new LibraryMetadata();
            // JSON can deserialize null despite the non-nullable annotation.
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            metadata.Records ??= [];
            foreach (LibraryMetadataRecord record in metadata.Records)
            {
                // JSON can deserialize null despite the non-nullable annotation.
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (record is null)
                {
                    continue;
                }

                // JSON can deserialize a null list despite the non-nullable annotation.
                // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                record.Media ??= [];
            }

            return metadata;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Library metadata {Path} is unreadable; ignoring it.", resolved);
            return new LibraryMetadata();
        }
    }

    /// <summary>Adds or replaces the record for a game/path and saves the document.</summary>
    /// <param name="record">Record to store.</param>
    /// <param name="path">Metadata path. Defaults to <see cref="DefaultPath"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the record has neither a game ID nor a path.</exception>
    public static void Upsert(LibraryMetadataRecord record, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        var gameId = NormalizeGameId(record.GameId);
        var fullPath = NormalizePath(record.Path);
        if (gameId.Length == 0 && fullPath.Length == 0)
        {
            throw new ArgumentException("Metadata needs a game ID or disc path.", nameof(record));
        }

        LibraryMetadata metadata = Load(path);
        for (var i = metadata.Records.Count - 1; i >= 0; i--)
        {
            LibraryMetadataRecord existing = metadata.Records[i];
            var existingId = NormalizeGameId(existing.GameId);
            var existingPath = NormalizePath(existing.Path);
            if ((gameId.Length > 0 && existingId.Length > 0
                    && string.Equals(gameId, existingId, StringComparison.OrdinalIgnoreCase))
                || (fullPath.Length > 0 && existingPath.Length > 0
                    && string.Equals(fullPath, existingPath, StringComparison.OrdinalIgnoreCase)))
            {
                metadata.Records.RemoveAt(i);
            }
        }

        metadata.Records.Add(record);
        Save(metadata, path);
    }

    /// <summary>Removes the record matching a game ID or path and saves the document.</summary>
    /// <param name="gameId">4DO game ID, or empty.</param>
    /// <param name="discPath">Disc path, or empty.</param>
    /// <param name="path">Metadata path. Defaults to <see cref="DefaultPath"/>.</param>
    /// <returns><see langword="true"/> when a record was removed.</returns>
    public static bool Remove(string? gameId, string? discPath, string? path = null)
    {
        var normalizedId = NormalizeGameId(gameId);
        var normalizedPath = NormalizePath(discPath);
        LibraryMetadata metadata = Load(path);
        var removed = false;
        for (var i = metadata.Records.Count - 1; i >= 0; i--)
        {
            LibraryMetadataRecord existing = metadata.Records[i];
            if ((normalizedId.Length > 0
                    && string.Equals(normalizedId, NormalizeGameId(existing.GameId), StringComparison.OrdinalIgnoreCase))
                || (normalizedPath.Length > 0
                    && string.Equals(normalizedPath, NormalizePath(existing.Path), StringComparison.OrdinalIgnoreCase)))
            {
                metadata.Records.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            Save(metadata, path);
        }

        return removed;
    }

    private static string NormalizeGameId(string? gameId)
    {
        var normalized = gameId?.Trim().ToUpperInvariant() ?? string.Empty;
        return normalized.Length > 8 ? normalized[..8] : normalized;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim();
        }
    }

    /// <summary>Saves a metadata document atomically.</summary>
    /// <param name="metadata">Document to save.</param>
    /// <param name="path">Metadata path. Defaults to <see cref="DefaultPath"/>.</param>
    public static void Save(LibraryMetadata metadata, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
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
                JsonSerializer.Serialize(metadata, LibraryMetadataJsonContext.Default.LibraryMetadata));
            File.Move(temporary, resolved, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Library metadata {Path} could not be written.", resolved);
        }
    }
}
