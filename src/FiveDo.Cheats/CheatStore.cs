using System;
using System.IO;
using System.Text.Json;

namespace FiveDo.Cheats;

/// <summary>
/// Loads and saves per-game cheat documents atomically.
/// </summary>
/// <remarks>
/// Files are named <c>&lt;gameId&gt;.json</c> in the configured directory. A missing file
/// is an empty document rather than an error, so enabling a cheat for a new game creates
/// the file on the first save. Invalid JSON or a future document version raises
/// <see cref="InvalidDataException"/>; invalid individual codes are preserved in the
/// document and reported through <see cref="CheatEntry.Error"/>.
/// </remarks>
public static class CheatStore
{
    /// <summary>Gets the default per-user cheat directory.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "5DO",
        "cheats");

    /// <summary>Resolves the configured cheat directory.</summary>
    /// <param name="configuredDirectory">Configured directory, or an empty value for the default.</param>
    /// <returns>The absolute directory path.</returns>
    public static string ResolveDirectory(string? configuredDirectory)
    {
        var directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? DefaultDirectory
            : configuredDirectory;
        return Path.GetFullPath(directory);
    }

    /// <summary>Resolves the file path for a game.</summary>
    /// <param name="directory">Cheat directory.</param>
    /// <param name="gameId">4DO game ID, or an empty value for the shared file.</param>
    /// <returns>The absolute file path.</returns>
    public static string ResolvePath(string directory, string? gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return Path.Combine(ResolveDirectory(directory), $"{NormalizeGameId(gameId)}.json");
    }

    /// <summary>Loads a cheat document, returning an empty document when the file is missing.</summary>
    /// <param name="path">Document path.</param>
    /// <param name="gameId">Game ID used when the document does not record one.</param>
    /// <returns>The loaded document.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file is not a supported cheat document.</exception>
    public static CheatDocument Load(string path, string? gameId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return new CheatDocument { GameId = NormalizeGameId(gameId) };
        }

        try
        {
            var json = File.ReadAllText(path);
            CheatDocument? document = JsonSerializer.Deserialize(
                json,
                CheatJsonContext.Default.CheatDocument);
            if (document is null)
            {
                throw new InvalidDataException($"The cheat file '{path}' is empty.");
            }

            if (document.Version > 1)
            {
                throw new InvalidDataException(
                    $"The cheat file '{path}' uses unsupported version {document.Version}.");
            }

            if (document.Version < 1)
            {
                document.Version = 1;
            }

            if (document.Cheats is null)
            {
                document.Cheats = [];
            }

            if (string.IsNullOrWhiteSpace(document.GameId))
            {
                document.GameId = NormalizeGameId(gameId);
            }

            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The cheat file '{path}' is not valid JSON.",
                exception);
        }
    }

    /// <summary>Saves a cheat document atomically.</summary>
    /// <param name="path">Document path.</param>
    /// <param name="document">Document to save.</param>
    public static void Save(string path, CheatDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(document, CheatJsonContext.Default.CheatDocument));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Normalizes a game ID into a safe file name.</summary>
    /// <param name="gameId">Game ID, or an empty value for the shared file.</param>
    /// <returns>The normalized identifier.</returns>
    public static string NormalizeGameId(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return "global";
        }

        var normalized = gameId.Trim().ToUpperInvariant();
        var buffer = new char[Math.Min(normalized.Length, 64)];
        for (var i = 0; i < buffer.Length; i++)
        {
            var character = normalized[i];
            buffer[i] = char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_';
        }

        return new string(buffer);
    }
}
