using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Collects the environment block attached to every bug report.
/// </summary>
public static class EnvironmentDetails
{
    /// <summary>Gets the current platform name used by the report fields.</summary>
    public static string PlatformName
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "Windows";
            }

            if (OperatingSystem.IsLinux())
            {
                return "Linux";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "MacOSX";
            }

            return "Unknown";
        }
    }

    /// <summary>Formats the environment details block required by the bug-report service.</summary>
    /// <returns>A multi-line, human-readable environment description.</returns>
    public static string Format()
    {
        var platform = PlatformName;
        return string.Join(
            Environment.NewLine,
            "=== Environment Details ===",
            $"Date: {DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)}",
            $"Application Name: {FiveDo.Core.FiveDoVersion.ProductName}",
            $"Application Version: {FiveDo.Core.FiveDoVersion.Version}",
            $"OS Version: {RuntimeInformation.OSDescription}",
            $"Architecture: {RuntimeInformation.ProcessArchitecture}",
            $"Bitness: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}",
            $"{platform} Version: {Environment.OSVersion.VersionString}",
            $"Processor Count: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}",
            $"Base Directory: {AppContext.BaseDirectory}",
            $"Temp Path: {Path.GetTempPath()}");
    }
}
