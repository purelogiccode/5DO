using System;
using System.IO;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Resolves the per-user directory used by the rolling Serilog file sink.
/// </summary>
public static class LogPaths
{
    /// <summary>Gets the platform-appropriate log directory.</summary>
    /// <param name="applicationName">Product folder name.</param>
    /// <returns>The log directory path. It is not created.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="applicationName"/> is empty.</exception>
    public static string GetLogDirectory(string applicationName = "5DO")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                applicationName,
                "logs");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Logs",
                applicationName);
        }

        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (!string.IsNullOrWhiteSpace(stateHome))
        {
            return Path.Combine(stateHome, applicationName, "logs");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "state",
            applicationName,
            "logs");
    }

    /// <summary>Gets the rolling log file path.</summary>
    /// <param name="applicationName">Product folder name.</param>
    /// <returns>The log file path.</returns>
    public static string GetLogFilePath(string applicationName = "5DO")
    {
        return Path.Combine(GetLogDirectory(applicationName), "5do-.log");
    }
}
