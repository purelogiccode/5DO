using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FiveDo.Audio;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Host.Osd;
using FiveDo.Host.Rewind;
using FiveDo.Input;
using FiveDo.Storage;
using FiveDo.Video;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Edits presentation, audio, emulation, and path settings. Changes are applied by the caller
/// only after the dialog returns <see langword="true"/>.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly RendererOption[] _rendererOptions = CreateRendererOptions();

    private static RendererOption[] CreateRendererOptions()
    {
        return OperatingSystem.IsWindows()
            ?
            [
                new(VideoRendererKind.OpenGl, "OpenGL (recommended)"),
                new(VideoRendererKind.Direct3D11, "DirectX 11 (Windows)"),
                new(VideoRendererKind.Vulkan, "Vulkan (offscreen)"),
                new(VideoRendererKind.Software, "Software (CPU)"),
            ]
            :
            [
                new(VideoRendererKind.OpenGl, "OpenGL (recommended)"),
                new(VideoRendererKind.Vulkan, "Vulkan (offscreen)"),
                new(VideoRendererKind.Software, "Software (CPU)"),
            ];
    }

    private static readonly FrameSkipOption[] _frameSkipOptions =
    [
        new(FrameSkipMode.Auto, "Auto (recommended)"),
        new(FrameSkipMode.Off, "Off - present every frame"),
        new(FrameSkipMode.OneInTwo, "Skip 1 of 2 (30 fps)"),
        new(FrameSkipMode.OneInThree, "Skip 2 of 3 (20 fps)"),
        new(FrameSkipMode.OneInFour, "Skip 3 of 4 (15 fps)"),
        new(FrameSkipMode.OneInFive, "Skip 4 of 5 (12 fps)"),
    ];

    private static readonly DeinterlaceOption[] _deinterlaceOptions =
    [
        new(DeinterlaceMode.Auto, "Auto (detect 480i)"),
        new(DeinterlaceMode.Bob, "Bob - show each field"),
        new(DeinterlaceMode.Weave, "Weave - combine fields"),
        new(DeinterlaceMode.Blend, "Blend - average fields"),
    ];

    private static readonly DisplayRefreshRateOption[] _displayRefreshRateOptions =
    [
        new(AppSettings.MinDisplayRefreshRate, "30 Hz"),
        new(AppSettings.DefaultDisplayRefreshRate, "60 Hz (default)"),
        new(75, "75 Hz"),
        new(90, "90 Hz"),
        new(120, "120 Hz"),
        new(144, "144 Hz"),
        new(165, "165 Hz"),
        new(240, "240 Hz"),
        new(0, "Match display (unlimited)"),
    ];

    private static readonly PointerPortOption[] _pointerPortOptions =
    [
        new(0, "All pointer devices"),
        new(1, "Port 1"),
        new(2, "Port 2"),
        new(3, "Port 3"),
        new(4, "Port 4"),
        new(5, "Port 5"),
        new(6, "Port 6"),
        new(7, "Port 7"),
        new(8, "Port 8"),
    ];

    private static readonly RegionOverrideOption[] _regionOverrideOptions =
    [
        new(null, "Automatic (follow BIOS)"),
        new(VideoRegion.Ntsc, "NTSC (320 x 240, 60 Hz)"),
        new(VideoRegion.Pal1, "PAL1 (320 x 288, 50 Hz)"),
        new(VideoRegion.Pal2, "PAL2 (384 x 288, 50 Hz)"),
    ];

    private static readonly RegionOption[] _regionOptions =
    [
        new(VideoRegion.Ntsc, "NTSC (320 x 240, 60 Hz)"),
        new(VideoRegion.Pal1, "PAL1 (320 x 288, 50 Hz)"),
        new(VideoRegion.Pal2, "PAL2 (384 x 288, 50 Hz)"),
    ];

    private static readonly ProfileOption[] _profileOptions =
    [
        new(EmulationProfile.Compatible, "Compatible (FreeDO timing, default)"),
        new(EmulationProfile.Accuracy, "Accuracy (ARM60 + Opera timing)"),
        new(EmulationProfile.Performance, "Performance (1.5x CPU)"),
        new(EmulationProfile.Custom, "Custom (keep current options)"),
    ];

    private static readonly ArmTimingOption[] _armTimingOptions =
    [
        new(ArmTimingMode.FreeDoCompatible, "FreeDO-compatible (N=4, default)"),
        new(ArmTimingMode.Arm60Datasheet, "ARM60 datasheet (N=2, accuracy)"),
    ];

    private static readonly ClioTimerOption[] _clioTimerOptions =
    [
        new(ClioTimerMode.FreeDoCompatible, "FreeDO-compatible (all timers, default)"),
        new(ClioTimerMode.OperaScheduler, "Opera per-slot scheduler (accuracy)"),
    ];

    private static readonly CelTimingOption[] _celTimingOptions =
    [
        new(CelTimingMode.FreeDoCompatible, "FreeDO-compatible (synchronous, default)"),
        new(CelTimingMode.OperaBusy, "Opera busy timing (accuracy)"),
    ];

    private static readonly CdromTimingOption[] _cdromTimingOptions =
    [
        new(CdromTimingMode.FreeDoCompatible, "FreeDO-compatible (immediate, default)"),
        new(CdromTimingMode.OperaCycleBased, "Opera cycle-based (2x, accuracy)"),
    ];

    private static readonly CdSpeedOption[] _cdSpeedOptions =
    [
        new(CdSpeed.Unbounded, "Unbounded (no host limit)"),
        new(CdSpeed.X1, "1x (~150 KB/s)"),
        new(CdSpeed.X2, "2x (~300 KB/s, stock)"),
        new(CdSpeed.X4, "4x (~600 KB/s)"),
        new(CdSpeed.X8, "8x (~1200 KB/s)"),
        new(CdSpeed.X16, "16x (~2400 KB/s)"),
        new(CdSpeed.X24, "24x (~3600 KB/s)"),
        new(CdSpeed.X32, "32x (~4800 KB/s)"),
    ];

    private static readonly MemoryOption[] _memoryOptions =
    [
        new(MemoryConfiguration.Dram2MbVram1Mb, "2 MB DRAM / 1 MB VRAM (stock, 21)"),
        new(MemoryConfiguration.Dram2MbVram2Mb, "2 MB DRAM / 2 MB VRAM (22)"),
        new(MemoryConfiguration.Dram4MbVram1Mb, "4 MB DRAM / 1 MB VRAM (41)"),
        new(MemoryConfiguration.Dram4MbVram2Mb, "4 MB DRAM / 2 MB VRAM (42)"),
        new(MemoryConfiguration.Dram8MbVram1Mb, "8 MB DRAM / 1 MB VRAM (81)"),
        new(MemoryConfiguration.Dram8MbVram2Mb, "8 MB DRAM / 2 MB VRAM (82)"),
        new(MemoryConfiguration.Dram14MbVram2Mb, "14 MB DRAM / 2 MB VRAM (E2)"),
        new(MemoryConfiguration.Dram15MbVram1Mb, "15 MB DRAM / 1 MB VRAM (F1)"),
    ];

    private static readonly ActiveDevicesOption[] _activeDeviceOptions =
    [
        new(null, "Automatic (configured ports)"),
        new(0, "0 (no devices)"),
        new(1, "1"),
        new(2, "2"),
        new(3, "3"),
        new(4, "4"),
        new(5, "5"),
        new(6, "6"),
        new(7, "7"),
        new(8, "8 (all ports)"),
    ];

    private static readonly InputDeviceOption[] _inputDeviceOptions =
    [
        new(InputDeviceType.Joypad, "Joypad"),
        new(InputDeviceType.Flightstick, "Flightstick"),
        new(InputDeviceType.Mouse, "Mouse"),
        new(InputDeviceType.Lightgun, "Lightgun"),
        new(InputDeviceType.ArcadeLightgun, "Arcade lightgun"),
        new(InputDeviceType.OrbatakTrackball, "Orbatak trackball"),
        new(InputDeviceType.None, "None"),
    ];

    private static readonly NvramStorageOption[] _nvramStorageOptions =
    [
        new(NvramStorageMode.Global, "Shared (one image, default)"),
        new(NvramStorageMode.PerGame, "Per game (isolated saves)"),
    ];

    private static readonly OsdPositionOption[] _osdPositionOptions =
    [
        new(OsdPosition.TopLeft, "Top left"),
        new(OsdPosition.TopCenter, "Top center"),
        new(OsdPosition.TopRight, "Top right (default)"),
        new(OsdPosition.BottomLeft, "Bottom left"),
        new(OsdPosition.BottomCenter, "Bottom center"),
        new(OsdPosition.BottomRight, "Bottom right"),
    ];

    private static readonly ResolutionOption[] _resolutionOptions =
    [
        new(OutputResolution.Window, "Match window"),
        new(OutputResolution.Native, "Native 320 x 240"),
        new(OutputResolution.Scale2X, "2x 640 x 480"),
        new(OutputResolution.Scale3X, "3x 960 x 720"),
        new(OutputResolution.Scale4X, "4x 1280 x 960"),
        new(OutputResolution.Hd720, "HD 1280 x 720"),
        new(OutputResolution.FullHd1080, "Full HD 1920 x 1080"),
        new(OutputResolution.Qhd1440, "QHD 2560 x 1440"),
        new(OutputResolution.Uhd2160, "4K UHD 3840 x 2160"),
    ];

    private readonly string? _gameId;
    private readonly AppSettings _settings;
    private readonly List<string> _libraryFolders = [];
    private bool _initializing = true;

    /// <summary>Creates the dialog with the persisted settings.</summary>
    public SettingsWindow()
        : this(AppSettingsStore.Load(), null, null)
    {
    }

    /// <summary>Creates the dialog.</summary>
    /// <param name="settings">Current settings used to initialize the controls.</param>
    /// <param name="gameId">Current disc game ID, or <see langword="null"/>.</param>
    /// <param name="gameName">Current disc display name, or <see langword="null"/>.</param>
    public SettingsWindow(AppSettings settings, string? gameId, string? gameName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();
        _gameId = gameId;
        _settings = settings;
        _libraryFolders.AddRange(settings.LibraryFolders);

        RendererCombo.ItemsSource = _rendererOptions;
        RendererCombo.SelectedItem = _rendererOptions.FirstOrDefault(option => option.Value == settings.Renderer)
            ?? _rendererOptions[0];
        ResolutionCombo.ItemsSource = _resolutionOptions;
        ResolutionCombo.SelectedItem = _resolutionOptions.FirstOrDefault(option => option.Value == settings.OutputResolution)
            ?? _resolutionOptions[0];
        FilterCombo.ItemsSource = Enum.GetValues<VideoFilter>();
        FilterCombo.SelectedItem = settings.Filter;
        DeinterlaceCombo.ItemsSource = _deinterlaceOptions;
        DeinterlaceCombo.SelectedItem = _deinterlaceOptions.FirstOrDefault(
                option => option.Value == settings.Deinterlace)
            ?? _deinterlaceOptions[0];
        FilterOverrideCombo.ItemsSource = new object[] { "Automatic" }
            .Concat(Enum.GetValues<VideoFilter>().Cast<object>())
            .ToArray();
        FilterOverrideCombo.SelectedItem = settings.GetFilterOverride(gameId) is { } gameFilter
            ? gameFilter
            : "Automatic";
        AspectCombo.ItemsSource = Enum.GetValues<AspectMode>();
        AspectCombo.SelectedItem = settings.Aspect;
        CustomAspectBox.Text = settings.CustomAspect.ToString("0.###", CultureInfo.InvariantCulture);
        IntegerCheck.IsChecked = settings.IntegerScale;
        VsyncCheck.IsChecked = settings.Vsync;
        FrameSkipCombo.ItemsSource = _frameSkipOptions;
        FrameSkipCombo.SelectedItem = _frameSkipOptions.FirstOrDefault(option => option.Value == settings.FrameSkip)
            ?? _frameSkipOptions[0];
        DisplayRefreshRateCombo.ItemsSource = _displayRefreshRateOptions;
        DisplayRefreshRateCombo.SelectedItem = _displayRefreshRateOptions.FirstOrDefault(
                option => option.Value == settings.DisplayRefreshRate)
            ?? _displayRefreshRateOptions.First(
                option => option.Value == AppSettings.DefaultDisplayRefreshRate);
        CropInsets overscan = settings.ResolveOverscan();
        OverscanLeftBox.Text = overscan.Left.ToString(CultureInfo.InvariantCulture);
        OverscanTopBox.Text = overscan.Top.ToString(CultureInfo.InvariantCulture);
        OverscanRightBox.Text = overscan.Right.ToString(CultureInfo.InvariantCulture);
        OverscanBottomBox.Text = overscan.Bottom.ToString(CultureInfo.InvariantCulture);
        VideoBorderBox.Text = settings.VideoBorderPixels.ToString(CultureInfo.InvariantCulture);
        VoidAreaColorBox.Text = settings.ResolveVoidAreaColor();
        OverlayCheck.IsChecked = settings.ShowOverlay;
        InputPlayerCombo.ItemsSource = new[] { 1, 2, 3, 4, 5, 6 };
        InputPlayerCombo.SelectedItem = Math.Clamp(settings.InputDisplayPlayer, 1, 6);
        OsdCheck.IsChecked = settings.OsdEnabled;
        OsdPositionCombo.ItemsSource = _osdPositionOptions;
        OsdPositionCombo.SelectedItem = _osdPositionOptions.FirstOrDefault(option => option.Value == settings.OsdPosition)
            ?? _osdPositionOptions[2];
        OsdOpacitySlider.Value = Math.Clamp(settings.OsdOpacity, 0.1, 1);
        OsdDurationBox.Text = settings.OsdDurationMilliseconds.ToString(CultureInfo.InvariantCulture);
        QuickMenuCheck.IsChecked = settings.QuickMenuEnabled;
        NativeScreenshotCheck.IsChecked = settings.SaveNativeScreenshot;
        CheckUpdatesCheck.IsChecked = settings.CheckForUpdates;
        BloomSlider.Value = settings.Bloom;
        FsrSharpnessSlider.Value = settings.FsrSharpness;
        AudioCheck.IsChecked = settings.AudioEnabled;
        PopulateAudioDevices(settings.AudioDevice);
        AudioBufferBox.Text = settings.AudioBufferMilliseconds.ToString(CultureInfo.InvariantCulture);
        VolumeSlider.Value = settings.Volume;
        CdVolumeSlider.Value = Math.Clamp(settings.CdAudioVolume, 0, 1);
        CdMuteCheck.IsChecked = settings.CdAudioMuted;
        AutoSwapCheck.IsChecked = settings.AutoSwapOnEject;
        AutoFixesCheck.IsChecked = settings.AutoGameFixes;
        HighResCheck.IsChecked = settings.HighResolution;
        RegionCombo.ItemsSource = _regionOptions;
        RegionCombo.SelectedItem = _regionOptions.FirstOrDefault(option => option.Value == settings.Region)
            ?? _regionOptions[0];
        AutoRegionCheck.IsChecked = settings.AutoRegion;
        PointerPortCombo.ItemsSource = _pointerPortOptions;
        PointerPortCombo.SelectedItem = _pointerPortOptions.FirstOrDefault(
                option => option.Value == settings.PointerPort)
            ?? _pointerPortOptions[0];
        HideLightgunCrosshairCheck.IsChecked = settings.HideLightgunCrosshair;
        PointerScaleBox.Text = settings.PointerScale.ToString("0.00", CultureInfo.InvariantCulture);
        PointerOffsetXBox.Text = settings.PointerOffsetX.ToString(CultureInfo.InvariantCulture);
        PointerOffsetYBox.Text = settings.PointerOffsetY.ToString(CultureInfo.InvariantCulture);
        RegionOverrideCombo.ItemsSource = _regionOverrideOptions;
        RegionOverrideCombo.SelectedItem = _regionOverrideOptions.FirstOrDefault(
                option => option.Value == settings.GetRegionOverride(gameId))
            ?? _regionOverrideOptions[0];
        RegionOverrideCombo.IsEnabled = !string.IsNullOrWhiteSpace(gameId);
        ProfileCombo.ItemsSource = _profileOptions;
        ProfileCombo.SelectedItem = _profileOptions.FirstOrDefault(option => option.Value == settings.Profile)
            ?? _profileOptions[0];
        ArmTimingCombo.ItemsSource = _armTimingOptions;
        ArmTimingCombo.SelectedItem = _armTimingOptions.FirstOrDefault(option => option.Value == settings.ArmTiming)
            ?? _armTimingOptions[0];
        ClioTimersCombo.ItemsSource = _clioTimerOptions;
        ClioTimersCombo.SelectedItem = _clioTimerOptions.FirstOrDefault(option => option.Value == settings.ClioTimers)
            ?? _clioTimerOptions[0];
        CelTimingCombo.ItemsSource = _celTimingOptions;
        CelTimingCombo.SelectedItem = _celTimingOptions.FirstOrDefault(option => option.Value == settings.CelTiming)
            ?? _celTimingOptions[0];
        CdromTimingCombo.ItemsSource = _cdromTimingOptions;
        CdromTimingCombo.SelectedItem = _cdromTimingOptions.FirstOrDefault(option => option.Value == settings.CdromTiming)
            ?? _cdromTimingOptions[0];
        CdSpeedCombo.ItemsSource = _cdSpeedOptions;
        CdSpeedCombo.SelectedItem = _cdSpeedOptions.FirstOrDefault(option => option.Value == settings.CdSpeed)
            ?? _cdSpeedOptions[2];
        MemoryCombo.ItemsSource = _memoryOptions;
        MemoryCombo.SelectedItem = _memoryOptions.FirstOrDefault(
                option => option.Value == settings.MemoryConfiguration)
            ?? _memoryOptions[0];
        NvramStorageCombo.ItemsSource = _nvramStorageOptions;
        NvramStorageCombo.SelectedItem = _nvramStorageOptions.FirstOrDefault(option => option.Value == settings.NvramStorageMode)
            ?? _nvramStorageOptions[0];
        NvramVersionBox.Text = settings.NvramVersion.ToString(CultureInfo.InvariantCulture);
        CpuClockBox.Text = settings.CpuClockMultiplier.ToString("0.###", CultureInfo.InvariantCulture);
        RandomSeedBox.Text = settings.RandomSeed is { } seed ? $"0x{seed:x8}" : string.Empty;
        OperaDipirCheck.IsChecked = settings.OperaDipir;
        OperaSoftResetCheck.IsChecked = settings.OperaSoftReset;
        SwiHleCheck.IsChecked = settings.SwiHle;
        RewindCheck.IsChecked = settings.RewindEnabled;
        RewindSecondsBox.Text = settings.RewindSeconds.ToString(CultureInfo.InvariantCulture);
        FastForwardBox.Text = settings.FastForwardMultiplier.ToString(CultureInfo.InvariantCulture);
        PopulateInputDevices(settings, gameId);
        SaveDirectoryBox.Text = settings.SaveDirectory;
        ScreenshotDirectoryBox.Text = settings.ScreenshotDirectory;
        CoverDirectoryBox.Text = settings.CoverDirectory;
        CheatsDirectoryBox.Text = settings.CheatsDirectory;
        LibraryMetadataPathBox.Text = settings.LibraryMetadataPath;
        DiscDatPathBox.Text = settings.DiscDatPath;
        ShowDuplicateLibraryEntriesCheck.IsChecked = settings.ShowDuplicateLibraryEntries;
        RefreshLibraryFoldersList();
        PopulateConfigProfiles(PortablePaths.ProfileName);
        BiosFolderBox.Text = settings.BiosFolder;
        BiosPathBox.Text = settings.BiosPath;
        Bios2PathBox.Text = settings.Bios2Path;
        AutoBiosCheck.IsChecked = settings.AutoBios;
        BiosOverrideCombo.IsEnabled = !string.IsNullOrWhiteSpace(gameId);
        var biosFolder = settings.BiosFolder;
        if (string.IsNullOrWhiteSpace(biosFolder) && !string.IsNullOrWhiteSpace(settings.BiosPath))
        {
            try
            {
                biosFolder = Path.GetDirectoryName(Path.GetFullPath(settings.BiosPath));
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                Serilog.Log.Debug(exception, "Configured BIOS path could not be resolved in the settings dialog.");
            }
        }

        PopulateBiosOverrides(biosFolder, settings.GetBiosOverride(gameId));
        GameText.Text = string.IsNullOrWhiteSpace(gameId)
            ? "No disc loaded."
            : $"{gameName ?? "Unknown"} ({gameId[..Math.Min(8, gameId.Length)]})";

        FixMode? overrideMode = settings.GetFixModeOverride(gameId);
        FixModeBox.Text = overrideMode is null ? string.Empty : $"0x{(uint)overrideMode.Value:x}";
        _initializing = false;
        UpdateGameProfileStatus();
    }

    private void PopulateConfigProfiles(string? selectedName)
    {
        var options = new List<ConfigProfileOption>
        {
            new(null, "Default (shared)"),
        };
        foreach (var name in AppSettingsStore.ListProfiles())
        {
            options.Add(new ConfigProfileOption(name, name));
        }

        if (!string.IsNullOrWhiteSpace(selectedName)
            && !options.Exists(option => string.Equals(option.Name, selectedName, StringComparison.Ordinal)))
        {
            options.Add(new ConfigProfileOption(selectedName, $"{selectedName} (current)"));
        }

        ConfigProfileCombo.ItemsSource = options;
        ConfigProfileCombo.SelectedItem = options.FirstOrDefault(
                option => string.Equals(option.Name, selectedName, StringComparison.Ordinal))
            ?? options[0];
        UpdateProfileStatus();
    }

    private void UpdateProfileStatus()
    {
        var current = PortablePaths.ProfileName ?? "Default";
        var next = PortablePaths.GetDefaultProfile(PortablePaths.BaseDataDirectory) ?? "Default";
        ProfileStatusText.Text =
            $"Current: {current}. Next launch: {next}. Changing the profile requires a restart.";
    }

    private void OnSetDefaultProfileClick(object? sender, RoutedEventArgs e)
    {
        if (ConfigProfileCombo.SelectedItem is not ConfigProfileOption option)
        {
            return;
        }

        try
        {
            PortablePaths.SetDefaultProfile(option.Name);
            UpdateProfileStatus();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Could not set the default profile {Profile}.", option.Name);
            ProfileStatusText.Text = $"Could not set the default profile: {exception.Message}";
        }
    }

    private void OnCreateProfileClick(object? sender, RoutedEventArgs e)
    {
        var name = NewProfileBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ProfileStatusText.Text = "Enter a profile name first.";
            return;
        }

        try
        {
            AppSettings target = _settings.CreateEffectiveForGame(_gameId);
            AppSettingsStore.Save(target, AppSettingsStore.GetProfilePath(name));
            NewProfileBox.Text = string.Empty;
            PopulateConfigProfiles(PortablePaths.NormalizeProfileName(name));
            ProfileStatusText.Text = $"Created profile {PortablePaths.NormalizeProfileName(name)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Could not create profile {Profile}.", name);
            ProfileStatusText.Text = $"Could not create the profile: {exception.Message}";
        }
    }

    private void OnOpenProfilesFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(PortablePaths.ProfilesDirectory);
            Process.Start(new ProcessStartInfo(PortablePaths.ProfilesDirectory) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Serilog.Log.Debug(exception, "Could not open the profiles folder.");
            ProfileStatusText.Text = $"Could not open the profiles folder: {exception.Message}";
        }
    }

    private void OnImportProfileSettingsAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ImportProfileSettingsAsync, "importing profile settings");
    }

    private async Task ImportProfileSettingsAsync()
    {
        if (ConfigProfileCombo.SelectedItem is not ConfigProfileOption { Name: { } name })
        {
            ProfileStatusText.Text = "Select a named profile before importing settings.";
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import profile settings",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("5DO settings") { Patterns = ["*.json"] },
            ],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            AppSettings imported = AppSettingsStore.Load(path);
            AppSettingsStore.Save(imported, AppSettingsStore.GetProfilePath(name));
            PopulateConfigProfiles(name);
            ProfileStatusText.Text = $"Imported {Path.GetFileName(path)} into {name}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Profile settings import failed for {Path}.", path);
            ProfileStatusText.Text = $"Import failed: {exception.Message}";
        }
    }

    private void OnExportProfileSettingsAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ExportProfileSettingsAsync, "exporting profile settings");
    }

    private async Task ExportProfileSettingsAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export settings",
            SuggestedFileName = "5do-settings.json",
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("5DO settings") { Patterns = ["*.json"] },
            ],
        }).ConfigureAwait(true);
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            AppSettingsStore.Save(_settings, path);
            ProfileStatusText.Text = $"Exported settings to {Path.GetFileName(path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Profile settings export failed for {Path}.", path);
            ProfileStatusText.Text = $"Export failed: {exception.Message}";
        }
    }

    private void OnSaveGameProfileClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            return;
        }

        GameProfile profile = CaptureProfile();
        _settings.SetGameProfile(_gameId, profile);
        UpdateGameProfileStatus();
    }

    private void OnDeleteGameProfileClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            return;
        }

        _settings.SetGameProfile(_gameId, null);
        UpdateGameProfileStatus();
    }

    private void OnImportGameProfileAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ImportGameProfileAsync, "importing a game profile");
    }

    private async Task ImportGameProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import game profile",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("5DO game profile") { Patterns = ["*.json"] },
            ],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            GameProfile profile = GameProfileJson.Deserialize(await File.ReadAllTextAsync(path).ConfigureAwait(true));
            _settings.SetGameProfile(_gameId, profile);
            LoadProfileIntoControls(profile);
            UpdateGameProfileStatus();
            GameProfileText.Text = $"Imported {Path.GetFileName(path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Game profile import failed for {Path}.", path);
            GameProfileText.Text = $"Import failed: {exception.Message}";
        }
    }

    private void OnExportGameProfileAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ExportGameProfileAsync, "exporting a game profile");
    }

    private async Task ExportGameProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export game profile",
            SuggestedFileName = "5do-game-profile.json",
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("5DO game profile") { Patterns = ["*.json"] },
            ],
        }).ConfigureAwait(true);
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            GameProfile profile = _settings.GetGameProfile(_gameId) ?? CaptureProfile();
            await File.WriteAllTextAsync(path, GameProfileJson.Serialize(profile)).ConfigureAwait(true);
            GameProfileText.Text = $"Exported {Path.GetFileName(path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Game profile export failed for {Path}.", path);
            GameProfileText.Text = $"Export failed: {exception.Message}";
        }
    }

    private GameProfile CaptureProfile()
    {
        GameProfile profile = GameProfile.FromSettings(_settings, _gameId);
        profile.BiosMd5 = BiosOverrideCombo.SelectedItem is BiosOverrideOption { Md5: { } biosMd5 }
            ? biosMd5
            : string.Empty;
        profile.FixMode = null;
        var fixText = FixModeBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(fixText) && TryParseFixMode(fixText, out FixMode fixMode))
        {
            profile.FixMode = fixMode;
        }

        profile.Filter = FilterOverrideCombo.SelectedItem is VideoFilter filter ? filter : null;
        profile.Deinterlace = DeinterlaceCombo.SelectedItem is DeinterlaceOption deinterlace
            ? deinterlace.Value
            : _settings.Deinterlace;
        profile.HighResolution = HighResCheck.IsChecked;
        profile.InputDevices = PerGameInputCheck.IsChecked == true
            ? GetInputDeviceNames()
            : [];
        profile.NvramStorageMode = NvramStorageCombo.SelectedItem is NvramStorageOption nvramStorage
            ? nvramStorage.Value
            : null;
        if (int.TryParse(
                NvramVersionBox.Text?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var nvramVersion)
            && nvramVersion is >= NvramLayout.MinVersion and <= NvramLayout.MaxVersion)
        {
            profile.NvramVersion = nvramVersion;
        }

        profile.Profile = ProfileCombo.SelectedItem is ProfileOption profileOption
            ? profileOption.Value
            : EmulationProfile.Custom;
        profile.Region = RegionCombo.SelectedItem is RegionOption region
            ? region.Value
            : VideoRegion.Ntsc;
        profile.ArmTiming = ArmTimingCombo.SelectedItem is ArmTimingOption armTiming
            ? armTiming.Value
            : ArmTimingMode.FreeDoCompatible;
        profile.ClioTimers = ClioTimersCombo.SelectedItem is ClioTimerOption clioTimers
            ? clioTimers.Value
            : ClioTimerMode.FreeDoCompatible;
        profile.CelTiming = CelTimingCombo.SelectedItem is CelTimingOption celTiming
            ? celTiming.Value
            : CelTimingMode.FreeDoCompatible;
        profile.CdromTiming = CdromTimingCombo.SelectedItem is CdromTimingOption cdromTiming
            ? cdromTiming.Value
            : CdromTimingMode.FreeDoCompatible;
        profile.CdSpeed = CdSpeedCombo.SelectedItem is CdSpeedOption cdSpeed
            ? cdSpeed.Value
            : CdSpeed.X2;
        profile.MemoryConfiguration = MemoryCombo.SelectedItem is MemoryOption memory
            ? memory.Value
            : MemoryConfiguration.Dram2MbVram1Mb;
        if (double.TryParse(
                CpuClockBox.Text?.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var clock)
            && double.IsFinite(clock)
            && clock is >= 0.5 and <= 4.0)
        {
            profile.CpuClockMultiplier = clock;
        }

        try
        {
            profile.RandomSeed = ParseRandomSeedText(RandomSeedBox.Text);
        }
        catch (FormatException)
        {
            // Saving a profile must not crash on an unfinished seed edit; the main
            // settings save reports the validation error.
            profile.RandomSeed = _settings.RandomSeed;
        }

        profile.OperaDipir = OperaDipirCheck.IsChecked == true;
        profile.OperaSoftReset = OperaSoftResetCheck.IsChecked == true;
        profile.SwiHle = SwiHleCheck.IsChecked == true;
        profile.CdAudioVolume = (float)Math.Clamp(CdVolumeSlider.Value, 0, 1);
        profile.CdAudioMuted = CdMuteCheck.IsChecked == true;
        return profile;
    }

    private void LoadProfileIntoControls(GameProfile profile)
    {
        _initializing = true;
        try
        {
            ProfileCombo.SelectedItem = _profileOptions.FirstOrDefault(
                    option => option.Value == profile.Profile)
                ?? _profileOptions[^1];
            RegionCombo.SelectedItem = _regionOptions.FirstOrDefault(
                    option => option.Value == profile.Region)
                ?? _regionOptions[0];
            ArmTimingCombo.SelectedItem = _armTimingOptions.First(
                option => option.Value == profile.ArmTiming);
            ClioTimersCombo.SelectedItem = _clioTimerOptions.First(
                option => option.Value == profile.ClioTimers);
            CelTimingCombo.SelectedItem = _celTimingOptions.First(
                option => option.Value == profile.CelTiming);
            CdromTimingCombo.SelectedItem = _cdromTimingOptions.First(
                option => option.Value == profile.CdromTiming);
            CdSpeedCombo.SelectedItem = _cdSpeedOptions.First(
                option => option.Value == profile.CdSpeed);
            MemoryCombo.SelectedItem = _memoryOptions.First(
                option => option.Value == profile.MemoryConfiguration);
            CpuClockBox.Text = profile.CpuClockMultiplier.ToString("0.###", CultureInfo.InvariantCulture);
            RandomSeedBox.Text = profile.RandomSeed is { } seed ? $"0x{seed:x8}" : string.Empty;
            OperaDipirCheck.IsChecked = profile.OperaDipir;
            OperaSoftResetCheck.IsChecked = profile.OperaSoftReset;
            SwiHleCheck.IsChecked = profile.SwiHle;
            CdVolumeSlider.Value = Math.Clamp(profile.CdAudioVolume, 0, 1);
            CdMuteCheck.IsChecked = profile.CdAudioMuted;
            SelectBiosOverride(profile.BiosMd5);
            FixModeBox.Text = profile.FixMode is { } fix
                ? $"0x{(uint)fix:x}"
                : string.Empty;
            FilterOverrideCombo.SelectedItem = profile.Filter is { } filter
                ? filter
                : "Automatic";
            DeinterlaceCombo.SelectedItem = _deinterlaceOptions.FirstOrDefault(
                    option => option.Value == (profile.Deinterlace ?? _settings.Deinterlace))
                ?? _deinterlaceOptions[0];
            HighResCheck.IsChecked = profile.HighResolution ?? _settings.HighResolution;
            if (profile.InputDevices.Count > 0)
            {
                SetInputDevices(profile.InputDevices.ToArray());
            }
            else
            {
                PopulateInputDevices(_settings, _gameId);
            }

            NvramStorageCombo.SelectedItem = _nvramStorageOptions.FirstOrDefault(
                    option => option.Value == (profile.NvramStorageMode ?? _settings.NvramStorageMode))
                ?? _nvramStorageOptions[0];
            NvramVersionBox.Text = (profile.NvramVersion ?? _settings.NvramVersion)
                .ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _initializing = false;
        }
    }

    private void SelectBiosOverride(string? md5)
    {
        var options = (BiosOverrideCombo.ItemsSource as IEnumerable<BiosOverrideOption>)?.ToList()
            ?? [new BiosOverrideOption(null, "Automatic (region-based)")];
        if (!string.IsNullOrWhiteSpace(md5)
            && !options.Exists(option => string.Equals(option.Md5, md5, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new BiosOverrideOption(md5, $"Saved override ({md5[..Math.Min(8, md5.Length)]})"));
        }

        BiosOverrideCombo.ItemsSource = options;
        BiosOverrideCombo.SelectedItem = options.FirstOrDefault(
                option => string.Equals(option.Md5, md5, StringComparison.OrdinalIgnoreCase))
            ?? options[0];
    }

    private string[] GetInputDeviceNames()
    {
        ComboBox[] combos =
        [
            InputDevice1Combo,
            InputDevice2Combo,
            InputDevice3Combo,
            InputDevice4Combo,
            InputDevice5Combo,
            InputDevice6Combo,
            InputDevice7Combo,
            InputDevice8Combo,
        ];
        var devices = new string[combos.Length];
        for (var i = 0; i < combos.Length; i++)
        {
            devices[i] = (combos[i].SelectedItem is InputDeviceOption option
                    ? option.Value
                    : InputDeviceType.Joypad)
                .ToString();
        }

        return devices;
    }

    private void SetInputDevices(string[] devices)
    {
        ComboBox[] combos =
        [
            InputDevice1Combo,
            InputDevice2Combo,
            InputDevice3Combo,
            InputDevice4Combo,
            InputDevice5Combo,
            InputDevice6Combo,
            InputDevice7Combo,
            InputDevice8Combo,
        ];
        for (var i = 0; i < combos.Length; i++)
        {
            InputDeviceType device = i < devices.Length
                && Enum.TryParse(devices[i], ignoreCase: true, out InputDeviceType parsed)
                    ? parsed
                    : InputDeviceType.Joypad;
            combos[i].ItemsSource = _inputDeviceOptions;
            combos[i].SelectedItem = _inputDeviceOptions.FirstOrDefault(option => option.Value == device)
                ?? _inputDeviceOptions[0];
        }

        PerGameInputCheck.IsChecked = true;
        PerGameInputCheck.IsEnabled = !string.IsNullOrWhiteSpace(_gameId);
    }

    private void UpdateGameProfileStatus()
    {
        bool hasGame = !string.IsNullOrWhiteSpace(_gameId);
        hasGame = hasGame && _settings.GetGameProfile(_gameId) is not null;
        SaveGameProfileButton.IsEnabled = !string.IsNullOrWhiteSpace(_gameId);
        DeleteGameProfileButton.IsEnabled = hasGame;
        ImportGameProfileButton.IsEnabled = !string.IsNullOrWhiteSpace(_gameId);
        ExportGameProfileButton.IsEnabled = !string.IsNullOrWhiteSpace(_gameId);
        GameProfileText.Text = string.IsNullOrWhiteSpace(_gameId)
            ? "Open a disc to store a per-game profile."
            : hasGame
                ? "A profile is stored for this game. Applying it requires the next machine creation."
                : "No profile stored; Save captures the current timing/CD-DA options.";
    }

    private void OnAudioDeviceRefreshClick(object? sender, RoutedEventArgs e)
    {
        PopulateAudioDevices(
            (AudioDeviceCombo.SelectedItem as AudioDeviceOption)?.Name);
    }

    private void PopulateAudioDevices(string? selectedDevice)
    {
        var options = new List<AudioDeviceOption>
        {
            new(null, "Default device"),
        };
        foreach (var device in OpenAlAudioOutput.EnumerateDevices())
        {
            if (!options.Exists(option => string.Equals(option.Name, device, StringComparison.Ordinal)))
            {
                options.Add(new AudioDeviceOption(device, device));
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedDevice)
            && !options.Exists(option => string.Equals(option.Name, selectedDevice, StringComparison.Ordinal)))
        {
            options.Add(new AudioDeviceOption(selectedDevice, $"{selectedDevice} (saved)"));
        }

        AudioDeviceCombo.ItemsSource = options;
        AudioDeviceCombo.SelectedItem = options.FirstOrDefault(
                option => string.Equals(option.Name, selectedDevice, StringComparison.Ordinal))
            ?? options[0];
    }

    private void OnProfileChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializing
            || ProfileCombo.SelectedItem is not ProfileOption option
            || option.Value == EmulationProfile.Custom)
        {
            return;
        }

        var preview = new AppSettings();
        preview.ApplyProfile(option.Value);
        ArmTimingCombo.SelectedItem = _armTimingOptions.First(item => item.Value == preview.ArmTiming);
        ClioTimersCombo.SelectedItem = _clioTimerOptions.First(item => item.Value == preview.ClioTimers);
        CelTimingCombo.SelectedItem = _celTimingOptions.First(item => item.Value == preview.CelTiming);
        CdromTimingCombo.SelectedItem = _cdromTimingOptions.First(item => item.Value == preview.CdromTiming);
        CdSpeedCombo.SelectedItem = _cdSpeedOptions.First(item => item.Value == preview.CdSpeed);
        CpuClockBox.Text = preview.CpuClockMultiplier.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private void OnTestAudioClickAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(TestAudioAsync, "testing audio output");
    }

    private async Task TestAudioAsync()
    {
        TestAudioButton.IsEnabled = false;
        TestAudioText.Text = "Playing a 1 s 440 Hz tone...";
        OpenAlAudioOutput? output = OpenAlAudioOutput.TryCreate(
            100,
            (AudioDeviceCombo.SelectedItem as AudioDeviceOption)?.Name,
            out var error);
        if (output is null)
        {
            TestAudioText.Text = $"OpenAL is unavailable: {error ?? "unknown error"}.";
            TestAudioButton.IsEnabled = true;
            return;
        }

        try
        {
            output.Volume = (float)Math.Clamp(VolumeSlider.Value, 0, 1);
            await Task.Run(() =>
            {
                for (var frame = 0; frame < 60; frame++)
                {
                    for (var i = 0; i < 735; i++)
                    {
                        var phase = (frame * 735 + i) * 2.0 * Math.PI * 440.0 / 44_100.0;
                        var value = (short)(Math.Sin(phase) * 6000.0);
                        // The await completes before the finally disposes the output.
                        // ReSharper disable once AccessToDisposedClosure
                        output.PushSample(new AudioSample(value, value).Packed);
                    }

                    Thread.Sleep(16);
                }
            }).ConfigureAwait(false);
            TestAudioText.Text = "Test tone finished.";
        }
        catch (Exception exception)
        {
            Serilog.Log.Warning(exception, "Audio test tone failed.");
            TestAudioText.Text = $"Test tone failed: {exception.Message}";
        }
        finally
        {
            output.Dispose();
            TestAudioButton.IsEnabled = true;
        }
    }

    private static int ParseCrop(TextBox box, string label)
    {
        var text = box.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value is < 0 or > CropInsets.MaxPixels)
        {
            throw new FormatException($"{label} must be between 0 and {CropInsets.MaxPixels} pixels.");
        }

        return value;
    }

    private static int ParseBorder(TextBox box)
    {
        var text = box.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value is < 0 or > 64)
        {
            throw new FormatException("The void border must be between 0 and 64 pixels.");
        }

        return value;
    }

    /// <summary>Copies the edited values into a settings object.</summary>
    /// <param name="settings">Destination settings.</param>
    /// <exception cref="FormatException">Thrown when the fix-mode override is not hexadecimal.</exception>
    public void ApplyTo(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Filter = FilterCombo.SelectedItem is VideoFilter filter ? filter : VideoFilter.SharpBilinear;
        settings.Deinterlace = DeinterlaceCombo.SelectedItem is DeinterlaceOption deinterlace
            ? deinterlace.Value
            : DeinterlaceMode.Auto;
        settings.Aspect = AspectCombo.SelectedItem is AspectMode aspect ? aspect : AspectMode.FourThree;
        if (!double.TryParse(
                CustomAspectBox.Text?.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var customAspect)
            || !double.IsFinite(customAspect)
            || customAspect <= 0)
        {
            throw new FormatException("The custom aspect ratio must be a positive number such as 1.333 or 1.6.");
        }

        settings.CustomAspect = customAspect;
        settings.Renderer = RendererCombo.SelectedItem is RendererOption renderer
            ? renderer.Value
            : VideoRendererKind.OpenGl;
        settings.OutputResolution = ResolutionCombo.SelectedItem is ResolutionOption resolution
            ? resolution.Value
            : OutputResolution.Window;
        settings.IntegerScale = IntegerCheck.IsChecked == true;
        settings.Vsync = VsyncCheck.IsChecked == true;
        settings.FrameSkip = FrameSkipCombo.SelectedItem is FrameSkipOption frameSkip
            ? frameSkip.Value
            : FrameSkipMode.Auto;
        settings.DisplayRefreshRate = DisplayRefreshRateCombo.SelectedItem is DisplayRefreshRateOption refreshRate
            ? refreshRate.Value
            : AppSettings.DefaultDisplayRefreshRate;
        settings.SetOverscan(new CropInsets(
            ParseCrop(OverscanLeftBox, "Left overscan"),
            ParseCrop(OverscanTopBox, "Top overscan"),
            ParseCrop(OverscanRightBox, "Right overscan"),
            ParseCrop(OverscanBottomBox, "Bottom overscan")));
        settings.VideoBorderPixels = ParseBorder(VideoBorderBox);
        var voidColor = VoidAreaColorBox.Text?.Trim();
        if (!AppSettings.IsValidColor(voidColor))
        {
            throw new FormatException("The void color must be #RRGGBB or #AARRGGBB.");
        }

        settings.VoidAreaColor = voidColor!;
        settings.ShowOverlay = OverlayCheck.IsChecked == true;
        settings.InputDisplayPlayer = InputPlayerCombo.SelectedItem is int inputPlayer
            ? inputPlayer
            : 1;
        settings.OsdEnabled = OsdCheck.IsChecked == true;
        settings.OsdPosition = OsdPositionCombo.SelectedItem is OsdPositionOption osdPosition
            ? osdPosition.Value
            : OsdPosition.TopRight;
        settings.OsdOpacity = Math.Clamp(OsdOpacitySlider.Value, 0.1, 1);
        var osdDurationText = OsdDurationBox.Text?.Trim();
        if (string.IsNullOrEmpty(osdDurationText))
        {
            settings.OsdDurationMilliseconds = 2200;
        }
        else if (!int.TryParse(osdDurationText, NumberStyles.None, CultureInfo.InvariantCulture, out var osdDuration)
            || osdDuration is < OsdOptions.MinDurationMilliseconds or > OsdOptions.MaxDurationMilliseconds)
        {
            throw new FormatException(
                $"The OSD duration must be between {OsdOptions.MinDurationMilliseconds} and {OsdOptions.MaxDurationMilliseconds} milliseconds.");
        }
        else
        {
            settings.OsdDurationMilliseconds = osdDuration;
        }

        settings.QuickMenuEnabled = QuickMenuCheck.IsChecked == true;
        settings.SaveNativeScreenshot = NativeScreenshotCheck.IsChecked == true;
        settings.CheckForUpdates = CheckUpdatesCheck.IsChecked == true;
        settings.Bloom = (float)BloomSlider.Value;
        settings.FsrSharpness = (float)FsrSharpnessSlider.Value;
        settings.AudioEnabled = AudioCheck.IsChecked == true;
        settings.AudioDevice = AudioDeviceCombo.SelectedItem is AudioDeviceOption { Name: { } device }
            ? device
            : string.Empty;
        var audioBufferText = AudioBufferBox.Text?.Trim();
        if (string.IsNullOrEmpty(audioBufferText))
        {
            settings.AudioBufferMilliseconds = 100;
        }
        else if (!int.TryParse(
                     audioBufferText,
                     NumberStyles.Integer,
                     CultureInfo.InvariantCulture,
                     out var audioBufferMilliseconds)
                 || audioBufferMilliseconds is < 20 or > 500)
        {
            throw new FormatException("The audio buffer latency must be between 20 and 500 milliseconds.");
        }
        else
        {
            settings.AudioBufferMilliseconds = audioBufferMilliseconds;
        }

        settings.Volume = (float)VolumeSlider.Value;
        settings.CdAudioVolume = (float)Math.Clamp(CdVolumeSlider.Value, 0, 1);
        settings.CdAudioMuted = CdMuteCheck.IsChecked == true;
        settings.AutoSwapOnEject = AutoSwapCheck.IsChecked == true;
        settings.AutoGameFixes = AutoFixesCheck.IsChecked == true;
        settings.HighResolution = HighResCheck.IsChecked == true;
        settings.ArmTiming = ArmTimingCombo.SelectedItem is ArmTimingOption armTiming
            ? armTiming.Value
            : ArmTimingMode.FreeDoCompatible;
        settings.ClioTimers = ClioTimersCombo.SelectedItem is ClioTimerOption clioTimers
            ? clioTimers.Value
            : ClioTimerMode.FreeDoCompatible;
        settings.CelTiming = CelTimingCombo.SelectedItem is CelTimingOption celTiming
            ? celTiming.Value
            : CelTimingMode.FreeDoCompatible;
        settings.CdromTiming = CdromTimingCombo.SelectedItem is CdromTimingOption cdromTiming
            ? cdromTiming.Value
            : CdromTimingMode.FreeDoCompatible;
        settings.CdSpeed = CdSpeedCombo.SelectedItem is CdSpeedOption cdSpeed
            ? cdSpeed.Value
            : CdSpeed.X2;
        settings.MemoryConfiguration = MemoryCombo.SelectedItem is MemoryOption memory
            ? memory.Value
            : MemoryConfiguration.Dram2MbVram1Mb;
        SaveInputDevices(settings);
        settings.NvramStorageMode = NvramStorageCombo.SelectedItem is NvramStorageOption nvramStorage
            ? nvramStorage.Value
            : NvramStorageMode.Global;
        var nvramVersionText = NvramVersionBox.Text?.Trim();
        if (string.IsNullOrEmpty(nvramVersionText))
        {
            settings.NvramVersion = NvramLayout.MinVersion;
        }
        else if (!int.TryParse(nvramVersionText, NumberStyles.None, CultureInfo.InvariantCulture, out var nvramVersion)
            || nvramVersion is < NvramLayout.MinVersion or > NvramLayout.MaxVersion)
        {
            throw new FormatException(
                $"The NVRAM version must be between {NvramLayout.MinVersion} and {NvramLayout.MaxVersion}.");
        }
        else
        {
            settings.NvramVersion = nvramVersion;
        }

        settings.RewindEnabled = RewindCheck.IsChecked == true;
        var rewindSecondsText = RewindSecondsBox.Text?.Trim();
        if (string.IsNullOrEmpty(rewindSecondsText))
        {
            settings.RewindSeconds = 60;
        }
        else if (!int.TryParse(
                     rewindSecondsText,
                     NumberStyles.Integer,
                     CultureInfo.InvariantCulture,
                     out var rewindSeconds)
                 || rewindSeconds is < RewindOptions.MinSeconds or > RewindOptions.MaxSeconds)
        {
            throw new FormatException(
                $"The rewind history must be between {RewindOptions.MinSeconds} and {RewindOptions.MaxSeconds} seconds.");
        }
        else
        {
            settings.RewindSeconds = rewindSeconds;
        }

        var fastForwardText = FastForwardBox.Text?.Trim();
        if (string.IsNullOrEmpty(fastForwardText))
        {
            settings.FastForwardMultiplier = 4;
        }
        else if (!int.TryParse(
                     fastForwardText,
                     NumberStyles.Integer,
                     CultureInfo.InvariantCulture,
                     out var fastForwardMultiplier)
                 || fastForwardMultiplier is < 2 or > 10)
        {
            throw new FormatException("The fast-forward multiplier must be between 2 and 10.");
        }
        else
        {
            settings.FastForwardMultiplier = fastForwardMultiplier;
        }

        settings.Region = RegionCombo.SelectedItem is RegionOption regionOption
            ? regionOption.Value
            : VideoRegion.Ntsc;
        settings.AutoRegion = AutoRegionCheck.IsChecked == true;
        settings.PointerPort = PointerPortCombo.SelectedItem is PointerPortOption pointerPort
            ? pointerPort.Value
            : 0;
        settings.HideLightgunCrosshair = HideLightgunCrosshairCheck.IsChecked == true;
        settings.PointerScale = ParsePointerNumber(
            PointerScaleBox.Text,
            "pointer scale",
            0.5,
            2.0);
        settings.PointerOffsetX = (int)ParsePointerNumber(
            PointerOffsetXBox.Text,
            "pointer X offset",
            -160,
            160);
        settings.PointerOffsetY = (int)ParsePointerNumber(
            PointerOffsetYBox.Text,
            "pointer Y offset",
            -120,
            120);
        if (!string.IsNullOrWhiteSpace(_gameId))
        {
            settings.SetRegionOverride(
                _gameId,
                RegionOverrideCombo.SelectedItem is RegionOverrideOption { Value: { } region }
                    ? region
                    : null);
        }
        EmulationProfile selectedProfile = ProfileCombo.SelectedItem is ProfileOption profileOption
            ? profileOption.Value
            : EmulationProfile.Custom;
        if (!double.TryParse(
                CpuClockBox.Text?.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var cpuClockMultiplier)
            || !double.IsFinite(cpuClockMultiplier)
            || cpuClockMultiplier < 0.5
            || cpuClockMultiplier > 4.0)
        {
            throw new FormatException("The CPU clock multiplier must be between 0.5 and 4.0.");
        }

        settings.CpuClockMultiplier = cpuClockMultiplier;
        settings.Profile = selectedProfile != EmulationProfile.Custom
            && settings.DetectProfile() == selectedProfile
                ? selectedProfile
                : EmulationProfile.Custom;
        settings.RandomSeed = ParseRandomSeedText(RandomSeedBox.Text);
        settings.OperaDipir = OperaDipirCheck.IsChecked == true;
        settings.OperaSoftReset = OperaSoftResetCheck.IsChecked == true;
        settings.SwiHle = SwiHleCheck.IsChecked == true;

        settings.SaveDirectory = SaveDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.ScreenshotDirectory = ScreenshotDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.CoverDirectory = CoverDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.CheatsDirectory = CheatsDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.LibraryMetadataPath = LibraryMetadataPathBox.Text?.Trim() ?? string.Empty;
        settings.DiscDatPath = DiscDatPathBox.Text?.Trim() ?? string.Empty;
        settings.ShowDuplicateLibraryEntries = ShowDuplicateLibraryEntriesCheck.IsChecked == true;
        settings.LibraryFolders.Clear();
        foreach (var folder in _libraryFolders)
        {
            settings.LibraryFolders.Add(folder);
        }
        settings.BiosFolder = BiosFolderBox.Text?.Trim() ?? string.Empty;
        settings.BiosPath = BiosPathBox.Text?.Trim() ?? string.Empty;
        settings.Bios2Path = Bios2PathBox.Text?.Trim() ?? string.Empty;
        settings.AutoBios = AutoBiosCheck.IsChecked == true;

        if (!string.IsNullOrWhiteSpace(_gameId))
        {
            settings.SetBiosOverride(
                _gameId,
                BiosOverrideCombo.SelectedItem is BiosOverrideOption { Md5: { } biosMd5 } ? biosMd5 : null);
            settings.SetFilterOverride(
                _gameId,
                FilterOverrideCombo.SelectedItem is VideoFilter filterOverride ? filterOverride : null);

            var text = FixModeBox.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                settings.SetFixModeOverride(_gameId, null);
            }
            else if (TryParseFixMode(text, out FixMode mode))
            {
                settings.SetFixModeOverride(_gameId, mode);
            }
            else
            {
                throw new FormatException("The fix-mode override must be a hexadecimal mask such as 0x20.");
            }
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(LibraryFolderBox.Text))
        {
            AddLibraryFoldersFromBox();
            if (!string.IsNullOrWhiteSpace(LibraryFolderBox.Text))
            {
                return;
            }
        }

        try
        {
            ApplyTo(new AppSettings());
        }
        catch (FormatException exception)
        {
            Serilog.Log.Debug(exception, "Settings validation failed.");
            ValidationText.Text = exception.Message;
            return;
        }

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void RefreshLibraryFoldersList()
    {
        LibraryFoldersList.ItemsSource = null;
        LibraryFoldersList.ItemsSource = _libraryFolders.ToArray();
    }

    private void OnAddLibraryFolderClick(object? sender, RoutedEventArgs e)
    {
        AddLibraryFoldersFromBox();
    }

    private void OnLibraryFolderKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        AddLibraryFoldersFromBox();
        e.Handled = true;
    }

    private void OnRemoveLibraryFolderClick(object? sender, RoutedEventArgs e)
    {
        if (LibraryFoldersList.SelectedItem is not string selected)
        {
            ValidationText.Text = "Select a game folder to remove.";
            return;
        }

        _libraryFolders.RemoveAll(folder =>
            string.Equals(folder, selected, StringComparison.OrdinalIgnoreCase));
        RefreshLibraryFoldersList();
        ValidationText.Text = "Game folder removed.";
    }

    private void OnBrowseLibraryFolderAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseLibraryFolderAsync, "selecting a game folder");
    }

    private async Task BrowseLibraryFolderAsync()
    {
        var start = _libraryFolders.Count > 0 ? _libraryFolders[0] : LibraryFolderBox.Text;
        var path = await PickFolderAsync("Add a game folder", start).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        LibraryFolderBox.Text = path;
        AddLibraryFoldersFromBox();
    }

    private void AddLibraryFoldersFromBox()
    {
        var text = LibraryFolderBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            ValidationText.Text = "Enter a folder path before adding it.";
            return;
        }

        var invalid = new List<string>();
        var added = 0;
        foreach (var candidate in text.Split(
                     [';', '\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var normalized = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(candidate.Trim().Trim('"')));
                if (!Directory.Exists(normalized))
                {
                    invalid.Add(candidate);
                    continue;
                }

                if (_libraryFolders.Exists(folder =>
                        string.Equals(folder, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _libraryFolders.Add(normalized);
                added++;
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                Serilog.Log.Debug(exception, "Game folder {Folder} could not be resolved.", candidate);
                invalid.Add(candidate);
            }
        }

        if (added > 0)
        {
            LibraryFolderBox.Text = string.Empty;
            RefreshLibraryFoldersList();
            ValidationText.Text = added == 1 ? "Game folder added." : $"{added} game folders added.";
        }

        if (invalid.Count > 0)
        {
            ValidationText.Text = $"Folder not found: {string.Join(", ", invalid)}";
        }
    }

    private void OnBrowseSaveDirectoryAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseSaveDirectoryAsync, "selecting the save-state directory");
    }

    private async Task BrowseSaveDirectoryAsync()
    {
        var path = await PickFolderAsync("Select the save-state directory", SaveDirectoryBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            SaveDirectoryBox.Text = path;
        }
    }

    private void OnBrowseScreenshotDirectoryAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseScreenshotDirectoryAsync, "selecting the screenshot directory");
    }

    private async Task BrowseScreenshotDirectoryAsync()
    {
        var path = await PickFolderAsync("Select the screenshot directory", ScreenshotDirectoryBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            ScreenshotDirectoryBox.Text = path;
        }
    }

    private void OnBrowseCoverDirectoryAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseCoverDirectoryAsync, "selecting the cover-art directory");
    }

    private async Task BrowseCoverDirectoryAsync()
    {
        var path = await PickFolderAsync("Select the game cover directory", CoverDirectoryBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            CoverDirectoryBox.Text = path;
        }
    }

    private void OnBrowseCheatsDirectoryAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseCheatsDirectoryAsync, "selecting the cheat directory");
    }

    private async Task BrowseCheatsDirectoryAsync()
    {
        var path = await PickFolderAsync("Select the cheat directory", CheatsDirectoryBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            CheatsDirectoryBox.Text = path;
        }
    }

    private void OnBrowseLibraryMetadataAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseLibraryMetadataAsync, "selecting the library metadata file");
    }

    private void OnBrowseDiscDatAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseDiscDatAsync, "selecting the disc DAT file");
    }

    private async Task BrowseDiscDatAsync()
    {
        var path = await PickFileAsync(
            "Select a Redump/No-Intro DAT",
            DiscDatPathBox.Text,
            "DAT or XML",
            ["*.dat", "*.xml"]).ConfigureAwait(true);
        if (path is not null)
        {
            DiscDatPathBox.Text = path;
        }
    }

    private async Task BrowseLibraryMetadataAsync()
    {
        var path = await PickFileAsync(
            "Select the library metadata file",
            LibraryMetadataPathBox.Text,
            "Library metadata",
            ["*.json"]).ConfigureAwait(true);
        if (path is not null)
        {
            LibraryMetadataPathBox.Text = path;
        }
    }

    private async Task<string?> PickFolderAsync(string title, string? current)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        };
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            IStorageFolder? start = await StorageProvider.TryGetFolderFromPathAsync(current).ConfigureAwait(true);
            if (start is not null)
            {
                options.SuggestedStartLocation = start;
            }
        }

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(options).ConfigureAwait(true);
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void PopulateInputDevices(AppSettings settings, string? gameId)
    {
        ComboBox[] combos =
        [
            InputDevice1Combo,
            InputDevice2Combo,
            InputDevice3Combo,
            InputDevice4Combo,
            InputDevice5Combo,
            InputDevice6Combo,
            InputDevice7Combo,
            InputDevice8Combo,
        ];
        IReadOnlyList<string> devices = settings.ResolveInputDevices(gameId);
        for (var i = 0; i < combos.Length; i++)
        {
            combos[i].ItemsSource = _inputDeviceOptions;
            InputDeviceType device = Enum.TryParse(devices[i], ignoreCase: true, out InputDeviceType parsed)
                ? parsed
                : InputDeviceType.Joypad;
            combos[i].SelectedItem = _inputDeviceOptions.FirstOrDefault(option => option.Value == device)
                ?? _inputDeviceOptions[0];
        }

        ActiveInputDevicesCombo.ItemsSource = _activeDeviceOptions;
        ActiveInputDevicesCombo.SelectedItem = _activeDeviceOptions.FirstOrDefault(
                option => option.Value == settings.ActiveInputDevices)
            ?? _activeDeviceOptions[0];
        PerGameInputCheck.IsChecked = settings.GetInputDeviceOverride(gameId) is not null;
        PerGameInputCheck.IsEnabled = !string.IsNullOrWhiteSpace(gameId);
    }

    private void SaveInputDevices(AppSettings settings)
    {
        ComboBox[] combos =
        [
            InputDevice1Combo,
            InputDevice2Combo,
            InputDevice3Combo,
            InputDevice4Combo,
            InputDevice5Combo,
            InputDevice6Combo,
            InputDevice7Combo,
            InputDevice8Combo,
        ];
        var devices = new string[combos.Length];
        for (var i = 0; i < combos.Length; i++)
        {
            devices[i] = (combos[i].SelectedItem is InputDeviceOption option
                    ? option.Value
                    : InputDeviceType.Joypad)
                .ToString();
        }

        settings.ActiveInputDevices = ActiveInputDevicesCombo.SelectedItem is ActiveDevicesOption active
            ? active.Value
            : null;
        if (!string.IsNullOrWhiteSpace(_gameId) && PerGameInputCheck.IsChecked == true)
        {
            settings.SetInputDeviceOverride(_gameId, devices);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(_gameId))
            {
                settings.SetInputDeviceOverride(_gameId, null);
            }

            for (var i = 0; i < devices.Length; i++)
            {
                settings.SetInputDevice(
                    i,
                    Enum.TryParse(devices[i], ignoreCase: true, out InputDeviceType device)
                        ? device
                        : InputDeviceType.Joypad);
            }
        }
    }

    private void PopulateBiosOverrides(string? folder, string? selectedMd5)
    {
        var options = new List<BiosOverrideOption>
        {
            new(null, "Automatic (region-based)"),
        };

        BiosLibrary? library = TryScanBiosFolder(folder);
        if (library is not null)
        {
            foreach (BiosProfile profile in library.Primaries)
            {
                options.Add(new BiosOverrideOption(
                    profile.Md5,
                    $"{profile.Name} [{profile.Md5[..8]}] - {profile.RegionLabel}"));
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedMd5)
            && options.All(option => !string.Equals(option.Md5, selectedMd5, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new BiosOverrideOption(
                selectedMd5,
                $"Missing BIOS {selectedMd5[..Math.Min(8, selectedMd5.Length)]}"));
        }

        BiosOverrideCombo.ItemsSource = options;
        BiosOverrideCombo.SelectedItem = options.FirstOrDefault(
            option => string.Equals(option.Md5, selectedMd5, StringComparison.OrdinalIgnoreCase))
            ?? options[0];
    }

    private static BiosLibrary? TryScanBiosFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        try
        {
            return BiosLibrary.Scan(folder);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            Serilog.Log.Debug(exception, "BIOS folder scan failed in the settings dialog.");
            return null;
        }
    }

    private void OnBrowseBiosFolderAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseBiosFolderAsync, "selecting the BIOS folder");
    }

    private async Task BrowseBiosFolderAsync()
    {
        var path = await PickFolderAsync("Select the BIOS folder", BiosFolderBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            BiosFolderBox.Text = path;
            PopulateBiosOverrides(
                path,
                (BiosOverrideCombo.SelectedItem as BiosOverrideOption)?.Md5);
        }
    }

    private void OnBrowseBiosPathAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseBiosPathAsync, "selecting the primary BIOS");
    }

    private async Task BrowseBiosPathAsync()
    {
        var path = await PickFileAsync("Select the primary 3DO BIOS", BiosPathBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            BiosPathBox.Text = path;
        }
    }

    private void OnBrowseBios2PathAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseBios2PathAsync, "selecting the BIOS2/font ROM");
    }

    private async Task BrowseBios2PathAsync()
    {
        var path = await PickFileAsync("Select the optional BIOS2 or Kanji font ROM", Bios2PathBox.Text)
            .ConfigureAwait(true);
        if (path is not null)
        {
            Bios2PathBox.Text = path;
        }
    }

    private void OnScanBiosAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ScanBiosAsync, "scanning the BIOS folder");
    }

    private async Task ScanBiosAsync()
    {
        var folder = BiosFolderBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(folder) && !string.IsNullOrWhiteSpace(BiosPathBox.Text))
        {
            folder = Path.GetDirectoryName(Path.GetFullPath(BiosPathBox.Text));
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            BiosScanText.Text = "Set a BIOS folder or primary BIOS first.";
            return;
        }

        try
        {
            ScanBiosButton.IsEnabled = false;
            BiosLibrary library = await Task.Run(() => BiosLibrary.Scan(folder)).ConfigureAwait(true);
            var errors = library.Errors.Count == 0
                ? string.Empty
                : $" First error: {library.Errors[0]}";
            BiosScanText.Text =
                $"{library.Primaries.Count} primary, {library.Secondaries.Count} secondary, "
                + $"{library.Errors.Count} error(s).{errors}";
            PopulateBiosOverrides(
                folder,
                (BiosOverrideCombo.SelectedItem as BiosOverrideOption)?.Md5);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            Serilog.Log.Debug(exception, "BIOS scan failed in the settings dialog.");
            BiosScanText.Text = $"Scan failed: {exception.Message}";
        }
        finally
        {
            ScanBiosButton.IsEnabled = true;
        }
    }

    private async Task<string?> PickFileAsync(
        string title,
        string? current,
        string description = "BIOS ROM",
        string[]? patterns = null)
    {
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(description) { Patterns = patterns ?? ["*.bin", "*.rom"] }],
        };
        if (!string.IsNullOrWhiteSpace(current))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(current));
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                IStorageFolder? start = await StorageProvider.TryGetFolderFromPathAsync(directory).ConfigureAwait(true);
                if (start is not null)
                {
                    options.SuggestedStartLocation = start;
                }
            }
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options).ConfigureAwait(true);
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static uint? ParseRandomSeedText(string? text)
    {
        var seedText = text?.Trim();
        if (string.IsNullOrEmpty(seedText))
        {
            return null;
        }

        var seedValue = seedText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? seedText[2..]
            : seedText;
        if (!uint.TryParse(seedValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var seed))
        {
            throw new FormatException("The random seed must be a 32-bit hexadecimal value such as 0x1234, or blank.");
        }

        return seed;
    }

    private static bool TryParseFixMode(string text, out FixMode mode)
    {
        var value = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits))
        {
            mode = (FixMode)bits;
            return true;
        }

        mode = FixMode.None;
        return false;
    }

    private sealed record RendererOption(VideoRendererKind Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record ResolutionOption(OutputResolution Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record FrameSkipOption(FrameSkipMode Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record DeinterlaceOption(DeinterlaceMode Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record DisplayRefreshRateOption(int Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record AudioDeviceOption(string? Name, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ConfigProfileOption(string? Name, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record PointerPortOption(int Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record RegionOverrideOption(VideoRegion? Value, string Label)
    {
        public override string ToString() => Label;
    }

    private static double ParsePointerNumber(string? text, string label, double minimum, double maximum)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return label.Contains("scale", StringComparison.Ordinal) ? 1.0 : 0.0;
        }

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value)
            || value < minimum
            || value > maximum)
        {
            throw new FormatException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The {label} must be between {minimum} and {maximum}."));
        }

        return value;
    }

    private void OnPointerResetClick(object? sender, RoutedEventArgs e)
    {
        PointerScaleBox.Text = "1.00";
        PointerOffsetXBox.Text = "0";
        PointerOffsetYBox.Text = "0";
    }

    private sealed record RegionOption(VideoRegion Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ProfileOption(EmulationProfile Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record ArmTimingOption(ArmTimingMode Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record ClioTimerOption(ClioTimerMode Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record CelTimingOption(CelTimingMode Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record CdromTimingOption(CdromTimingMode Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record MemoryOption(MemoryConfiguration Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record CdSpeedOption(CdSpeed Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record ActiveDevicesOption(int? Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record InputDeviceOption(InputDeviceType Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record NvramStorageOption(NvramStorageMode Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record OsdPositionOption(OsdPosition Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record BiosOverrideOption(string? Md5, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}
