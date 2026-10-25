using System;
using System.Collections.Generic;

namespace ThreeDo.Core;

/// <summary>
/// A client-side filter for captured <see cref="LogEntry"/> records. Used by the in-app
/// log viewer and available to host tooling.
/// </summary>
public sealed record LogViewFilter
{
    /// <summary>Gets the minimum severity to show.</summary>
    public LogLevel MinimumLevel { get; init; } = LogLevel.Info;

    /// <summary>Gets the category mask to show. <see cref="LogCategory.All"/> shows every category.</summary>
    public LogCategory Category { get; init; } = LogCategory.All;

    /// <summary>Gets an optional case-insensitive substring to match in the message.</summary>
    public string Search { get; init; } = string.Empty;

    /// <summary>Tests whether one entry passes the filter.</summary>
    /// <param name="entry">Entry to test.</param>
    /// <returns><see langword="true"/> when the entry should be displayed.</returns>
    public bool Matches(in LogEntry entry)
    {
        if (entry.Level < MinimumLevel)
        {
            return false;
        }

        if (Category != LogCategory.All && (entry.Category & Category) == LogCategory.None)
        {
            return false;
        }

        return string.IsNullOrEmpty(Search)
            || entry.Message.Contains(Search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Filters a snapshot of entries, preserving their order.</summary>
    /// <param name="entries">Entries to filter.</param>
    /// <returns>The matching entries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
    public IReadOnlyList<LogEntry> Apply(IReadOnlyList<LogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var result = new List<LogEntry>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (Matches(entry))
            {
                result.Add(entry);
            }
        }

        return result;
    }
}
