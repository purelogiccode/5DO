using System;
using System.IO;
using System.Linq;

namespace FiveDo.Core;

/// <summary>
/// Resolves the directory that owns settings, NVRAM, logs, cheats, and library state.
/// </summary>
/// <remarks>
/// By default the directory is the per-user application-data folder. A
/// <c>portable.flag</c> file next to the executable (or <c>--portable</c>, which sets
/// <c>FIVEDO_PORTABLE=1</c>) keeps everything beside the executable instead. Setting
/// <c>FIVEDO_DATA_DIR</c> overrides both, and <c>FIVEDO_PROFILE</c> selects a named
/// profile directory under <c>profiles/</c>.
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

    /// <summary>Gets the normalized named configuration profile, if one is configured.</summary>
    public static string? ProfileName
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("FIVEDO_PROFILE");
            return string.IsNullOrWhiteSpace(value) ? null : NormalizeProfileName(value);
        }
    }

    /// <summary>Gets a value indicating whether portable mode is active.</summary>
    public static bool IsPortable =>
        string.Equals(Environment.GetEnvironmentVariable("FIVEDO_PORTABLE"), "1", StringComparison.Ordinal)
        || File.Exists(Path.Combine(ApplicationDirectory, PortableFlagName));

    /// <summary>
    /// Gets a value indicating whether a data-directory override or named profile is active.
    /// </summary>
    public static bool IsCustomDataDirectory => DataDirectoryOverride is not null || ProfileName is not null;

    /// <summary>Gets the data directory without applying a named profile.</summary>
    public static string BaseDataDirectory =>
        ResolveDataDirectory(ApplicationDirectory, IsPortable, DataDirectoryOverride);

    /// <summary>Gets the directory that holds named configuration profiles.</summary>
    public static string ProfilesDirectory => Path.Combine(BaseDataDirectory, "profiles");

    /// <summary>Gets the effective data directory.</summary>
    public static string DataDirectory =>
        ResolveDataDirectory(ApplicationDirectory, IsPortable, DataDirectoryOverride, ProfileName);

    /// <summary>Resolves a data directory from explicit inputs.</summary>
    /// <param name="applicationDirectory">Directory containing the executable.</param>
    /// <param name="portable">Whether portable mode is active.</param>
    /// <param name="overrideDirectory">Optional <c>FIVEDO_DATA_DIR</c> value.</param>
    /// <param name="profileName">Optional named configuration profile.</param>
    /// <returns>The absolute data directory.</returns>
    public static string ResolveDataDirectory(
        string applicationDirectory,
        bool portable,
        string? overrideDirectory,
        string? profileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        var baseDirectory = ResolveBaseDataDirectory(applicationDirectory, portable, overrideDirectory);
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return baseDirectory;
        }

        return Path.Combine(baseDirectory, "profiles", NormalizeProfileName(profileName));
    }

    /// <summary>Normalizes a profile name into a safe directory name.</summary>
    /// <param name="name">Raw profile name.</param>
    /// <returns>A non-empty name containing only letters, digits, dash, underscore, and dot.</returns>
    /// <exception cref="ArgumentException">Thrown when the name has no usable characters.</exception>
    public static string NormalizeProfileName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = new string(
            name.Trim()
                .Select(character =>
                    char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                        ? character
                        : '_')
                .ToArray());
        if (normalized.Length == 0 || normalized is "." or "..")
        {
            throw new ArgumentException("The profile name has no usable characters.", nameof(name));
        }

        return normalized;
    }

    private static string ResolveBaseDataDirectory(
        string applicationDirectory,
        bool portable,
        string? overrideDirectory)
    {
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
