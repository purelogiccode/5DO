using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using FiveDo.Core;
using Serilog.Core;
using Serilog.Events;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Keeps the most recent Serilog events in memory for the in-app log viewer.
/// </summary>
/// <remarks>
/// Serilog fans out to this sink alongside the console, file, and bug-report sinks. The
/// viewer therefore shows exactly what the rest of the pipeline received.
/// </remarks>
public sealed class InMemorySerilogSink : ILogEventSink
{
    private readonly Lock _gate = new();
    private readonly List<LogEntry> _entries = new();
    private readonly int _capacity;

    /// <summary>Initializes a new instance of the <see cref="InMemorySerilogSink"/> class.</summary>
    /// <param name="capacity">Maximum number of retained entries. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is not positive.</exception>
    public InMemorySerilogSink(int capacity = 1000)
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
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var entry = new LogEntry(
            logEvent.Level,
            ResolveCategory(logEvent),
            logEvent.RenderMessage(CultureInfo.InvariantCulture),
            logEvent.Timestamp);

        lock (_gate)
        {
            if (_entries.Count == _capacity)
            {
                _entries.RemoveAt(0);
            }

            _entries.Add(entry);
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

    private static LogCategory ResolveCategory(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("Category", out LogEventPropertyValue? value))
        {
            return LogCategory.None;
        }

        return value switch
        {
            ScalarValue { Value: LogCategory category } => category,
            ScalarValue { Value: string text } when Enum.TryParse(text, ignoreCase: true, out LogCategory parsed) => parsed,
            _ => LogCategory.None,
        };
    }
}
