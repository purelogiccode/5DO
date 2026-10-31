using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>
/// Resolves NVRAM file locations for the global and per-game storage modes.
/// </summary>
/// <remarks>
/// <para>
/// The layout mirrors Opera's NVRAM options while keeping 5DO's historical
/// <c>nvram.ram</c> readable. A version is a separate save slot (0-9): switching
/// versions never overwrites another version's data.
/// </para>
/// <list type="bullet">
/// <item><description>Global, version 0: <c>&lt;root&gt;/nvram.ram</c> (legacy).</description></item>
/// <item><description>Global, version N: <c>&lt;root&gt;/nvram.N.ram</c>.</description></item>
/// <item><description>Per-game, version N: <c>&lt;root&gt;/nvram/per-game/&lt;gameId&gt;.N.ram</c>.</description></item>
/// </list>
/// <para>
/// When a per-game image does not exist yet, the loader falls back to the shared
/// image so an existing global NVRAM is not lost when the mode is switched. The
/// fallback is read-only: the next flush writes the per-game file.
/// </para>
/// </remarks>
public static class NvramLayout
{
    /// <summary>Lowest supported NVRAM version.</summary>
    public const int MinVersion = 0;

    /// <summary>Highest supported NVRAM version.</summary>
    public const int MaxVersion = 9;

    /// <summary>Historical shared NVRAM file name.</summary>
    public const string LegacyFileName = "nvram.ram";

    /// <summary>Directory below the NVRAM root that holds per-game images.</summary>
    public const string PerGameDirectoryName = "per-game";

    private const string NvramDirectoryName = "nvram";
    private const int MaxGameIdLength = 64;

    /// <summary>Validates an NVRAM version.</summary>
    /// <param name="version">Version number to validate.</param>
    /// <returns>The validated version.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the version is outside 0-9.</exception>
    public static int ValidateVersion(int version)
    {
        if (version is < MinVersion or > MaxVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                $"NVRAM version must be between {MinVersion} and {MaxVersion}.");
        }

        return version;
    }

    /// <summary>Resolves the primary NVRAM path for a storage mode.</summary>
    /// <param name="rootDirectory">Base directory that owns the NVRAM layout.</param>
    /// <param name="mode">Global or per-game storage.</param>
    /// <param name="version">Version 0-9.</param>
    /// <param name="gameId">4DO game ID. Required for <see cref="NvramStorageMode.PerGame"/>.</param>
    /// <returns>The absolute NVRAM path.</returns>
    /// <exception cref="ArgumentException">Thrown when the root is empty or a per-game game ID is missing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the version is invalid.</exception>
    public static string Resolve(
        string rootDirectory,
        NvramStorageMode mode,
        int version,
        string? gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ValidateVersion(version);
        var root = Path.GetFullPath(rootDirectory);

        if (mode == NvramStorageMode.PerGame)
        {
            var normalized = NormalizeGameId(gameId)
                ?? throw new ArgumentException(
                    "Per-game NVRAM needs a non-empty game ID.",
                    nameof(gameId));
            return Path.Combine(
                root,
                NvramDirectoryName,
                PerGameDirectoryName,
                $"{normalized}.{version.ToString(CultureInfo.InvariantCulture)}.ram");
        }

        if (mode != NvramStorageMode.Global)
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown NVRAM storage mode.");
        }

        return version == MinVersion
            ? Path.Combine(root, LegacyFileName)
            : Path.Combine(
                root,
                $"nvram.{version.ToString(CultureInfo.InvariantCulture)}.ram");
    }

    /// <summary>
    /// Returns migration fallbacks for a primary path, most-specific first.
    /// </summary>
    /// <param name="rootDirectory">Base directory that owns the NVRAM layout.</param>
    /// <param name="mode">Global or per-game storage.</param>
    /// <param name="version">Version 0-9.</param>
    /// <param name="gameId">4DO game ID, required for per-game storage.</param>
    /// <returns>Candidate paths that do not include the primary path.</returns>
    public static IReadOnlyList<string> GetFallbackPaths(
        string rootDirectory,
        NvramStorageMode mode,
        int version,
        string? gameId)
    {
        string primary = Resolve(rootDirectory, mode, version, gameId);
        var root = Path.GetFullPath(rootDirectory);
        var candidates = new List<string>();

        if (mode == NvramStorageMode.PerGame)
        {
            if (version != MinVersion)
            {
                candidates.Add(Resolve(root, NvramStorageMode.PerGame, MinVersion, gameId));
            }

            candidates.Add(Resolve(root, NvramStorageMode.Global, version, null));
            if (version != MinVersion)
            {
                candidates.Add(Resolve(root, NvramStorageMode.Global, MinVersion, null));
            }
        }
        else if (version != MinVersion)
        {
            candidates.Add(Resolve(root, NvramStorageMode.Global, MinVersion, null));
        }

        return candidates
            .Where(path => !string.Equals(path, primary, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Normalizes a game ID for use as a file name.
    /// </summary>
    /// <param name="gameId">Game ID, usually a 32-character MD5.</param>
    /// <returns>A lower-case safe name, or <see langword="null"/> for an empty ID.</returns>
    public static string? NormalizeGameId(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var builder = new StringBuilder(Math.Min(gameId.Length, MaxGameIdLength));
        foreach (var character in gameId.Trim())
        {
            if (builder.Length == MaxGameIdLength)
            {
                break;
            }

            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_'
                ? char.ToLowerInvariant(character)
                : '_');
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>Enumerates recognized NVRAM images below a root directory.</summary>
    /// <param name="rootDirectory">Base directory that owns the NVRAM layout.</param>
    /// <returns>Recognized images, ordered by path.</returns>
    public static IReadOnlyList<NvramFile> Enumerate(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        var files = new List<NvramFile>();

        if (Directory.Exists(root))
        {
            foreach (var path in Directory.EnumerateFiles(root, "nvram*.ram", SearchOption.TopDirectoryOnly))
            {
                if (TryParseFileName(path, out NvramFile? file) && file is not null)
                {
                    files.Add(file);
                }
            }

            var perGameDirectory = Path.Combine(root, NvramDirectoryName, PerGameDirectoryName);
            if (Directory.Exists(perGameDirectory))
            {
                foreach (var path in Directory.EnumerateFiles(perGameDirectory, "*.ram", SearchOption.TopDirectoryOnly))
                {
                    if (TryParseFileName(path, out NvramFile? file) && file is not null)
                    {
                        files.Add(file);
                    }
                }
            }
        }

        return files
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Parses a recognized NVRAM path into its layout metadata.</summary>
    /// <param name="path">Path to inspect.</param>
    /// <param name="file">Receives the metadata when the path is recognized.</param>
    /// <returns><see langword="true"/> when the path follows the NVRAM layout.</returns>
    public static bool TryParseFileName(string path, out NvramFile? file)
    {
        file = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var fileName = Path.GetFileName(fullPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is null)
        {
            return false;
        }

        var parentName = Path.GetFileName(directory);
        var grandParentName = Path.GetFileName(Path.GetDirectoryName(directory));
        var isPerGame = string.Equals(parentName, PerGameDirectoryName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(grandParentName, NvramDirectoryName, StringComparison.OrdinalIgnoreCase);

        if (isPerGame)
        {
            // Expected <gameId>.<version>.ram. Normalized game IDs never contain dots.
            var parts = fileName.Split('.');
            if (parts.Length != 3
                || !string.Equals(parts[2], "ram", StringComparison.OrdinalIgnoreCase)
                || parts[0].Length == 0
                || !TryParseVersion(parts[1], out var perGameVersion))
            {
                return false;
            }

            file = CreateFile(
                fullPath,
                NvramStorageMode.PerGame,
                perGameVersion,
                NormalizeGameId(parts[0]));
            return true;
        }

        if (string.Equals(fileName, LegacyFileName, StringComparison.OrdinalIgnoreCase))
        {
            file = CreateFile(fullPath, NvramStorageMode.Global, MinVersion, null);
            return true;
        }

        if (fileName.StartsWith("nvram.", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".ram", StringComparison.OrdinalIgnoreCase))
        {
            var versionText = fileName[6..^4];
            if (TryParseVersion(versionText, out var globalVersion))
            {
                file = CreateFile(fullPath, NvramStorageMode.Global, globalVersion, null);
                return true;
            }
        }

        return false;
    }

    private static NvramFile CreateFile(
        string path,
        NvramStorageMode mode,
        int version,
        string? gameId)
    {
        var info = new FileInfo(path);
        return new NvramFile(
            path,
            mode,
            version,
            gameId,
            info.Exists ? info.Length : 0,
            info.Exists ? info.LastWriteTimeUtc : null);
    }

    private static bool TryParseVersion(string text, out int version)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out version)
            && version is >= MinVersion and <= MaxVersion;
    }
}

/// <summary>Describes one NVRAM image discovered by <see cref="NvramLayout.Enumerate"/>.</summary>
/// <param name="Path">Absolute file path.</param>
/// <param name="Mode">Storage mode encoded by the path.</param>
/// <param name="Version">NVRAM version 0-9.</param>
/// <param name="GameId">Game ID for per-game images; <see langword="null"/> for shared images.</param>
/// <param name="Length">File size in bytes, or zero when the file is missing.</param>
/// <param name="LastWriteTimeUtc">Last write time, or <see langword="null"/> when missing.</param>
public sealed record NvramFile(
    string Path,
    NvramStorageMode Mode,
    int Version,
    string? GameId,
    long Length,
    DateTime? LastWriteTimeUtc);
