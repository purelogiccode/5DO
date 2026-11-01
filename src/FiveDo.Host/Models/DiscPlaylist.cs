using System;
using System.Collections.Generic;
using System.IO;

namespace FiveDo.Host;

/// <summary>One entry in a multi-disc playlist.</summary>
/// <param name="Path">Absolute or playlist-relative disc image path.</param>
/// <param name="Title">Optional display title from an <c>#EXTINF</c> line.</param>
public sealed record DiscPlaylistEntry(string Path, string? Title)
{
    /// <summary>Gets the title to show in the UI, falling back to the file name.</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Title)
            ? System.IO.Path.GetFileNameWithoutExtension(Path)
            : Title;
}

/// <summary>
/// A multi-disc playlist loaded from an M3U/M3U8 file.
/// </summary>
/// <remarks>
/// Blank lines and comments are ignored. A standard <c>#EXTINF:duration,title</c>
/// line applies its title to the next entry. Relative paths resolve against the
/// playlist directory. Paths are not opened until an entry is selected, so a playlist
/// may reference discs on removable media.
/// </remarks>
public sealed class DiscPlaylist
{
    private readonly List<DiscPlaylistEntry> _entries;

    private DiscPlaylist(string sourcePath, List<DiscPlaylistEntry> entries)
    {
        SourcePath = sourcePath;
        _entries = entries;
    }

    /// <summary>Gets the playlist file path.</summary>
    public string SourcePath { get; }

    /// <summary>Gets the entries in playlist order.</summary>
    public IReadOnlyList<DiscPlaylistEntry> Entries => _entries;

    /// <summary>Gets the number of entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Gets or sets the zero-based selected entry.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>Gets the selected entry.</summary>
    /// <exception cref="InvalidOperationException">The playlist is empty.</exception>
    public DiscPlaylistEntry Current =>
        _entries.Count == 0 ? throw new InvalidOperationException("The playlist is empty.") : _entries[CurrentIndex];

    /// <summary>Loads a playlist from disk.</summary>
    /// <param name="path">M3U or M3U8 path.</param>
    /// <returns>The loaded playlist with the first entry selected.</returns>
    /// <exception cref="InvalidDataException">The file has no entries.</exception>
    public static DiscPlaylist Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        var entries = new List<DiscPlaylistEntry>();
        string? pendingTitle = null;
        foreach (var rawLine in File.ReadLines(fullPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = line.IndexOf(',');
                pendingTitle = comma >= 0 && comma + 1 < line.Length
                    ? line[(comma + 1)..].Trim()
                    : null;
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            var resolved = Path.IsPathRooted(line)
                ? Path.GetFullPath(line)
                : Path.GetFullPath(Path.Combine(directory, line));
            entries.Add(new DiscPlaylistEntry(resolved, pendingTitle));
            pendingTitle = null;
        }

        if (entries.Count == 0)
        {
            throw new InvalidDataException($"Playlist '{path}' has no disc entries.");
        }

        return new DiscPlaylist(fullPath, entries);
    }

    /// <summary>Selects an entry by index.</summary>
    /// <param name="index">Zero-based index.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the playlist.</exception>
    public void Select(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _entries.Count);
        CurrentIndex = index;
    }

    /// <summary>Selects the next entry, wrapping to the first at the end.</summary>
    /// <returns>The newly selected entry.</returns>
    public DiscPlaylistEntry MoveNext()
    {
        CurrentIndex = (CurrentIndex + 1) % _entries.Count;
        return Current;
    }

    /// <summary>Selects the previous entry, wrapping to the last at the start.</summary>
    /// <returns>The newly selected entry.</returns>
    public DiscPlaylistEntry MovePrevious()
    {
        CurrentIndex = (CurrentIndex + _entries.Count - 1) % _entries.Count;
        return Current;
    }

    /// <summary>Formats the one-based position for status text.</summary>
    public string FormatPosition() => $"disc {CurrentIndex + 1}/{_entries.Count}";
}
