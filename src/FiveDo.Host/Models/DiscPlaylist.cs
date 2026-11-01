using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
    public string SourcePath { get; private set; }

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

    /// <summary>Creates an editable playlist from existing entries.</summary>
    /// <param name="entries">Entries to copy. Paths are converted to absolute paths.</param>
    /// <param name="sourcePath">Optional path used by <see cref="Save"/>.</param>
    /// <returns>A playlist with the first entry selected.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
    public static DiscPlaylist Create(
        IEnumerable<DiscPlaylistEntry> entries,
        string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var copy = new List<DiscPlaylistEntry>();
        foreach (DiscPlaylistEntry entry in entries)
        {
            copy.Add(new DiscPlaylistEntry(Path.GetFullPath(entry.Path), entry.Title));
        }

        return new DiscPlaylist(
            string.IsNullOrWhiteSpace(sourcePath) ? string.Empty : Path.GetFullPath(sourcePath),
            copy);
    }

    /// <summary>Adds a disc to the end of the playlist.</summary>
    /// <param name="path">Disc image or cue path.</param>
    /// <param name="title">Optional display title.</param>
    /// <returns>The added entry.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    public DiscPlaylistEntry Add(string path, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var entry = new DiscPlaylistEntry(Path.GetFullPath(path), title);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Removes an entry and keeps a valid selection.</summary>
    /// <param name="index">Zero-based index.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the playlist.</exception>
    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _entries.Count);
        _entries.RemoveAt(index);
        if (_entries.Count == 0)
        {
            CurrentIndex = 0;
            return;
        }

        if (index < CurrentIndex)
        {
            CurrentIndex--;
        }
        else if (CurrentIndex >= _entries.Count)
        {
            CurrentIndex = _entries.Count - 1;
        }
    }

    /// <summary>Moves an entry one position toward the start.</summary>
    /// <param name="index">Zero-based index.</param>
    /// <returns><see langword="true"/> when the entry moved.</returns>
    public bool MoveEntryUp(int index)
    {
        if (index <= 0 || index >= _entries.Count)
        {
            return false;
        }

        DiscPlaylistEntry entry = _entries[index];
        _entries.RemoveAt(index);
        _entries.Insert(index - 1, entry);
        if (CurrentIndex == index)
        {
            CurrentIndex--;
        }
        else if (CurrentIndex == index - 1)
        {
            CurrentIndex++;
        }

        return true;
    }

    /// <summary>Moves an entry one position toward the end.</summary>
    /// <param name="index">Zero-based index.</param>
    /// <returns><see langword="true"/> when the entry moved.</returns>
    public bool MoveEntryDown(int index)
    {
        if (index < 0 || index >= _entries.Count - 1)
        {
            return false;
        }

        DiscPlaylistEntry entry = _entries[index];
        _entries.RemoveAt(index);
        _entries.Insert(index + 1, entry);
        if (CurrentIndex == index)
        {
            CurrentIndex++;
        }
        else if (CurrentIndex == index + 1)
        {
            CurrentIndex--;
        }

        return true;
    }

    /// <summary>Writes the playlist as an extended M3U file.</summary>
    /// <param name="path">
    /// Destination path. Defaults to <see cref="SourcePath"/> when the playlist was loaded
    /// from disk.
    /// </param>
    /// <returns>The absolute path written.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the playlist is empty or no destination path is available.
    /// </exception>
    public string Save(string? path = null)
    {
        var target = string.IsNullOrWhiteSpace(path) ? SourcePath : path;
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("No playlist path is set.");
        }

        if (_entries.Count == 0)
        {
            throw new InvalidOperationException("Cannot save an empty playlist.");
        }

        var fullPath = Path.GetFullPath(target);
        var directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var lines = new List<string> { "#EXTM3U" };
        foreach (DiscPlaylistEntry entry in _entries)
        {
            lines.Add($"#EXTINF:-1,{entry.DisplayName}");
            lines.Add(FormatEntryPath(directory, entry.Path));
        }

        File.WriteAllLines(fullPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        SourcePath = fullPath;
        return fullPath;
    }

    private static string FormatEntryPath(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            ? path
            : relative;
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
    /// <exception cref="InvalidOperationException">The playlist is empty.</exception>
    public DiscPlaylistEntry MoveNext()
    {
        if (_entries.Count == 0)
        {
            throw new InvalidOperationException("The playlist is empty.");
        }

        CurrentIndex = (CurrentIndex + 1) % _entries.Count;
        return Current;
    }

    /// <summary>Selects the previous entry, wrapping to the last at the start.</summary>
    /// <returns>The newly selected entry.</returns>
    /// <exception cref="InvalidOperationException">The playlist is empty.</exception>
    public DiscPlaylistEntry MovePrevious()
    {
        if (_entries.Count == 0)
        {
            throw new InvalidOperationException("The playlist is empty.");
        }

        CurrentIndex = (CurrentIndex + _entries.Count - 1) % _entries.Count;
        return Current;
    }

    /// <summary>Formats the one-based position for status text.</summary>
    public string FormatPosition() => $"disc {CurrentIndex + 1}/{_entries.Count}";
}
