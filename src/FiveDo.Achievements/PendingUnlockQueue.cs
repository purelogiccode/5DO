using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Serilog;

namespace FiveDo.Achievements;

/// <summary>A queued achievement award that still needs to reach the server.</summary>
internal sealed record PendingUnlock(
    uint AchievementId,
    uint GameId,
    string GameHash,
    string Username,
    bool Hardcore,
    DateTimeOffset UnlockedAtUtc,
    string Title,
    uint Points);

/// <summary>
/// Durable, atomically written queue of achievement unlocks created while the server
/// was unavailable. The queue never stores the API token.
/// </summary>
/// <remarks>
/// The payload is wrapped with a SHA-256 checksum so accidental corruption is detected
/// before an award is replayed. Files are written to a temporary sibling and moved into
/// place, and are restricted to the current user on Unix-like systems.
/// </remarks>
internal sealed class PendingUnlockQueue
{
    private const int FormatVersion = 1;
    private const int MaxEntries = 1000;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private readonly Lock _gate = new();
    private readonly List<PendingUnlock> _entries = [];
    private readonly string? _path;

    public PendingUnlockQueue(string? path)
    {
        _path = string.IsNullOrWhiteSpace(path) ? null : path;
        Load();
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public IReadOnlyList<PendingUnlock> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }

    public bool Add(PendingUnlock entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.AchievementId == 0 || entry.AchievementId >= 101_000_000)
        {
            return false;
        }

        lock (_gate)
        {
            PruneLocked(DateTimeOffset.UtcNow);
            foreach (PendingUnlock existing in _entries)
            {
                if (existing.AchievementId == entry.AchievementId
                    && existing.Hardcore == entry.Hardcore
                    && string.Equals(existing.Username, entry.Username, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            _entries.Add(entry);
            PruneLocked(DateTimeOffset.UtcNow);
            SaveLocked();
            return true;
        }
    }

    public bool Remove(uint achievementId, string username, bool hardcore)
    {
        lock (_gate)
        {
            bool removed = _entries.RemoveAll(entry =>
                entry.AchievementId == achievementId
                && entry.Hardcore == hardcore
                && string.Equals(entry.Username, username, StringComparison.OrdinalIgnoreCase)) != 0;

            if (removed)
            {
                SaveLocked();
            }

            return removed;
        }
    }

    public bool Contains(uint achievementId, string username, bool hardcore)
    {
        lock (_gate)
        {
            foreach (PendingUnlock entry in _entries)
            {
                if (entry.AchievementId == achievementId
                    && entry.Hardcore == hardcore
                    && string.Equals(entry.Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Prune()
    {
        lock (_gate)
        {
            if (PruneLocked(DateTimeOffset.UtcNow))
            {
                SaveLocked();
            }
        }
    }

    private void Load()
    {
        if (_path is null || !File.Exists(_path))
        {
            return;
        }

        try
        {
            byte[] wrapperBytes = File.ReadAllBytes(_path);
            using JsonDocument wrapper = JsonDocument.Parse(wrapperBytes);
            JsonElement root = wrapper.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("payload", out JsonElement payloadElement)
                || !root.TryGetProperty("sha256", out JsonElement checksumElement)
                || payloadElement.ValueKind != JsonValueKind.String
                || checksumElement.ValueKind != JsonValueKind.String)
            {
                Quarantine("missing payload or checksum");
                return;
            }

            byte[] payload = Convert.FromBase64String(payloadElement.GetString() ?? string.Empty);
            byte[] expected = Convert.FromHexString(checksumElement.GetString() ?? string.Empty);
            byte[] actual = SHA256.HashData(payload);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                Quarantine("checksum mismatch");
                return;
            }

            using JsonDocument document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("entries", out JsonElement entriesElement)
                || entriesElement.ValueKind != JsonValueKind.Array)
            {
                Quarantine("entries is missing or is not an array");
                return;
            }

            lock (_gate)
            {
                foreach (JsonElement entry in entriesElement.EnumerateArray())
                {
                    PendingUnlock? parsed = ParseEntry(entry);
                    if (parsed is not null)
                    {
                        _entries.Add(parsed);
                    }
                }

                if (PruneLocked(DateTimeOffset.UtcNow))
                {
                    SaveLocked();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or FormatException or ArgumentException or NotSupportedException)
        {
            Quarantine(exception.Message);
        }
    }

    private static PendingUnlock? ParseEntry(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("achievementId", out JsonElement achievementElement)
            || !element.TryGetProperty("gameId", out JsonElement gameElement)
            || !element.TryGetProperty("gameHash", out JsonElement hashElement)
            || !element.TryGetProperty("username", out JsonElement usernameElement)
            || !element.TryGetProperty("hardcore", out JsonElement hardcoreElement)
            || !element.TryGetProperty("unlockedAt", out JsonElement unlockedElement)
            || !element.TryGetProperty("title", out JsonElement titleElement)
            || !element.TryGetProperty("points", out JsonElement pointsElement))
        {
            return null;
        }

        if (!achievementElement.TryGetUInt32(out uint achievementId)
            || !gameElement.TryGetUInt32(out uint gameId)
            || !unlockedElement.TryGetInt64(out long unlockedAt)
            || !pointsElement.TryGetUInt32(out uint points))
        {
            return null;
        }

        bool hardcore;
        if (hardcoreElement.ValueKind == JsonValueKind.True)
        {
            hardcore = true;
        }
        else if (hardcoreElement.ValueKind == JsonValueKind.False)
        {
            hardcore = false;
        }
        else
        {
            return null;
        }

        string hash = hashElement.GetString() ?? string.Empty;
        string username = usernameElement.GetString() ?? string.Empty;
        string title = titleElement.GetString() ?? string.Empty;
        if (achievementId == 0
            || achievementId >= 101_000_000
            || gameId == 0
            || hash.Length == 0
            || username.Length == 0
            || unlockedAt <= 0)
        {
            return null;
        }

        return new PendingUnlock(
            achievementId,
            gameId,
            hash,
            username,
            hardcore,
            DateTimeOffset.FromUnixTimeSeconds(unlockedAt),
            title,
            points);
    }

    private bool PruneLocked(DateTimeOffset now)
    {
        int removed = _entries.RemoveAll(entry => now - entry.UnlockedAtUtc > MaxAge);
        if (_entries.Count > MaxEntries)
        {
            _entries.Sort(static (left, right) => left.UnlockedAtUtc.CompareTo(right.UnlockedAtUtc));
            removed += _entries.Count - MaxEntries;
            _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }

        return removed != 0;
    }

    private void SaveLocked()
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            byte[] payload = BuildPayload();
            byte[] checksum = SHA256.HashData(payload);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("payload", Convert.ToBase64String(payload));
                writer.WriteString("sha256", Convert.ToHexStringLower(checksum));
                writer.WriteEndObject();
            }

            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporary = _path + ".tmp";
            File.WriteAllBytes(temporary, stream.ToArray());
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
            Log.Warning(exception, "RetroAchievements pending-unlock queue could not be saved to {Path}", _path);
        }
    }

    private byte[] BuildPayload()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", FormatVersion);
            writer.WriteStartArray("entries");
            foreach (PendingUnlock entry in _entries)
            {
                writer.WriteStartObject();
                writer.WriteNumber("achievementId", entry.AchievementId);
                writer.WriteNumber("gameId", entry.GameId);
                writer.WriteString("gameHash", entry.GameHash);
                writer.WriteString("username", entry.Username);
                writer.WriteBoolean("hardcore", entry.Hardcore);
                writer.WriteNumber("unlockedAt", entry.UnlockedAtUtc.ToUnixTimeSeconds());
                writer.WriteString("title", entry.Title);
                writer.WriteNumber("points", entry.Points);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private void Quarantine(string reason)
    {
        Log.Warning("RetroAchievements pending-unlock queue at {Path} is invalid: {Reason}", _path, reason);
        if (_path is null)
        {
            return;
        }

        try
        {
            string quarantine = _path + ".bad";
            File.Move(_path, quarantine, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Debug(exception, "RetroAchievements pending-unlock queue could not be quarantined");
        }
    }
}
