using Serilog.Events;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Host-level Serilog configuration.
/// </summary>
public sealed record LoggingOptions
{
    /// <summary>Gets the host mode shown in bug reports (CLI, Desktop, Probe).</summary>
    public string ApplicationMode { get; init; } = "Desktop";

    /// <summary>Gets the log directory. Defaults to the per-user platform log location.</summary>
    public string? LogDirectory { get; init; }

    /// <summary>Gets a value indicating whether events are written to the console (stderr).</summary>
    public bool WriteToConsole { get; init; } = true;

    /// <summary>Gets a value indicating whether events are written to a rolling file.</summary>
    public bool WriteToFile { get; init; } = true;

    /// <summary>Gets a value indicating whether events are retained for the in-app viewer.</summary>
    public bool EnableInMemory { get; init; } = true;

    /// <summary>Gets the in-memory entry capacity.</summary>
    public int InMemoryCapacity { get; init; } = 4000;

    /// <summary>Gets a value indicating whether warning-or-higher events are sent to the bug-report API.</summary>
    public bool EnableBugReports { get; init; } = true;

    /// <summary>Gets an optional bug-report API key override.</summary>
    public string? BugReportApiKey { get; init; }

    /// <summary>Gets an optional bug-report endpoint override.</summary>
    public string? BugReportEndpoint { get; init; }

    /// <summary>Gets the minimum level written to sinks.</summary>
    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Debug;
}
