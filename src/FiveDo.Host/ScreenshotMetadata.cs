using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Host;

/// <summary>
/// Optional sidecar metadata written beside a screenshot so a capture records the
/// settings it was taken with.
/// </summary>
public sealed class ScreenshotMetadata
{
    /// <summary>Gets the current metadata schema version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the schema version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets the game title.</summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>Gets or sets the 4DO game ID, or an empty string when unknown.</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>Gets or sets the emulated frame number, or -1 when unavailable.</summary>
    public long Frame { get; set; } = -1;

    /// <summary>Gets or sets the UTC capture time.</summary>
    public DateTimeOffset CapturedAtUtc { get; set; }

    /// <summary>Gets or sets the presentation renderer.</summary>
    public string Renderer { get; set; } = string.Empty;

    /// <summary>Gets or sets the video filter.</summary>
    public string Filter { get; set; } = string.Empty;

    /// <summary>Gets or sets the aspect mode.</summary>
    public string Aspect { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether high-resolution CEL mode was active.</summary>
    public bool HighResolution { get; set; }

    /// <summary>Gets or sets the active 480i/240p field handling mode.</summary>
    public string Deinterlace { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the active frame was woven from interlaced fields.</summary>
    public bool Interlaced { get; set; }

    /// <summary>Gets or sets the video region.</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>Gets or sets the left overscan crop.</summary>
    public int OverscanLeft { get; set; }

    /// <summary>Gets or sets the top overscan crop.</summary>
    public int OverscanTop { get; set; }

    /// <summary>Gets or sets the right overscan crop.</summary>
    public int OverscanRight { get; set; }

    /// <summary>Gets or sets the bottom overscan crop.</summary>
    public int OverscanBottom { get; set; }

    /// <summary>Gets or sets the void border width.</summary>
    public int VideoBorderPixels { get; set; }

    /// <summary>Gets or sets the void-area color.</summary>
    public string VoidAreaColor { get; set; } = string.Empty;

    /// <summary>Gets or sets the timing profile label.</summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>Gets or sets the ARM timing mode.</summary>
    public string ArmTiming { get; set; } = string.Empty;

    /// <summary>Gets or sets the CLIO timer mode.</summary>
    public string ClioTimers { get; set; } = string.Empty;

    /// <summary>Gets or sets the CEL timing mode.</summary>
    public string CelTiming { get; set; } = string.Empty;

    /// <summary>Gets or sets the CD-ROM timing mode.</summary>
    public string CdromTiming { get; set; } = string.Empty;

    /// <summary>Gets or sets the CD-ROM speed.</summary>
    public string CdSpeed { get; set; } = string.Empty;

    /// <summary>Gets or sets the DRAM/VRAM capacity code.</summary>
    public string Memory { get; set; } = string.Empty;

    /// <summary>Gets or sets the CPU clock multiplier.</summary>
    public double CpuClockMultiplier { get; set; } = 1.0;

    /// <summary>Captures the current settings.</summary>
    /// <param name="settings">Source settings.</param>
    /// <param name="gameName">Game title.</param>
    /// <param name="gameId">4DO game ID, or an empty string.</param>
    /// <param name="frame">Emulated frame number.</param>
    /// <param name="activeDeinterlace">
    /// Mode used by the machine for the captured frame, or <see langword="null"/> to use the
    /// configured setting.
    /// </param>
    /// <param name="interlaced">Whether the captured frame was woven from interlaced fields.</param>
    /// <returns>A metadata snapshot.</returns>
    public static ScreenshotMetadata Capture(
        AppSettings settings,
        string gameName,
        string gameId,
        long frame,
        DeinterlaceMode? activeDeinterlace = null,
        bool interlaced = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        CropInsets crop = settings.ResolveOverscan();
        return new ScreenshotMetadata
        {
            Version = CurrentVersion,
            GameName = gameName,
            GameId = gameId,
            Frame = frame,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Renderer = settings.Renderer.ToString(),
            Filter = settings.ResolveFilter(gameId).ToString(),
            Aspect = settings.Aspect.ToString(),
            HighResolution = settings.HighResolution,
            Deinterlace = (activeDeinterlace ?? settings.Deinterlace).ToString(),
            Interlaced = interlaced,
            Region = settings.Region.ToString(),
            OverscanLeft = crop.Left,
            OverscanTop = crop.Top,
            OverscanRight = crop.Right,
            OverscanBottom = crop.Bottom,
            VideoBorderPixels = settings.VideoBorderPixels,
            VoidAreaColor = settings.ResolveVoidAreaColor(),
            Profile = settings.Profile.ToString(),
            ArmTiming = settings.ArmTiming.ToString(),
            ClioTimers = settings.ClioTimers.ToString(),
            CelTiming = settings.CelTiming.ToString(),
            CdromTiming = settings.CdromTiming.ToString(),
            CdSpeed = settings.CdSpeed.ToString(),
            Memory = MemoryLayout.FormatCode(settings.MemoryConfiguration),
            CpuClockMultiplier = settings.CpuClockMultiplier,
        };
    }

    /// <summary>Gets the sidecar path for a screenshot.</summary>
    /// <param name="screenshotPath">Screenshot PNG path.</param>
    /// <returns>The adjacent <c>.json</c> path.</returns>
    public static string GetSidecarPath(string screenshotPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotPath);
        return screenshotPath + ".json";
    }

    /// <summary>Writes the sidecar beside a screenshot.</summary>
    /// <param name="screenshotPath">Screenshot PNG path.</param>
    public void Save(string screenshotPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotPath);
        var path = GetSidecarPath(screenshotPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(this, ScreenshotMetadataJsonContext.Default.ScreenshotMetadata));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Loads sidecar metadata for a screenshot.</summary>
    /// <param name="screenshotPath">Screenshot PNG path.</param>
    /// <returns>The metadata, or <see langword="null"/> when the sidecar is absent or invalid.</returns>
    public static ScreenshotMetadata? Load(string screenshotPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotPath);
        var path = GetSidecarPath(screenshotPath);
        if (!File.Exists(path) && screenshotPath.EndsWith("-native.png", StringComparison.OrdinalIgnoreCase))
        {
            path = GetSidecarPath(screenshotPath[..^"-native.png".Length] + ".png");
        }

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            ScreenshotMetadata? metadata = JsonSerializer.Deserialize(
                File.ReadAllText(path),
                ScreenshotMetadataJsonContext.Default.ScreenshotMetadata);
            return metadata?.Version == CurrentVersion ? metadata : null;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Gets a short display summary of the metadata.</summary>
    /// <returns>A one-line description, or an empty string when no metadata is available.</returns>
    public string Summary()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Renderer}/{Filter}, {Aspect}, {Region}, crop {OverscanLeft}/{OverscanTop}/{OverscanRight}/{OverscanBottom}, profile {Profile}, CPU {CpuClockMultiplier:0.##}x");
    }
}
