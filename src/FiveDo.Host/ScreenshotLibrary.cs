using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FiveDo.Host;

/// <summary>One PNG screenshot on disk.</summary>
/// <param name="Path">Absolute file path.</param>
/// <param name="FileName">File name with extension.</param>
/// <param name="GameName">Game title parsed from the file name, or the file stem.</param>
/// <param name="CapturedAt">Timestamp parsed from the file name or the file timestamp.</param>
/// <param name="FrameNumber">Emulated frame parsed from the file name, or -1.</param>
/// <param name="Native">Whether this is the optional native-resolution companion.</param>
public sealed record ScreenshotEntry(
    string Path,
    string FileName,
    string GameName,
    DateTimeOffset CapturedAt,
    long FrameNumber,
    bool Native)
{
    /// <summary>Gets a display caption for the screenshot.</summary>
    public string Caption
    {
        get
        {
            var frame = FrameNumber >= 0
                ? $" · frame {FrameNumber.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            var native = Native ? " · native" : string.Empty;
            return $"{GameName} · {CapturedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}{frame}{native}";
        }
    }
}

/// <summary>Lists, parses, and deletes screenshot PNGs.</summary>
public static class ScreenshotLibrary
{
    private const string StampFormat = "yyyyMMdd-HHmmss";

    /// <summary>Lists screenshots in a directory, newest first.</summary>
    /// <param name="directory">Screenshot directory.</param>
    /// <param name="gameFilter">Optional case-insensitive game-name substring.</param>
    /// <param name="from">Optional inclusive lower capture-time bound.</param>
    /// <param name="to">Optional inclusive upper capture-time bound.</param>
    /// <returns>Entries sorted by capture time and file name; empty when the directory is absent.</returns>
    public static IReadOnlyList<ScreenshotEntry> List(
        string directory,
        string? gameFilter = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var normalizedGame = gameFilter?.Trim();
        return Directory.GetFiles(directory, "*.png")
            .Select(Parse)
            .Where(entry => Matches(entry, normalizedGame, from, to))
            .OrderByDescending(entry => entry.CapturedAt)
            .ThenBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Lists the distinct game names present in a directory.</summary>
    /// <param name="directory">Screenshot directory.</param>
    /// <returns>Game names sorted case-insensitively; empty when the directory is absent.</returns>
    public static IReadOnlyList<string> ListGames(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(directory, "*.png")
            .Select(Parse)
            .Select(entry => entry.GameName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Copies a screenshot to a new path, creating the destination directory.</summary>
    /// <param name="path">Source screenshot path.</param>
    /// <param name="destination">Destination file path.</param>
    /// <returns>The absolute destination path.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the source does not exist.</exception>
    public static string Export(string path, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var source = Path.GetFullPath(path);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("The screenshot does not exist.", source);
        }

        var target = Path.GetFullPath(destination);
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(source, target, overwrite: true);
        return target;
    }

    private static bool Matches(
        ScreenshotEntry entry,
        string? gameFilter,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (!string.IsNullOrEmpty(gameFilter)
            && entry.GameName.IndexOf(gameFilter, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (from is { } lower && entry.CapturedAt < lower)
        {
            return false;
        }

        return to is not { } upper || entry.CapturedAt <= upper;
    }

    /// <summary>Parses a screenshot file name.</summary>
    /// <param name="path">Screenshot path.</param>
    /// <returns>The parsed entry.</returns>
    public static ScreenshotEntry Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var fileName = Path.GetFileName(fullPath);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var native = stem.EndsWith("-native", StringComparison.OrdinalIgnoreCase);
        if (native)
        {
            stem = stem[..^"-native".Length];
        }

        DateTimeOffset capturedAt = File.Exists(fullPath)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath), TimeSpan.Zero)
            : DateTimeOffset.MinValue;
        long frame = -1;
        var gameName = stem;

        int frameMarker = stem.LastIndexOf("-f", StringComparison.Ordinal);
        if (frameMarker > 0
            && long.TryParse(
                stem.AsSpan(frameMarker + 2),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedFrame))
        {
            frame = parsedFrame;
            gameName = stem[..frameMarker];
            int stampMarker = gameName.Length - ("-" + StampFormat).Length;
            if (stampMarker > 0
                && gameName[stampMarker] == '-'
                && DateTimeOffset.TryParseExact(
                    gameName.AsSpan(stampMarker + 1),
                    StampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsedStamp))
            {
                capturedAt = parsedStamp;
                gameName = gameName[..stampMarker];
            }
        }

        return new ScreenshotEntry(fullPath, fileName, gameName, capturedAt, frame, native);
    }

    /// <summary>Deletes a screenshot.</summary>
    /// <param name="path">Screenshot path.</param>
    /// <returns><see langword="true"/> when the file was deleted; <see langword="false"/> when it was absent.</returns>
    public static bool Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }
}
