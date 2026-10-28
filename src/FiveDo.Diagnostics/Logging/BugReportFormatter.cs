using System;
using System.Globalization;
using System.Text;
using FiveDo.Core;
using Serilog.Events;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Formats a Serilog event into the payload required by the bug-report service.
/// </summary>
/// <remarks>
/// The service accepts five short fields. The required environment, error, and exception
/// blocks are folded into <c>message</c> and <c>stackTrace</c>; the service truncates
/// overlong fields, and this formatter truncates first so the important blocks survive.
/// </remarks>
public static class BugReportFormatter
{
    private const int MaxMessageLength = 4000;
    private const int MaxStackTraceLength = 8000;
    private const int MaxEnvironmentLength = 50;
    private const int MaxApplicationNameLength = 100;
    private const int MaxVersionLength = 20;

    /// <summary>Builds a bug-report payload from one log event.</summary>
    /// <param name="logEvent">The warning-or-higher event to report.</param>
    /// <param name="applicationMode">Host mode shown in the environment field (CLI, Desktop, Probe).</param>
    /// <returns>The payload ready for JSON serialization.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logEvent"/> is null.</exception>
    public static BugReportPayload Create(LogEvent logEvent, string applicationMode)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        var environment = Truncate(
            $"{EnvironmentDetails.PlatformName} / {applicationMode}",
            MaxEnvironmentLength);
        var message = Truncate(
            string.Join(
                Environment.NewLine,
                EnvironmentDetails.Format(),
                string.Empty,
                "=== Error Details ===",
                $"Level: {logEvent.Level}",
                FormatSource(logEvent),
                $"Message: {logEvent.RenderMessage(CultureInfo.InvariantCulture)}"),
            MaxMessageLength);

        return new BugReportPayload(
            Message: message,
            ApplicationName: Truncate(FiveDoVersion.ProductName, MaxApplicationNameLength),
            Version: Truncate(FiveDoVersion.Version, MaxVersionLength),
            UserInfo: null,
            Environment: environment,
            StackTrace: Truncate(FormatException(logEvent.Exception), MaxStackTraceLength));
    }

    /// <summary>Formats the exception details block required by the bug-report service.</summary>
    /// <param name="exception">Exception to describe, or <see langword="null"/>.</param>
    /// <returns>A multi-line exception description.</returns>
    public static string FormatException(Exception? exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== Exception Details ===");
        if (exception is null)
        {
            builder.Append("None");
            return builder.ToString();
        }

        AppendException(builder, exception, depth: 0);
        return builder.ToString().TrimEnd();
    }

    private static string FormatSource(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? value)
            || value is not ScalarValue { Value: string source })
        {
            return "Source: (unknown)";
        }

        return $"Source: {source}";
    }

    private static void AppendException(StringBuilder builder, Exception exception, int depth)
    {
        var prefix = depth == 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"Inner Exception {depth}: ");
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{prefix}Type: {exception.GetType().FullName}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{prefix}Message: {exception.Message}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{prefix}Source: {exception.Source ?? "(unknown)"}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{prefix}StackTrace:"));
        builder.AppendLine(exception.StackTrace ?? "None");

        if (exception.InnerException is not null)
        {
            builder.AppendLine();
            AppendException(builder, exception.InnerException, depth + 1);
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }
}
