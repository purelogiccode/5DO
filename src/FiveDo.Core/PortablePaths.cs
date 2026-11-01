using System;
using System.IO;

namespace FiveDo.Core;

/// <summary>
/// Resolves the directory that owns settings, NVRAM, logs, cheats, and library state.
/// </summary>
/// <remarks>
/// By default the directory is the per-user application-data folder. A
/// <c>portable.flag</c> file next to the executable (or <c>--portable</c>, which sets
/// <c>FIVEDO_PORTABLE=1</c>) keeps everything beside the executable instead. Setting
/// <c>FIVEDO_DATA_DIR</c> overrides both, enabling per-instance configurations.
/// </remarks>
public static class PortablePaths
{
    /// <summary>The flag file name that enables portable mode.</summary>
    public const string PortableFlagName = "portable.flag";

    /// <summary>Gets the directory containing the running executable.</summary>
    public static string ApplicationDirectory => AppContext.BaseDirectory;

    /// <summary>Gets the explicit data-directory override, if one is configured.</summary>
    public static string? DataDirectoryOverride
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("FIVEDO_DATA_DIR");
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>Gets a value indicating whether portable mode is active.</summary>
    public static bool IsPortable =>
        string.Equals(Environment.GetEnvironmentVariable("FIVEDO_PORTABLE"), "1", StringComparison.Ordinal)
        || File.Exists(Path.Combine(ApplicationDirectory, PortableFlagName));

    /// <summary>Gets the effective data directory.</summary>
    public static string DataDirectory =>
        ResolveDataDirectory(ApplicationDirectory, IsPortable, DataDirectoryOverride);

    /// <summary>Resolves a data directory from explicit inputs.</summary>
    /// <param name="applicationDirectory">Directory containing the executable.</param>
    /// <param name="portable">Whether portable mode is active.</param>
    /// <param name="overrideDirectory">Optional <c>FIVEDO_DATA_DIR</c> value.</param>
    /// <returns>The absolute data directory.</returns>
    public static string ResolveDataDirectory(
        string applicationDirectory,
        bool portable,
        string? overrideDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return Path.GetFullPath(overrideDirectory);
        }

        if (portable)
        {
            return Path.GetFullPath(applicationDirectory);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "5DO");
    }
}
