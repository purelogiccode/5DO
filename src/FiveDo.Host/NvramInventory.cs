using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>
/// One NVRAM image discovered by the desktop manager.
/// </summary>
/// <param name="File">Layout metadata for the image.</param>
/// <param name="IsValid">Whether the file is a readable 32 KB NVRAM image.</param>
/// <param name="Error">Validation error when <paramref name="IsValid"/> is false.</param>
/// <param name="GameName">Game-database title for a per-game image, when known.</param>
public sealed record NvramInventoryEntry(
    NvramFile File,
    bool IsValid,
    string? Error,
    string? GameName)
{
    /// <summary>Gets a short storage-mode label.</summary>
    public string ModeLabel => File.Mode == NvramStorageMode.PerGame ? "Per game" : "Shared";

    /// <summary>Gets the game title, game ID, or shared label.</summary>
    public string GameLabel
    {
        get
        {
            if (string.IsNullOrEmpty(File.GameId))
            {
                return "Shared";
            }

            return string.IsNullOrWhiteSpace(GameName) ? File.GameId : GameName;
        }
    }

    /// <summary>Gets the game ID or an empty string for shared images.</summary>
    public string GameIdLabel => File.GameId ?? string.Empty;

    /// <summary>Gets a human-readable size.</summary>
    public string SizeLabel => File.Length == 0
        ? "missing"
        : $"{File.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes";

    /// <summary>Gets a human-readable last-write time.</summary>
    public string ModifiedLabel => File.LastWriteTimeUtc is { } modified
        ? modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        : "unknown";

    /// <summary>Gets a one-line validity label.</summary>
    public string StateLabel => IsValid ? "valid" : $"invalid: {Error}";
}

/// <summary>
/// File operations behind the desktop NVRAM manager.
/// </summary>
/// <remarks>
/// The manager never rewrites the active emulator image directly. Backup, import, reset,
/// and delete operate on the selected file and use temporary-file replacement for writes,
/// so a failed operation cannot leave a half-written NVRAM image.
/// </remarks>
public static class NvramInventory
{
    /// <summary>Enumerates and validates recognized NVRAM images below a root directory.</summary>
    /// <param name="rootDirectory">Base directory that owns the NVRAM layout.</param>
    /// <returns>Entries ordered by path, with validation and game metadata.</returns>
    public static IReadOnlyList<NvramInventoryEntry> Enumerate(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var entries = new List<NvramInventoryEntry>();
        foreach (NvramFile file in NvramLayout.Enumerate(rootDirectory))
        {
            var valid = false;
            string? error = null;
            try
            {
                NvramImage image = NvramImage.Load(file.Path);
                valid = image.Data.Length == EmulatorConstants.NvramSize;
                if (!valid)
                {
                    error = $"expected {EmulatorConstants.NvramSize} bytes";
                }
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or ArgumentException)
            {
                error = exception.Message;
            }

            entries.Add(new NvramInventoryEntry(file, valid, error, ResolveGameName(file.GameId)));
        }

        return entries
            .OrderBy(entry => entry.File.Mode)
            .ThenBy(entry => entry.File.Version)
            .ThenBy(entry => entry.File.GameId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.File.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Copies one NVRAM image to a backup file.</summary>
    /// <param name="sourcePath">Image to copy.</param>
    /// <param name="destinationPath">Destination file. Parent folders are created.</param>
    /// <returns>The absolute destination path.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the source does not exist.</exception>
    public static string Backup(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"NVRAM image '{sourcePath}' was not found.", source);
        }

        var destination = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(source, destination, overwrite: true);
        return destination;
    }

    /// <summary>Copies every recognized image below a root into a backup folder.</summary>
    /// <param name="rootDirectory">Base directory that owns the NVRAM layout.</param>
    /// <param name="destinationFolder">Folder that receives the layout-preserving copies.</param>
    /// <returns>The number of copied images.</returns>
    public static int BackupAll(string rootDirectory, string destinationFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        var root = Path.GetFullPath(rootDirectory);
        var destination = Path.GetFullPath(destinationFolder);
        var count = 0;
        foreach (NvramFile file in NvramLayout.Enumerate(root))
        {
            var relative = Path.GetRelativePath(root, file.Path);
            Backup(file.Path, Path.Combine(destination, relative));
            count++;
        }

        return count;
    }

    /// <summary>Validates and replaces an NVRAM image with a file from another location.</summary>
    /// <param name="sourcePath">32 KB image to import.</param>
    /// <param name="destinationPath">Image path to replace or create.</param>
    /// <returns>The absolute destination path.</returns>
    /// <exception cref="InvalidDataException">Thrown when the source is not a 32 KB image.</exception>
    public static string Import(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        NvramImage image = NvramImage.Load(sourcePath);
        WriteAtomic(destinationPath, image.Data.Span);
        return Path.GetFullPath(destinationPath);
    }

    /// <summary>Replaces an NVRAM image with a fresh initialized volume.</summary>
    /// <param name="destinationPath">Image path to reset or create.</param>
    /// <returns>The absolute destination path.</returns>
    public static string Reset(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        NvramImage image = NvramImage.CreateFresh();
        WriteAtomic(destinationPath, image.Data.Span);
        return Path.GetFullPath(destinationPath);
    }

    /// <summary>Deletes an NVRAM image.</summary>
    /// <param name="path">Image path.</param>
    /// <returns><see langword="true"/> when a file was deleted.</returns>
    public static bool Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var resolved = Path.GetFullPath(path);
        if (!File.Exists(resolved))
        {
            return false;
        }

        File.Delete(resolved);
        return true;
    }

    /// <summary>Suggests a descriptive backup file name for an image.</summary>
    /// <param name="file">Image metadata.</param>
    /// <param name="now">Timestamp used in the name.</param>
    /// <returns>A safe file name ending in <c>.ram</c>.</returns>
    public static string SuggestBackupFileName(NvramFile file, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(file);
        var game = string.IsNullOrEmpty(file.GameId) ? "shared" : file.GameId;
        var stem = $"nvram-{game}-v{file.Version.ToString(CultureInfo.InvariantCulture)}-{now:yyyyMMdd-HHmmss}";
        return BiosInventory.SanitizeFileName(stem) + ".ram";
    }

    /// <summary>Suggests a timestamped backup folder name.</summary>
    /// <param name="now">Timestamp used in the name.</param>
    /// <returns>A safe folder name.</returns>
    public static string SuggestBackupFolderName(DateTimeOffset now)
    {
        return $"nvram-backup-{now:yyyyMMdd-HHmmss}";
    }

    private static string? ResolveGameName(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        GameRecord? record = GameDatabase.Default.FindByChecksum(gameId)
            ?? GameDatabase.Default.FindById(gameId);
        return string.IsNullOrWhiteSpace(record?.Name) ? null : record.Name;
    }

    private static void WriteAtomic(string destinationPath, ReadOnlySpan<byte> data)
    {
        var destination = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = destination + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, data.ToArray());
            File.Move(temporary, destination, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Best effort; the original exception is more useful.
            }

            throw;
        }
    }
}
