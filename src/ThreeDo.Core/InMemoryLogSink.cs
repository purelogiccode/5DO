using System;
using System.Collections.Generic;

namespace ThreeDo.Core;

/// <summary>
/// One captured diagnostic message.
/// </summary>
/// <param name="Level">Severity.</param>
/// <param name="Category">Category.</param>
/// <param name="Message">Formatted message.</param>
/// <param name="TimestampUtc">Capture time in UTC, or <see langword="default"/> when unavailable.</param>
public readonly record struct LogEntry(
    LogLevel Level,
    LogCategory Category,
    string Message,
    DateTimeOffset TimestampUtc = default);

/// <summary>
/// Keeps the most recent diagnostic messages in memory for tests and the in-app log viewer.
/// </summary>
public sealed class InMemoryLogSink : ILogSink
{
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = new();
    private readonly int _capacity;

    /// <summary>Initializes a new instance of the <see cref="InMemoryLogSink"/> class.</summary>
    /// <param name="capacity">Maximum number of retained entries. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is not positive.</exception>
    public InMemoryLogSink(int capacity = 1000)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The log capacity must be positive.");
        }

        _capacity = capacity;
    }

    /// <summary>Gets a snapshot of the retained entries, oldest first.</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public void Write(LogLevel level, LogCategory category, string message)
    {
        lock (_gate)
        {
            if (_entries.Count == _capacity)
            {
                _entries.RemoveAt(0);
            }

            _entries.Add(new LogEntry(level, category, message, DateTimeOffset.UtcNow));
        }
    }

    /// <summary>Removes all retained entries.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
