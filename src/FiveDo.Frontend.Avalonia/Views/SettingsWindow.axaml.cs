using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FiveDo.Audio;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Storage;
using FiveDo.Video;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Edits presentation, audio, emulation, and path settings. Changes are applied by the caller
/// only after the dialog returns <see langword="true"/>.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly RendererOption[] _rendererOptions =
    [
        new(VideoRendererKind.OpenGl, "OpenGL (recommended)"),
        new(VideoRendererKind.Vulkan, "Vulkan (offscreen)"),
        new(VideoRendererKind.Software, "Software (CPU)"),
    ];

    private static readonly FrameSkipOption[] _frameSkipOptions =
    [
        new(FrameSkipMode.Auto, "Auto (recommended)"),
        new(FrameSkipMode.Off, "Off - present every frame"),
        new(FrameSkipMode.OneInTwo, "Skip 1 of 2 (30 fps)"),
        new(FrameSkipMode.OneInThree, "Skip 2 of 3 (20 fps)"),
        new(FrameSkipMode.OneInFour, "Skip 3 of 4 (15 fps)"),
        new(FrameSkipMode.OneInFive, "Skip 4 of 5 (12 fps)"),
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

    private static readonly NvramStorageOption[] _nvramStorageOptions =
    [
        new(NvramStorageMode.Global, "Shared (one image, default)"),
        new(NvramStorageMode.PerGame, "Per game (isolated saves)"),
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

        RendererCombo.ItemsSource = _rendererOptions;
        RendererCombo.SelectedItem = _rendererOptions.FirstOrDefault(option => option.Value == settings.Renderer)
            ?? _rendererOptions[0];
        ResolutionCombo.ItemsSource = _resolutionOptions;
        ResolutionCombo.SelectedItem = _resolutionOptions.FirstOrDefault(option => option.Value == settings.OutputResolution)
            ?? _resolutionOptions[0];
        FilterCombo.ItemsSource = Enum.GetValues<VideoFilter>();
        FilterCombo.SelectedItem = settings.Filter;
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
        OverlayCheck.IsChecked = settings.ShowOverlay;
        NativeScreenshotCheck.IsChecked = settings.SaveNativeScreenshot;
        BloomSlider.Value = settings.Bloom;
        FsrSharpnessSlider.Value = settings.FsrSharpness;
        AudioCheck.IsChecked = settings.AudioEnabled;
        VolumeSlider.Value = settings.Volume;
        AutoFixesCheck.IsChecked = settings.AutoGameFixes;
        HighResCheck.IsChecked = settings.HighResolution;
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
        NvramStorageCombo.ItemsSource = _nvramStorageOptions;
        NvramStorageCombo.SelectedItem = _nvramStorageOptions.FirstOrDefault(option => option.Value == settings.NvramStorageMode)
            ?? _nvramStorageOptions[0];
        NvramVersionBox.Text = settings.NvramVersion.ToString(CultureInfo.InvariantCulture);
        CpuClockBox.Text = settings.CpuClockMultiplier.ToString("0.###", CultureInfo.InvariantCulture);
        RandomSeedBox.Text = settings.RandomSeed is { } seed ? $"0x{seed:x8}" : string.Empty;
        SaveDirectoryBox.Text = settings.SaveDirectory;
        ScreenshotDirectoryBox.Text = settings.ScreenshotDirectory;
        CoverDirectoryBox.Text = settings.CoverDirectory;
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
        OpenAlAudioOutput? output = OpenAlAudioOutput.TryCreate(2048, out var error);
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

    /// <summary>Copies the edited values into a settings object.</summary>
    /// <param name="settings">Destination settings.</param>
    /// <exception cref="FormatException">Thrown when the fix-mode override is not hexadecimal.</exception>
    public void ApplyTo(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Filter = FilterCombo.SelectedItem is VideoFilter filter ? filter : VideoFilter.SharpBilinear;
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
        settings.ShowOverlay = OverlayCheck.IsChecked == true;
        settings.SaveNativeScreenshot = NativeScreenshotCheck.IsChecked == true;
        settings.Bloom = (float)BloomSlider.Value;
        settings.FsrSharpness = (float)FsrSharpnessSlider.Value;
        settings.AudioEnabled = AudioCheck.IsChecked == true;
        settings.Volume = (float)VolumeSlider.Value;
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
        var seedText = RandomSeedBox.Text?.Trim();
        if (string.IsNullOrEmpty(seedText))
        {
            settings.RandomSeed = null;
        }
        else
        {
            var seedValue = seedText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? seedText[2..]
                : seedText;
            if (!uint.TryParse(seedValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var seed))
            {
                throw new FormatException("The random seed must be a 32-bit hexadecimal value such as 0x1234, or blank.");
            }

            settings.RandomSeed = seed;
        }

        settings.SaveDirectory = SaveDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.ScreenshotDirectory = ScreenshotDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.CoverDirectory = CoverDirectoryBox.Text?.Trim() ?? string.Empty;
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
            BiosScanText.Text = $"Scan failed: {exception.Message}";
        }
        finally
        {
            ScanBiosButton.IsEnabled = true;
        }
    }

    private async Task<string?> PickFileAsync(string title, string? current)
    {
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("BIOS ROM") { Patterns = ["*.bin", "*.rom"] }],
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

    private sealed record CdSpeedOption(CdSpeed Value, string Label)
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

    private sealed record BiosOverrideOption(string? Md5, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}
