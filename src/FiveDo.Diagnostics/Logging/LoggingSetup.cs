using System;
using System.IO;
using System.Runtime.InteropServices;
using FiveDo.Core;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Builds the shared Serilog pipeline used by the CLI and desktop hosts.
/// </summary>
/// <remarks>
/// The console sink writes to stderr so the CLI's machine-readable JSON on stdout is never
/// mixed with diagnostics. The file sink rolls daily and keeps a week of logs. The
/// bug-report sink receives warning-or-higher events only.
/// </remarks>
public static class LoggingSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Category}] {Message:lj}{NewLine}{Exception}";

    /// <summary>Creates a Serilog logger with the console, file, in-memory, and bug-report sinks.</summary>
    /// <param name="options">Host logging options.</param>
    /// <param name="inMemorySink">Receives the in-memory sink used by the log viewer.</param>
    /// <returns>The configured logger. The caller owns it and should flush it on shutdown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static Logger CreateLogger(LoggingOptions options, out InMemorySerilogSink inMemorySink)
    {
        ArgumentNullException.ThrowIfNull(options);
        inMemorySink = new InMemorySerilogSink(options.InMemoryCapacity);
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(options.MinimumLevel)
            .Enrich.WithProperty("Application", FiveDoVersion.ProductName)
            .Enrich.WithProperty("ApplicationVersion", FiveDoVersion.Version)
            .Enrich.WithProperty("Category", LogCategory.Host)
            .Enrich.With<EnvironmentEnricher>();

        if (options.EnableInMemory)
        {
            configuration.WriteTo.Sink(inMemorySink);
        }

        if (options.WriteToConsole)
        {
            configuration.WriteTo.Console(
                standardErrorFromLevel: LogEventLevel.Verbose,
                outputTemplate: OutputTemplate);
        }

        if (options.WriteToFile)
        {
            var directory = options.LogDirectory
                ?? Environment.GetEnvironmentVariable("FIVEDO_LOG_DIR")
                ?? LogPaths.GetLogDirectory();
            Directory.CreateDirectory(directory);
            configuration.WriteTo.File(
                path: Path.Combine(directory, "5do-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                outputTemplate: OutputTemplate);
        }

        if (options.EnableBugReports && !IsBugReportingDisabled())
        {
            configuration.WriteTo.Sink(new BugReportSink(new BugReportOptions
            {
                Endpoint = options.BugReportEndpoint ?? BugReportOptions.DefaultEndpoint,
                ApiKey = ResolveApiKey(options.BugReportApiKey),
                ApplicationMode = options.ApplicationMode,
            }));
        }

        return configuration.CreateLogger();
    }

    private static bool IsBugReportingDisabled()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_BUG_REPORTS"),
                "0",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Never submit reports from automated builds or test runs.
        return IsTruthy("CI") || IsTruthy("GITHUB_ACTIONS") || IsTruthy("TF_BUILD");
    }

    private static bool IsTruthy(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static string ResolveApiKey(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var environment = Environment.GetEnvironmentVariable("FIVEDO_BUGREPORT_API_KEY");
        return string.IsNullOrWhiteSpace(environment) ? BugReportOptions.DefaultApiKey : environment;
    }

    private sealed class EnvironmentEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            ArgumentNullException.ThrowIfNull(propertyFactory);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OS", EnvironmentDetails.PlatformName));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "Architecture",
                RuntimeInformation.ProcessArchitecture.ToString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessId", Environment.ProcessId));
        }
    }
}
