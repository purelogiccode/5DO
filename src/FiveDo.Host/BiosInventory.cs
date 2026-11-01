using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>
/// Describes the result of importing a BIOS image into a library folder.
/// </summary>
/// <param name="Path">Absolute path of the imported or already-present image.</param>
/// <param name="Profile">Classification of the imported image.</param>
/// <param name="Copied"><see langword="true"/> when a new file was written.</param>
/// <param name="Note">Optional explanation for a skipped copy.</param>
public sealed record BiosImportResult(
    string Path,
    BiosProfile Profile,
    bool Copied,
    string? Note);

/// <summary>
/// File operations behind the desktop BIOS manager.
/// </summary>
/// <remarks>
/// Scanning and classification stay in <see cref="BiosLibrary"/>. This helper adds the
/// safe copy/import behavior the manager needs: files are validated before they are
/// copied, an image whose MD5 is already present is reused instead of duplicated, and
/// destination names are sanitized and never overwrite an unrelated BIOS.
/// </remarks>
public static class BiosInventory
{
    private static readonly string[] Extensions = [".bin", ".rom"];

    /// <summary>Scans a folder for BIOS images.</summary>
    /// <param name="folder">Folder to scan.</param>
    /// <param name="recursive">Whether to scan subfolders.</param>
    /// <returns>The classified library.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="folder"/> is empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the folder does not exist.</exception>
    public static BiosLibrary Scan(string folder, bool recursive = false)
    {
        return BiosLibrary.Scan(folder, recursive: recursive);
    }

    /// <summary>
    /// Validates and copies a BIOS file into the library folder.
    /// </summary>
    /// <param name="sourcePath">BIOS file to import.</param>
    /// <param name="folder">Destination BIOS folder. It is created when missing.</param>
    /// <returns>The import result.</returns>
    /// <exception cref="ArgumentException">Thrown when a path is empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the source is not a supported BIOS image.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read or written.</exception>
    public static BiosImportResult Import(string sourcePath, string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"BIOS file '{sourcePath}' was not found.", source);
        }

        BiosProfile profile = BiosLibrary.Describe(source);
        var destinationFolder = Path.GetFullPath(folder);
        Directory.CreateDirectory(destinationFolder);

        // An identical image already in the library is reused. This keeps repeated
        // imports from filling the folder with copies of the same ROM.
        BiosProfile? existing = FindByMd5(destinationFolder, profile.Md5);
        if (existing is not null)
        {
            return new BiosImportResult(
                existing.Path,
                existing,
                Copied: false,
                Note: $"The same BIOS image is already present as {existing.FileName}.");
        }

        var destination = ResolveDestination(destinationFolder, profile);
        var temporary = destination + ".tmp";
        try
        {
            File.Copy(source, temporary, overwrite: true);
            File.Move(temporary, destination, overwrite: false);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }

        BiosProfile imported = BiosLibrary.Describe(destination);
        return new BiosImportResult(destination, imported, Copied: true, Note: null);
    }

    /// <summary>Returns the file name the importer would use for a profile.</summary>
    /// <param name="folder">Destination folder.</param>
    /// <param name="profile">Profile to name.</param>
    /// <returns>A sanitized file name that does not collide with a different image.</returns>
    public static string SuggestFileName(string folder, BiosProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(profile);
        return Path.GetFileName(ResolveDestination(Path.GetFullPath(folder), profile));
    }

    /// <summary>Removes characters that are not valid in a cross-platform file name.</summary>
    /// <param name="name">Candidate name without a directory.</param>
    /// <returns>A safe file name.</returns>
    public static string SanitizeFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.');
        if (sanitized.Length == 0)
        {
            sanitized = "bios";
        }

        // Windows reserved device names are not valid even with an extension.
        var stem = Path.GetFileNameWithoutExtension(sanitized);
        if (IsReservedName(stem))
        {
            sanitized = "_" + sanitized;
        }

        const int maximumLength = 120;
        return sanitized.Length <= maximumLength ? sanitized : sanitized[..maximumLength];
    }

    private static BiosProfile? FindByMd5(string folder, string md5)
    {
        foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
        {
            if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                BiosProfile candidate = BiosLibrary.Describe(path);
                if (string.Equals(candidate.Md5, md5, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or ArgumentException)
            {
                // An unrelated malformed file must not block a valid import.
            }
        }

        return null;
    }

    private static string ResolveDestination(string folder, BiosProfile profile)
    {
        var extension = Path.GetExtension(profile.FileName);
        if (!Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            extension = ".bin";
        }

        extension = extension.ToLowerInvariant();
        var stem = SanitizeFileName(Path.GetFileNameWithoutExtension(profile.FileName));
        var candidate = Path.Combine(folder, stem + extension);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var tagged = $"{stem}-{profile.Md5[..Math.Min(8, profile.Md5.Length)]}{extension}";
        candidate = Path.Combine(folder, tagged);
        var suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folder, $"{stem}-{suffix}{extension}");
            suffix++;
        }

        return candidate;
    }

    private static bool IsReservedName(string stem)
    {
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 4
                && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && char.IsAsciiDigit(stem[3]));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort; the original exception is more useful.
        }
    }
}
