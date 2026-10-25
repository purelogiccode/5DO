using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThreeDo.Input;
using ThreeDo.Video;

namespace ThreeDo.Host;

/// <summary>
/// Persistent desktop settings: paths, presentation options, audio, and input bindings.
/// </summary>
public sealed class AppSettings
{
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

    /// <summary>Gets or sets a value indicating whether the opt-in 640x480 high-resolution mode is used.</summary>
    public bool HighResolution { get; set; }

    /// <summary>Gets or sets the audio volume (0-1).</summary>
    public float Volume { get; set; } = 1f;

    /// <summary>Gets or sets a value indicating whether audio output is enabled.</summary>
    public bool AudioEnabled { get; set; } = true;

    /// <summary>Gets or sets player input bindings.</summary>
    public InputProfile Input { get; set; } = InputProfile.CreateDefault();

    /// <summary>Gets the renderer settings derived from this configuration.</summary>
    /// <returns>A validated renderer settings record.</returns>
    public RendererSettings ToRendererSettings()
    {
        var settings = new RendererSettings
        {
            Filter = Filter,
            Aspect = Aspect,
            Vsync = Vsync,
            IntegerScale = IntegerScale,
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
