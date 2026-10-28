using System;
using FiveDo.Core;
using Serilog.Events;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// One captured diagnostic message shown by the in-app log viewer.
/// </summary>
/// <param name="Level">Serilog severity.</param>
/// <param name="Category">Emulator subsystem category.</param>
/// <param name="Message">Rendered message.</param>
/// <param name="TimestampUtc">Capture time in UTC, or <see langword="default"/> when unavailable.</param>
public readonly record struct LogEntry(
    LogEventLevel Level,
    LogCategory Category,
    string Message,
    DateTimeOffset TimestampUtc = default);
