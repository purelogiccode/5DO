using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using FiveDo.Core;
using FiveDo.Host.Osd;
using FiveDo.Input;

namespace FiveDo.Host;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> atomically as JSON.
/// </summary>
public static class AppSettingsStore
{
    /// <summary>Gets the default settings directory under the user's local application data.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "5DO");

    /// <summary>Gets the default settings file path.</summary>
    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

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
