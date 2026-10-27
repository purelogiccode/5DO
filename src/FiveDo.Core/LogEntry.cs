using System;

namespace FiveDo.Core;

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
