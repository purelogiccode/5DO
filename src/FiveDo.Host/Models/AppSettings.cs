using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using FiveDo.Cheats;
using FiveDo.Core;
using FiveDo.Host.Osd;
using FiveDo.Host.Rewind;
using FiveDo.Input;
using FiveDo.Storage;
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
    /// <summary>The settings schema version written by this build.</summary>
    /// <remarks>
    /// Version 2 adds the CD-DA mix volume and mute flag. Version 3 adds the presentation
    /// refresh rate. Version 4 adds the game-library card size. Version 5 adds the
    /// unsigned-disc/RSA-patch option. Version 6 adds the latency options (run-ahead,
    /// frame delay, adaptive sync, and audio sync). Version 7 adds the online cover
    /// catalog options. Older files load with the documented defaults for new properties.
    /// </remarks>
    public const int CurrentVersion = 7;

    /// <summary>
    /// Gets or sets the settings schema version. Zero or missing means a pre-versioned file
    /// that is migrated by <see cref="AppSettingsStore"/> on load.
    /// </summary>
    public int SettingsVersion { get; set; }

    /// <summary>Gets or sets a value indicating whether the first-run guide has been shown.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the desktop shell checks GitHub for a newer
    /// release when it starts. The check sends only the public release request.
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Gets or sets the last primary BIOS path.</summary>
    public string BiosPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional BIOS2/Kanji font path.</summary>
    public string Bios2Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the folder scanned for automatic BIOS selection.</summary>
    public string BiosFolder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the shell chooses a region-appropriate BIOS
    /// from <see cref="BiosFolder"/> for each disc. The configured BIOS remains the fallback.
    /// </summary>
    public bool AutoBios { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the media RSA signature check is patched in
    /// memory so unsigned, homebrew, and arcade discs can boot. The BIOS file is never modified.
    /// </summary>
    /// <remarks>
    /// This is an explicit opt-in. A recognized No-RSA BIOS is preferred when available;
    /// otherwise a compatible retail image is patched with the documented two-site bypass.
    /// Save states record the effective BIOS MD5, so loading a state with a different
    /// patched/unpatched BIOS reports the usual BIOS-hash mismatch warning.
    /// </remarks>
    public bool AllowUnsignedDiscs { get; set; }

    /// <summary>Gets per-game BIOS overrides keyed by 4DO game ID. Values are BIOS MD5 hashes.</summary>
    public IDictionary<string, string> BiosOverrides { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

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

    /// <summary>
    /// Gets or sets a value indicating whether the online cover catalog may be used.
    /// </summary>
    public bool UseOnlineCovers { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether missing covers are downloaded automatically
    /// when a library is scanned.
    /// </summary>
    public bool AutoDownloadCovers { get; set; } = true;

    /// <summary>
    /// Gets or sets the online cover catalog URL. A base URL is accepted and normalized
    /// to <c>/catalog.json</c>.
    /// </summary>
    public string CoverCatalogUrl { get; set; } = CoverDefaults.CatalogUrl;

    /// <summary>
    /// Gets or sets a value indicating whether cover names use Jaro-Winkler fuzzy matching.
    /// </summary>
    public bool CoverFuzzyMatching { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether region, language, and version annotations
    /// are stripped before cover-name matching.
    /// </summary>
    public bool CoverAnnotationStripping { get; set; } = true;

    /// <summary>Gets or sets the Jaro-Winkler fuzzy-match threshold (0.70-0.95).</summary>
    public double CoverFuzzyThreshold { get; set; } = CoverDefaults.DefaultFuzzyThreshold;

    /// <summary>
    /// Gets or sets the directory containing per-game cheat files. An empty value uses
    /// <see cref="CheatStore.DefaultDirectory"/>.
    /// </summary>
    public string CheatsDirectory { get; set; } = string.Empty;

    /// <summary>Resolves the absolute directory that owns per-game cheat files.</summary>
    /// <returns>The configured directory, or the default cheat directory.</returns>
    public string ResolveCheatsDirectory()
    {
        return CheatStore.ResolveDirectory(CheatsDirectory);
    }

    /// <summary>Gets or sets the currently selected save slot.</summary>
    public int SaveSlot { get; set; }

    /// <summary>Gets or sets the number of save slots exposed by the shell.</summary>
    public int SaveSlotCount { get; set; } = 10;

    /// <summary>Gets folders scanned by the game library.</summary>
    public IList<string> LibraryFolders { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a value indicating whether the library lists every disc image instead of
    /// collapsing copies that share a 3DO game ID.
    /// </summary>
    public bool ShowDuplicateLibraryEntries { get; set; }

    /// <summary>Gets or sets the width in device-independent pixels of a game card in the grid library.</summary>
    public double LibraryCardSize { get; set; } = DefaultLibraryCardSize;

    /// <summary>Gets the default width of a game card in the grid library.</summary>
    public const double DefaultLibraryCardSize = 176;

    /// <summary>Gets the smallest selectable width of a game card in the grid library.</summary>
    public const double MinLibraryCardSize = 128;

    /// <summary>Gets the largest selectable width of a game card in the grid library.</summary>
    public const double MaxLibraryCardSize = 384;

    /// <summary>
    /// Gets or sets the optional offline library metadata file. An empty value uses
    /// <see cref="LibraryMetadataStore.DefaultPath"/>.
    /// </summary>
    public string LibraryMetadataPath { get; set; } = string.Empty;

    /// <summary>Resolves the absolute library metadata path.</summary>
    /// <returns>The configured path, or the default metadata path.</returns>
    public string ResolveLibraryMetadataPath()
    {
        if (string.IsNullOrWhiteSpace(LibraryMetadataPath))
        {
            return LibraryMetadataStore.DefaultPath;
        }

        try
        {
            return Path.GetFullPath(LibraryMetadataPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return LibraryMetadataStore.DefaultPath;
        }
    }

    /// <summary>
    /// Gets or sets the optional Redump/No-Intro DAT used by the library disc-health
    /// check. An empty value disables the known-dump match.
    /// </summary>
    public string DiscDatPath { get; set; } = string.Empty;

    /// <summary>Resolves the absolute DAT path, or an empty string when none is configured.</summary>
    /// <returns>The configured path, or an empty string.</returns>
    public string ResolveDiscDatPath()
    {
        if (string.IsNullOrWhiteSpace(DiscDatPath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(DiscDatPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    /// <summary>Gets or sets a value indicating whether database fixes are applied automatically.</summary>
    public bool AutoGameFixes { get; set; } = true;

    /// <summary>Gets per-game fix-mode overrides keyed by 4DO game ID.</summary>
    public IDictionary<string, FixMode> FixModeOverrides { get; set; } = new Dictionary<string, FixMode>(StringComparer.Ordinal);

    /// <summary>Gets per-game filter overrides keyed by 4DO game ID.</summary>
    public IDictionary<string, VideoFilter> FilterOverrides { get; set; } = new Dictionary<string, VideoFilter>(StringComparer.Ordinal);

    /// <summary>Gets per-game video-region overrides keyed by 4DO game ID.</summary>
    public IDictionary<string, VideoRegion> RegionOverrides { get; set; } = new Dictionary<string, VideoRegion>(StringComparer.Ordinal);

    /// <summary>Gets per-game PBUS device overrides keyed by 4DO game ID.</summary>
    public IDictionary<string, IList<string>> InputDeviceOverrides { get; set; } =
        new Dictionary<string, IList<string>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a value indicating whether known lightgun/trackball titles seed a
    /// per-game input-device override automatically.
    /// </summary>
    public bool AutoInputDevices { get; set; } = true;

    /// <summary>Gets per-game timing and CD-DA profiles keyed by 4DO game ID.</summary>
    public IDictionary<string, GameProfile> GameProfiles { get; set; } = new Dictionary<string, GameProfile>(StringComparer.Ordinal);

    /// <summary>Gets or sets the presentation filter.</summary>
    public VideoFilter Filter { get; set; } = VideoFilter.SharpBilinear;

    /// <summary>Gets or sets the 480i/240p field handling mode.</summary>
    /// <remarks>
    /// Auto detects complementary fields and weaves them, Bob presents every field
    /// unchanged, Weave always combines two fields, and Blend averages them. This is a
    /// presentation option: emulated state, input, audio, and save states are unchanged.
    /// </remarks>
    public DeinterlaceMode Deinterlace { get; set; } = DeinterlaceMode.Auto;

    /// <summary>
    /// Gets or sets the presentation frame-skip policy. The default <see cref="FrameSkipMode.Auto"/>
    /// only skips presentation when the core loop cannot hold its deadline; emulation and
    /// audio keep running at full speed.
    /// </summary>
    public FrameSkipMode FrameSkip { get; set; } = FrameSkipMode.Auto;

    /// <summary>
    /// Gets or sets the number of pixels cropped from every edge before presentation and
    /// native screenshots. Zero shows the full frame.
    /// </summary>
    /// <remarks>
    /// Overscan is a presentation option: it hides the outer pixels but does not change
    /// emulated state, input, frame hashes, or save states.
    /// </remarks>
    public int OverscanPixels { get; set; }

    /// <summary>Gets or sets the left-edge crop in native pixels (0-32).</summary>
    /// <remarks>
    /// The four per-edge values take precedence over <see cref="OverscanPixels"/> when any
    /// of them is nonzero. <see cref="SetOverscan"/> clears the legacy uniform value.
    /// </remarks>
    public int OverscanLeft { get; set; }

    /// <summary>Gets or sets the top-edge crop in native pixels (0-32).</summary>
    public int OverscanTop { get; set; }

    /// <summary>Gets or sets the right-edge crop in native pixels (0-32).</summary>
    public int OverscanRight { get; set; }

    /// <summary>Gets or sets the bottom-edge crop in native pixels (0-32).</summary>
    public int OverscanBottom { get; set; }

    /// <summary>Gets or sets the gray/void border drawn around the presented image (0-64 pixels).</summary>
    /// <remarks>
    /// The border is presentation-only. It is drawn outside the filtered image and does not
    /// change the emulated frame or native screenshots.
    /// </remarks>
    public int VideoBorderPixels { get; set; }

    /// <summary>Gets or sets the color of the void area and border.</summary>
    /// <remarks>
    /// Accepts <c>#RRGGBB</c> or <c>#AARRGGBB</c>. Invalid values fall back to
    /// <see cref="DefaultVoidAreaColor"/>.
    /// </remarks>
    public string VoidAreaColor { get; set; } = DefaultVoidAreaColor;

    /// <summary>Gets or sets the void-area pattern: <c>Solid</c>, <c>Checker</c>, <c>Grid</c>, or <c>Scanline</c>.</summary>
    public string VoidAreaPattern { get; set; } = DefaultVoidAreaPattern;

    /// <summary>Gets the default void-area color.</summary>
    public const string DefaultVoidAreaColor = "#FF101010";

    /// <summary>Gets the default void-area pattern.</summary>
    public const string DefaultVoidAreaPattern = nameof(VoidPattern.Solid);

    /// <summary>Gets the validated void-area pattern.</summary>
    /// <returns>The parsed pattern, or <see cref="VoidPattern.Solid"/> when the value is invalid.</returns>
    public VoidPattern ResolveVoidAreaPattern()
    {
        return Enum.TryParse(VoidAreaPattern, ignoreCase: true, out VoidPattern pattern)
            && Enum.IsDefined(pattern)
            ? pattern
            : VoidPattern.Solid;
    }

    /// <summary>Gets the effective per-edge crop, honoring the legacy uniform value.</summary>
    /// <returns>A clamped crop.</returns>
    public CropInsets ResolveOverscan()
    {
        var crop = new CropInsets(OverscanLeft, OverscanTop, OverscanRight, OverscanBottom);
        return crop.IsEmpty ? CropInsets.Uniform(OverscanPixels) : crop.Clamp();
    }

    /// <summary>Stores a per-edge crop and clears the legacy uniform value.</summary>
    /// <param name="crop">Crop to store.</param>
    public void SetOverscan(CropInsets crop)
    {
        CropInsets normalized = crop.Clamp();
        OverscanLeft = normalized.Left;
        OverscanTop = normalized.Top;
        OverscanRight = normalized.Right;
        OverscanBottom = normalized.Bottom;
        OverscanPixels = 0;
    }

    /// <summary>Gets a validated void-area color string.</summary>
    /// <returns>A <c>#RRGGBB</c> or <c>#AARRGGBB</c> string.</returns>
    public string ResolveVoidAreaColor()
    {
        return IsValidColor(VoidAreaColor) ? VoidAreaColor : DefaultVoidAreaColor;
    }

    /// <summary>Gets whether a string is a supported hex color.</summary>
    /// <param name="value">Candidate color.</param>
    /// <returns><see langword="true"/> for <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</returns>
    public static bool IsValidColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] != '#')
        {
            return false;
        }

        if (value.Length is not 7 and not 9)
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets or sets the aspect policy.</summary>
    public AspectMode Aspect { get; set; } = AspectMode.FourThree;

    /// <summary>Gets or sets the custom aspect ratio (width / height) used by <see cref="AspectMode.Custom"/>.</summary>
    public double CustomAspect { get; set; } = 4.0 / 3.0;

    /// <summary>Gets or sets a value indicating whether vsync is requested.</summary>
    public bool Vsync { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether presentation should avoid blocking on vsync
    /// and follow the compositor so a VRR/adaptive-sync display can drive the cadence.
    /// </summary>
    /// <remarks>
    /// VRR is a display/driver feature; 5DO cannot enable it. This option only disables the
    /// swap interval and presents through the compositor animation-frame loop. Actual
    /// adaptive-sync activation depends on the host GPU, driver, monitor, and desktop
    /// configuration.
    /// </remarks>
    public bool Vrr { get; set; }

    /// <summary>Gets or sets the number of speculative frames run ahead of presentation (0-2).</summary>
    /// <remarks>
    /// Run-ahead trades CPU time for lower input latency. It saves and restores machine state
    /// every displayed frame, so rewind history and deterministic movie recording are disabled
    /// while it is active. Zero is the safe default.
    /// </remarks>
    public int RunAheadFrames { get; set; }

    /// <summary>Gets or sets the number of frames the presenter holds behind the core (0-8).</summary>
    /// <remarks>
    /// Positive values add a fixed video delay. This is useful for aligning video with a
    /// high-latency audio path or for frame-by-frame inspection; zero is the lowest-latency
    /// default.
    /// </remarks>
    public int FrameDelayFrames { get; set; }

    /// <summary>Gets the largest supported run-ahead depth.</summary>
    public const int MaxRunAheadFrames = 2;

    /// <summary>Gets the largest supported presentation frame delay.</summary>
    public const int MaxFrameDelayFrames = 8;

    /// <summary>Gets or sets a value indicating whether integer scaling is used.</summary>
    public bool IntegerScale { get; set; } = true;

    /// <summary>
    /// Gets or sets the presentation refresh rate in hertz. 60 keeps render-on-demand,
    /// values above 60 re-present the latest frame at that rate, and 0 matches the display.
    /// </summary>
    /// <remarks>
    /// The 3DO outputs 60 fields per second (50 for PAL). A rate above 60 does not create
    /// new emulated frames; it presents the latest frame more often so the compositor and
    /// swap chain run at the requested rate. Presentation is always bounded by the host.
    /// </remarks>
    public int DisplayRefreshRate { get; set; } = DefaultDisplayRefreshRate;

    /// <summary>Gets the refresh rate used when the setting is absent.</summary>
    public const int DefaultDisplayRefreshRate = 60;

    /// <summary>Gets the lowest refresh rate that can be configured explicitly.</summary>
    public const int MinDisplayRefreshRate = 30;

    /// <summary>Gets the highest refresh rate that can be configured explicitly.</summary>
    public const int MaxDisplayRefreshRate = 480;

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

    /// <summary>Gets or sets a value indicating whether the frame/input display is shown.</summary>
    public bool ShowInputDisplay { get; set; }

    /// <summary>Gets or sets the controller port (1-6) shown by the input display.</summary>
    public int InputDisplayPlayer { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether on-screen notifications are shown.</summary>
    public bool OsdEnabled { get; set; } = true;

    /// <summary>Gets or sets the anchor position for on-screen notifications.</summary>
    public OsdPosition OsdPosition { get; set; } = OsdPosition.TopRight;

    /// <summary>Gets or sets the notification overlay opacity (0.1-1).</summary>
    public double OsdOpacity { get; set; } = 0.9;

    /// <summary>Gets or sets how long a notification stays visible, in milliseconds.</summary>
    public int OsdDurationMilliseconds { get; set; } = 2200;

    /// <summary>Gets or sets a value indicating whether the in-game quick menu is available.</summary>
    public bool QuickMenuEnabled { get; set; } = true;

    /// <summary>Creates validated OSD options from the current settings.</summary>
    /// <returns>The OSD configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an OSD setting is outside its supported range.</exception>
    public OsdOptions ToOsdOptions()
    {
        var options = new OsdOptions
        {
            Enabled = OsdEnabled,
            Position = OsdPosition,
            Opacity = OsdOpacity,
            DurationMilliseconds = OsdDurationMilliseconds,
        };
        options.Validate();
        return options;
    }

    /// <summary>Gets or sets a value indicating whether a screenshot also saves the native CPU frame.</summary>
    public bool SaveNativeScreenshot { get; set; }

    /// <summary>Gets or sets the CRT bloom strength (0-1).</summary>
    public float Bloom { get; set; } = 0.35f;

    /// <summary>Gets or sets the FSR 1.0 RCAS sharpness (0 = soft, 1 = maximum).</summary>
    public float FsrSharpness { get; set; } = 0.8f;

    /// <summary>Gets or sets a value indicating whether the opt-in 640x480 high-resolution mode is used.</summary>
    public bool HighResolution { get; set; }

    /// <summary>
    /// Gets or sets the last selected timing profile. The individual timing options below
    /// remain authoritative; the profile is a convenience label and becomes
    /// <see cref="EmulationProfile.Custom"/> when the options no longer match it.
    /// </summary>
    public EmulationProfile Profile { get; set; } = EmulationProfile.Compatible;

    /// <summary>
    /// Gets or sets the video region. PAL changes the field rate, visible height, and VDL
    /// timing and needs a PAL BIOS for European titles.
    /// </summary>
    public VideoRegion Region { get; set; } = VideoRegion.Ntsc;

    /// <summary>
    /// Gets or sets a value indicating whether the video region follows the resolved BIOS
    /// (PAL for a PAL BIOS, NTSC otherwise) instead of <see cref="Region"/>.
    /// </summary>
    public bool AutoRegion { get; set; } = true;

    /// <summary>
    /// Gets or sets the ARM60 N-cycle timing mode. The default is the FreeDO-compatible
    /// baseline; <see cref="ArmTimingMode.Arm60Datasheet"/> is the accuracy option.
    /// </summary>
    public ArmTimingMode ArmTiming { get; set; } = ArmTimingMode.FreeDoCompatible;

    /// <summary>
    /// Gets or sets the CLIO timer scan model. The default is the FreeDO-compatible
    /// all-timer scan; <see cref="ClioTimerMode.OperaScheduler"/> uses Opera's per-slot
    /// walk and explicit cascade carry.
    /// </summary>
    public ClioTimerMode ClioTimers { get; set; } = ClioTimerMode.FreeDoCompatible;

    /// <summary>
    /// Gets or sets the MADAM CEL timing model. The default is the synchronous
    /// FreeDO-compatible draw; <see cref="CelTimingMode.OperaBusy"/> keeps the CEL status
    /// busy until the estimated pixel/texel deadline.
    /// </summary>
    public CelTimingMode CelTiming { get; set; } = CelTimingMode.FreeDoCompatible;

    /// <summary>
    /// Gets or sets the CD-ROM data-sector timing model. The default transfers sectors
    /// immediately like FreeDO; <see cref="CdromTimingMode.OperaCycleBased"/> schedules each
    /// sector at the stock 2x drive speed.
    /// </summary>
    public CdromTimingMode CdromTiming { get; set; } = CdromTimingMode.FreeDoCompatible;

    /// <summary>
    /// Gets or sets the effective CD-ROM read speed for the cycle-based timing model. The
    /// stock 3DO drive is 2x; <see cref="CdSpeed.Unbounded"/> removes the host-side delay.
    /// </summary>
    public CdSpeed CdSpeed { get; set; } = CdSpeed.X2;

    /// <summary>
    /// Gets or sets the DRAM/VRAM capacity layout. The default is the stock 2 MB DRAM /
    /// 1 MB VRAM machine. Changing it restarts the machine.
    /// </summary>
    public MemoryConfiguration MemoryConfiguration { get; set; } = MemoryConfiguration.Dram2MbVram1Mb;

    /// <summary>
    /// Gets or sets the ARM clock multiplier. 1.0 is the stock 12.5 MHz clock; the
    /// supported range is 0.5x to 4x (6.25-50 MHz).
    /// </summary>
    public double CpuClockMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the optional initial DSP PRNG seed. <see langword="null"/> uses
    /// FreeDO's default seed.
    /// </summary>
    public uint? RandomSeed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether CLIO DIPIR latches and XBUS access aborts
    /// are enabled. This is an opt-in accuracy option and restarts the machine.
    /// </summary>
    public bool OperaDipir { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether CLIO CSTAT soft resets use Opera's
    /// ARM/MADAM/DSP reset path. This is an opt-in accuracy option and restarts the
    /// machine.
    /// </summary>
    public bool OperaSoftReset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Portfolio OS fixed-point math SWIs are
    /// handled on the host. This is an opt-in performance option and restarts the machine.
    /// </summary>
    public bool SwiHle { get; set; }

    /// <summary>
    /// Gets or sets the guest CPU backend. The interpreter is the reference and the only
    /// implemented backend.
    /// </summary>
    public CpuBackendKind CpuBackend { get; set; } = CpuBackendKind.Interpreter;

    /// <summary>
    /// Gets the PBUS device type for each of the six ports. Values are the
    /// <see cref="InputDeviceType"/> names; missing or invalid entries use a joypad.
    /// </summary>
    public IList<string> InputDevices { get; set; } = new List<string>
    {
        "Joypad",
        "Joypad",
        "Joypad",
        "Joypad",
        "Joypad",
        "Joypad",
        "Joypad",
        "Joypad",
    };

    /// <summary>
    /// Gets or sets the maximum number of active input devices, 0-8, or <see langword="null"/>
    /// for automatic selection.
    /// </summary>
    /// <remarks>
    /// This is Opera's <c>opera_active_devices</c> cap. Zero reports an empty PBUS chain;
    /// eight allows every daisy-chain port. Automatic keeps 5DO's historical behavior:
    /// one joypad, growing to the highest configured non-joypad port.
    /// </remarks>
    public int? ActiveInputDevices { get; set; }

    /// <summary>Resolves the configured device for a zero-based port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The configured device, or <see cref="InputDeviceType.Joypad"/>.</returns>
    public InputDeviceType GetInputDevice(int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(port, PbusEncoder.MaxPorts);
        // JSON can deserialize null despite the non-nullable annotation.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (InputDevices is null || port >= InputDevices.Count)
        {
            return InputDeviceType.Joypad;
        }

        return Enum.TryParse(InputDevices[port], ignoreCase: true, out InputDeviceType device)
            && Enum.IsDefined(device)
            ? device
            : InputDeviceType.Joypad;
    }

    /// <summary>Sets the configured device for a zero-based port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="device">Device type.</param>
    public void SetInputDevice(int port, InputDeviceType device)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(port, PbusEncoder.MaxPorts);
        while (InputDevices.Count <= port)
        {
            InputDevices.Add(nameof(InputDeviceType.Joypad));
        }

        InputDevices[port] = device.ToString();
    }

    /// <summary>Applies the configured devices to an input manager.</summary>
    /// <param name="input">Input manager to configure.</param>
    /// <remarks>
    /// The connected count only grows to the highest non-joypad port so the default
    /// single-pad packet is unchanged for existing configurations.
    /// </remarks>
    public void ApplyInputDevices(InputManager input) => ApplyInputDevices(input, gameId: null);

    /// <summary>Applies the resolved devices for a game to an input manager.</summary>
    /// <param name="input">Input manager to configure.</param>
    /// <param name="gameId">4DO game ID, or <see langword="null"/> for the global devices.</param>
    public void ApplyInputDevices(InputManager input, string? gameId)
    {
        ArgumentNullException.ThrowIfNull(input);
        IReadOnlyList<string> devices = ResolveInputDevices(gameId);
        var count = 1;
        for (var port = 0; port < PbusEncoder.MaxPorts; port++)
        {
            InputDeviceType device = ParseDevice(devices[port]);
            input.Devices.SetDevice(port, device);
            if (device != InputDeviceType.Joypad)
            {
                count = port + 1;
            }
        }

        input.ControllerCount = ActiveInputDevices is { } active
            ? Math.Clamp(active, 0, PbusEncoder.MaxPorts)
            : count;
    }

    /// <summary>Gets the per-game input-device override, if one is configured.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <returns>Six normalized device names, or <see langword="null"/> when the game uses the global list.</returns>
    public IReadOnlyList<string>? GetInputDeviceOverride(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var normalized = NormalizeGameId(gameId);
        foreach ((var id, IList<string> devices) in InputDeviceOverrides)
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                return NormalizeInputDevices(devices);
            }
        }

        return null;
    }

    /// <summary>Sets or removes a per-game input-device override.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <param name="devices">Six device names, or <see langword="null"/> to use the global list.</param>
    public void SetInputDeviceOverride(string gameId, IReadOnlyList<string>? devices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var normalized = NormalizeGameId(gameId);
        foreach (var id in InputDeviceOverrides.Keys.ToArray())
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                InputDeviceOverrides.Remove(id);
            }
        }

        if (devices is not null)
        {
            InputDeviceOverrides[normalized] = NormalizeInputDevices(devices).ToList();
        }
    }

    /// <summary>Resolves the devices for a game, honoring per-game overrides and the database.</summary>
    /// <param name="gameId">4DO game ID, or <see langword="null"/> for the global list.</param>
    /// <returns>The resolved device names.</returns>
    public IReadOnlyList<string> ResolveInputDevices(string? gameId)
    {
        GameRecord? record = string.IsNullOrWhiteSpace(gameId)
            ? null
            : GameDatabase.Default.FindById(gameId);
        IReadOnlyList<string>? devices = GetInputDeviceOverride(gameId)
            ?? GameControlDefaults.ParseDeviceSpec(record?.InputDevices);
        return devices ?? NormalizeInputDevices(InputDevices);
    }

    /// <summary>Creates the known-title input override when none exists.</summary>
    /// <param name="gameId">4DO game ID.</param>
    /// <param name="title">Game title from the database or file name.</param>
    /// <returns><see langword="true"/> when an automatic override was added.</returns>
    public bool EnsureAutoInputDeviceOverride(string gameId, string? title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        if (!AutoInputDevices || GetInputDeviceOverride(gameId) is not null)
        {
            return false;
        }

        // The game database already drives ResolveInputDevices; do not persist a copy
        // that would shadow future database updates.
        if (GameControlDefaults.ParseDeviceSpec(GameDatabase.Default.FindById(gameId)?.InputDevices) is not null)
        {
            return false;
        }

        IReadOnlyList<string>? devices = GameControlDefaults.TryGetInputDevices(title);
        if (devices is null)
        {
            return false;
        }

        SetInputDeviceOverride(gameId, devices);
        return true;
    }

    /// <summary>Normalizes a device list to six valid names.</summary>
    /// <param name="devices">Source names; missing or invalid entries become joypads.</param>
    /// <returns>Six device names.</returns>
    public static IReadOnlyList<string> NormalizeInputDevices(IEnumerable<string>? devices)
    {
        var source = devices as IList<string> ?? (devices is null ? null : new List<string>(devices));
        var normalized = new string[PbusEncoder.MaxPorts];
        for (var port = 0; port < normalized.Length; port++)
        {
            string? name = source is not null && port < source.Count ? source[port] : null;
            normalized[port] = ParseDevice(name).ToString();
        }

        return normalized;
    }

    private static InputDeviceType ParseDevice(string? name)
    {
        return Enum.TryParse(name, ignoreCase: true, out InputDeviceType device)
            && Enum.IsDefined(device)
            ? device
            : InputDeviceType.Joypad;
    }

    /// <summary>
    /// Gets or sets the NVRAM storage mode. <see cref="NvramStorageMode.Global"/> keeps
    /// the historical single image; <see cref="NvramStorageMode.PerGame"/> gives every
    /// game its own save data.
    /// </summary>
    public NvramStorageMode NvramStorageMode { get; set; } = NvramStorageMode.Global;

    /// <summary>
    /// Gets or sets the NVRAM version slot (0-9). Versions are independent images, so
    /// changing the version never overwrites another version's data.
    /// </summary>
    public int NvramVersion { get; set; }

    /// <summary>
    /// Gets or sets the directory that owns the NVRAM layout. An empty value uses
    /// <see cref="AppSettingsStore.DefaultDirectory"/>.
    /// </summary>
    public string NvramDirectory { get; set; } = string.Empty;

    /// <summary>Gets the absolute directory that owns the NVRAM layout.</summary>
    /// <returns>The configured directory, or the default settings directory.</returns>
    public string ResolveNvramDirectory()
    {
        var directory = string.IsNullOrWhiteSpace(NvramDirectory)
            ? AppSettingsStore.DefaultDirectory
            : NvramDirectory;
        return Path.GetFullPath(directory);
    }

    /// <summary>Resolves the primary NVRAM image path for a game.</summary>
    /// <param name="gameId">4DO game ID. Required in per-game mode.</param>
    /// <returns>The absolute NVRAM path.</returns>
    public string ResolveNvramPath(string? gameId)
    {
        return NvramLayout.Resolve(
            ResolveNvramDirectory(),
            NvramStorageMode,
            NvramVersion,
            gameId);
    }

    /// <summary>Creates a debounced store for the configured NVRAM image.</summary>
    /// <param name="gameId">4DO game ID. Required in per-game mode.</param>
    /// <returns>A store over the resolved primary and migration fallback paths.</returns>
    public NvramStore CreateNvramStore(string? gameId)
    {
        var directory = ResolveNvramDirectory();
        return new NvramStore(
            NvramLayout.Resolve(directory, NvramStorageMode, NvramVersion, gameId),
            NvramLayout.GetFallbackPaths(directory, NvramStorageMode, NvramVersion, gameId));
    }

    /// <summary>Gets or sets the audio volume (0-1).</summary>
    public float Volume { get; set; } = 1f;

    /// <summary>Gets or sets a value indicating whether audio output is enabled.</summary>
    public bool AudioEnabled { get; set; } = true;

    /// <summary>Gets or sets the OpenAL output device name, or an empty string for the default.</summary>
    public string AudioDevice { get; set; } = string.Empty;

    /// <summary>Gets or sets the target OpenAL buffer latency in milliseconds (20-500).</summary>
    public int AudioBufferMilliseconds { get; set; } = 100;

    /// <summary>
    /// Gets or sets the audio/video sync offset in milliseconds (-250 to 250).
    /// </summary>
    /// <remarks>
    /// Positive values delay audio by inserting silence at the head of the stream; negative
    /// values advance it by dropping the first samples. The offset is applied when OpenAL
    /// output is created or reset, so changing it recreates the output without resetting the
    /// machine.
    /// </remarks>
    public int AudioSyncMilliseconds { get; set; }

    /// <summary>Gets the smallest supported audio sync offset.</summary>
    public const int MinAudioSyncMilliseconds = -250;

    /// <summary>Gets the largest supported audio sync offset.</summary>
    public const int MaxAudioSyncMilliseconds = 250;

    /// <summary>
    /// Gets or sets the controller port that receives host pointer input, or zero to
    /// send it to every mouse/lightgun/arcade-lightgun/trackball port.
    /// </summary>
    /// <remarks>
    /// Multiplayer lightgun sessions set this to the active player so one host pointer
    /// cannot aim two guns at the same place.
    /// </remarks>
    public int PointerPort { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the desktop lightgun crosshair is hidden.
    /// </summary>
    /// <remarks>
    /// This is a presentation-only option. The pointer still drives the emulated lightgun;
    /// hiding the crosshair is useful when the game draws its own reticle or for recording.
    /// </remarks>
    public bool HideLightgunCrosshair { get; set; }

    /// <summary>Gets or sets the pointer calibration scale (0.5-2.0).</summary>
    public double PointerScale { get; set; } = 1.0;

    /// <summary>Gets or sets the horizontal pointer offset in native pixels.</summary>
    public int PointerOffsetX { get; set; }

    /// <summary>Gets or sets the vertical pointer offset in native pixels.</summary>
    public int PointerOffsetY { get; set; }

    /// <summary>Gets or sets the CD-DA mix level (0-1).</summary>
    public float CdAudioVolume { get; set; } = 0.8f;

    /// <summary>Gets or sets a value indicating whether CD-DA mixing is muted.</summary>
    public bool CdAudioMuted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a guest eject automatically advances to the
    /// next entry in the loaded playlist.
    /// </summary>
    /// <remarks>
    /// This is the ODE-style behavior expected by multi-disc titles. When no playlist is
    /// loaded, or the option is off, the frontend asks the user to insert a disc instead.
    /// </remarks>
    public bool AutoSwapOnEject { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether emulation pauses when the window loses focus.</summary>
    public bool PauseOnFocusLoss { get; set; }

    /// <summary>Gets or sets a value indicating whether rewind history is recorded.</summary>
    public bool RewindEnabled { get; set; } = true;

    /// <summary>Gets or sets the rewind history length in seconds (30-300).</summary>
    public int RewindSeconds { get; set; } = 60;

    /// <summary>Gets or sets the fast-forward multiplier (2-10).</summary>
    public int FastForwardMultiplier { get; set; } = 4;

    /// <summary>Creates validated rewind options from the current settings.</summary>
    /// <returns>The rewind configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a rewind setting is outside its supported range.</exception>
    public RewindOptions ToRewindOptions()
    {
        var options = new RewindOptions
        {
            Enabled = RewindEnabled,
            Seconds = RewindSeconds,
        };
        options.Validate();
        return options;
    }

    /// <summary>Validates and returns the fast-forward multiplier.</summary>
    /// <returns>The configured multiplier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier is outside 2-10.</exception>
    public int ResolveFastForwardMultiplier()
    {
        if (FastForwardMultiplier is < 2 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: FastForwardMultiplier,
                message: "The fast-forward multiplier must be between 2 and 10.");
        }

        return FastForwardMultiplier;
    }

    /// <summary>Validates and returns the presentation refresh rate.</summary>
    /// <returns>The configured rate in hertz, where 0 means "match the display".</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the rate is nonzero and outside
    /// <see cref="MinDisplayRefreshRate"/>-<see cref="MaxDisplayRefreshRate"/>.
    /// </exception>
    public int ResolveDisplayRefreshRate()
    {
        if (DisplayRefreshRate != 0
            && DisplayRefreshRate is < MinDisplayRefreshRate or > MaxDisplayRefreshRate)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: DisplayRefreshRate,
                message: $"The display refresh rate must be 0 (match display) or between "
                    + $"{MinDisplayRefreshRate} and {MaxDisplayRefreshRate} Hz.");
        }

        return DisplayRefreshRate;
    }

    /// <summary>Validates and returns the run-ahead depth.</summary>
    /// <returns>The configured depth, 0-2.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the depth is outside 0-2.</exception>
    public int ResolveRunAheadFrames()
    {
        if (RunAheadFrames is < 0 or > MaxRunAheadFrames)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: RunAheadFrames,
                message: $"The run-ahead depth must be between 0 and {MaxRunAheadFrames} frames.");
        }

        return RunAheadFrames;
    }

    /// <summary>Validates and returns the presentation frame delay.</summary>
    /// <returns>The configured delay, 0-8.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the delay is outside 0-8.</exception>
    public int ResolveFrameDelayFrames()
    {
        if (FrameDelayFrames is < 0 or > MaxFrameDelayFrames)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: FrameDelayFrames,
                message: $"The presentation frame delay must be between 0 and {MaxFrameDelayFrames} frames.");
        }

        return FrameDelayFrames;
    }

    /// <summary>Validates and returns the audio sync offset.</summary>
    /// <returns>The configured offset in milliseconds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is outside -250 to 250 ms.</exception>
    public int ResolveAudioSyncMilliseconds()
    {
        if (AudioSyncMilliseconds is < MinAudioSyncMilliseconds or > MaxAudioSyncMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: AudioSyncMilliseconds,
                message: $"The audio sync offset must be between {MinAudioSyncMilliseconds} "
                    + $"and {MaxAudioSyncMilliseconds} milliseconds.");
        }

        return AudioSyncMilliseconds;
    }

    /// <summary>Gets or sets player input bindings.</summary>
    public InputProfile Input { get; set; } = InputProfile.CreateDefault();

    /// <summary>Gets the per-game BIOS override MD5, if one is configured.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <returns>The override MD5, or <see langword="null"/> when the game uses automatic selection.</returns>
    public string? GetBiosOverride(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var normalized = NormalizeGameId(gameId);
        foreach ((var id, var md5) in BiosOverrides)
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                return md5.Trim();
            }
        }

        return null;
    }

    /// <summary>Sets or removes a per-game BIOS override.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <param name="md5">BIOS MD5, or <see langword="null"/> to restore automatic selection.</param>
    public void SetBiosOverride(string gameId, string? md5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var normalized = NormalizeGameId(gameId);
        foreach (var id in BiosOverrides.Keys.ToArray())
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                BiosOverrides.Remove(id);
            }
        }

        if (!string.IsNullOrWhiteSpace(md5))
        {
            BiosOverrides[normalized] = md5.Trim().ToLowerInvariant();
        }
    }

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
    public VideoFilter ResolveFilter(string? gameId)
    {
        return GetFilterOverride(gameId) ?? Filter;
    }

    /// <summary>Returns whether host pointer input should drive a one-based controller port.</summary>
    /// <param name="port">One-based controller port.</param>
    /// <returns><see langword="true"/> when <see cref="PointerPort"/> is zero or matches the port.</returns>
    public bool RoutesPointerTo(int port) => PointerPort == 0 || PointerPort == port;

    /// <summary>Maps a normalized host pointer position into native frame coordinates.</summary>
    /// <param name="normalizedX">Horizontal position in 0..1 across the video area.</param>
    /// <param name="normalizedY">Vertical position in 0..1 across the video area.</param>
    /// <param name="width">Native frame width in pixels.</param>
    /// <param name="height">Native frame height in lines.</param>
    /// <returns>The clamped pixel coordinate.</returns>
    public (int X, int Y) MapPointer(double normalizedX, double normalizedY, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        double x = (((normalizedX - 0.5) * PointerScale) + 0.5) * width + PointerOffsetX;
        double y = (((normalizedY - 0.5) * PointerScale) + 0.5) * height + PointerOffsetY;
        return (
            Math.Clamp((int)Math.Round(x), 0, width - 1),
            Math.Clamp((int)Math.Round(y), 0, height - 1));
    }

    /// <summary>Resolves the screenshot directory, defaulting to the user's Pictures folder.</summary>
    /// <returns>The absolute screenshot directory.</returns>
    public string ResolveScreenshotDirectory()
    {
        if (string.IsNullOrWhiteSpace(ScreenshotDirectory))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

        return Path.GetFullPath(ScreenshotDirectory);
    }

    /// <summary>Resolves the effective video region.</summary>
    /// <param name="detected">Region inferred from the BIOS or disc.</param>
    /// <returns>The detected region when <see cref="AutoRegion"/> is enabled; otherwise <see cref="Region"/>.</returns>
    public VideoRegion ResolveRegion(VideoRegion detected) => AutoRegion ? detected : Region;

    /// <summary>Resolves the effective video region for a game, honoring per-game overrides.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <param name="detected">Region inferred from the BIOS or disc.</param>
    /// <returns>The per-game override, otherwise the automatic or global region.</returns>
    public VideoRegion ResolveRegionForGame(string? gameId, VideoRegion detected) =>
        GetRegionOverride(gameId) ?? ResolveRegion(detected);

    /// <summary>Gets the per-game region override, if one is configured.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <returns>The override, or <see langword="null"/> when the game follows the global region.</returns>
    public VideoRegion? GetRegionOverride(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var normalized = NormalizeGameId(gameId);
        foreach ((var id, VideoRegion region) in RegionOverrides)
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>Sets or removes a per-game region override.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <param name="region">Region, or <see langword="null"/> to follow the global/automatic choice.</param>
    public void SetRegionOverride(string gameId, VideoRegion? region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var normalized = NormalizeGameId(gameId);
        foreach (var id in RegionOverrides.Keys.ToArray())
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                RegionOverrides.Remove(id);
            }
        }

        if (region is not null)
        {
            RegionOverrides[normalized] = region.Value;
        }
    }

    /// <summary>
    /// Creates a shallow settings snapshot with the game's stored profile applied.
    /// </summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <returns>A copy for machine construction; the live settings are unchanged.</returns>
    /// <remarks>
    /// Collection references are shared intentionally. Machine construction only reads
    /// scalar tuning values and CD-DA volume/mute, so the copy never mutates a collection.
    /// </remarks>
    public AppSettings CreateEffectiveForGame(string? gameId)
    {
        var copy = (AppSettings)MemberwiseClone();
        // MemberwiseClone shares the override dictionaries. Applying a profile updates
        // those dictionaries, so give the copy its own maps before touching them.
        copy.BiosOverrides = new Dictionary<string, string>(BiosOverrides, StringComparer.Ordinal);
        copy.FixModeOverrides = new Dictionary<string, FixMode>(FixModeOverrides, StringComparer.Ordinal);
        copy.FilterOverrides = new Dictionary<string, VideoFilter>(FilterOverrides, StringComparer.Ordinal);
        copy.RegionOverrides = new Dictionary<string, VideoRegion>(RegionOverrides, StringComparer.Ordinal);
        copy.InputDeviceOverrides = new Dictionary<string, IList<string>>(InputDeviceOverrides, StringComparer.Ordinal);
        if (GetGameProfile(gameId) is { } profile)
        {
            profile.ApplyTo(copy, gameId);
        }

        return copy;
    }

    /// <summary>Gets the per-game timing profile, if one is configured.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <returns>The profile, or <see langword="null"/> when the game uses global options.</returns>
    public GameProfile? GetGameProfile(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var normalized = NormalizeGameId(gameId);
        foreach ((var id, GameProfile profile) in GameProfiles)
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                return profile;
            }
        }

        return null;
    }

    /// <summary>Sets or removes a per-game timing profile.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <param name="profile">Profile, or <see langword="null"/> to restore global options.</param>
    public void SetGameProfile(string gameId, GameProfile? profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var normalized = NormalizeGameId(gameId);
        foreach (var id in GameProfiles.Keys.ToArray())
        {
            if (string.Equals(NormalizeGameId(id), normalized, StringComparison.Ordinal))
            {
                GameProfiles.Remove(id);
            }
        }

        if (profile is not null)
        {
            GameProfiles[normalized] = profile;
        }
    }

    /// <summary>Applies a game's stored timing profile to the global settings.</summary>
    /// <param name="gameId">4DO game ID, case-insensitive.</param>
    /// <returns><see langword="true"/> when a profile was found and applied.</returns>
    public bool ApplyGameProfile(string? gameId)
    {
        GameProfile? profile = GetGameProfile(gameId);
        if (profile is null)
        {
            return false;
        }

        profile.ApplyTo(this, gameId);
        return true;
    }

    /// <summary>Resolves the fix-mode bits for a game, honoring per-game overrides.</summary>
    /// <param name="gameId">4DO game ID, or <see langword="null"/> when no disc is loaded.</param>
    /// <returns>The override when present, the database mapping when automatic fixes are enabled, or none.</returns>
    public FixMode ResolveFixMode(string? gameId)
    {
        FixMode? overrideMode = GetFixModeOverride(gameId);
        return overrideMode ?? (AutoGameFixes ? GameCompatibility.ForGameId(gameId).FixMode : FixMode.None);
    }

    /// <summary>Resolves and validates the configured ARM clock in hertz.</summary>
    /// <returns>The clock derived from <see cref="CpuClockMultiplier"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the multiplier is outside 0.5x-4x.</exception>
    public int ResolveArmClock()
    {
        if (!double.IsFinite(CpuClockMultiplier) || CpuClockMultiplier < 0.5 || CpuClockMultiplier > 4.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: CpuClockMultiplier,
                message: "The CPU clock multiplier must be between 0.5 and 4.0.");
        }

        return (int)Math.Round(EmulatorConstants.ArmClock * CpuClockMultiplier);
    }

    /// <summary>Creates a machine tuning record from the configured machine options.</summary>
    /// <returns>A validated tuning record with the configured level; the caller adds per-game fixes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the CPU clock multiplier is outside 0.5x-4x.</exception>
    /// <exception cref="MachineOptionValidationException">Thrown when a machine option is unknown.</exception>
    public EmulatorTuning CreateEmulatorTuning()
    {
        var tuning = new EmulatorTuning
        {
            ArmClock = ResolveArmClock(),
            Region = Region,
            ArmTiming = ArmTiming,
            ClioTimers = ClioTimers,
            CelTiming = CelTiming,
            CdromTiming = CdromTiming,
            CdSpeed = CdSpeed,
            MemoryConfiguration = MemoryConfiguration,
            RandomSeed = RandomSeed,
            CpuBackend = CpuBackend,
            HighResolution = HighResolution,
            OperaDipir = OperaDipir,
            OperaSoftReset = OperaSoftReset,
            SwiHle = SwiHle,
        };
        tuning.Validate();
        return tuning;
    }

    /// <summary>Applies a timing profile to the individual emulation options.</summary>
    /// <param name="profile">Profile to apply.</param>
    /// <remarks>
    /// <see cref="EmulationProfile.Custom"/> leaves the current options unchanged. The
    /// profile never changes the per-game fixes, high-resolution mode, or random seed.
    /// </remarks>
    public void ApplyProfile(EmulationProfile profile)
    {
        EmulationProfiles.Validate(profile);
        if (profile == EmulationProfile.Custom)
        {
            Profile = EmulationProfile.Custom;
            return;
        }

        EmulatorTuning applied = EmulationProfiles.Apply(profile);
        ArmTiming = applied.ArmTiming;
        ClioTimers = applied.ClioTimers;
        CelTiming = applied.CelTiming;
        CdromTiming = applied.CdromTiming;
        CdSpeed = applied.CdSpeed;
        CpuClockMultiplier = applied.ArmClock / (double)EmulatorConstants.ArmClock;
        Profile = profile;
    }

    /// <summary>Detects which timing profile the current individual options match.</summary>
    /// <returns>The matching profile, or <see cref="EmulationProfile.Custom"/>.</returns>
    public EmulationProfile DetectProfile()
    {
        var tuning = new EmulatorTuning
        {
            ArmTiming = ArmTiming,
            ClioTimers = ClioTimers,
            CelTiming = CelTiming,
            CdromTiming = CdromTiming,
            CdSpeed = CdSpeed,
            ArmClock = ResolveArmClock(),
        };
        return EmulationProfiles.Detect(tuning);
    }

    /// <summary>Gets the renderer settings derived from this configuration.</summary>
    /// <param name="gameId">Current disc game ID, or <see langword="null"/> to use the global filter.</param>
    /// <returns>A validated renderer settings record.</returns>
    public RendererSettings ToRendererSettings(string? gameId = null)
    {
        AppSettings effective = CreateEffectiveForGame(gameId);
        var settings = new RendererSettings
        {
            Filter = effective.ResolveFilter(gameId),
            Aspect = Aspect,
            CustomAspect = CustomAspect,
            // VRR presentation deliberately avoids a blocking swap interval; the
            // compositor loop and the display/driver own the cadence.
            Vsync = Vsync && !effective.Vrr,
            IntegerScale = IntegerScale,
            Bloom = Bloom,
            FsrSharpness = FsrSharpness,
            OutputResolution = OutputResolution,
        };
        settings.Validate();
        return settings;
    }
}
