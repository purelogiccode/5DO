using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using FiveDo.Core;
using FiveDo.Host.Osd;
using FiveDo.Input;

namespace FiveDo.Host;

/// <summary>Describes the on-disk state of a settings file.</summary>
/// <param name="Exists">Whether the file exists.</param>
/// <param name="Valid">Whether the file is valid JSON with a readable schema version.</param>
/// <param name="Version">The schema version in the file, or 0 when missing or invalid.</param>
/// <param name="Error">The parse error when <paramref name="Valid"/> is false.</param>
public sealed record SettingsFileInfo(bool Exists, bool Valid, int Version, string? Error);

/// <summary>
/// Loads and saves <see cref="AppSettings"/> atomically as JSON.
/// </summary>
public static class AppSettingsStore
{
    /// <summary>Gets the default settings directory under the user's local application data.</summary>
    /// <remarks>
    /// Portable mode and <c>FIVEDO_DATA_DIR</c> are resolved by
    /// <see cref="PortablePaths.DataDirectory"/>.
    /// </remarks>
    public static string DefaultDirectory => PortablePaths.DataDirectory;

    /// <summary>Gets the default settings file path.</summary>
    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    /// <summary>Describes a settings file without applying migrations.</summary>
    /// <param name="path">Settings path. Defaults to <see cref="DefaultPath"/>.</param>
    /// <returns>Existence, JSON validity, schema version, and the parse error when invalid.</returns>
    public static SettingsFileInfo Inspect(string? path = null)
    {
        var resolved = path ?? DefaultPath;
        if (!File.Exists(resolved))
        {
            return new SettingsFileInfo(false, false, 0, "Settings file not found.");
        }

        try
        {
            var json = File.ReadAllText(resolved);
            AppSettings settings = JsonSerializer.Deserialize(
                json,
                AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            return new SettingsFileInfo(true, true, settings.SettingsVersion, null);
        }
        catch (JsonException exception)
        {
            return new SettingsFileInfo(true, false, 0, exception.Message);
        }
    }

    /// <summary>Loads, normalizes, and rewrites a settings file at the current schema version.</summary>
    /// <param name="path">Settings path. Defaults to <see cref="DefaultPath"/>.</param>
    /// <returns>The schema version read before migration.</returns>
    /// <exception cref="FileNotFoundException">The settings file does not exist.</exception>
    /// <exception cref="InvalidDataException">The settings file is not valid JSON.</exception>
    public static int Migrate(string? path = null)
    {
        var resolved = path ?? DefaultPath;
        SettingsFileInfo info = Inspect(resolved);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Settings file not found.", resolved);
        }

        if (!info.Valid)
        {
            throw new InvalidDataException($"Settings file is not valid JSON: {info.Error}");
        }

        AppSettings settings = Load(resolved);
        Save(settings, resolved);
        return info.Version;
    }

    /// <summary>Loads settings, creating defaults when the file does not exist or is invalid.</summary>
    /// <param name="path">Settings path. Defaults to <see cref="DefaultPath"/>.</param>
    /// <returns>The loaded or default settings.</returns>
    public static AppSettings Load(string? path = null)
    {
        var resolved = path ?? DefaultPath;
        if (!File.Exists(resolved))
        {
            return new AppSettings { SettingsVersion = AppSettings.CurrentVersion };
        }

        try
        {
            var json = File.ReadAllText(resolved);
            AppSettings settings = JsonSerializer.Deserialize(
                json,
                AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            if (settings.SettingsVersion > AppSettings.CurrentVersion)
            {
                Serilog.Log.Warning(
                    "Settings file {Path} uses schema version {Version}; this build supports version {Supported}.",
                    resolved,
                    settings.SettingsVersion,
                    AppSettings.CurrentVersion);
            }

            ApplyLegacyRendererPreference(json, settings);
            // JSON can deserialize null despite the non-nullable annotation.
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            settings.Input ??= InputProfile.CreateDefault();
            settings.Input.AddMissingDefaultHotkeys();
            // JSON can deserialize null despite the non-nullable annotation.
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            settings.BiosOverrides ??= new Dictionary<string, string>(StringComparer.Ordinal);
            if (!double.IsFinite(settings.CpuClockMultiplier)
                || settings.CpuClockMultiplier < 0.5
                || settings.CpuClockMultiplier > 4.0)
            {
                settings.CpuClockMultiplier = 1.0;
            }

            if (!float.IsFinite(settings.CdAudioVolume) || settings.CdAudioVolume is < 0 or > 1)
            {
                settings.CdAudioVolume = 0.8f;
            }

            // JSON can deserialize null despite the non-nullable annotation.
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            settings.GameProfiles ??= new Dictionary<string, GameProfile>(StringComparer.Ordinal);
            foreach ((var id, GameProfile profile) in settings.GameProfiles.ToArray())
            {
                if (string.IsNullOrWhiteSpace(id) || profile is null)
                {
                    settings.GameProfiles.Remove(id);
                    continue;
                }

                if (!Enum.IsDefined(profile.Profile))
                {
                    profile.Profile = EmulationProfile.Custom;
                }

                if (!Enum.IsDefined(profile.ArmTiming))
                {
                    profile.ArmTiming = ArmTimingMode.FreeDoCompatible;
                }

                if (!Enum.IsDefined(profile.ClioTimers))
                {
                    profile.ClioTimers = ClioTimerMode.FreeDoCompatible;
                }

                if (!Enum.IsDefined(profile.CelTiming))
                {
                    profile.CelTiming = CelTimingMode.FreeDoCompatible;
                }

                if (!Enum.IsDefined(profile.CdromTiming))
                {
                    profile.CdromTiming = CdromTimingMode.FreeDoCompatible;
                }

                if (!Enum.IsDefined(profile.CdSpeed))
                {
                    profile.CdSpeed = CdSpeed.X2;
                }

                if (!double.IsFinite(profile.CpuClockMultiplier)
                    || profile.CpuClockMultiplier is < 0.5 or > 4.0)
                {
                    profile.CpuClockMultiplier = 1.0;
                }

                if (!float.IsFinite(profile.CdAudioVolume) || profile.CdAudioVolume is < 0 or > 1)
                {
                    profile.CdAudioVolume = 0.8f;
                }
            }

            if (!Enum.IsDefined(settings.Renderer))
            {
                settings.Renderer = VideoRendererKind.OpenGl;
            }

            if (!Enum.IsDefined(settings.Profile))
            {
                settings.Profile = EmulationProfile.Compatible;
            }

            if (!Enum.IsDefined(settings.ArmTiming))
            {
                settings.ArmTiming = ArmTimingMode.FreeDoCompatible;
            }

            if (!Enum.IsDefined(settings.ClioTimers))
            {
                settings.ClioTimers = ClioTimerMode.FreeDoCompatible;
            }

            if (!Enum.IsDefined(settings.CelTiming))
            {
                settings.CelTiming = CelTimingMode.FreeDoCompatible;
            }

            if (!Enum.IsDefined(settings.CdromTiming))
            {
                settings.CdromTiming = CdromTimingMode.FreeDoCompatible;
            }

            if (!Enum.IsDefined(settings.CdSpeed))
            {
                settings.CdSpeed = CdSpeed.X2;
            }

            if (!Enum.IsDefined(settings.NvramStorageMode))
            {
                settings.NvramStorageMode = NvramStorageMode.Global;
            }

            if (settings.NvramVersion is < NvramLayout.MinVersion or > NvramLayout.MaxVersion)
            {
                settings.NvramVersion = NvramLayout.MinVersion;
            }

            if (!Enum.IsDefined(settings.OsdPosition))
            {
                settings.OsdPosition = OsdPosition.TopRight;
            }

            if (!double.IsFinite(settings.OsdOpacity) || settings.OsdOpacity is < 0.1 or > 1.0)
            {
                settings.OsdOpacity = 0.9;
            }

            if (settings.OsdDurationMilliseconds is < OsdOptions.MinDurationMilliseconds
                or > OsdOptions.MaxDurationMilliseconds)
            {
                settings.OsdDurationMilliseconds = 2200;
            }

            // The profile is a convenience label over the individual options. Keep an explicit
            // Custom selection, but correct a stale named profile so hand-edited or older
            // settings cannot report a profile they do not match.
            EmulationProfile detectedProfile = settings.DetectProfile();
            if (settings.Profile != EmulationProfile.Custom && detectedProfile != settings.Profile)
            {
                settings.Profile = detectedProfile;
            }

            settings.SettingsVersion = AppSettings.CurrentVersion;
            return settings;
        }
        catch (JsonException exception)
        {
            TryBackupInvalidFile(resolved);
            Serilog.Log.Warning(exception, "Settings file {Path} is invalid; using defaults.", resolved);
            return new AppSettings { SettingsVersion = AppSettings.CurrentVersion };
        }
    }

    private static void TryBackupInvalidFile(string path)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture);
            File.Copy(path, $"{path}.invalid-{timestamp}.bak", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Warning(exception, "Could not back up the invalid settings file {Path}.", path);
        }
    }

    private static void ApplyLegacyRendererPreference(string json, AppSettings settings)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && !root.TryGetProperty(nameof(AppSettings.Renderer), out _)
            && root.TryGetProperty("PreferOpenGl", out JsonElement preferOpenGl)
            && preferOpenGl.ValueKind == JsonValueKind.False)
        {
            settings.Renderer = VideoRendererKind.Software;
        }
    }

    /// <summary>Saves settings atomically.</summary>
    /// <param name="settings">Settings to save.</param>
    /// <param name="path">Settings path. Defaults to <see cref="DefaultPath"/>.</param>
    public static void Save(AppSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var resolved = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(Path.GetFullPath(resolved));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        settings.SettingsVersion = AppSettings.CurrentVersion;
        var temporary = resolved + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings));
        File.Move(temporary, resolved, overwrite: true);
    }
}
