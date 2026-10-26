using System;
using System.Collections.Generic;
using System.IO;
using ThreeDo.Storage;

namespace ThreeDo.Host;

/// <summary>One disc found by the library scanner.</summary>
/// <param name="Path">Disc file path.</param>
/// <param name="Format">Container format label.</param>
/// <param name="SectorCount">Number of 2048-byte sectors.</param>
/// <param name="GameId">MD5 game identifier, or empty when the image is unreadable.</param>
/// <param name="Name">Database title, or the file name for unknown discs.</param>
/// <param name="Known">Whether the 4DO game database recognized the disc.</param>
/// <param name="Error">Error message when the image could not be opened.</param>
/// <param name="CoverPath">Cover-art image path, or empty when none was found.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "NotAccessedPositionalProperty.Global",
    Justification = "The game identifier is part of the scanner's public result for host tooling.")]
public sealed record GameLibraryEntry(
    string Path,
    string Format,
    int SectorCount,
    string GameId,
    string Name,
    bool Known,
    string Error,
    string CoverPath = "");

/// <summary>
/// Scans folders for ISO/BIN/CUE/CHD images and resolves titles through the 4DO game database.
/// </summary>
public static class GameLibraryScanner
{
    private static readonly string[] Extensions = [".iso", ".bin", ".cue", ".chd"];

    private static readonly string[] CoverExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    /// <summary>Scans one or more folders, recursively.</summary>
    /// <param name="folders">Folders to scan. Missing folders are skipped.</param>
    /// <param name="maximumDepth">Maximum recursion depth (0 scans only the top level).</param>
    /// <returns>Entries sorted by title then path.</returns>
    public static IReadOnlyList<GameLibraryEntry> Scan(IEnumerable<string> folders, int maximumDepth = 4)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);

        var entries = new List<GameLibraryEntry>();
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            ScanFolder(folder, maximumDepth, entries);
        }

        entries.Sort(static (left, right) =>
        {
            var title = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return title != 0 ? title : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
        });

        return entries;
    }

    /// <summary>Builds a library entry for one disc, or returns null for an unsupported file.</summary>
    /// <param name="path">Disc path.</param>
    /// <returns>The entry, or null when the extension is not a supported disc container.</returns>
    public static GameLibraryEntry? Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsDiscPath(path))
        {
            return null;
        }

        var coverPath = FindCover(path);
        try
        {
            using var disc = DiscImageFactory.Open(path);
            var gameId = disc.SectorCount >= 2 ? GameId.Compute(disc) : string.Empty;
            var record = gameId.Length == 0 ? null : GameDatabase.Default.FindByChecksum(gameId);
            return new GameLibraryEntry(
                path,
                Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
                disc.SectorCount,
                gameId,
                record?.Name ?? Path.GetFileNameWithoutExtension(path),
                record is not null,
                string.Empty,
                coverPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or DiscImageException or ArgumentException)
        {
            return new GameLibraryEntry(
                path,
                Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
                0,
                string.Empty,
                Path.GetFileNameWithoutExtension(path),
                false,
                exception.Message,
                coverPath);
        }
    }

    /// <summary>
    /// Finds cover art next to a disc: a sibling image with the same base name
    /// (for example <c>game.png</c> beside <c>game.iso</c>).
    /// </summary>
    /// <param name="discPath">Disc path.</param>
    /// <returns>The cover path, or an empty string when none exists.</returns>
    public static string FindCover(string discPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(discPath));
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        var baseName = Path.GetFileNameWithoutExtension(discPath);
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

    private static void ScanFolder(string folder, int depth, List<GameLibraryEntry> entries)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var file in files)
        {
            var entry = Inspect(file);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        if (depth == 0)
        {
            return;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var directory in directories)
        {
            ScanFolder(directory, depth - 1, entries);
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
