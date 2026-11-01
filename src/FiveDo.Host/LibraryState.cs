using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FiveDo.Host;

/// <summary>
/// User-owned library state for one game: favorite flag, play time, and launch history.
/// </summary>
public sealed class LibraryGameState
{
    /// <summary>Gets or sets a value indicating whether the user marked the game as a favorite.</summary>
    public bool Favorite { get; set; }

    /// <summary>Gets or sets the accumulated play time in seconds.</summary>
    public long PlaytimeSeconds { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last launch or play session.</summary>
    public DateTime? LastPlayedUtc { get; set; }

    /// <summary>Gets or sets the number of times the disc was opened.</summary>
    public int LaunchCount { get; set; }
}

/// <summary>
/// Persistent user data for the game library. Records are keyed by 4DO game ID when one is
/// known and by the full disc path for unknown or unreadable images.
/// </summary>
public sealed class LibraryState
{
    /// <summary>Current library-state schema version.</summary>
    public const int CurrentVersion = 1;

    private const string GameIdPrefix = "id:";
    private const string PathPrefix = "path:";

    /// <summary>Gets or sets the schema version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets the per-game state records keyed by <see cref="ResolveKey"/>.</summary>
    public IDictionary<string, LibraryGameState> Games { get; set; } = new Dictionary<string, LibraryGameState>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolves the stable state key for a game.</summary>
    /// <param name="gameId">4DO game ID, or empty for an unknown disc.</param>
    /// <param name="path">Full disc path.</param>
    /// <returns>A prefixed key that cannot collide between IDs and paths.</returns>
    public static string ResolveKey(string? gameId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalizedId = NormalizeGameId(gameId);
        return normalizedId.Length > 0
            ? GameIdPrefix + normalizedId
            : PathPrefix + NormalizePath(path);
    }

    /// <summary>Finds the state for a game without creating a record.</summary>
    /// <param name="gameId">4DO game ID, or empty for an unknown disc.</param>
    /// <param name="path">Full disc path.</param>
    /// <returns>The existing state, or <see langword="null"/>.</returns>
    public LibraryGameState? Find(string? gameId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalizedId = NormalizeGameId(gameId);
        if (normalizedId.Length > 0
            && Games.TryGetValue(GameIdPrefix + normalizedId, out LibraryGameState? byId))
        {
            return byId;
        }

        return Games.TryGetValue(PathPrefix + NormalizePath(path), out LibraryGameState? byPath)
            ? byPath
            : null;
    }

    /// <summary>Gets or creates the state for a game.</summary>
    /// <param name="gameId">4DO game ID, or empty for an unknown disc.</param>
    /// <param name="path">Full disc path.</param>
    /// <returns>The existing or newly created state.</returns>
    /// <remarks>
    /// When a disc was first seen before its game ID was known, the path-keyed record is
    /// migrated to the ID key so favorites and play time survive a library metadata refresh.
    /// </remarks>
    public LibraryGameState GetOrCreate(string? gameId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalizedId = NormalizeGameId(gameId);
        var key = normalizedId.Length > 0 ? GameIdPrefix + normalizedId : PathPrefix + NormalizePath(path);
        if (Games.TryGetValue(key, out LibraryGameState? existing))
        {
            return existing;
        }

        if (normalizedId.Length > 0)
        {
            var pathKey = PathPrefix + NormalizePath(path);
            if (Games.TryGetValue(pathKey, out LibraryGameState? byPath))
            {
                Games[key] = byPath;
                Games.Remove(pathKey);
                return byPath;
            }
        }

        var state = new LibraryGameState();
        Games[key] = state;
        return state;
    }

    /// <summary>Toggles the favorite flag for a game.</summary>
    /// <param name="gameId">4DO game ID, or empty for an unknown disc.</param>
    /// <param name="path">Full disc path.</param>
    /// <returns>The new favorite state.</returns>
    public bool ToggleFavorite(string? gameId, string path)
    {
        LibraryGameState state = GetOrCreate(gameId, path);
        state.Favorite = !state.Favorite;
        return state.Favorite;
    }

    /// <summary>Records that a disc was opened.</summary>
    /// <param name="gameId">4DO game ID, or empty for an unknown disc.</param>
    /// <param name="path">Full disc path.</param>
    /// <param name="utcNow">Timestamp to record; defaults to the current UTC time.</param>
    public void RecordLaunch(string? gameId, string path, DateTime? utcNow = null)
    {
        LibraryGameState state = GetOrCreate(gameId, path);
        state.LaunchCount = state.LaunchCount == int.MaxValue ? int.MaxValue : state.LaunchCount + 1;
        state.LastPlayedUtc = utcNow ?? DateTime.UtcNow;
    }

    /// <summary>Adds play time to a game.</summary>
    /// <param name="gameId">4DO game ID, or empty for an unknown disc.</param>
    /// <param name="path">Full disc path.</param>
    /// <param name="elapsed">Elapsed play time. Zero and negative values are ignored.</param>
    /// <param name="utcNow">Session-end timestamp; defaults to the current UTC time.</param>
    public void AddPlaytime(string? gameId, string path, TimeSpan elapsed, DateTime? utcNow = null)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        LibraryGameState state = GetOrCreate(gameId, path);
        var seconds = Math.Max(1L, (long)Math.Round(elapsed.TotalSeconds, MidpointRounding.AwayFromZero));
        state.PlaytimeSeconds = state.PlaytimeSeconds > long.MaxValue - seconds
            ? long.MaxValue
            : state.PlaytimeSeconds + seconds;
        state.LastPlayedUtc = utcNow ?? DateTime.UtcNow;
    }

    /// <summary>Copies user data onto a scanner entry.</summary>
    /// <param name="entry">Scanner entry to decorate.</param>
    /// <returns>The entry with favorite, play time, and launch history applied.</returns>
    public GameLibraryEntry Apply(GameLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        LibraryGameState? state = Find(entry.GameId, entry.Path);
        return state is null
            ? entry
            : entry with
            {
                Favorite = state.Favorite,
                PlaytimeSeconds = Math.Max(0, state.PlaytimeSeconds),
                LastPlayedUtc = state.LastPlayedUtc,
                LaunchCount = Math.Max(0, state.LaunchCount),
            };
    }

    /// <summary>Removes records that do not belong to the supplied entries.</summary>
    /// <param name="entries">Current library entries.</param>
    /// <returns>The number of records removed.</returns>
    public int Prune(IEnumerable<GameLibraryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (GameLibraryEntry entry in entries)
        {
            keys.Add(ResolveKey(entry.GameId, entry.Path));
            var normalizedId = NormalizeGameId(entry.GameId);
            if (normalizedId.Length > 0)
            {
                keys.Add(PathPrefix + NormalizePath(entry.Path));
            }
        }

        var removed = 0;
        foreach (var key in Games.Keys.ToArray())
        {
            if (!keys.Contains(key))
            {
                Games.Remove(key);
                removed++;
            }
        }

        return removed;
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

/// <summary>Loads and saves <see cref="LibraryState"/> atomically.</summary>
public static class LibraryStateStore
{
    /// <summary>Gets the default library-state file path.</summary>
    public static string DefaultPath => Path.Combine(AppSettingsStore.DefaultDirectory, "library-state.json");

    /// <summary>Loads library state, returning an empty state when the file is missing or invalid.</summary>
    /// <param name="path">State path. Defaults to <see cref="DefaultPath"/>.</param>
    /// <returns>The loaded state.</returns>
    public static LibraryState Load(string? path = null)
    {
        var resolved = path ?? DefaultPath;
        try
        {
            if (!File.Exists(resolved))
            {
                return new LibraryState();
            }

            LibraryState state = JsonSerializer.Deserialize(
                File.ReadAllText(resolved),
                LibraryStateJsonContext.Default.LibraryState) ?? new LibraryState();
            if (state.Version != LibraryState.CurrentVersion)
            {
                Serilog.Log.Debug(
                    "Library state {Path} has unsupported version {Version}; rebuilding it.",
                    resolved,
                    state.Version);
                return new LibraryState();
            }

            IDictionary<string, LibraryGameState> games = state.Games
                ?? new Dictionary<string, LibraryGameState>(StringComparer.OrdinalIgnoreCase);
            state.Games = new Dictionary<string, LibraryGameState>(games, StringComparer.OrdinalIgnoreCase);
            return state;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Library state {Path} is unreadable; rebuilding it.", resolved);
            return new LibraryState();
        }
    }

    /// <summary>Saves library state atomically.</summary>
    /// <param name="state">State to save.</param>
    /// <param name="path">State path. Defaults to <see cref="DefaultPath"/>.</param>
    public static void Save(LibraryState state, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(state);
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
                JsonSerializer.Serialize(state, LibraryStateJsonContext.Default.LibraryState));
            File.Move(temporary, resolved, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Serilog.Log.Debug(exception, "Library state {Path} could not be written.", resolved);
        }
    }
}
