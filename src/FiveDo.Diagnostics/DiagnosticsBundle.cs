using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FiveDo.Core;
using FiveDo.Diagnostics.Logging;

namespace FiveDo.Diagnostics;

/// <summary>Inputs for a diagnostics bundle.</summary>
public sealed record DiagnosticsBundleOptions
{
    /// <summary>Gets the optional settings file to include (sanitized).</summary>
    public string? SettingsPath { get; init; }

    /// <summary>Gets the optional log directory to include.</summary>
    public string? LogDirectory { get; init; }

    /// <summary>Gets the optional save state to include.</summary>
    public string? StatePath { get; init; }

    /// <summary>Gets the optional frame PNG to include.</summary>
    public string? FramePath { get; init; }

    /// <summary>Gets the optional TAS movie/input replay to include.</summary>
    public string? MoviePath { get; init; }

    /// <summary>Gets optional user notes included in the manifest.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the maximum number of log files to include. Default 5.</summary>
    public int MaxLogFiles { get; init; } = 5;

    /// <summary>Gets the maximum bytes read from each log file. Default 8 MB.</summary>
    public long MaxLogBytes { get; init; } = 8 * 1024 * 1024;
}

/// <summary>Result of writing a diagnostics bundle.</summary>
/// <param name="Path">Absolute zip path.</param>
/// <param name="Entries">Entry names written to the archive.</param>
/// <param name="ScrubHits">Number of private paths or user names replaced.</param>
public sealed record DiagnosticsBundleResult(
    string Path,
    IReadOnlyList<string> Entries,
    int ScrubHits);

/// <summary>
/// Creates a privacy-scrubbed support archive with logs, settings, environment, and
/// optional state/screenshot/replay evidence.
/// </summary>
/// <remarks>
/// The archive is meant for bug reports. It never includes raw user paths or the account
/// name: the user profile, executable directory, and temp directory are replaced with
/// placeholders, and known path-valued settings are removed. State, frame, and movie
/// files are copied only when the caller supplies their paths.
/// </remarks>
public static class DiagnosticsBundle
{
    /// <summary>Manifest schema version written by this build.</summary>
    public const int ManifestVersion = 1;

    private static readonly string[] SensitiveSettingsKeys =
    [
        "DiscPath",
        "RecentDiscs",
        "BiosPath",
        "Bios2Path",
        "BiosFolder",
        "SaveDirectory",
        "ScreenshotDirectory",
        "CheatsDirectory",
        "CoverDirectory",
        "LibraryFolders",
        "LibraryMetadataPath",
        "NvramDirectory",
        "AudioDevice",
        "BiosOverrides",
    ];

    /// <summary>Writes a diagnostics bundle.</summary>
    /// <param name="outputPath">Destination zip path.</param>
    /// <param name="options">Optional evidence and limits.</param>
    /// <returns>The written entries and scrub count.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="outputPath"/> is empty.</exception>
    /// <exception cref="IOException">Thrown when the destination cannot be written.</exception>
    public static DiagnosticsBundleResult Create(
        string outputPath,
        DiagnosticsBundleOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        options ??= new DiagnosticsBundleOptions();
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var entries = new List<string>();
        var scrubHits = 0;
        var temporary = fullPath + ".tmp";
        if (File.Exists(temporary))
        {
            File.Delete(temporary);
        }

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var manifest = new JsonObject
                {
                    ["schemaVersion"] = ManifestVersion,
                    ["createdAt"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    ["product"] = FiveDoVersion.ProductName,
                    ["version"] = FiveDoVersion.Version,
                    ["platform"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    ["architecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    ["processorCount"] = Environment.ProcessorCount,
                    ["settingsIncluded"] = false,
                    ["logFiles"] = new JsonArray(),
                    ["evidence"] = new JsonArray(),
                };

                AddTextEntry(
                    archive,
                    entries,
                    "environment.txt",
                    ScrubText(EnvironmentDetails.Format(), ref scrubHits));

                if (!string.IsNullOrWhiteSpace(options.SettingsPath) && File.Exists(options.SettingsPath))
                {
                    string sanitized = SanitizeSettings(options.SettingsPath, ref scrubHits);
                    AddTextEntry(archive, entries, "settings.json", sanitized);
                    manifest["settingsIncluded"] = true;
                }

                var logFiles = new JsonArray();
                foreach (var log in SelectLogs(options))
                {
                    var entryName = "logs/" + Path.GetFileName(log);
                    var text = ReadTail(log, options.MaxLogBytes);
                    AddTextEntry(archive, entries, entryName, ScrubText(text, ref scrubHits));
                    logFiles.Add((JsonNode?)JsonValue.Create(entryName));
                }

                manifest["logFiles"] = logFiles;

                var evidence = new JsonArray();
                AddEvidence(archive, entries, evidence, "state.bin", options.StatePath, ref scrubHits);
                AddEvidence(archive, entries, evidence, "frame.png", options.FramePath, ref scrubHits);
                AddEvidence(archive, entries, evidence, "input.replay", options.MoviePath, ref scrubHits);
                manifest["evidence"] = evidence;

                if (!string.IsNullOrWhiteSpace(options.Notes))
                {
                    manifest["notes"] = ScrubText(options.Notes.Trim(), ref scrubHits);
                }

                AddTextEntry(archive, entries, "manifest.json", WriteJson(manifest));
            }

            File.Move(temporary, fullPath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup only.
            }

            throw;
        }

        return new DiagnosticsBundleResult(fullPath, entries, scrubHits);
    }

    /// <summary>Replaces private paths and the account name with placeholders.</summary>
    /// <param name="text">Text to scrub.</param>
    /// <param name="hits">Incremented once per replacement.</param>
    /// <returns>The scrubbed text.</returns>
    public static string ScrubText(string text, ref int hits)
    {
        ArgumentNullException.ThrowIfNull(text);
        var scrubbed = text;
        foreach (var (value, replacement) in GetReplacements())
        {
            if (scrubbed.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                scrubbed = scrubbed.Replace(value, replacement, StringComparison.OrdinalIgnoreCase);
                hits++;
            }
        }

        return scrubbed;
    }

    /// <summary>Scrubs one value and removes path-like settings.</summary>
    /// <param name="path">Settings file.</param>
    /// <param name="hits">Incremented once per replacement.</param>
    /// <returns>Sanitized JSON, or a placeholder when the file is malformed.</returns>
    public static string SanitizeSettings(string path, ref int hits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"# settings could not be read: {exception.Message}";
        }

        try
        {
            JsonNode? node = JsonNode.Parse(text);
            if (node is JsonObject root)
            {
                foreach (var key in SensitiveSettingsKeys)
                {
                    if (root.Remove(key))
                    {
                        hits++;
                    }
                }

                ScrubNode(root, ref hits);
            }

            return node is null ? "{}" : WriteJson(node);
        }
        catch (JsonException exception)
        {
            return "# settings are not valid JSON: " + ScrubText(exception.Message, ref hits);
        }
    }

    private static string WriteJson(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static IEnumerable<string> SelectLogs(DiagnosticsBundleOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LogDirectory) || !Directory.Exists(options.LogDirectory))
        {
            return [];
        }

        return Directory.GetFiles(options.LogDirectory, "*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(Math.Clamp(options.MaxLogFiles, 1, 20));
    }

    private static string ReadTail(string path, long maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var length = stream.Length;
        if (length > maxBytes)
        {
            stream.Seek(-maxBytes, SeekOrigin.End);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        return length > maxBytes
            ? "[truncated to the last " + maxBytes.ToString(CultureInfo.InvariantCulture) + " bytes]" + Environment.NewLine + text
            : text;
    }

    private static void AddEvidence(
        ZipArchive archive,
        List<string> entries,
        JsonArray manifest,
        string entryName,
        string? path,
        ref int hits)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using (Stream destination = entry.Open())
        using (FileStream source = File.OpenRead(path))
        {
            source.CopyTo(destination);
        }

        entries.Add(entryName);
        manifest.Add((JsonNode?)JsonValue.Create(entryName));
        hits++;
    }

    private static void AddTextEntry(
        ZipArchive archive,
        List<string> entries,
        string entryName,
        string text)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
        entries.Add(entryName);
    }

    private static void ScrubNode(JsonNode node, ref int hits)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToArray())
                {
                    if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        if (LooksLikePath(text))
                        {
                            obj[property.Key] = "<path>";
                            hits++;
                        }
                        else
                        {
                            obj[property.Key] = ScrubText(text, ref hits);
                        }
                    }
                    else if (property.Value is not null)
                    {
                        ScrubNode(property.Value, ref hits);
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        if (LooksLikePath(text))
                        {
                            array[i] = "<path>";
                            hits++;
                        }
                        else
                        {
                            array[i] = ScrubText(text, ref hits);
                        }
                    }
                    else if (array[i] is not null)
                    {
                        ScrubNode(array[i]!, ref hits);
                    }
                }

                break;
        }
    }

    private static bool LooksLikePath(string value)
    {
        if (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] is '\\' or '/')
        {
            return true;
        }

        return value.StartsWith('/')
            || value.StartsWith("\\\\", StringComparison.Ordinal);
    }

    private static IEnumerable<(string Value, string Replacement)> GetReplacements()
    {
        var values = new List<(string, string)>
        {
            (AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), "<app>"),
            (Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), "<temp>"),
        };
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            values.Add((home.TrimEnd(Path.DirectorySeparatorChar), "<home>"));
        }

        var userName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(userName) && userName.Length >= 3)
        {
            values.Add((userName, "<user>"));
        }

        return values
            .Where(item => item.Item1.Length >= 3)
            .DistinctBy(item => item.Item1, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(item => item.Item1.Length);
    }
}
