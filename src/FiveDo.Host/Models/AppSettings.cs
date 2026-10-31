using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using FiveDo.Core;
using FiveDo.Input;
using FiveDo.Video;

namespace FiveDo.Host;


/// <summary>
/// Persistent desktop settings: paths, presentation options, audio, and input bindings.
/// </summary>
/// <remarks>
/// Fresh settings use the optimized video/output defaults: OpenGL, match-window output,
/// sharp-bilinear filtering, vsync, integer scaling, and Auto presentation frame skip.
/// </remarks>
public sealed class AppSettings
{
    /// <summary>Gets or sets a value indicating whether the first-run guide has been shown.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Gets or sets the last BIOS path.</summary>
    public string BiosPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional email used to recognize a donation.</summary>
    public string DonationEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a donation was confirmed for <see cref="DonationEmail"/>.</summary>
    public bool HasDonated { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last successful donation check.</summary>
    public DateTime? DonationLastCheckedUtc { get; set; }

    /// <summary>Gets or sets the last disc path.</summary>
    public string DiscPath { get; set; } = string.Empty;

    /// <summary>Gets recent disc paths, newest first.</summary>
    public IList<string> RecentDiscs { get; set; } = new List<string>();

    /// <summary>Gets or sets the save-state directory.</summary>
    public string SaveDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the screenshot directory.</summary>
    public string ScreenshotDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory containing cover art named after the disc files.
    /// Sibling images next to a disc still take precedence.
    /// </summary>
    public string CoverDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the currently selected save slot.</summary>
    public int SaveSlot { get; set; }

    /// <summary>Gets or sets the number of save slots exposed by the shell.</summary>
    public int SaveSlotCount { get; set; } = 10;

    /// <summary>Gets folders scanned by the game library.</summary>
    public IList<string> LibraryFolders { get; set; } = new List<string>();

    /// <summary>Gets or sets a value indicating whether database fixes are applied automatically.</summary>
    public bool AutoGameFixes { get; set; } = true;

    /// <summary>Gets per-game fix-mode overrides keyed by 4DO game ID.</summary>
    public IDictionary<string, FixMode> FixModeOverrides { get; set; } = new Dictionary<string, FixMode>(StringComparer.Ordinal);

    /// <summary>Gets per-game filter overrides keyed by 4DO game ID.</summary>
    public IDictionary<string, VideoFilter> FilterOverrides { get; set; } = new Dictionary<string, VideoFilter>(StringComparer.Ordinal);

    /// <summary>Gets or sets the presentation filter.</summary>
    public VideoFilter Filter { get; set; } = VideoFilter.SharpBilinear;

    /// <summary>
    /// Gets or sets the presentation frame-skip policy. The default <see cref="FrameSkipMode.Auto"/>
    /// only skips presentation when the core loop cannot hold its deadline; emulation and
    /// audio keep running at full speed.
    /// </summary>
    public FrameSkipMode FrameSkip { get; set; } = FrameSkipMode.Auto;

    /// <summary>Gets or sets the aspect policy.</summary>
    public AspectMode Aspect { get; set; } = AspectMode.FourThree;

    /// <summary>Gets or sets the custom aspect ratio (width / height) used by <see cref="AspectMode.Custom"/>.</summary>
    public double CustomAspect { get; set; } = 4.0 / 3.0;

    /// <summary>Gets or sets a value indicating whether vsync is requested.</summary>
    public bool Vsync { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether integer scaling is used.</summary>
    public bool IntegerScale { get; set; } = true;

    /// <summary>Gets or sets the desktop presentation renderer.</summary>
    public VideoRendererKind Renderer { get; set; } = VideoRendererKind.OpenGl;

    /// <summary>
    /// Gets or sets the legacy OpenGL preference. New settings use <see cref="Renderer"/>;
    /// this property keeps older settings files working.
    /// </summary>
    [JsonIgnore]
    public bool PreferOpenGl
    {
        get => Renderer == VideoRendererKind.OpenGl;
        init
        {
            if (!value && Renderer == VideoRendererKind.OpenGl)
            {
                Renderer = VideoRendererKind.Software;
            }
        }
    }

    /// <summary>Gets or sets the output resolution used for presentation.</summary>
    public OutputResolution OutputResolution { get; set; } = OutputResolution.Window;

    /// <summary>Gets or sets a value indicating whether the FPS/status overlay is shown over the video.</summary>
    public bool ShowOverlay { get; set; }

    /// <summary>Gets or sets a value indicating whether a screenshot also saves the native CPU frame.</summary>
    public bool SaveNativeScreenshot { get; set; }

    /// <summary>Gets or sets the CRT bloom strength (0-1).</summary>
    public float Bloom { get; set; } = 0.35f;

    /// <summary>Gets or sets the FSR 1.0 RCAS sharpness (0 = soft, 1 = maximum).</summary>
    public float FsrSharpness { get; set; } = 0.8f;

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
        foreach ((var id, FixMode mode) in FixModeOverrides)
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
        foreach ((var id, VideoFilter filter) in FilterOverrides)
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
        FixMode? overrideMode = GetFixModeOverride(gameId);
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
            CustomAspect = CustomAspect,
            Vsync = Vsync,
            IntegerScale = IntegerScale,
            Bloom = Bloom,
            FsrSharpness = FsrSharpness,
            OutputResolution = OutputResolution,
        };
        settings.Validate();
        return settings;
    }
}
