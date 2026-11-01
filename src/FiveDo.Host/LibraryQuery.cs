using System;
using System.Collections.Generic;
using System.Globalization;

namespace FiveDo.Host;

/// <summary>Library collection filters exposed by the desktop shell.</summary>
public enum LibraryFilter
{
    /// <summary>Show every game.</summary>
    All,

    /// <summary>Show only favorites.</summary>
    Favorites,

    /// <summary>Show games that have been played.</summary>
    Played,

    /// <summary>Show games that have never been played.</summary>
    Unplayed,
}

/// <summary>Library sort orders exposed by the desktop shell.</summary>
public enum LibrarySort
{
    /// <summary>Sort by title, then path.</summary>
    Title,

    /// <summary>Sort by the most recent play time.</summary>
    RecentlyPlayed,

    /// <summary>Sort by accumulated play time.</summary>
    MostPlayed,

    /// <summary>Sort by release year, newest first.</summary>
    ReleaseYear,

    /// <summary>Sort by publisher, then title.</summary>
    Publisher,
}

/// <summary>Search, filter, and sort rules shared by the library windows and tests.</summary>
public static class LibraryQuery
{
    /// <summary>Returns whether an entry matches the search text and collection filter.</summary>
    /// <param name="entry">Library entry.</param>
    /// <param name="query">Search text; empty matches everything.</param>
    /// <param name="filter">Collection filter.</param>
    /// <returns><see langword="true"/> when the entry should be visible.</returns>
    public static bool Matches(GameLibraryEntry entry, string? query, LibraryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!MatchesFilter(entry, filter))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var trimmed = query.Trim();
        return Contains(entry.Name, trimmed)
               || Contains(entry.Path, trimmed)
               || Contains(entry.GameId, trimmed)
               || Contains(entry.Format, trimmed)
               || Contains(entry.ReleaseYear, trimmed)
               || Contains(entry.Publisher, trimmed)
               || Contains(entry.Regions, trimmed)
               || Contains(entry.Developer, trimmed)
               || Contains(entry.Genre, trimmed)
               || Contains(entry.Description, trimmed);
    }

    /// <summary>Compares two entries for the requested sort order.</summary>
    /// <param name="left">Left entry.</param>
    /// <param name="right">Right entry.</param>
    /// <param name="sort">Sort order.</param>
    /// <returns>A negative, zero, or positive value.</returns>
    public static int Compare(GameLibraryEntry left, GameLibraryEntry right, LibrarySort sort)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return sort switch
        {
            LibrarySort.RecentlyPlayed => CompareRecentlyPlayed(left, right) is var dateOrder and not 0
                ? dateOrder
                : CompareTitles(left, right),
            LibrarySort.MostPlayed => right.PlaytimeSeconds.CompareTo(left.PlaytimeSeconds) is var timeOrder and not 0
                ? timeOrder
                : CompareTitles(left, right),
            LibrarySort.ReleaseYear => CompareYears(right.ReleaseYear, left.ReleaseYear) is var yearOrder and not 0
                ? yearOrder
                : CompareTitles(left, right),
            LibrarySort.Publisher => CompareText(left.Publisher, right.Publisher) is var publisherOrder and not 0
                ? publisherOrder
                : CompareTitles(left, right),
            _ => CompareTitles(left, right),
        };
    }

    /// <summary>Filters and sorts a sequence of entries.</summary>
    /// <param name="entries">Entries to process.</param>
    /// <param name="query">Search text; empty matches everything.</param>
    /// <param name="filter">Collection filter.</param>
    /// <param name="sort">Sort order.</param>
    /// <returns>A new, sorted list.</returns>
    public static IReadOnlyList<GameLibraryEntry> Apply(
        IEnumerable<GameLibraryEntry> entries,
        string? query,
        LibraryFilter filter,
        LibrarySort sort)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var result = new List<GameLibraryEntry>();
        foreach (GameLibraryEntry entry in entries)
        {
            if (Matches(entry, query, filter))
            {
                result.Add(entry);
            }
        }

        result.Sort((left, right) => Compare(left, right, sort));
        return result;
    }

    private static bool MatchesFilter(GameLibraryEntry entry, LibraryFilter filter)
    {
        return filter switch
        {
            LibraryFilter.Favorites => entry.Favorite,
            LibraryFilter.Played => entry.PlaytimeSeconds > 0 || entry.LastPlayedUtc is not null,
            LibraryFilter.Unplayed => entry.PlaytimeSeconds <= 0 && entry.LastPlayedUtc is null,
            _ => true,
        };
    }

    private static bool Contains(string value, string query)
    {
        return value.Length > 0 && value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareTitles(GameLibraryEntry left, GameLibraryEntry right)
    {
        var title = CompareText(left.Name, right.Name);
        return title != 0 ? title : CompareText(left.Path, right.Path);
    }

    private static int CompareText(string left, string right)
    {
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareRecentlyPlayed(GameLibraryEntry left, GameLibraryEntry right)
    {
        if (left.LastPlayedUtc is null && right.LastPlayedUtc is null)
        {
            return 0;
        }

        if (left.LastPlayedUtc is null)
        {
            return 1;
        }

        return right.LastPlayedUtc is null
            ? -1
            : right.LastPlayedUtc.Value.CompareTo(left.LastPlayedUtc.Value);
    }

    private static int CompareYears(string left, string right)
    {
        var hasLeft = int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftYear);
        var hasRight = int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightYear);
        if (hasLeft && hasRight)
        {
            return leftYear.CompareTo(rightYear);
        }

        if (hasLeft)
        {
            return -1;
        }

        return hasRight ? 1 : 0;
    }
}
