using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThreeDo.Core;
using ThreeDo.Input;
using ThreeDo.Video;

namespace ThreeDo.Host;

/// <summary>
/// Persistent desktop settings: paths, presentation options, audio, and input bindings.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Gets or sets a value indicating whether the first-run guide has been shown.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Gets or sets the last BIOS path.</summary>
    public string BiosPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the last disc path.</summary>
    public string DiscPath { get; set; } = string.Empty;

    /// <summary>Gets recent disc paths, newest first.</summary>
    public List<string> RecentDiscs { get; set; } = [];

    /// <summary>Gets or sets the save-state directory.</summary>
    public string SaveDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the screenshot directory.</summary>
    public string ScreenshotDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the currently selected save slot.</summary>
    public int SaveSlot { get; set; }

    /// <summary>Gets or sets the number of save slots exposed by the shell.</summary>
    public int SaveSlotCount { get; set; } = 10;

    /// <summary>Gets folders scanned by the game library.</summary>
    public List<string> LibraryFolders { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether database fixes are applied automatically.</summary>
    public bool AutoGameFixes { get; set; } = true;

    /// <summary>Gets per-game fix-mode overrides keyed by 4DO game ID.</summary>
    public Dictionary<string, FixMode> FixModeOverrides { get; set; } = [];

    /// <summary>Gets per-game filter overrides keyed by 4DO game ID.</summary>
    public Dictionary<string, VideoFilter> FilterOverrides { get; set; } = [];

    /// <summary>Gets or sets the presentation filter.</summary>
    public VideoFilter Filter { get; set; } = VideoFilter.SharpBilinear;

    /// <summary>Gets or sets the aspect policy.</summary>
    public AspectMode Aspect { get; set; } = AspectMode.FourThree;

    /// <summary>Gets or sets a value indicating whether vsync is requested.</summary>
    public bool Vsync { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether integer scaling is used.</summary>
    public bool IntegerScale { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the OpenGL renderer is preferred.</summary>
    public bool PreferOpenGl { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the FPS/status overlay is shown over the video.</summary>
    public bool ShowOverlay { get; set; }

    /// <summary>Gets or sets a value indicating whether a screenshot also saves the native CPU frame.</summary>
    public bool SaveNativeScreenshot { get; set; }

    /// <summary>Gets or sets the CRT bloom strength (0-1).</summary>
    public float Bloom { get; set; } = 0.35f;

    /// <summary>Gets or sets a value indicating whether the opt-in 640x480 high-resolution mode is used.</summary>
    public bool HighResolution { get; set; }

    /// <summary>Gets or sets the audio volume (0-1).</summary>
    public float Volume { get; set; } = 1f;

    /// <summary>Gets or sets a value indicating whether audio output is enabled.</summary>
    public bool AudioEnabled { get; set; } = true;

    /// <summary>Gets or sets player input bindings.</summary>
    public InputProfile Input { get; set; } = InputProfile.CreateDefault();

    /// <summary>Gets the per-game fix-mode override, if one is configured.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <returns>The override, or <see langword="null"/> when the game uses automatic fixes.</returns>
    public FixMode? GetFixModeOverride(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var normalized = NormalizeGameId(gameId);
        foreach (var (id, mode) in FixModeOverrides)
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                return mode;
            }
        }

        return null;
    }

    /// <summary>Sets or removes a per-game fix-mode override.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <param name="mode">Fix-mode bits, or <see langword="null"/> to restore automatic fixes.</param>
    public void SetFixModeOverride(string gameId, FixMode? mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var normalized = NormalizeGameId(gameId);
        foreach (var id in FixModeOverrides.Keys.ToArray())
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                FixModeOverrides.Remove(id);
            }
        }

        if (mode is not null)
        {
            FixModeOverrides[normalized] = mode.Value;
        }
    }

    private static string NormalizeGameId(string gameId)
    {
        var normalized = gameId.Trim().ToUpperInvariant();
        return normalized.Length > 8 ? normalized[..8] : normalized;
    }

    /// <summary>Gets the per-game filter override, if one is configured.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <returns>The override, or <see langword="null"/> when the game uses the global filter.</returns>
    public VideoFilter? GetFilterOverride(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var normalized = NormalizeGameId(gameId);
        foreach (var (id, filter) in FilterOverrides)
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                return filter;
            }
        }

        return null;
    }

    /// <summary>Sets or removes a per-game filter override.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <param name="filter">Filter, or <see langword="null"/> to restore the global filter.</param>
    public void SetFilterOverride(string gameId, VideoFilter? filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var normalized = NormalizeGameId(gameId);
        foreach (var id in FilterOverrides.Keys.ToArray())
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                FilterOverrides.Remove(id);
            }
        }

        if (filter is not null)
        {
            FilterOverrides[normalized] = filter.Value;
        }
    }

    /// <summary>Resolves the presentation filter for a game, honoring per-game overrides.</summary>
    /// <param name="gameId">4DO game ID, or <see langword="null"/> when no disc is loaded.</param>
    /// <returns>The override when present, otherwise the global filter.</returns>
    public VideoFilter ResolveFilter(string? gameId) => GetFilterOverride(gameId) ?? Filter;

    /// <summary>Resolves the fix-mode bits for a game, honoring per-game overrides.</summary>
    /// <param name="gameId">4DO game ID, or <see langword="null"/> when no disc is loaded.</param>
    /// <returns>The override when present, the database mapping when automatic fixes are enabled, or none.</returns>
    public FixMode ResolveFixMode(string? gameId)
    {
        var overrideMode = GetFixModeOverride(gameId);
        return overrideMode ?? (AutoGameFixes ? GameCompatibility.ForGameId(gameId).FixMode : FixMode.None);
    }

    /// <summary>Gets the renderer settings derived from this configuration.</summary>
    /// <param name="gameId">Current disc game ID, or <see langword="null"/> to use the global filter.</param>
    /// <returns>A validated renderer settings record.</returns>
    public RendererSettings ToRendererSettings(string? gameId = null)
    {
        var settings = new RendererSettings
        {
            Filter = ResolveFilter(gameId),
            Aspect = Aspect,
            Vsync = Vsync,
            IntegerScale = IntegerScale,
            ShowOverlay = ShowOverlay,
            Bloom = Bloom,
        };
        settings.Validate();
        return settings;
    }
}

/// <summary>
/// Loads and saves <see cref="AppSettings"/> atomically as JSON.
/// </summary>
public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Gets the default settings directory under the user's local application data.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "3DOEmulator");

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
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(resolved), Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
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

        var temporary = resolved + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
        File.Move(temporary, resolved, overwrite: true);
    }
}
