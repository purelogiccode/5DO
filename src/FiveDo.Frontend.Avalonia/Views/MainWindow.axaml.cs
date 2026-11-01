using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FiveDo.Audio;
using FiveDo.Cheats;
using FiveDo.Core;
using FiveDo.Diagnostics;
using FiveDo.Diagnostics.Donations;
using FiveDo.Diagnostics.Logging;
using FiveDo.Frontend.Avalonia.Controls;
using FiveDo.Frontend.Avalonia.Input;
using FiveDo.Frontend.Avalonia.ViewModels;
using FiveDo.Host;
using FiveDo.Host.Osd;
using FiveDo.Host.Rewind;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Storage;
using FiveDo.Tas;
using FiveDo.Video;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// The desktop shell: an RPCS3-style game library with a menu bar, icon toolbar, searchable
/// grid/list views, log dock, and status bar. Booting a disc switches the central area to the
/// video surface, where the machine runs on <see cref="CoreRunner"/>'s dedicated 60 Hz thread
/// and the presenter renders the latest mailbox frame on the UI thread.
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private NvramStore _nvramStore;
    private readonly AppSettings _settings;
    private readonly InMemorySerilogSink _logSink;
    private readonly Serilog.ILogger _logger;
    private readonly PlatformGamepadBackend? _gamepad;
    private readonly ObservableCollection<LibraryItem> _visibleGames = [];
    private readonly List<LibraryItem> _allGames = [];
    private readonly LibraryState _libraryState;
    private LibraryMetadataCatalog _libraryMetadata;
    private readonly LibraryPlaytimeTracker _playtime;
    private readonly DispatcherTimer _logTimer;
    private InputManager _input;
    private readonly SaveStateSlotManager _slots;
    private InputRecorder? _inputRecorder;
    private string? _inputRecordingPath;
    private MovieRecorder? _movieRecorder;
    private MovieRecordingPbusSource? _movieRecordingSource;
    private MoviePbusSource? _moviePlaybackSource;
    private Movie? _moviePlayback;
    private string? _movieRecordingPath;
    private byte[]? _movieStartState;
    private long _movieStartFrame;
    private DispatcherTimer? _movieTimer;
    private readonly OsdNotifications _osdNotifications = new();
    private readonly ObservableCollection<OsdEntry> _osdEntries = [];
    private readonly Button[] _quickMenuButtons;
    private DispatcherTimer? _osdTimer;
    private bool _quickMenuOpen;
    private bool _quickMenuResumeAfterClose;
    private bool _fastForwardHeld;
    private Point? _lastPointerPosition;
    private bool _pointerLeftDown;
    private bool _pointerMiddleDown;
    private bool _pointerRightDown;
    private OpenAlAudioOutput? _audio;
    private float _meterPeak;
    private FiveDoMachine? _machine;
    private CoreRunner? _runner;
    private CheatSet? _cheats;
    private string _cheatPath = string.Empty;
    private IDiscSource? _disc;
    private DiscPlaylist? _playlist;
    private string? _playlistPath;
    private string _gameId = string.Empty;
    private string _gameName = string.Empty;
    private string _nvramGameId = string.Empty;
    private NvramStorageMode _nvramMode;
    private int _nvramVersion;
    private string _nvramDirectory;
    private CpuVideoRenderer? _cpuVideo;
    private VideoRendererKind _rendererKind;
    private volatile bool _vulkanPresenter;
    private bool _openGlDisabled;
    private WriteableBitmap? _cpuBitmap;
    private DispatcherTimer? _cpuTimer;
    private byte[]? _cpuPixels;
    private bool _running;
    private bool _shuttingDown;
    private bool _biosLoaded;
    private string _biosMd5 = string.Empty;
    private string _bios2Md5 = string.Empty;
    private string _biosSelectionReason = string.Empty;
    private bool _gridMode = true;
    private bool _syncingSelection;
    private bool _logDockVisible;
    private int _lastLogEntryCount = -1;
    private DateTime _lastPlaytimeFlushUtc = DateTime.UtcNow;
    private int _scanGeneration;
    private bool _libraryReady;
    private CancellationTokenSource? _libraryScanCts;
    private DateTime _lastDonationCheckUtc;
    private string? _donationBannerMessage;
    private bool _donationCheckInProgress;
    private bool _donationBannerDismissed;

    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);
        AddHandler(DragDrop.DropEvent, OnWindowDrop);
        Deactivated += (_, _) => CancelFastForward();
        _settings = AppSettingsStore.Load();
        _libraryState = LibraryStateStore.Load();
        _libraryMetadata = LibraryMetadataCatalog.Load(_settings.ResolveLibraryMetadataPath());
        _playtime = new LibraryPlaytimeTracker(
            _libraryState,
            () => LibraryStateStore.Save(_libraryState));
        _lastDonationCheckUtc = _settings.DonationLastCheckedUtc ?? DateTime.MinValue;
        _rendererKind = ResolveRendererKind();
        _logSink = AppLogging.InMemorySink ?? new InMemorySerilogSink(4000);
        _logger = Serilog.Log.ForContext<MainWindow>();
        _logger.ForCategory(LogCategory.Host).Information(
            "Desktop shell starting ({Version}).",
            FiveDoVersion.Version);

        _osdNotifications.Lifetime = TimeSpan.FromMilliseconds(Math.Clamp(
            _settings.OsdDurationMilliseconds,
            OsdOptions.MinDurationMilliseconds,
            OsdOptions.MaxDurationMilliseconds));
        OsdList.ItemsSource = _osdEntries;
        _quickMenuButtons =
        [
            QuickMenuResumeButton,
            QuickMenuSaveButton,
            QuickMenuLoadButton,
            QuickMenuRewindButton,
            QuickMenuFastForwardButton,
            QuickMenuScreenshotButton,
            QuickMenuResetButton,
            QuickMenuCheatsButton,
            QuickMenuSettingsButton,
            QuickMenuStopButton,
            QuickMenuCloseButton,
        ];
        ApplyOsdSettings();
        if (string.IsNullOrWhiteSpace(_settings.SaveDirectory))
        {
            _settings.SaveDirectory = Path.Combine(AppSettingsStore.DefaultDirectory, "states");
        }

        _settings.SaveSlotCount = Math.Clamp(_settings.SaveSlotCount, 1, 100);
        _slots = new SaveStateSlotManager(_settings.SaveDirectory, _settings.SaveSlotCount);
        _slots.Select(Math.Clamp(_settings.SaveSlot, 0, _slots.SlotCount - 1));
        _gamepad = PlatformGamepadBackend.TryCreate(_settings.Input);
        _input = new InputManager(_settings.Input, _gamepad);
        _settings.ApplyInputDevices(_input);
        string? audioError = null;
        _audio = _settings.AudioEnabled ? OpenAlAudioOutput.TryCreate(8192, out audioError) : null;
        if (_audio is not null)
        {
            _audio.Volume = _settings.Volume;
            _logger.ForCategory(LogCategory.Audio).Information(
                "OpenAL audio ready ({SampleRate} Hz stereo).",
                OpenAlAudioOutput.OutputSampleRate);
        }
        else if (_settings.AudioEnabled)
        {
            _logger.ForCategory(LogCategory.Audio).WithoutBugReport().Warning(
                "OpenAL audio is unavailable; emulation continues silently ({Error}).",
                audioError ?? "unknown error");
        }

        _nvramStore = CreateNvramStore(null);
        _nvramMode = _settings.NvramStorageMode;
        _nvramVersion = _settings.NvramVersion;
        _nvramDirectory = _settings.NvramDirectory;
        HighResMenuItem.IsChecked = _settings.HighResolution;
        OverlayPanel.IsVisible = _settings.ShowOverlay;
        InputDisplayPanel.IsVisible = _settings.ShowInputDisplay;
        InputDisplayMenuItem.IsChecked = _settings.ShowInputDisplay;
        Video.Settings = _settings.ToRendererSettings(_gameId);
        if (_rendererKind != VideoRendererKind.OpenGl
            && (!Program.SmokeTest || Program.RequestedGlBackend != GlBackend.Auto))
        {
            // Software uses the CPU bitmap and Vulkan uses its native child surface.
            // Hide the OpenGL control so it never creates a WGL/ANGLE context.
            _openGlDisabled = true;
            Video.IsVisible = false;
            StartCpuFallback(new NotSupportedException(
                _rendererKind == VideoRendererKind.Vulkan
                    ? "Vulkan renderer selected."
                    : "Software renderer selected."));
        }

        if (Program.SmokeTest)
        {
            // CI runners may not expose a desktop GL context. The smoke test
            // validates XAML, trimming, settings, and window startup; GPU
            // presentation is covered by the renderer tests and manual runs.
            Video.IsVisible = false;

            // Construct the settings and cheat dialogs so their XAML and named controls
            // are also validated by the trimmed smoke test. Do not close them: closing a
            // window before the main loop starts can shut down the dispatcher.
            _ = new SettingsWindow(_settings, _gameId, _gameName);
            _ = new CheatsWindow();
            _ = new DiscBrowserWindow();
            _ = new DiscInfoWindow();
            _ = new BiosNvramManagerWindow();
        }

        GridLibraryList.ItemsSource = _visibleGames;
        ListLibraryList.ItemsSource = _visibleGames;
        _logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _logTimer.Tick += (_, _) =>
        {
            if (LogPanel.IsVisible)
            {
                RefreshLogDock(force: false);
            }

            if (_playtime.IsTracking
                && (DateTime.UtcNow - _lastPlaytimeFlushUtc).TotalSeconds >= 60)
            {
                _playtime.Flush();
                _lastPlaytimeFlushUtc = DateTime.UtcNow;
            }
        };
        _logTimer.Start();
        _movieTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _movieTimer.Tick += (_, _) => UpdateMovieStatus();
        _movieTimer.Start();
        SetLogDockVisible(false);
        SetLibraryMode(grid: true);
        LibraryFilterBox.SelectedIndex = 0;
        LibrarySortBox.SelectedIndex = 0;
        _libraryReady = true;
        ApplyLibraryFilter();
        UpdateRecentMenu();
        UpdateDonationBanner();
        Activated += OnDonationCheckActivated;

        Video.OpenGlReady += OnOpenGlReady;
        Video.OpenGlFailed += OnOpenGlFailed;
        VulkanVideo.Failed += OnVulkanFailed;
        VulkanVideo.Started += OnVulkanStarted;
        Deactivated += (_, _) => _input.Keyboard.Reset();
        Closing += (_, _) =>
        {
            try
            {
                Shutdown();
            }
            catch (Exception exception)
            {
                _logger.ForCategory(LogCategory.Host).Error(exception, "Desktop shell shutdown failed.");
            }
        };
        Opened += OnOpened;
        UpdateSlotText();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (Program.SmokeTest)
        {
            // The packaged smoke test validates XAML/window construction and exits;
            // it must not touch the user's configured BIOS/disc or first-run state.
            return;
        }

        if (!_settings.FirstRunCompleted
            && string.IsNullOrWhiteSpace(_settings.BiosPath)
            && Program.StartupBiosPath is null)
        {
            ShowWelcome(firstRun: true);
            return;
        }

        LoadConfiguredPaths();
        EnterFullscreenIfRequested();
        AsyncEvent.Run(RefreshLibraryAsync, "refreshing the game library");
    }

    private void ShowWelcome(bool firstRun)
    {
        try
        {
            var welcome = new WelcomeWindow();
            welcome.Closed += (_, _) =>
            {
                IsEnabled = true;
                if (firstRun)
                {
                    _settings.FirstRunCompleted = true;
                    AppSettingsStore.Save(_settings);
                }

                switch (welcome.Action)
                {
                    case WelcomeAction.OpenBios:
                        OnOpenBiosAsync(null, new RoutedEventArgs());
                        break;
                    case WelcomeAction.OpenDisc:
                        OnOpenDiscAsync(null, new RoutedEventArgs());
                        break;
                }

                if (Program.StartupDiscPath is not null && _disc is null)
                {
                    LoadConfiguredPaths();
                }

                EnterFullscreenIfRequested();
                AsyncEvent.Run(RefreshLibraryAsync, "refreshing the game library");
            };
            IsEnabled = false;
            welcome.Show(this);
        }
        catch (Exception exception)
        {
            IsEnabled = true;
            StatusText.Text = $"Welcome error: {exception.Message}";
            _logger.ForCategory(LogCategory.Host).Error(exception, "Welcome dialog failed.");
        }
    }

    private void OnWelcome(object? sender, RoutedEventArgs e)
    {
        ShowWelcome(firstRun: false);
    }

    private void LoadConfiguredPaths()
    {
        var startupBios = Program.StartupBiosPath;
        ResolvedBios resolved = startupBios is not null
            ? new ResolvedBios(startupBios, string.Empty, "startup", "Using the --bios startup path.", [])
            : BiosResolver.Resolve(_settings, null, null);
        if (resolved.HasPrimary && File.Exists(resolved.PrimaryPath))
        {
            OpenBiosPath(resolved.PrimaryPath!, resolved.SecondaryPath, persist: false, resolved.Reason);
        }

        var startupDisc = Program.StartupDiscPath;
        var discPath = startupDisc ?? _settings.DiscPath;
        if (File.Exists(discPath))
        {
            OpenDiscPath(discPath, autoStart: startupDisc is not null);
        }
    }

    private void OnOpenBiosAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(OpenBiosAsync, "opening a BIOS");
    }

    private async Task OpenBiosAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open 3DO BIOS",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("BIOS ROM") { Patterns = ["*.bin", "*.rom"] }],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
        {
            OpenBiosPath(path);
        }
    }

    private void OnAssignBiosToGameAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(AssignBiosToGameAsync, "assigning a BIOS to the current game");
    }

    private async Task AssignBiosToGameAsync()
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            StatusText.Text = "Load a game before assigning a BIOS.";
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Assign a BIOS to the current game",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("BIOS ROM") { Patterns = ["*.bin", "*.rom"] }],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is null)
        {
            return;
        }

        try
        {
            var bios = BiosImage.Load(path);
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory)
                && !string.Equals(
                    directory,
                    _settings.BiosFolder,
                    StringComparison.OrdinalIgnoreCase))
            {
                // The override is resolved by MD5 from the BIOS folder, so keep the
                // assigned image discoverable even when it lives outside the old folder.
                _settings.BiosFolder = directory;
            }

            _settings.SetBiosOverride(_gameId, bios.Md5);
            AppSettingsStore.Save(_settings);
            StatusText.Text =
                $"BIOS {bios.Md5[..8]} assigned to {_gameName}; restart the game to apply it.";
            _logger.ForCategory(LogCategory.Storage).Information(
                "BIOS {Md5} assigned to game {GameId} ({Game}).",
                bios.Md5,
                _gameId,
                _gameName);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            StatusText.Text = $"BIOS assignment error: {exception.Message}";
            _logger.ForCategory(LogCategory.Storage).Error(exception, "BIOS assignment failed.");
            ShowError("BIOS assignment error", exception.Message);
        }
    }

    private void OnClearBiosAssignment(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            StatusText.Text = "Load a game before clearing a BIOS assignment.";
            return;
        }

        _settings.SetBiosOverride(_gameId, null);
        AppSettingsStore.Save(_settings);
        StatusText.Text = $"Automatic BIOS selection restored for {_gameName}; restart the game to apply it.";
        _logger.ForCategory(LogCategory.Storage).Information(
            "BIOS override cleared for game {GameId} ({Game}).",
            _gameId,
            _gameName);
    }

    private void OnExportNvramAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ExportNvramAsync, "exporting NVRAM");
    }

    private async Task ExportNvramAsync()
    {
        if (_machine is null)
        {
            StatusText.Text = "Start a game before exporting NVRAM.";
            return;
        }

        var suffix = _gameId.Length > 0
            ? _gameId[..Math.Min(8, _gameId.Length)]
            : "shared";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export NVRAM",
            SuggestedFileName = $"nvram-{suffix}.ram",
            DefaultExtension = "ram",
            FileTypeChoices = [new FilePickerFileType("NVRAM image") { Patterns = ["*.ram"] }],
        }).ConfigureAwait(true);
        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        try
        {
            var buffer = new byte[EmulatorConstants.NvramSize];
            if (_runner is not null)
            {
                _runner.Invoke(() => _machine.CopyNvram(buffer));
            }
            else
            {
                _machine.CopyNvram(buffer);
            }

            File.WriteAllBytes(path, buffer);
            StatusText.Text = $"NVRAM exported to {path}.";
            _logger.ForCategory(LogCategory.Storage).Information(
                "NVRAM exported for {Game} ({GameId}) to {Path}.",
                _gameName,
                _gameId,
                path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"NVRAM export failed: {exception.Message}";
            ShowError("NVRAM export failed", exception.Message);
        }
    }

    private void OnImportNvramAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ImportNvramAsync, "importing NVRAM");
    }

    private async Task ImportNvramAsync()
    {
        if (_machine is null)
        {
            StatusText.Text = "Start a game before importing NVRAM.";
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import NVRAM",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("NVRAM image") { Patterns = ["*.ram"] }],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is null)
        {
            return;
        }

        try
        {
            NvramImage image = NvramImage.Load(path);
            byte[] data = image.Data.ToArray();
            if (_runner is not null)
            {
                _runner.Invoke(() =>
                {
                    _machine.LoadNvram(data);
                    PersistNvram(_machine);
                });
            }
            else
            {
                _machine.LoadNvram(data);
                PersistNvram(_machine);
            }

            StatusText.Text = $"NVRAM imported from {path}.";
            _logger.ForCategory(LogCategory.Storage).Information(
                "NVRAM imported for {Game} ({GameId}) from {Path}.",
                _gameName,
                _gameId,
                path);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            StatusText.Text = $"NVRAM import failed: {exception.Message}";
            ShowError("NVRAM import failed", exception.Message);
        }
    }

    private void OnResetNvram(object? sender, RoutedEventArgs e)
    {
        if (_machine is null)
        {
            StatusText.Text = "Start a game before resetting NVRAM.";
            return;
        }

        byte[] fresh = NvramImage.CreateFresh().Data.ToArray();
        if (_runner is not null)
        {
            _runner.Invoke(() =>
            {
                _machine.LoadNvram(fresh);
                PersistNvram(_machine);
            });
        }
        else
        {
            _machine.LoadNvram(fresh);
            PersistNvram(_machine);
        }

        StatusText.Text = $"NVRAM reset for {(_gameName.Length > 0 ? _gameName : "the current machine")}.";
        _logger.ForCategory(LogCategory.Storage).Information(
            "NVRAM reset for {Game} ({GameId}).",
            _gameName,
            _gameId);
    }

    private void OnOpenDiscAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(OpenDiscAsync, "opening a disc");
    }

    private async Task OpenDiscAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open 3DO disc",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("3DO disc")
                {
                    Patterns = ["*.iso", "*.bin", "*.cue", "*.chd", "*.m3u", "*.m3u8"],
                },
            ],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
        {
            OpenDiscPath(path, autoStart: true);
        }
    }

    private void OnDiscBrowserAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowDiscBrowserAsync, "opening the disc browser");
    }

    private async Task ShowDiscBrowserAsync()
    {
        var path = _playlist?.Current.Path ?? _settings.DiscPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText.Text = "Boot a disc first to browse its files.";
            return;
        }

        var window = new DiscBrowserWindow(path);
        await window.ShowDialog(this).ConfigureAwait(true);
    }

    private void OnDiscInfoAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowDiscInfoAsync, "reading disc information");
    }

    private async Task ShowDiscInfoAsync()
    {
        var path = _playlist?.Current.Path ?? _settings.DiscPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText.Text = "Boot a disc first to show its information.";
            return;
        }

        DiscInfo? info;
        try
        {
            info = await Task.Run(() =>
            {
                using DiscBrowser browser = DiscBrowser.Open(path);
                return browser.Info;
            }).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or DiscImageException
            or ArgumentException)
        {
            StatusText.Text = $"Disc information could not be read: {exception.Message}";
            _logger.ForCategory(LogCategory.Storage).Error(
                exception,
                "Disc information failed for {Path}.",
                path);
            return;
        }

        var window = new DiscInfoWindow(info);
        await window.ShowDialog(this).ConfigureAwait(true);
    }

    private void OpenBiosPath(string path, string? bios2Path = null, bool persist = true, string? reason = null)
    {
        StopMovie(save: true);
        StopInputRecording(save: true);
        if (_running)
        {
            PauseEmulation(showLibrary: false);
        }

        try
        {
            var bios = BiosImage.Load(path, string.IsNullOrWhiteSpace(bios2Path) ? null : bios2Path);
            var machine = CreateMachine();
            _runner!.Invoke(() =>
            {
                machine.LoadBios(bios.Bios1.Span, bios.Bios2.Span);
                // Switching BIOS is a cold start: the previous game's RAM can leave
                // the new BIOS waiting at PC 0x1658 before it ever reaches the disc.
                machine.Memory.ClearRam();
                machine.Reset();
                var nvram = _nvramStore.LoadOrCreate();
                machine.LoadNvram(nvram.Data.Span);
            });
            _biosLoaded = true;
            _biosMd5 = bios.Md5;
            _bios2Md5 = bios.Md5Bios2;
            _biosSelectionReason = reason ?? string.Empty;
            if (persist)
            {
                _settings.BiosPath = path;
                _settings.Bios2Path = string.IsNullOrWhiteSpace(bios2Path) ? string.Empty : bios2Path;
                AppSettingsStore.Save(_settings);
            }

            var secondary = string.IsNullOrWhiteSpace(bios2Path)
                ? string.Empty
                : $" + BIOS2 {bios.Md5Bios2[..Math.Min(8, bios.Md5Bios2.Length)]}";
            StatusText.Text =
                $"BIOS {bios.Md5[..8]}{secondary} loaded{(bios.IsAnvil ? " (Anvil)" : string.Empty)}.";
            _logger.ForCategory(LogCategory.Storage).Information(
                "BIOS loaded: {Path} ({Md5}, {Kind}){Reason}.",
                path,
                bios.Md5,
                bios.IsAnvil ? "Anvil" : "retail",
                string.IsNullOrWhiteSpace(reason) ? string.Empty : $" [{reason}]");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            StatusText.Text = $"BIOS error: {exception.Message}";
            _logger.ForCategory(LogCategory.Storage).Error(exception, "BIOS load failed.");
            ShowError("BIOS error", exception.Message);
        }
    }

    private static bool IsPlaylistPath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".m3u", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private void OnSwapDiscAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(SwapDiscAsync, "swapping discs");
    }

    private void OnPlaylistEditorAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowPlaylistEditorAsync, "editing the disc playlist");
    }

    private async Task ShowPlaylistEditorAsync()
    {
        List<DiscPlaylistEntry> entries = _playlist is not null ? [.. _playlist.Entries] : [];
        if (entries.Count == 0
            && !string.IsNullOrWhiteSpace(_settings.DiscPath)
            && File.Exists(_settings.DiscPath)
            && !IsPlaylistPath(_settings.DiscPath))
        {
            entries.Add(new DiscPlaylistEntry(Path.GetFullPath(_settings.DiscPath), _gameName));
        }

        string? currentPath = _playlist?.Current.Path;
        var window = new DiscPlaylistWindow(entries, _playlistPath);
        string? savedPath = await window.ShowDialog<string?>(this).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(savedPath))
        {
            return;
        }

        DiscPlaylist saved = DiscPlaylist.Load(savedPath);
        var selected = 0;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            for (var i = 0; i < saved.Count; i++)
            {
                if (string.Equals(saved.Entries[i].Path, currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    selected = i;
                    break;
                }
            }
        }

        saved.Select(selected);
        _playlist = saved;
        _playlistPath = savedPath;
        _settings.DiscPath = savedPath;
        AppSettingsStore.Save(_settings);
        ShowOsd(
            $"Playlist saved: {Path.GetFileName(savedPath)} ({saved.Count} discs).",
            OsdSeverity.Success);
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        e.DragEffects =
            files is not null && files.Any(file => IsSupportedDroppedPath(file.TryGetLocalPath()))
                ? DragDropEffects.Copy
                : DragDropEffects.None;
    }

    private void OnWindowDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return;
        }

        string[] paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(IsSupportedDroppedPath)
            .Cast<string>()
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        e.Handled = true;
        AsyncEvent.Run(() => ImportDroppedPathsAsync(paths), "importing dropped files");
    }

    private async Task ImportDroppedPathsAsync(string[] paths)
    {
        if (paths.Length == 1 && IsPlaylistPath(paths[0]))
        {
            if (!_running)
            {
                OpenDiscPath(paths[0], autoStart: true);
                ShowOsd(
                    $"Opened playlist: {Path.GetFileName(paths[0])}.",
                    OsdSeverity.Success);
                return;
            }

            DiscPlaylist loaded = DiscPlaylist.Load(paths[0]);
            _playlist = loaded;
            _playlistPath = paths[0];
            _settings.DiscPath = paths[0];
            AppSettingsStore.Save(_settings);
            ShowOsd(
                $"Loaded playlist: {loaded.Count} discs. Press F3 to swap.",
                OsdSeverity.Success);
            return;
        }

        var entries = new List<DiscPlaylistEntry>();
        foreach (var path in paths)
        {
            if (!IsPlaylistPath(path))
            {
                entries.Add(new DiscPlaylistEntry(Path.GetFullPath(path), null));
                continue;
            }

            try
            {
                entries.AddRange(DiscPlaylist.Load(path).Entries);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                ShowOsd(
                    $"Could not import {Path.GetFileName(path)}: {exception.Message}",
                    OsdSeverity.Warning);
            }
        }

        if (entries.Count == 0)
        {
            return;
        }

        DiscPlaylist dropped = DiscPlaylist.Create(entries);
        if (_running)
        {
            _playlist = dropped;
            _playlistPath = null;
            ShowOsd(
                $"Imported {dropped.Count} discs. Press F3 to swap.",
                OsdSeverity.Success);
            return;
        }

        if (OpenDiscPath(entries[0].Path, autoStart: true, preservePlaylist: true))
        {
            _playlist = dropped;
            _playlistPath = null;
            ShowOsd(
                $"Imported {dropped.Count} discs and booted {entries[0].DisplayName}.",
                OsdSeverity.Success);
        }
    }

    private static bool IsSupportedDroppedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".iso", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bin", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cue", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".chd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".m3u", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SwapDiscAsync()
    {
        if (_disc is null)
        {
            StatusText.Text = "Boot a disc before swapping.";
            return;
        }

        if (_playlist is { Count: > 1 })
        {
            int nextIndex = (_playlist.CurrentIndex + 1) % _playlist.Count;
            DiscPlaylistEntry entry = _playlist.Entries[nextIndex];
            if (OpenDiscPath(entry.Path, autoStart: false, preservePlaylist: true))
            {
                _playlist.Select(nextIndex);
                ShowOsd($"Disc {_playlist.FormatPosition()}: {entry.DisplayName}", OsdSeverity.Success);
            }

            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Swap 3DO disc",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("3DO disc or playlist")
                {
                    Patterns = ["*.iso", "*.bin", "*.cue", "*.chd", "*.m3u", "*.m3u8"],
                },
            ],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
        {
            if (OpenDiscPath(path, autoStart: false))
            {
                ShowOsd($"Disc swapped: {Path.GetFileName(path)}", OsdSeverity.Success);
            }
        }
    }

    private void HandleGuestDiscEjection()
    {
        if (!_settings.AutoSwapOnEject || _playlist is not { Count: > 1 } || _disc is null)
        {
            ShowOsd(
                "The game ejected the disc. Press F3 to insert the next disc.",
                OsdSeverity.Warning);
            return;
        }

        int nextIndex = _playlist.CurrentIndex + 1;
        if (nextIndex >= _playlist.Count)
        {
            ShowOsd(
                "The game ejected the last disc in the playlist. Press F3 to choose a disc.",
                OsdSeverity.Warning);
            return;
        }

        DiscPlaylistEntry entry = _playlist.Entries[nextIndex];
        if (OpenDiscPath(entry.Path, autoStart: false, preservePlaylist: true))
        {
            _playlist.Select(nextIndex);
            ShowOsd(
                $"ODE inserted disc {_playlist.FormatPosition()}: {entry.DisplayName}",
                OsdSeverity.Success);
        }
    }

    private bool OpenDiscPath(string path, bool autoStart = false, bool preservePlaylist = false)
    {
        StopMovie(save: true);
        StopInputRecording(save: true);
        if (autoStart && _running)
        {
            PauseEmulation(showLibrary: false);
        }

        try
        {
            if (!preservePlaylist)
            {
                _playlist = null;
                _playlistPath = null;
                if (IsPlaylistPath(path))
                {
                    _playlist = DiscPlaylist.Load(path);
                    _playlistPath = path;
                    path = _playlist.Current.Path;
                }
            }
            var machine = _machine ?? CreateMachine();
            var disc = DiscImageFactory.Open(path);
            var gameId = disc.SectorCount >= 2 ? GameId.Compute(disc) : string.Empty;
            var record = gameId.Length == 0 ? null : GameDatabase.Default.FindByChecksum(gameId);
            var fixMode = _settings.ResolveFixMode(gameId);
            ResolvedBios resolved;
            try
            {
                resolved = BiosResolver.Resolve(_settings, gameId, record, path);
            }
            catch
            {
                disc.Dispose();
                throw;
            }

            foreach (var warning in resolved.Warnings)
            {
                _logger.ForCategory(LogCategory.Storage).Warning("BIOS selection: {Warning}", warning);
            }

            BiosImage? selectedBios = null;
            var biosChanged = false;
            if (resolved.HasPrimary && File.Exists(resolved.PrimaryPath))
            {
                try
                {
                    selectedBios = BiosImage.Load(
                        resolved.PrimaryPath!,
                        string.IsNullOrWhiteSpace(resolved.SecondaryPath) ? null : resolved.SecondaryPath);
                }
                catch
                {
                    disc.Dispose();
                    throw;
                }

                biosChanged = !_biosLoaded
                    || !string.Equals(_biosMd5, selectedBios.Md5, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(_bios2Md5, selectedBios.Md5Bios2, StringComparison.OrdinalIgnoreCase);
            }
            else if (!_biosLoaded)
            {
                disc.Dispose();
                throw new InvalidDataException(resolved.Reason);
            }

            var nvramChanged =
                _settings.NvramStorageMode != _nvramMode
                || _settings.NvramVersion != _nvramVersion
                || !string.Equals(_settings.NvramDirectory, _nvramDirectory, StringComparison.OrdinalIgnoreCase)
                || (_settings.NvramStorageMode == NvramStorageMode.PerGame
                    && !string.Equals(_nvramGameId, gameId, StringComparison.OrdinalIgnoreCase));
            NvramStore? nextNvramStore = null;
            _runner!.Invoke(() =>
            {
                if (nvramChanged)
                {
                    // Save the outgoing game's NVRAM before switching images, then load
                    // the incoming game's image (or the shared fallback on first use).
                    if (!string.IsNullOrEmpty(_nvramGameId))
                    {
                        PersistNvram(machine);
                    }

                    nextNvramStore = CreateNvramStore(gameId);
                    NvramImage nvram = nextNvramStore.LoadOrCreate();
                    machine.LoadNvram(nvram.Data.Span);
                }

                if (selectedBios is not null && biosChanged)
                {
                    machine.LoadBios(selectedBios.Bios1.Span, selectedBios.Bios2.Span);
                    machine.Memory.ClearRam();
                }

                _disc?.Dispose();
                _disc = disc;
                machine.Compatibility.FixMode = fixMode;
                machine.InsertDisc(disc);
                if (autoStart || biosChanged)
                {
                    // A different game must boot from a cold RAM image; the previous
                    // game's leftovers can leave the BIOS waiting at PC 0x1658.
                    machine.Memory.ClearRam();
                    machine.Reset();
                }
            });
            if (nextNvramStore is not null)
            {
                NvramStore previous = _nvramStore;
                _nvramStore = nextNvramStore;
                _nvramGameId = gameId;
                _nvramMode = _settings.NvramStorageMode;
                _nvramVersion = _settings.NvramVersion;
                _nvramDirectory = _settings.NvramDirectory;
                previous.Dispose();
            }

            if (selectedBios is not null && biosChanged)
            {
                _biosLoaded = true;
                _biosMd5 = selectedBios.Md5;
                _bios2Md5 = selectedBios.Md5Bios2;
                _biosSelectionReason = resolved.Reason;
            }

            _gameId = gameId;
            _gameName = record?.Name ?? Path.GetFileNameWithoutExtension(path);
            _libraryState.RecordLaunch(gameId, path);
            LibraryStateStore.Save(_libraryState);
            RefreshLibraryDecorations();
            ReloadCheats();
            Video.Settings = _settings.ToRendererSettings(_gameId);
            if (_cpuVideo is not null)
            {
                _cpuVideo.Settings = _settings.ToRendererSettings(_gameId);
            }

            _settings.DiscPath = _playlistPath ?? path;
            var recentPath = _playlistPath ?? path;
            for (var i = _settings.RecentDiscs.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_settings.RecentDiscs[i], recentPath, StringComparison.OrdinalIgnoreCase))
                {
                    _settings.RecentDiscs.RemoveAt(i);
                }
            }

            _settings.RecentDiscs.Insert(0, recentPath);
            while (_settings.RecentDiscs.Count > 10)
            {
                _settings.RecentDiscs.RemoveAt(_settings.RecentDiscs.Count - 1);
            }

            AppSettingsStore.Save(_settings);
            UpdateRecentMenu();
            UpdateEmulationHeader();
            var biosText = string.IsNullOrWhiteSpace(_biosMd5)
                ? "no BIOS"
                : $"BIOS {_biosMd5[..Math.Min(8, _biosMd5.Length)]}";
            StatusText.Text = $"{_gameName}: {disc.SectorCount} sectors, fixes 0x{(uint)fixMode:x8}, {biosText}.";
            if (_playlist is not null)
            {
                StatusText.Text += $" ({_playlist.FormatPosition()})";
            }
            _logger.ForCategory(LogCategory.Storage).Information(
                "Disc opened: {Game} ({GameId}), {Sectors} sectors, fixes 0x{FixMode:x8}, {Bios}.",
                _gameName,
                gameId[..Math.Min(8, gameId.Length)],
                disc.SectorCount,
                (uint)fixMode,
                biosText);

            if (autoStart)
            {
                StartEmulation(showGameView: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or DiscImageException or ArgumentException)
        {
            StatusText.Text = $"Disc error: {exception.Message}";
            _logger.ForCategory(LogCategory.Storage).Error(exception, "Disc open failed.");
            ShowError("Disc error", exception.Message);
            return false;
        }
    }

    private void OnPlayPause(object? sender, RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void TogglePlayPause()
    {
        if (_running)
        {
            PauseEmulation(showLibrary: false);
        }
        else
        {
            StartEmulation(showGameView: true);
        }
    }

    private void StartEmulation(bool showGameView)
    {
        if ((!_biosLoaded && _moviePlayback is null) || _machine is null || _runner is null)
        {
            StatusText.Text = "Open a 3DO BIOS before starting emulation.";
            return;
        }

        if (!_running)
        {
            _running = true;
            if (_runner.IsRunning)
            {
                _runner.Resume();
            }
            else
            {
                _runner.Start();
            }

            _logger.ForCategory(LogCategory.Host).Information("Emulation started.");
        }

        if (!string.IsNullOrWhiteSpace(_settings.DiscPath))
        {
            _playtime.Start(_gameId, _settings.DiscPath);
        }

        SetPlayState(running: true);
        if (showGameView)
        {
            ShowEmulationView();
        }

        if (_rendererKind == VideoRendererKind.OpenGl)
        {
            Video.RequestRedraw();
        }
        else if (_vulkanPresenter)
        {
            VulkanVideo.RequestRedraw();
        }

        StatusText.Text = string.IsNullOrWhiteSpace(_gameName)
            ? "Emulation running."
            : $"Running {_gameName}.";
    }

    private void PauseEmulation(bool showLibrary)
    {
        if (_running)
        {
            _running = false;
            CancelFastForward();
            _runner?.Pause();
            _playtime.Stop();
            RefreshLibraryDecorations();
            FlushNvram();
            _logger.ForCategory(LogCategory.Host).Information("Emulation paused.");
        }

        SetPlayState(running: false);
        if (showLibrary)
        {
            ShowLibraryView();
            StatusText.Text = "Emulation stopped.";
        }
        else
        {
            StatusText.Text = "Emulation paused.";
        }
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        StopEmulation();
    }

    private void StopEmulation()
    {
        StopMovie(save: true);
        StopInputRecording(save: true);
        CancelFastForward();

        // Stop, not pause: join the core thread and reset the machine so a later
        // Start (or a new game selection) begins from a known state.
        _runner?.Stop();
        _playtime.Stop();
        RefreshLibraryDecorations();
        FlushNvram();
        _machine?.Memory.ClearRam();
        _machine?.Reset();
        _running = false;
        SetPlayState(running: false);
        ShowLibraryView();
        StatusText.Text = "Emulation stopped.";
        _logger.ForCategory(LogCategory.Host).Information("Emulation stopped.");
    }

    private void SetPlayState(bool running)
    {
        PlayPauseText.Text = running ? "Pause" : "Start";
        PlayPauseMenuItem.Header = running ? "Pause" : "Start";
        HeaderPlayPauseText.Text = running ? "Pause" : "Start";
        if (this.FindResource(running ? "IconPause" : "IconPlay") is Geometry icon)
        {
            PlayPauseIcon.Data = icon;
            HeaderPlayPauseIcon.Data = icon;
        }
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        ResetMachine();
    }

    private void OnRewind(object? sender, RoutedEventArgs e)
    {
        RewindSeconds(1);
    }

    private void OnRewindFrame(object? sender, RoutedEventArgs e)
    {
        RewindFrames(1);
    }

    private void OnFastForward(object? sender, RoutedEventArgs e)
    {
        if (FastForwardMenuItem.IsChecked)
        {
            StartFastForward();
            if (!_fastForwardHeld)
            {
                FastForwardMenuItem.IsChecked = false;
            }
        }
        else
        {
            StopFastForward();
        }
    }

    private void ResetMachine()
    {
        CancelFastForward();
        _runner?.Invoke(() => _machine?.Reset());
        StatusText.Text = "Reset.";
        ShowOsd("Console reset.");
        _logger.ForCategory(LogCategory.Machine).Information("Machine reset.");
    }

    private void OnSaveState(object? sender, RoutedEventArgs e)
    {
        SaveStateToCurrentSlot();
    }

    private void SaveStateToCurrentSlot()
    {
        if (_machine is null)
        {
            StatusText.Text = "Nothing to save.";
            ShowOsd("Nothing to save.", OsdSeverity.Warning);
            return;
        }

        var slot = _slots.CurrentSlot;
        _runner!.Invoke(() =>
        {
            var discHash = _disc is null ? string.Empty : GameId.Compute(_disc);
            _slots.Save(_machine, discHash, _gameName, slot);
        });
        PersistSlot();
        UpdateSlotText();
        StatusText.Text = $"State saved: slot {slot + 1} ({_gameName}).";
        ShowOsd($"State saved: slot {slot + 1}", OsdSeverity.Success);
        _logger.ForCategory(LogCategory.State).Information(
            "State saved to slot {Slot} ({Game}).",
            slot + 1,
            _gameName);
    }

    private void OnLoadState(object? sender, RoutedEventArgs e)
    {
        LoadStateFromCurrentSlot();
    }

    private void LoadStateFromCurrentSlot()
    {
        var slot = _slots.CurrentSlot;
        if (_machine is null || !_slots.Exists(slot))
        {
            StatusText.Text = $"Slot {slot + 1} is empty.";
            ShowOsd($"Slot {slot + 1} is empty.", OsdSeverity.Warning);
            return;
        }

        _runner!.Invoke(() => _slots.Load(_machine, slot, _gameId));
        if (_machine.StateWarnings.Count > 0)
        {
            StatusText.Text = $"State loaded with warnings: {string.Join(" ", _machine.StateWarnings)}";
            ShowOsd($"State loaded with warnings: {string.Join(" ", _machine.StateWarnings)}", OsdSeverity.Warning);
            _logger.ForCategory(LogCategory.State).Warning(
                "State loaded from slot {Slot} with warnings: {Warnings}",
                slot + 1,
                string.Join(" ", _machine.StateWarnings));
        }
        else
        {
            StatusText.Text = $"State loaded: slot {slot + 1}.";
            ShowOsd($"State loaded: slot {slot + 1}", OsdSeverity.Success);
            _logger.ForCategory(LogCategory.State).Information("State loaded from slot {Slot}.", slot + 1);
        }
    }

    private void OnPreviousSlot(object? sender, RoutedEventArgs e)
    {
        SelectPreviousSlot();
    }

    private void SelectPreviousSlot()
    {
        _slots.Previous();
        PersistSlot();
        UpdateSlotText();
        StatusText.Text = $"Selected slot {_slots.CurrentSlot + 1}.";
        ShowOsd($"Selected slot {_slots.CurrentSlot + 1}");
    }

    private void OnNextSlot(object? sender, RoutedEventArgs e)
    {
        SelectNextSlot();
    }

    private void SelectNextSlot()
    {
        _slots.Next();
        PersistSlot();
        UpdateSlotText();
        StatusText.Text = $"Selected slot {_slots.CurrentSlot + 1}.";
        ShowOsd($"Selected slot {_slots.CurrentSlot + 1}");
    }

    private void PersistSlot()
    {
        _settings.SaveSlot = _slots.CurrentSlot;
        AppSettingsStore.Save(_settings);
    }

    private void UpdateSlotText()
    {
        SlotText.Text = $"Slot {_slots.CurrentSlot + 1}/{_slots.SlotCount}";
    }

    private void OnScreenshot(object? sender, RoutedEventArgs e)
    {
        CaptureScreenshot();
    }

    private void CaptureScreenshot()
    {
        if (_machine is null)
        {
            ShowOsd("Boot a disc before taking a screenshot.", OsdSeverity.Warning);
            return;
        }
        var directory = string.IsNullOrWhiteSpace(_settings.ScreenshotDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : _settings.ScreenshotDirectory;
        var name = string.IsNullOrWhiteSpace(_gameName) ? "5do" : SanitizeFileName(_gameName);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"{name}-{stamp}-f{_machine.FramesExecuted:D6}.png");
        var nativePath = Path.Combine(directory, $"{name}-{stamp}-f{_machine.FramesExecuted:D6}-native.png");
        if (Video.IsOpenGlAvailable)
        {
            Video.CaptureCompleted += OnCaptured;
            Video.RequestCapture();
            StatusText.Text = "Capturing screenshot...";
            return;

            void OnCaptured(object? sender, CaptureCompletedEventArgs e)
            {
                Video.CaptureCompleted -= OnCaptured;
                PngDumper.WriteBgra(e.Pixels, e.Width, e.Height, path);
                if (_settings.SaveNativeScreenshot)
                {
                    SaveNativeScreenshot(nativePath);
                }

                StatusText.Text = $"Screenshot: {path}";
                ShowOsd($"Screenshot: {Path.GetFileName(path)}", OsdSeverity.Success);
                _logger.ForCategory(LogCategory.Host).Information("Screenshot written: {Path}.", path);
            }
        }

        SaveNativeScreenshot(path);
        StatusText.Text = $"Screenshot: {path}";
        ShowOsd($"Screenshot: {Path.GetFileName(path)}", OsdSeverity.Success);
        _logger.ForCategory(LogCategory.Host).Information("Screenshot written from the CPU frame: {Path}.", path);
    }

    private void OnRecordInput(object? sender, RoutedEventArgs e)
    {
        if (RecordInputMenuItem.IsChecked)
        {
            StartInputRecording();
        }
        else
        {
            StopInputRecording(save: true);
        }
    }

    private void StartInputRecording()
    {
        if (_machine is null || _runner is null)
        {
            RecordInputMenuItem.IsChecked = false;
            StatusText.Text = "Open a 3DO BIOS before recording input.";
            return;
        }

        var directory = string.IsNullOrWhiteSpace(_settings.SaveDirectory)
            ? AppSettingsStore.DefaultDirectory
            : _settings.SaveDirectory;
        var name = string.IsNullOrWhiteSpace(_gameName) ? "5do" : SanitizeFileName(_gameName);
        var stamp = DateTime.Now.ToString(
            "yyyyMMdd-HHmmss",
            System.Globalization.CultureInfo.InvariantCulture);
        _inputRecordingPath = Path.Combine(directory, $"{name}-{stamp}-input.txt");
        _inputRecorder = new InputRecorder();
        _input.FrameProvider = () => _machine?.FramesExecuted ?? 0;
        _input.Recorder = _inputRecorder;
        StatusText.Text = $"Recording input to {_inputRecordingPath}.";
        _logger.ForCategory(LogCategory.Input).Information(
            "Input recording started: {Path}.",
            _inputRecordingPath);
    }

    private void StopInputRecording(bool save)
    {
        _input.Recorder = null;
        _input.FrameProvider = null;
        InputRecorder? recorder = _inputRecorder;
        var path = _inputRecordingPath;
        _inputRecorder = null;
        _inputRecordingPath = null;
        RecordInputMenuItem.IsChecked = false;
        if (!save || recorder is null || path is null)
        {
            return;
        }

        try
        {
            recorder.Save(path);
            StatusText.Text = $"Input recorded: {path} ({recorder.Frames} frames, {recorder.EntryCount} entries).";
            _logger.ForCategory(LogCategory.Input).Information(
                "Input recording saved: {Path} ({Frames} frames, {Entries} entries).",
                path,
                recorder.Frames,
                recorder.EntryCount);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = $"Input recording could not be saved: {exception.Message}";
            _logger.ForCategory(LogCategory.Input).Warning(
                exception,
                "Input recording save failed: {Path}.",
                path);
        }
    }

    private void OnRecordMovieAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(StartMovieRecordingAsync, "starting movie recording");
    }

    private void OnPlayMovieAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(PlayMovieAsync, "playing a movie");
    }

    private void OnStopMovie(object? sender, RoutedEventArgs e)
    {
        StopMovie(save: true);
    }

    private async Task StartMovieRecordingAsync()
    {
        if (_machine is null || _runner is null)
        {
            StatusText.Text = "Open a 3DO BIOS before recording a movie.";
            return;
        }

        StopMovie(save: true);
        var suffix = _gameId.Length > 0
            ? _gameId[..Math.Min(8, _gameId.Length)]
            : "5do";
        var stamp = DateTime.Now.ToString(
            "yyyyMMdd-HHmmss",
            System.Globalization.CultureInfo.InvariantCulture);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Record 5DO Movie",
            SuggestedFileName = $"{SanitizeFileName(_gameName.Length > 0 ? _gameName : "5do")}-{suffix}-{stamp}.5domovie",
            DefaultExtension = "5domovie",
            FileTypeChoices = [new FilePickerFileType("5DO movie") { Patterns = ["*.5domovie"] }],
        }).ConfigureAwait(true);
        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        _movieRecorder = new MovieRecorder();
        _movieRecordingPath = path;
        MovieRecorder recorder = _movieRecorder;
        _runner.Invoke(() =>
        {
            _movieStartFrame = _machine.FramesExecuted;
            using var state = new MemoryStream();
            _machine.SaveState(state, _gameId);
            _movieStartState = state.ToArray();
            _movieRecordingSource = new MovieRecordingPbusSource(_input, recorder);
            _machine.PbusSource = _movieRecordingSource;
        });

        StopMovieMenuItem.IsEnabled = true;
        StatusText.Text = _running
            ? $"Recording movie to {path}."
            : $"Movie armed for {path}; start emulation to record.";
        _logger.ForCategory(LogCategory.Host).Information("Movie recording started: {Path}.", path);
    }

    private async Task PlayMovieAsync()
    {
        if (_machine is null || _runner is null)
        {
            StatusText.Text = "Open a 3DO BIOS before playing a movie.";
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Play 5DO Movie",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("5DO movie") { Patterns = ["*.5domovie"] }],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is null)
        {
            return;
        }

        Movie movie;
        try
        {
            movie = MovieFile.Load(path);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException)
        {
            StatusText.Text = $"Movie could not be loaded: {exception.Message}";
            _logger.ForCategory(LogCategory.Host).Warning(exception, "Movie load failed: {Path}.", path);
            return;
        }

        StopMovie(save: true);
        _runner.Invoke(() =>
        {
            if (movie.State is not null)
            {
                using var state = new MemoryStream(movie.State);
                _machine.LoadState(state);
            }

            _moviePlayback = movie;
            _moviePlaybackSource = new MoviePbusSource(movie);
            _machine.PbusSource = _moviePlaybackSource;
        });

        StopMovieMenuItem.IsEnabled = true;
        if (!_running)
        {
            StartEmulation(showGameView: true);
        }

        StatusText.Text = $"Playing movie {Path.GetFileName(path)} ({movie.FrameCount} frames).";
        _logger.ForCategory(LogCategory.Host).Information(
            "Movie playback started: {Path} ({Frames} frames).",
            path,
            movie.FrameCount);
    }

    private void StopMovie(bool save)
    {
        MovieRecorder? recorder = _movieRecorder;
        MoviePbusSource? playback = _moviePlaybackSource;
        MovieRecordingPbusSource? recording = _movieRecordingSource;
        var recordingPath = _movieRecordingPath;
        byte[]? startState = _movieStartState;
        var startFrame = _movieStartFrame;
        _movieRecorder = null;
        _movieRecordingSource = null;
        _moviePlaybackSource = null;
        _moviePlayback = null;
        _movieRecordingPath = null;
        _movieStartState = null;
        StopMovieMenuItem.IsEnabled = false;

        if (_machine is not null && _runner is not null && (recording is not null || playback is not null))
        {
            _runner.Invoke(() =>
            {
                if (ReferenceEquals(_machine.PbusSource, recording)
                    || ReferenceEquals(_machine.PbusSource, playback))
                {
                    _machine.PbusSource = _input;
                }
            });
        }

        if (!save || recorder is null || recordingPath is null || _machine is null)
        {
            return;
        }

        MovieHeader? header = null;
        _runner?.Invoke(() =>
        {
            header = new MovieHeader
            {
                GameName = _gameName.Length > 0 ? _gameName : null,
                GameId = _gameId.Length > 0 ? _gameId : null,
                DiscHash = _gameId.Length > 0 ? _gameId : null,
                DiscFile = string.IsNullOrWhiteSpace(_settings.DiscPath)
                    ? null
                    : Path.GetFileName(_settings.DiscPath),
                BiosMd5 = _biosMd5.Length > 0 ? _biosMd5 : null,
                BiosName = string.IsNullOrWhiteSpace(_settings.BiosPath)
                    ? null
                    : Path.GetFileNameWithoutExtension(_settings.BiosPath),
                StartFrame = startFrame,
                FrameCount = recorder.FrameCount,
                FinalFrameHash = FrameHasher.Hash(_machine.Frame),
                FinalProgramCounter = _machine.Cpu.ProgramCounter,
                FinalStateHash = HashMachineState(_machine),
                HasState = startState is not null,
                Options = BuildMovieOptions(_machine),
            };
        });

        if (header is null)
        {
            return;
        }

        try
        {
            MovieFile.Save(recordingPath, recorder.Build(header, startState));
            StatusText.Text = $"Movie recorded: {recordingPath} ({recorder.FrameCount} frames).";
            _logger.ForCategory(LogCategory.Host).Information(
                "Movie recording saved: {Path} ({Frames} frames).",
                recordingPath,
                recorder.FrameCount);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            StatusText.Text = $"Movie could not be saved: {exception.Message}";
            _logger.ForCategory(LogCategory.Host).Warning(
                exception,
                "Movie save failed: {Path}.",
                recordingPath);
        }
    }

    private void UpdateMovieStatus()
    {
        MoviePbusSource? playback = _moviePlaybackSource;
        if (playback is null)
        {
            return;
        }

        if (playback.IsFinished)
        {
            var frameCount = playback.FrameCount;
            StopMovie(save: false);
            PauseEmulation(showLibrary: false);
            StatusText.Text = $"Movie finished ({frameCount} frames).";
            return;
        }

        ControllerState player1 = playback.CurrentControllers.Length > 0
            ? playback.CurrentControllers[0]
            : ControllerState.Empty;
        StatusText.Text =
            $"Movie {playback.Position}/{playback.FrameCount}: "
            + $"{PbusDecoder.FormatButtons(player1)}";
    }

    private static uint HashMachineState(FiveDoMachine machine)
    {
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        return MovieHash.Fnv1a(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }

    private static Dictionary<string, string> BuildMovieOptions(FiveDoMachine machine)
    {
        EmulatorTuning tuning = machine.Tuning;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profile"] = EmulationProfiles.Detect(tuning).ToString(),
            ["armTiming"] = tuning.ArmTiming.ToString(),
            ["clioTimers"] = tuning.ClioTimers.ToString(),
            ["celTiming"] = tuning.CelTiming.ToString(),
            ["cdromTiming"] = tuning.CdromTiming.ToString(),
            ["cdSpeed"] = tuning.CdSpeed.ToString(),
            ["cpuClock"] = tuning.ArmClock.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["randomSeed"] = tuning.RandomSeed?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "default",
            ["fixMode"] = $"0x{(uint)tuning.FixMode:x8}",
            ["highResolution"] = tuning.HighResolution.ToString(),
            ["operaDipir"] = tuning.OperaDipir.ToString(),
            ["operaSoftReset"] = tuning.OperaSoftReset.ToString(),
        };
    }

    private void SaveNativeScreenshot(string path)
    {
        if (_machine is null)
        {
            return;
        }

        _runner!.Invoke(() =>
        {
            var frame = new FrameBuffer(_machine.Frame.SourceWidth, _machine.Frame.SourceHeight);
            FrameAssembler.Assemble(_machine.Frame, frame, frame.Width, frame.Height);
            PngDumper.Write(frame, path);
        });
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = value.ToCharArray();
        for (var i = 0; i < buffer.Length; i++)
        {
            if (Array.IndexOf(invalid, buffer[i]) >= 0)
            {
                buffer[i] = '-';
            }
        }

        return new string(buffer).Trim();
    }

    private void ShowError(string title, string message)
    {
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };
        ToolTip.SetTip(text, "Error details.");
        var close = new Button
        {
            Content = "Close",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
        };
        ToolTip.SetTip(close, "Close this dialog.");
        var panel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
        };
        panel.Children.Add(text);
        panel.Children.Add(close);
        var window = new Window
        {
            Title = title,
            Width = 480,
            Height = 220,
            Content = panel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        close.Click += (_, _) => window.Close();
        _ = window.ShowDialog(this);
    }

    private void OnFullscreen(object? sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            Cursor = null;
            SetChromeVisible(visible: true);
            return;
        }

        if (!EmulationView.IsVisible)
        {
            if (_machine is null || !_biosLoaded)
            {
                StatusText.Text = "Start a game to use fullscreen.";
                return;
            }

            ShowEmulationView();
        }

        EnterFullscreen();
    }

    private void EnterFullscreenIfRequested()
    {
        if (Program.StartFullscreen)
        {
            EnterFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            return;
        }

        WindowState = WindowState.FullScreen;
        Cursor = new Cursor(StandardCursorType.None);
        SetChromeVisible(visible: false);
    }

    private void SetChromeVisible(bool visible)
    {
        MainMenu.IsVisible = visible;
        ToolbarBorder.IsVisible = visible;
        StatusBarBorder.IsVisible = visible;
        EmulationHeader.IsVisible = visible;
        LogPanel.IsVisible = visible && _logDockVisible;
    }

    private void OnShowLibrary(object? sender, RoutedEventArgs e)
    {
        ShowLibraryView();
    }

    private void ShowLibraryView()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            Cursor = null;
            SetChromeVisible(visible: true);
        }

        Title = "5DO";
        EmulationView.IsVisible = false;
        VulkanVideo.SetPresentationVisible(false);
        LibraryView.IsVisible = true;
        _input.Keyboard.Reset();
        ApplyLibraryFilter();
    }

    private void ShowEmulationView()
    {
        if (_machine is null)
        {
            StatusText.Text = "Boot a disc first.";
            return;
        }

        Title = string.IsNullOrWhiteSpace(_gameName) ? "5DO" : $"{_gameName} — 5DO";
        UpdateEmulationHeader();
        LibraryView.IsVisible = false;
        EmulationView.IsVisible = true;
        if (_vulkanPresenter)
        {
            // Hiding the emulation view can stop the embedded surface. Bring it back
            // before presenting the first frame of the restarted game.
            if (!VulkanVideo.IsRunning)
            {
                try
                {
                    _logger.ForCategory(LogCategory.Video).Information(
                        "Vulkan surface was stopped; restarting it for the game view.");
                    VulkanVideo.Start();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    OnVulkanFailed(this, new VulkanFailedEventArgs(exception));
                }
            }

            VulkanVideo.SetPresentationVisible(true);
            VulkanVideo.RequestRedraw();
        }
        else if (_rendererKind == VideoRendererKind.OpenGl && !_openGlDisabled && Video.Capabilities is null)
        {
            // A stopped OpenGL surface is reattached to create a fresh context.
            ReattachOpenGlSurface();
        }

        // Returning from the library can leave its search box focused. The key
        // handlers intentionally ignore TextBox input, so move focus back to the
        // window before the game starts receiving pad input.
        FocusManager.Focus(this);
    }

    private void UpdateEmulationHeader()
    {
        EmulationTitleText.Text = string.IsNullOrWhiteSpace(_gameName) ? "No disc loaded" : _gameName;
        var id = _gameId.Length >= 8 ? _gameId[..8].ToUpperInvariant() : _gameId.ToUpperInvariant();
        EmulationSubtitleText.Text = id.Length == 0
            ? "Open a disc to begin"
            : $"{id} · fixes 0x{(uint)_settings.ResolveFixMode(_gameId):x8}";
    }

    private void OnLogs(object? sender, RoutedEventArgs e)
    {
        _ = new LogsWindow(_logSink).ShowDialog(this);
    }

    private void OnAbout(object? sender, RoutedEventArgs e)
    {
        var text = new StringBuilder();
        text.AppendLine($"{FiveDoVersion.ProductName} {FiveDoVersion.Version}");
        text.AppendLine();
        text.AppendLine("A pure C# / .NET 3DO emulator with an Avalonia frontend and OpenGL, Vulkan, or software presentation.");
        RendererCapabilities? renderer = Video.Capabilities ?? _cpuVideo?.Capabilities;
        if (renderer is { } capabilities)
        {
            text.AppendLine(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Renderer: {capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion} "
                + $"({capabilities.Api}), max texture {capabilities.MaxTextureSize}.");
        }
        else
        {
            text.AppendLine("Renderer: CPU fallback (OpenGL unavailable).");
        }

        text.AppendLine($"Output resolution: {_settings.OutputResolution}.");

        if (_machine is not null)
        {
            text.AppendLine(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Machine: frame {_machine.FramesExecuted}, PC 0x{_machine.Cpu.ProgramCounter:x8}, "
                + $"DSP 0x{_machine.Dsp.ProgramCounter:x4}.");
        }

        _gamepad?.Poll();

        if (!string.IsNullOrWhiteSpace(_gameName))
        {
            var id = _gameId.Length > 8 ? _gameId[..8] : _gameId;
            text.AppendLine(
                $"Disc: {_gameName} ({id}), fixes 0x{(uint)_settings.ResolveFixMode(_gameId):x8}.");
        }

        text.AppendLine($"BIOS: {(_settings.BiosPath.Length > 0 ? _settings.BiosPath : "(not set)")}");
        if (!string.IsNullOrWhiteSpace(_bios2Md5))
        {
            text.AppendLine($"BIOS2: {_settings.Bios2Path} ({_bios2Md5[..Math.Min(8, _bios2Md5.Length)]})");
        }

        if (!string.IsNullOrWhiteSpace(_biosSelectionReason))
        {
            text.AppendLine($"BIOS selection: {_biosSelectionReason}");
        }

        text.AppendLine($"Save states: {_settings.SaveDirectory}");
        text.AppendLine($"Screenshots: {_settings.ScreenshotDirectory}");
        text.AppendLine(
            _gamepad is null
                ? "Gamepads: backend unavailable."
                : $"Gamepads: {_gamepad.ConnectedControllers} connected ({_gamepad.SourceName}).");
        text.AppendLine();
        text.AppendLine("5DO is distributed under the FreeDO modified GNU LGPL. See LICENSE, "
            + "THIRD-PARTY-NOTICES.md, and docs/Licensing.md before distributing builds.");
        text.AppendLine();
        text.AppendLine("This project does not include BIOS ROMs or game images.");

        var textBlock = new TextBlock
        {
            Text = text.ToString(),
            TextWrapping = TextWrapping.Wrap,
        };
        ToolTip.SetTip(textBlock, "Version, renderer, audio, input, and license information.");
        var scroll = new ScrollViewer
        {
            Content = textBlock,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var close = new Button
        {
            Content = "Close",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
        };
        ToolTip.SetTip(close, "Close this dialog.");
        var panel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
        };
        panel.Children.Add(scroll);
        panel.Children.Add(close);
        var window = new Window
        {
            Title = "About 5DO",
            Width = 460,
            MaxHeight = 720,
            SizeToContent = SizeToContent.Height,
            Content = panel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        close.Click += (_, _) => window.Close();
        _ = window.ShowDialog(this);
    }

    private void OnDonationStatus(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowDonationStatusAsync, "checking donation status");
    }

    private void OnDonate(object? sender, RoutedEventArgs e)
    {
        OpenDonationPage();
    }

    private void OnDismissDonationBanner(object? sender, RoutedEventArgs e)
    {
        _donationBannerDismissed = true;
        UpdateDonationBanner();
    }

    private async Task ShowDonationStatusAsync()
    {
        if (_donationCheckInProgress)
        {
            return;
        }

        var email = await DonationEmailWindow
            .ShowAsync(this, _settings.DonationEmail)
            .ConfigureAwait(true);
        if (email is null)
        {
            return;
        }

        _donationBannerDismissed = false;
        SaveDonationEmail(email);
        await RefreshDonationStatusAsync(showMessages: true).ConfigureAwait(true);
    }

    private void SaveDonationEmail(string email)
    {
        var trimmed = email.Trim();
        if (string.Equals(_settings.DonationEmail, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.DonationEmail = trimmed;
        _settings.HasDonated = false;
        _settings.DonationLastCheckedUtc = null;
        _donationBannerMessage = null;
        AppSettingsStore.Save(_settings);
        UpdateDonationBanner();
    }

    private void OnDonationCheckActivated(object? sender, EventArgs e)
    {
        if (Program.SmokeTest ||
            _donationCheckInProgress ||
            _settings.HasDonated ||
            string.IsNullOrWhiteSpace(_settings.DonationEmail) ||
            DateTime.UtcNow - _lastDonationCheckUtc < TimeSpan.FromMinutes(2))
        {
            return;
        }

        AsyncEvent.Run(
            () => RefreshDonationStatusAsync(showMessages: false),
            "checking donation status");
    }

    private async Task RefreshDonationStatusAsync(bool showMessages)
    {
        if (_donationCheckInProgress)
        {
            return;
        }

        var email = _settings.DonationEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        _donationCheckInProgress = true;
        _lastDonationCheckUtc = DateTime.UtcNow;
        if (showMessages)
        {
            _donationBannerMessage = "Checking donation status...";
        }

        UpdateDonationBanner();
        try
        {
            DonationVerificationResult result = await DonationVerificationClient
                .VerifyAsync(email)
                .ConfigureAwait(true);
            switch (result.Status)
            {
                case DonationVerificationStatus.Donated:
                    _settings.HasDonated = true;
                    _settings.DonationLastCheckedUtc = DateTime.UtcNow;
                    AppSettingsStore.Save(_settings);
                    _donationBannerMessage = null;
                    _logger.ForCategory(LogCategory.Host).Information(
                        "Donation confirmed for the saved email.");
                    if (showMessages)
                    {
                        StatusText.Text = "Thank you for supporting 5DO!";
                    }

                    break;

                case DonationVerificationStatus.NotDonated:
                    _settings.HasDonated = false;
                    _settings.DonationLastCheckedUtc = DateTime.UtcNow;
                    AppSettingsStore.Save(_settings);
                    if (showMessages)
                    {
                        _donationBannerMessage =
                            "No donation was found for that email yet. If you just donated, try again in a moment.";
                    }

                    break;

                default:
                    if (showMessages)
                    {
                        _donationBannerMessage =
                            "Could not check donation status. Please try again later.";
                    }

                    break;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger.ForCategory(LogCategory.Host).Warning(
                exception,
                "Unexpected donation verification failure.");
            if (showMessages)
            {
                _donationBannerMessage =
                    "Could not check donation status. Please try again later.";
            }
        }
        finally
        {
            _donationCheckInProgress = false;
            UpdateDonationBanner();
        }
    }

    private void UpdateDonationBanner()
    {
        DonationBanner.IsVisible = !_settings.HasDonated && !_donationBannerDismissed;
        if (!DonationBanner.IsVisible)
        {
            return;
        }

        DonationBannerText.Text = string.IsNullOrWhiteSpace(_donationBannerMessage)
            ? "5DO is free donationware. If it helps you, please consider supporting development."
            : _donationBannerMessage;
        DonationStatusButton.IsEnabled = !_donationCheckInProgress;
        DonateButton.IsEnabled = !_donationCheckInProgress;
    }

    private void OpenDonationPage()
    {
        try
        {
            Uri uri = DonationVerificationClient.CreateDonationPageUri(_settings.DonationEmail);
            var launcher = GetTopLevel(this)?.Launcher;
            if (launcher is null)
            {
                StatusText.Text = "Could not open the donation page.";
                return;
            }

            AsyncEvent.Run(
                () => LaunchDonationPageAsync(launcher, uri),
                "opening the donation page");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger.ForCategory(LogCategory.Host).Warning(
                exception,
                "Could not open the donation page.");
            StatusText.Text = "Could not open the donation page.";
        }
    }

    private async Task LaunchDonationPageAsync(ILauncher launcher, Uri uri)
    {
        if (!await launcher.LaunchUriAsync(uri).ConfigureAwait(true))
        {
            StatusText.Text = "Could not open the donation page.";
            return;
        }

        _lastDonationCheckUtc = DateTime.MinValue;
        if (_settings.HasDonated)
        {
            return;
        }

        _donationBannerDismissed = false;
        _donationBannerMessage = "After donating, click 'I already donated' to check your support.";
        UpdateDonationBanner();
    }

    private void OnAddFolderAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(AddFolderAsync, "adding a game folder");
    }

    private async Task AddFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add a game folder",
            AllowMultiple = true,
        }).ConfigureAwait(true);

        var added = false;
        foreach (var folder in folders)
        {
            var path = folder.TryGetLocalPath();
            if (path is null || _settings.LibraryFolders.Any(existing =>
                    string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _settings.LibraryFolders.Add(path);
            added = true;
        }

        if (added)
        {
            AppSettingsStore.Save(_settings);
            await RefreshLibraryAsync().ConfigureAwait(true);
        }
        else if (folders.Count > 0)
        {
            StatusText.Text = "That folder is already in the game library.";
        }
    }

    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(RefreshLibraryAsync, "refreshing the game library");
    }

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyLibraryFilter();
    }

    private void OnIconSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        var width = Math.Round(e.NewValue);
        Resources["LibraryCardWidth"] = width;
        Resources["LibraryCardImageHeight"] = Math.Max(80, width - 14);
    }

    private void OnGridView(object? sender, RoutedEventArgs e)
    {
        SetLibraryMode(grid: true);
    }

    private void OnListView(object? sender, RoutedEventArgs e)
    {
        SetLibraryMode(grid: false);
    }

    private void SetLibraryMode(bool grid)
    {
        _gridMode = grid;
        GridLibraryList.IsVisible = grid;
        ListLibraryList.IsVisible = !grid;
        GridModeMenuItem.IsChecked = grid;
        ListModeMenuItem.IsChecked = !grid;
        GridViewButton.Classes.Set("active", grid);
        ListViewButton.Classes.Set("active", !grid);
        if ((GridLibraryList.SelectedItem as LibraryItem) is { } selected)
        {
            SelectLibraryItem(selected);
        }
    }

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || GridLibraryList.SelectedItem is not LibraryItem item)
        {
            return;
        }

        _syncingSelection = true;
        ListLibraryList.SelectedItem = item;
        _syncingSelection = false;
        UpdateSelectedStatus(item);
    }

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || ListLibraryList.SelectedItem is not LibraryItem item)
        {
            return;
        }

        _syncingSelection = true;
        GridLibraryList.SelectedItem = item;
        _syncingSelection = false;
        UpdateSelectedStatus(item);
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        BootLibrarySelection(GridLibraryList.SelectedItem as LibraryItem);
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        BootLibrarySelection(ListLibraryList.SelectedItem as LibraryItem);
    }

    private void OnLibraryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var item = ReferenceEquals(sender, ListLibraryList)
                ? ListLibraryList.SelectedItem as LibraryItem
                : GridLibraryList.SelectedItem as LibraryItem;
            BootLibrarySelection(item);
            e.Handled = true;
        }
    }

    private void BootLibrarySelection(LibraryItem? item)
    {
        if (item is null)
        {
            StatusText.Text = "Select a game first.";
            return;
        }

        OpenDiscPath(item.Path, autoStart: true);
    }

    private LibraryItem? SelectedLibraryItem()
    {
        var item = _gridMode
            ? GridLibraryList.SelectedItem as LibraryItem
            : ListLibraryList.SelectedItem as LibraryItem;
        return item
            ?? GridLibraryList.SelectedItem as LibraryItem
            ?? ListLibraryList.SelectedItem as LibraryItem;
    }

    private void SelectLibraryItem(LibraryItem item)
    {
        _syncingSelection = true;
        GridLibraryList.SelectedItem = item;
        ListLibraryList.SelectedItem = item;
        _syncingSelection = false;
        UpdateSelectedStatus(item);
    }

    private void UpdateSelectedStatus(LibraryItem item)
    {
        StatusText.Text = string.IsNullOrWhiteSpace(item.Entry.Error)
            ? $"{item.Name} · {item.Format} · {item.SectorText}"
            : $"{item.Name} · {item.Entry.Error}";
    }

    private void OnLibraryViewChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_libraryReady)
        {
            ApplyLibraryFilter();
        }
    }

    private void OnToggleFavorite(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path })
        {
            return;
        }

        LibraryItem? item = _allGames.FirstOrDefault(candidate =>
            string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        var favorite = _libraryState.ToggleFavorite(item.Entry.GameId, item.Path);
        LibraryStateStore.Save(_libraryState);
        item.Update(DecorateEntry(item.Entry));
        ApplyLibraryFilter();
        StatusText.Text = favorite
            ? $"{item.Name} added to favorites."
            : $"{item.Name} removed from favorites.";
        e.Handled = true;
    }

    private void ApplyLibraryFilter()
    {
        if (!_libraryReady)
        {
            return;
        }

        var query = SearchBox.Text?.Trim() ?? string.Empty;
        LibraryFilter filter = CurrentLibraryFilter();
        LibrarySort sort = CurrentLibrarySort();
        var selected = SelectedLibraryItem();
        var visible = _allGames
            .Where(item => LibraryQuery.Matches(item.Entry, query, filter))
            .ToList();
        visible.Sort((left, right) => LibraryQuery.Compare(left.Entry, right.Entry, sort));

        _visibleGames.Clear();
        foreach (LibraryItem item in visible)
        {
            _visibleGames.Add(item);
        }

        _syncingSelection = true;
        if (selected is not null && _visibleGames.Contains(selected))
        {
            GridLibraryList.SelectedItem = selected;
            ListLibraryList.SelectedItem = selected;
        }
        else
        {
            GridLibraryList.SelectedItem = null;
            ListLibraryList.SelectedItem = null;
        }

        _syncingSelection = false;

        var hasGames = _allGames.Count > 0;
        var hasVisible = _visibleGames.Count > 0;
        EmptyLibraryPanel.IsVisible = !hasVisible;
        if (!hasGames)
        {
            EmptyLibraryTitle.Text = "No games found";
            EmptyLibraryText.Text = _settings.LibraryFolders.Count == 0
                ? "Add a folder that contains ISO, BIN, CUE, or CHD images."
                : "No supported disc images were found in the configured folders.";
        }
        else if (!hasVisible)
        {
            EmptyLibraryTitle.Text = "No matching games";
            EmptyLibraryText.Text = query.Length > 0
                ? $"No game matches \"{query}\"."
                : filter == LibraryFilter.Favorites
                    ? "No favorites yet. Select a game and click the star."
                    : "No game matches the selected collection.";
        }

        var narrowed = query.Length > 0 || filter != LibraryFilter.All;
        LibraryCountText.Text = !hasGames
            ? "0 games"
            : narrowed
                ? $"{_visibleGames.Count} of {CountLabel(_allGames.Count)}"
                : CountLabel(_allGames.Count);
    }

    private LibraryFilter CurrentLibraryFilter()
    {
        return ParseLibraryTag(LibraryFilterBox.SelectedItem, LibraryFilter.All);
    }

    private LibrarySort CurrentLibrarySort()
    {
        return ParseLibraryTag(LibrarySortBox.SelectedItem, LibrarySort.Title);
    }

    private static T ParseLibraryTag<T>(object? selectedItem, T fallback)
        where T : struct, Enum
    {
        return selectedItem is ComboBoxItem { Tag: string tag }
               && Enum.TryParse(tag, ignoreCase: true, out T value)
               && Enum.IsDefined(value)
            ? value
            : fallback;
    }

    private GameLibraryEntry DecorateEntry(GameLibraryEntry entry)
    {
        return _libraryMetadata.Apply(_libraryState.Apply(entry));
    }

    private void RefreshLibraryDecorations()
    {
        foreach (LibraryItem item in _allGames)
        {
            item.Update(DecorateEntry(item.Entry));
        }

        if (_libraryReady)
        {
            ApplyLibraryFilter();
        }
    }

    private static string CountLabel(int count)
    {
        return count == 1 ? "1 game" : $"{count} games";
    }

    private async Task RefreshLibraryAsync()
    {
        if (Program.SmokeTest)
        {
            return;
        }

        _libraryMetadata = LibraryMetadataCatalog.Load(_settings.ResolveLibraryMetadataPath());
        var generation = ++_scanGeneration;
        _libraryScanCts?.Cancel();
        _libraryScanCts?.Dispose();
        var scanCts = new CancellationTokenSource();
        _libraryScanCts = scanCts;
        var folders = _settings.LibraryFolders.Count > 0
            ? _settings.LibraryFolders.ToList()
            : RecentFolders();
        var coverDirectory = _settings.CoverDirectory;
        StatusText.Text = folders.Count == 0
            ? "No game folders configured. Use File > Add Game Folder."
            : "Scanning game folders...";

        var scanToken = scanCts.Token;
        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<GameLibraryEntry> finalEntries;
        try
        {
            GameLibraryScanResult initial = folders.Count == 0
                ? new GameLibraryScanResult([], [])
                : await Task.Run(
                    () => GameLibraryScanner.BeginScan(
                        folders,
                        cachePath: GameLibraryScanner.DefaultCachePath,
                        cancellationToken: scanToken,
                        coverDirectory: coverDirectory),
                    scanToken).ConfigureAwait(true);
            if (_shuttingDown || generation != _scanGeneration)
            {
                return;
            }

            ReplaceLibraryEntries(initial.Entries);
            finalEntries = initial.Entries;
            _logger.ForCategory(LogCategory.Storage).Information(
                "Library listed: {Count} entries in {ElapsedMs} ms ({Pending} pending metadata).",
                initial.Entries.Count,
                stopwatch.ElapsedMilliseconds,
                initial.PendingPaths.Count);
            if (initial.PendingPaths.Count > 0)
            {
                var pendingCount = initial.PendingPaths.Count;
                var metadataProgress = new Progress<int>(count =>
                {
                    if (!_shuttingDown && generation == _scanGeneration && _libraryScanCts is not null)
                    {
                        StatusText.Text = $"Scanning metadata... {count} of {pendingCount} games";
                    }
                });
                IReadOnlyList<GameLibraryEntry> enriched = await Task.Run(
                    () => GameLibraryScanner.InspectPending(
                        initial.PendingPaths,
                        cachePath: GameLibraryScanner.DefaultCachePath,
                        progress: metadataProgress,
                        coverDirectory: coverDirectory,
                        cancellationToken: scanToken),
                    scanToken).ConfigureAwait(true);
                if (_shuttingDown || generation != _scanGeneration)
                {
                    return;
                }

                finalEntries = initial.Entries
                    .Where(entry => !entry.Pending)
                    .Concat(enriched)
                    .ToList();
                ReplaceLibraryEntries(finalEntries);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = $"Library scan failed: {exception.Message}";
            _logger.ForCategory(LogCategory.Storage).Error(exception, "Library scan failed.");
            return;
        }
        finally
        {
            if (ReferenceEquals(_libraryScanCts, scanCts))
            {
                _libraryScanCts = null;
            }

            scanCts.Dispose();
        }

        stopwatch.Stop();
        _logger.ForCategory(LogCategory.Storage).Information(
            "Library metadata completed: {Count} entries in {ElapsedMs} ms.",
            finalEntries.Count,
            stopwatch.ElapsedMilliseconds);

        StatusText.Text = finalEntries.Count == 0
            ? "No discs found. Add a game folder from the File menu."
            : $"{CountLabel(finalEntries.Count)} in the library.";
    }

    private void ReplaceLibraryEntries(IReadOnlyList<GameLibraryEntry> entries)
    {
        var selectedPath = SelectedLibraryItem()?.Path;
        ClearLibraryItems();
        foreach (var entry in entries
                     .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            _allGames.Add(new LibraryItem(DecorateEntry(entry)));
        }

        ApplyLibraryFilter();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var reselect = _visibleGames.FirstOrDefault(item =>
            string.Equals(item.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
        if (reselect is not null)
        {
            SelectLibraryItem(reselect);
        }
    }

    private void ClearLibraryItems()
    {
        _syncingSelection = true;
        GridLibraryList.SelectedItem = null;
        ListLibraryList.SelectedItem = null;
        _syncingSelection = false;
        _visibleGames.Clear();
        foreach (var item in _allGames)
        {
            item.Dispose();
        }

        _allGames.Clear();
        CoverCache.Clear();
    }

    private List<string> RecentFolders()
    {
        var folders = new List<string>();
        foreach (var disc in _settings.RecentDiscs)
        {
            var folder = Path.GetDirectoryName(disc);
            if (folder is not null && !folders.Exists(existing =>
                    string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase)))
            {
                folders.Add(folder);
            }
        }

        return folders;
    }

    private void UpdateRecentMenu()
    {
        RecentMenu.Items.Clear();
        if (_settings.RecentDiscs.Count == 0)
        {
            var empty = new MenuItem { Header = "No recent discs", IsEnabled = false };
            ToolTip.SetTip(empty, "No discs have been opened recently.");
            RecentMenu.Items.Add(empty);
            return;
        }

        foreach (var path in _settings.RecentDiscs.Take(10))
        {
            var item = new MenuItem { Header = Path.GetFileName(path) };
            ToolTip.SetTip(item, path);
            item.Click += (_, _) => OpenDiscPath(path, autoStart: true);
            RecentMenu.Items.Add(item);
        }

        RecentMenu.Items.Add(new Separator());
        var clear = new MenuItem { Header = "Clear Recent" };
        ToolTip.SetTip(clear, "Clear the recent-discs list.");
        clear.Click += (_, _) =>
        {
            _settings.RecentDiscs.Clear();
            AppSettingsStore.Save(_settings);
            UpdateRecentMenu();
        };
        RecentMenu.Items.Add(clear);
    }

    private void OnToggleLog(object? sender, RoutedEventArgs e)
    {
        SetLogDockVisible(!_logDockVisible);
    }

    private void SetLogDockVisible(bool visible)
    {
        _logDockVisible = visible;
        LogPanel.IsVisible = visible && WindowState != WindowState.FullScreen;
        ShowLogMenuItem.IsChecked = visible;
        LogsButton.Classes.Set("active", visible);
        if (visible)
        {
            RefreshLogDock(force: true);
        }
    }

    private void OnClearLog(object? sender, RoutedEventArgs e)
    {
        _logSink.Clear();
        RefreshLogDock(force: true);
    }

    private void RefreshLogDock(bool force)
    {
        var entries = _logSink.Entries;
        if (!force && entries.Count == _lastLogEntryCount)
        {
            return;
        }

        _lastLogEntryCount = entries.Count;
        var start = Math.Max(0, entries.Count - 250);
        var lines = new List<string>(entries.Count - start);
        for (var i = start; i < entries.Count; i++)
        {
            var entry = entries[i];
            var time = entry.TimestampUtc == default
                ? string.Empty
                : entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " ";
            lines.Add($"{time}[{entry.Level,-11}] {entry.Category,-12} {entry.Message}");
        }

        LogList.ItemsSource = lines;
        LogSummaryText.Text = entries.Count == 1 ? "1 entry" : $"{entries.Count} entries";
        if (lines.Count > 0)
        {
            LogList.ScrollIntoView(lines[^1]);
        }
    }

    private void OnSettingsAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowSettingsAsync, "showing settings");
    }

    private async Task ShowSettingsAsync()
    {
        var previousCoverDirectory = _settings.CoverDirectory;
        var previousLibraryMetadataPath = _settings.ResolveLibraryMetadataPath();
        var previousBiosPath = _settings.BiosPath;
        var previousBios2Path = _settings.Bios2Path;
        var previousBiosFolder = _settings.BiosFolder;
        var previousCheatsDirectory = _settings.ResolveCheatsDirectory();
        var previousAutoBios = _settings.AutoBios;
        var previousBiosOverride = _settings.GetBiosOverride(_gameId);
        var previousInputDevices = string.Join(",", _settings.InputDevices);
        var window = new SettingsWindow(_settings, _gameId, _gameName);
        if (!await window.ShowDialog<bool>(this).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            window.ApplyTo(_settings);
        }
        catch (FormatException exception)
        {
            StatusText.Text = $"Settings error: {exception.Message}";
            _logger.ForCategory(LogCategory.Host).Warning(exception, "Settings dialog returned invalid values.");
            return;
        }

        AppSettingsStore.Save(_settings);
        if (!string.Equals(
                previousLibraryMetadataPath,
                _settings.ResolveLibraryMetadataPath(),
                StringComparison.OrdinalIgnoreCase))
        {
            _libraryMetadata = LibraryMetadataCatalog.Load(_settings.ResolveLibraryMetadataPath());
            RefreshLibraryDecorations();
        }

        _settings.ApplyInputDevices(_input);
        if (!string.Equals(
                previousInputDevices,
                string.Join(",", _settings.InputDevices),
                StringComparison.Ordinal))
        {
            _runner?.ResetRewind();
        }

        Video.Settings = _settings.ToRendererSettings(_gameId);
        if (_runner is not null)
        {
            _runner.FrameSkip = _settings.FrameSkip;
        }

        OverlayPanel.IsVisible = _settings.ShowOverlay;
        InputDisplayPanel.IsVisible = _settings.ShowInputDisplay;
        InputDisplayMenuItem.IsChecked = _settings.ShowInputDisplay;
        ApplyOsdSettings();

        ApplyAudioSettings();
        ApplyRendererSelection();
        HighResMenuItem.IsChecked = _settings.HighResolution;
        UpdateEmulationHeader();
        if (!string.Equals(
                previousCoverDirectory,
                _settings.CoverDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            AsyncEvent.Run(RefreshLibraryAsync, "refreshing game covers");
        }

        var biosSettingsChanged =
            !string.Equals(previousBiosPath, _settings.BiosPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previousBios2Path, _settings.Bios2Path, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previousBiosFolder, _settings.BiosFolder, StringComparison.OrdinalIgnoreCase)
            || previousAutoBios != _settings.AutoBios
            || !string.Equals(previousBiosOverride, _settings.GetBiosOverride(_gameId), StringComparison.OrdinalIgnoreCase);
        var nvramSettingsChanged =
            _settings.NvramStorageMode != _nvramMode
            || _settings.NvramVersion != _nvramVersion
            || !string.Equals(_settings.NvramDirectory, _nvramDirectory, StringComparison.OrdinalIgnoreCase);
        if (_machine is not null
            && (biosSettingsChanged
                || nvramSettingsChanged
                || _settings.HighResolution != _machine.Tuning.HighResolution
                || _settings.ArmTiming != _machine.Tuning.ArmTiming
                || _settings.ClioTimers != _machine.Tuning.ClioTimers
                || _settings.CelTiming != _machine.Tuning.CelTiming
                || _settings.CdromTiming != _machine.Tuning.CdromTiming
                || _settings.CdSpeed != _machine.Tuning.CdSpeed
                || _settings.ResolveArmClock() != _machine.Tuning.ArmClock
                || _settings.RandomSeed != _machine.Tuning.RandomSeed))
        {
            RecreateMachine();
            ShowOsd("Settings saved; machine restarted.", OsdSeverity.Success);
            return;
        }

        if (_machine is null && nvramSettingsChanged)
        {
            ReplaceNvramStore(_gameId.Length > 0 ? _gameId : null);
        }

        if (!string.Equals(
                previousCheatsDirectory,
                _settings.ResolveCheatsDirectory(),
                StringComparison.OrdinalIgnoreCase))
        {
            ReloadCheats();
        }

        StatusText.Text = "Settings saved.";
        ShowOsd("Settings saved.", OsdSeverity.Success);
        _logger.ForCategory(LogCategory.Host).Information("Settings saved.");
    }

    private void OnBiosNvramManagerAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowBiosNvramManagerAsync, "showing the BIOS/NVRAM manager");
    }

    private async Task ShowBiosNvramManagerAsync()
    {
        var wasRunning = _running;
        if (wasRunning)
        {
            // NVRAM reset/delete must not race the running store's next flush.
            PauseEmulation(showLibrary: false);
        }

        var previousBiosPath = _settings.BiosPath;
        var previousBios2Path = _settings.Bios2Path;
        var previousBiosFolder = _settings.BiosFolder;
        var previousBiosOverride = _settings.GetBiosOverride(_gameId);
        var previousNvramMode = _settings.NvramStorageMode;
        var previousNvramVersion = _settings.NvramVersion;
        var previousNvramDirectory = _settings.NvramDirectory;

        var window = new BiosNvramManagerWindow(_settings, _gameId, _gameName);
        await window.ShowDialog<bool>(this).ConfigureAwait(true);
        var settingsChanged = window.SettingsChanged;
        var nvramFilesChanged = window.NvramFilesChanged;
        if (!settingsChanged && !nvramFilesChanged)
        {
            if (wasRunning)
            {
                StartEmulation(showGameView: false);
            }

            return;
        }

        if (settingsChanged)
        {
            AppSettingsStore.Save(_settings);
        }

        var biosChanged =
            !string.Equals(previousBiosPath, _settings.BiosPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previousBios2Path, _settings.Bios2Path, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previousBiosFolder, _settings.BiosFolder, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previousBiosOverride, _settings.GetBiosOverride(_gameId), StringComparison.OrdinalIgnoreCase);
        var nvramChanged =
            previousNvramMode != _settings.NvramStorageMode
            || previousNvramVersion != _settings.NvramVersion
            || !string.Equals(previousNvramDirectory, _settings.NvramDirectory, StringComparison.OrdinalIgnoreCase);

        if (_machine is not null && (biosChanged || nvramChanged || nvramFilesChanged))
        {
            // A reset/delete/import must not be overwritten by the machine's old
            // in-memory NVRAM, so skip the pre-recreate flush in that case.
            RecreateMachine(flushNvram: !nvramFilesChanged);
            return;
        }

        if (_machine is null && (nvramChanged || nvramFilesChanged))
        {
            ReplaceNvramStore(_gameId.Length > 0 ? _gameId : null);
        }

        StatusText.Text = nvramFilesChanged
            ? "NVRAM image changed; the next game start uses the new data."
            : "BIOS and NVRAM settings saved.";
        _logger.ForCategory(LogCategory.Host).Information(
            "BIOS/NVRAM manager closed (settings={Settings}, nvramFiles={NvramFiles}).",
            settingsChanged,
            nvramFilesChanged);
        if (wasRunning)
        {
            StartEmulation(showGameView: false);
        }
    }

    private void OnSaveSlotsAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowSaveSlotsAsync, "showing save slots");
    }

    private async Task ShowSaveSlotsAsync()
    {
        var window = new SaveSlotsWindow(
            _slots,
            slot =>
            {
                if (_machine is null)
                {
                    return null;
                }

                SaveStateSlotInfo? result = null;
                _runner!.Invoke(() => result = _slots.Save(_machine, _gameId, _gameName, slot));
                return result;
            },
            slot =>
            {
                if (_machine is not null)
                {
                    _runner!.Invoke(() => _slots.Load(_machine, slot, _gameId));
                }
            },
            slot => _slots.Delete(slot));
        await window.ShowDialog(this).ConfigureAwait(true);
        PersistSlot();
        UpdateSlotText();
    }

    private void OnInputBindingsAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowInputBindingsAsync, "showing input bindings");
    }

    private async Task ShowInputBindingsAsync()
    {
        var window = new InputBindingsWindow(_settings.Input);
        var profile = await window.ShowDialog<InputProfile?>(this).ConfigureAwait(true);
        if (profile is null)
        {
            return;
        }

        _settings.Input = profile;
        AppSettingsStore.Save(_settings);
        StopMovie(save: true);
        _gamepad?.UpdateProfile(profile);
        _input = new InputManager(_settings.Input, _gamepad);
        _settings.ApplyInputDevices(_input);
        if (_machine is not null)
        {
            _machine.PbusSource = _input;
        }

        var conflicts = _settings.Input.FindConflicts();
        StatusText.Text = conflicts.Count == 0
            ? "Input bindings saved."
            : $"Input bindings saved with {conflicts.Count} conflict(s).";
        _logger.ForCategory(LogCategory.Input).Information(
            "Input bindings saved ({Conflicts} conflict(s)); {Gamepads} gamepad(s).",
            conflicts.Count,
            _gamepad?.ConnectedControllers ?? 0);
    }

    private void ApplyAudioSettings()
    {
        if (_machine is not null)
        {
            _machine.CdAudioVolume = _settings.CdAudioVolume;
            _machine.CdAudioMuted = _settings.CdAudioMuted;
        }

        if (_settings.AudioEnabled)
        {
            if (_audio is null)
            {
                _audio = OpenAlAudioOutput.TryCreate(8192, out var audioError);
                if (_audio is not null && _machine is not null)
                {
                    _machine.AudioSink = _audio;
                }

                if (_audio is null)
                {
                    _logger.ForCategory(LogCategory.Audio).WithoutBugReport().Warning(
                        "OpenAL audio is unavailable; emulation continues silently ({Error}).",
                        audioError ?? "unknown error");
                }
                else
                {
                    _logger.ForCategory(LogCategory.Audio).Information(
                        "OpenAL audio ready ({SampleRate} Hz stereo).",
                        OpenAlAudioOutput.OutputSampleRate);
                }
            }

            if (_audio is not null)
            {
                _audio.Volume = _settings.Volume;
            }
        }
        else
        {
            _audio?.Dispose();
            _audio = null;
            if (_machine is not null)
            {
                _machine.AudioSink = null;
            }
        }
    }

    private void OnHighResChanged(object? sender, RoutedEventArgs e)
    {
        var enabled = HighResMenuItem.IsChecked;
        if (_settings.HighResolution == enabled)
        {
            return;
        }

        _settings.HighResolution = enabled;
        AppSettingsStore.Save(_settings);
        if (enabled)
        {
            _logger.ForCategory(LogCategory.Video).Information("High-resolution mode enabled.");
        }
        else
        {
            _logger.ForCategory(LogCategory.Video).Information("High-resolution mode disabled.");
        }
        if (_machine is null)
        {
            StatusText.Text = enabled
                ? "High-resolution mode enabled for the next game."
                : "High-resolution mode disabled.";
            return;
        }

        RecreateMachine();
    }

    private void RecreateMachine(bool flushNvram = true)
    {
        StopInputRecording(save: true);
        if (flushNvram)
        {
            FlushNvram();
        }
        var discPath = _settings.DiscPath;
        GameRecord? record = _gameId.Length == 0 ? null : GameDatabase.Default.FindByChecksum(_gameId);
        ResolvedBios resolved = BiosResolver.Resolve(_settings, _gameId, record, discPath);
        if (!resolved.HasPrimary || !File.Exists(resolved.PrimaryPath))
        {
            StatusText.Text = $"Reload the BIOS to apply the emulation mode: {resolved.Reason}";
            return;
        }

        _running = false;
        SetPlayState(running: false);
        _runner?.Dispose();
        _runner = null;
        _machine = null;
        _disc?.Dispose();
        _disc = null;
        _biosLoaded = false;
        _biosMd5 = string.Empty;
        _bios2Md5 = string.Empty;
        _biosSelectionReason = string.Empty;
        OpenBiosPath(resolved.PrimaryPath!, resolved.SecondaryPath, persist: false, resolved.Reason);
        if (!string.IsNullOrWhiteSpace(discPath) && File.Exists(discPath))
        {
            OpenDiscPath(discPath);
        }

        ShowLibraryView();
        StatusText.Text = "Emulation options changed; machine restarted.";
    }

    private void ApplyOsdSettings()
    {
        OsdOptions options;
        try
        {
            options = _settings.ToOsdOptions();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _logger.ForCategory(LogCategory.Host).Warning(
                exception,
                "Invalid OSD settings; using defaults.");
            options = new OsdOptions();
        }

        OsdList.IsVisible = options.Enabled;
        OsdList.Opacity = options.Opacity;
        OsdList.HorizontalAlignment = options.Position switch
        {
            OsdPosition.TopLeft or OsdPosition.BottomLeft => global::Avalonia.Layout.HorizontalAlignment.Left,
            OsdPosition.TopCenter or OsdPosition.BottomCenter => global::Avalonia.Layout.HorizontalAlignment.Center,
            _ => global::Avalonia.Layout.HorizontalAlignment.Right,
        };
        OsdList.VerticalAlignment = options.Position is OsdPosition.TopLeft
            or OsdPosition.TopCenter
            or OsdPosition.TopRight
            ? global::Avalonia.Layout.VerticalAlignment.Top
            : global::Avalonia.Layout.VerticalAlignment.Bottom;
        _osdNotifications.Lifetime = TimeSpan.FromMilliseconds(options.DurationMilliseconds);
        if (!options.Enabled)
        {
            _osdNotifications.Clear();
            SyncOsdEntries();
            _osdTimer?.Stop();
        }
    }

    private void ShowOsd(string text, OsdSeverity severity = OsdSeverity.Info)
    {
        if (!_settings.OsdEnabled)
        {
            return;
        }

        if (_osdNotifications.Enqueue(text, severity, DateTimeOffset.UtcNow) is null)
        {
            return;
        }

        SyncOsdEntries();
        if (_osdTimer is null)
        {
            _osdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _osdTimer.Tick += (_, _) =>
            {
                if (_osdNotifications.RemoveExpired(DateTimeOffset.UtcNow) > 0)
                {
                    SyncOsdEntries();
                }

                if (_osdNotifications.Count == 0)
                {
                    _osdTimer?.Stop();
                }
            };
        }

        _osdTimer.Start();
    }

    private void SyncOsdEntries()
    {
        _osdEntries.Clear();
        foreach (OsdMessage message in _osdNotifications.Messages)
        {
            _osdEntries.Add(new OsdEntry(message.Text, message.Severity));
        }
    }

    private void ToggleQuickMenu()
    {
        if (_quickMenuOpen)
        {
            CloseQuickMenu();
            return;
        }

        if (!_settings.QuickMenuEnabled)
        {
            StatusText.Text = "The quick menu is disabled in Settings.";
            ShowOsd("The quick menu is disabled in Settings.", OsdSeverity.Warning);
            return;
        }

        if (!EmulationView.IsVisible || _machine is null)
        {
            StatusText.Text = "Boot a disc before opening the quick menu.";
            ShowOsd("Boot a disc before opening the quick menu.", OsdSeverity.Warning);
            return;
        }

        _quickMenuOpen = true;
        _quickMenuResumeAfterClose = _running;
        if (_running)
        {
            PauseEmulation(showLibrary: false);
        }

        UpdateQuickMenuState();
        QuickMenuPanel.IsVisible = true;
        _input.Keyboard.Reset();
        QuickMenuResumeButton.Focus();
    }

    private void CloseQuickMenu(bool resume = true)
    {
        if (!_quickMenuOpen)
        {
            return;
        }

        _quickMenuOpen = false;
        QuickMenuPanel.IsVisible = false;
        var shouldResume = resume
            && _quickMenuResumeAfterClose
            && _machine is not null
            && (_biosLoaded || _moviePlayback is not null);
        _quickMenuResumeAfterClose = false;
        if (shouldResume)
        {
            StartEmulation(showGameView: false);
        }
    }

    private void UpdateQuickMenuState()
    {
        var multiplier = 4;
        try
        {
            multiplier = _settings.ResolveFastForwardMultiplier();
        }
        catch (ArgumentOutOfRangeException)
        {
            // A hand-edited settings file can carry an invalid multiplier; the quick
            // menu still opens and StartFastForward falls back to the same value.
        }

        QuickMenuResumeButton.Content = _running ? "Pause" : "Resume";
        QuickMenuFastForwardButton.Content = _fastForwardHeld
            ? "Stop Fast-Forward"
            : $"Fast-Forward ({multiplier}x)";
        QuickMenuSubtitleText.Text = _machine is null
            ? "No machine loaded."
            : $"{_gameName} · frame {_machine.FramesExecuted} · slot {_slots.CurrentSlot + 1}";
    }

    private void MoveQuickMenuFocus(int delta)
    {
        var focused = FocusManager.GetFocusedElement() as Button;
        var index = focused is null ? -1 : Array.IndexOf(_quickMenuButtons, focused);
        if (index < 0)
        {
            index = 0;
        }
        else
        {
            index = (index + delta + _quickMenuButtons.Length) % _quickMenuButtons.Length;
        }

        _quickMenuButtons[index].Focus();
    }

    private void OnQuickMenuResume(object? sender, RoutedEventArgs e)
    {
        if (_running)
        {
            PauseEmulation(showLibrary: false);
            CloseQuickMenu(resume: false);
            ShowOsd("Emulation paused.");
            return;
        }

        CloseQuickMenu(resume: false);
        StartEmulation(showGameView: false);
        ShowOsd("Emulation resumed.");
    }

    private void OnQuickMenuSave(object? sender, RoutedEventArgs e)
    {
        SaveStateToCurrentSlot();
        CloseQuickMenu();
    }

    private void OnQuickMenuLoad(object? sender, RoutedEventArgs e)
    {
        LoadStateFromCurrentSlot();
        CloseQuickMenu();
    }

    private void OnQuickMenuRewind(object? sender, RoutedEventArgs e)
    {
        CloseQuickMenu();
        RewindSeconds(1);
    }

    private void OnQuickMenuFastForward(object? sender, RoutedEventArgs e)
    {
        CloseQuickMenu();
        if (!_running)
        {
            StartEmulation(showGameView: false);
        }

        StartFastForward();
    }

    private void OnQuickMenuScreenshot(object? sender, RoutedEventArgs e)
    {
        CaptureScreenshot();
        CloseQuickMenu();
    }

    private void OnQuickMenuReset(object? sender, RoutedEventArgs e)
    {
        ResetMachine();
        CloseQuickMenu();
    }

    private void OnQuickMenuCheats(object? sender, RoutedEventArgs e)
    {
        CloseQuickMenu(resume: false);
        AsyncEvent.Run(ShowCheatsAsync, "showing cheats");
    }

    private void OnQuickMenuSettings(object? sender, RoutedEventArgs e)
    {
        CloseQuickMenu(resume: false);
        AsyncEvent.Run(ShowSettingsAsync, "showing settings");
    }

    private void OnQuickMenuStop(object? sender, RoutedEventArgs e)
    {
        CloseQuickMenu(resume: false);
        StopEmulation();
    }

    private void OnQuickMenuClose(object? sender, RoutedEventArgs e)
    {
        CloseQuickMenu();
    }

    private void OnCoreStatus(CoreStatus status)
    {
        if (_gamepad?.ConsumeQuickMenuToggle() == true && _settings.QuickMenuEnabled)
        {
            ToggleQuickMenu();
        }

        if (_quickMenuOpen)
        {
            UpdateQuickMenuState();
        }

        var skip = status.FrameSkipMode == FrameSkipMode.Off && status.FrameSkipCadence <= 1
            ? string.Empty
            : $" ({FrameSkipPolicy.Describe(status.FrameSkipMode, status.FrameSkipCadence)})";
        StatsText.Text =
            $"frame {status.Frames} · PC 0x{status.ProgramCounter:x8} · {status.Cycles} cyc";
        FpsText.Text = $"{status.FramesPerSecond,5:F1} fps{skip}";
        if (OverlayPanel.IsVisible)
        {
            var gpu = _vulkanPresenter ? VulkanVideo.GpuMilliseconds : Video.GpuMilliseconds;
            var present = _vulkanPresenter ? VulkanVideo.PresentMilliseconds : Video.PresentMilliseconds;
            var timing = gpu is null
                ? $"present {present:F2} ms"
                : $"gpu {gpu.Value:F2} ms  present {present:F2} ms";
            var audio = _audio is null
                ? string.Empty
                : $"  audio {_audio.BufferedFrames}f q{_audio.QueuedBuffers} u{_audio.UnderrunBuffers} "
                    + $"d{_audio.SamplesDropped} {FormatAudioMeter(_audio.TakePeakLevel())}";
            var shown = status.PresentedFramesPerSecond > 0 && status.PresentedFramesPerSecond < status.FramesPerSecond - 0.5
                ? $"  shown {status.PresentedFramesPerSecond:F1}"
                : string.Empty;
            OverlayText.Text =
                $"{status.FramesPerSecond,5:F1}/{status.TargetFramesPerSecond:F2} fps{shown}{skip}  "
                + $"{status.Cycles,6} cyc  PC 0x{status.ProgramCounter:x8}  {timing}{audio}";
        }

        if (InputDisplayPanel.IsVisible)
        {
            var disc = status.DiscPresent
                ? "disc"
                : status.TrayOpen ? "tray open" : "no disc";
            InputFrameText.Text =
                $"frame {status.Frames} · skip {status.SkippedFrames} · {disc}";
            InputButtonsText.Text = $"P1 {PbusDecoder.FormatButtons(status.Player1)}";
        }
    }

    private void OnInputDisplayChanged(object? sender, RoutedEventArgs e)
    {
        SetInputDisplay(InputDisplayMenuItem.IsChecked);
    }

    private void ToggleInputDisplay()
    {
        SetInputDisplay(!_settings.ShowInputDisplay);
    }

    private void SetInputDisplay(bool enabled)
    {
        if (_settings.ShowInputDisplay == enabled)
        {
            InputDisplayMenuItem.IsChecked = enabled;
            InputDisplayPanel.IsVisible = enabled;
            return;
        }

        _settings.ShowInputDisplay = enabled;
        InputDisplayMenuItem.IsChecked = enabled;
        InputDisplayPanel.IsVisible = enabled;
        AppSettingsStore.Save(_settings);
        ShowOsd(enabled ? "Input display on." : "Input display off.");
    }

    private string FormatAudioMeter(float level)
    {
        // Decay the displayed peak so the bar follows the music instead of flickering
        // with the buffer boundaries.
        _meterPeak = Math.Max(level, _meterPeak * 0.85f);
        var filled = Math.Clamp((int)MathF.Round(_meterPeak * 10f), 0, 10);
        return $"{new string('█', filled)}{new string('░', 10 - filled)} {_meterPeak:F2}";
    }

    private void OnOpenGlReady(object? sender, OpenGlReadyEventArgs e)
    {
        if (_rendererKind != VideoRendererKind.OpenGl || _vulkanPresenter || _openGlDisabled)
        {
            return;
        }

        var capabilities = e.Capabilities;
        StopCpuFallback();
        RendererText.Text = $"{capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion}";
        StatusText.Text =
            $"{capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion} renderer ready.";
        _logger.ForCategory(LogCategory.Video).Information(
            "OpenGL renderer ready: {Name} {Major}.{Minor} ({Api}), max texture {MaxTextureSize}.",
            capabilities.Name,
            capabilities.MajorVersion,
            capabilities.MinorVersion,
            capabilities.Api,
            capabilities.MaxTextureSize);
    }

    private void OnVulkanStarted(object? sender, EventArgs e)
    {
        if (_rendererKind != VideoRendererKind.Vulkan || !_vulkanPresenter)
        {
            return;
        }

        _logger.ForCategory(LogCategory.Video).Information(
            "Vulkan renderer ready: {Device}.",
            VulkanVideo.DeviceName);
        if (!_running)
        {
            StatusText.Text = "Vulkan renderer ready.";
        }
    }

    private void OnVulkanFailed(object? sender, VulkanFailedEventArgs e)
    {
        if (_rendererKind != VideoRendererKind.Vulkan || !_vulkanPresenter)
        {
            return;
        }

        var exception = e.Exception;
        _logger.ForCategory(LogCategory.Video).Warning(
            exception,
            "Vulkan surface failed; falling back to the CPU presenter.");
        _vulkanPresenter = false;
        VulkanVideo.Stop();
        VulkanVideo.IsVisible = false;
        StartCpuFallback(exception, allowVulkan: false);
    }

    private void OnOpenGlFailed(object? sender, OpenGlFailedEventArgs e)
    {
        if (_rendererKind != VideoRendererKind.OpenGl || _openGlDisabled)
        {
            return;
        }

        var exception = e.Exception;
        _logger.ForCategory(LogCategory.Video).Warning(
            exception,
            "OpenGL unavailable; falling back to the CPU presenter.");
        StartCpuFallback(exception);
    }

    private VideoRendererKind ResolveRendererKind()
    {
        return Program.RequestedGlBackend switch
        {
            GlBackend.Vulkan => VideoRendererKind.Vulkan,
            GlBackend.Software => VideoRendererKind.Software,
            GlBackend.Wgl or GlBackend.Angle => VideoRendererKind.OpenGl,
            _ => _settings.Renderer,
        };
    }

    private void ApplyRendererSelection()
    {
        var previousRenderer = _rendererKind;
        _rendererKind = _settings.Renderer;
        Video.Settings = _settings.ToRendererSettings(_gameId);
        if (_rendererKind == VideoRendererKind.OpenGl)
        {
            var hadCpuPresenter = _cpuVideo is not null || _vulkanPresenter;
            if (hadCpuPresenter)
            {
                StopCpuFallback();
            }

            _openGlDisabled = false;
            _vulkanPresenter = false;
            VulkanVideo.IsVisible = false;
            Video.IsVisible = true;
            if (previousRenderer != VideoRendererKind.OpenGl || hadCpuPresenter)
            {
                ReattachOpenGlSurface();
            }

            RendererText.Text = "OpenGL";
            StatusText.Text = "OpenGL renderer enabled.";
            _logger.ForCategory(LogCategory.Video).Information("OpenGL renderer enabled by settings.");
            return;
        }

        _openGlDisabled = true;
        Video.IsVisible = false;
        VulkanVideo.IsVisible = false;
        if (_cpuVideo is not null || _vulkanPresenter)
        {
            StopCpuFallback(showVideo: false);
        }

        StartCpuFallback(new NotSupportedException(
            _rendererKind == VideoRendererKind.Vulkan
                ? "Vulkan renderer selected in settings."
                : "Software renderer selected in settings."));
    }

    private void ReattachOpenGlSurface()
    {
        // OpenGlControlBase only creates its context when it is attached to the visual
        // tree. Reattach it when returning from the software/Vulkan presenter so a
        // previously hidden or failed surface is initialized again.
        if (Video.Parent is Panel parent)
        {
            parent.Children.Remove(Video);
            parent.Children.Insert(0, Video);
            _logger.ForCategory(LogCategory.Video).Information("OpenGL surface reattached.");
        }
    }

    private void StartCpuFallback(Exception exception, bool allowVulkan = true)
    {
        if (_cpuVideo is not null || _vulkanPresenter || _shuttingDown)
        {
            return;
        }

        var sourceWidth = _machine?.Frame.SourceWidth > 0
            ? _machine.Frame.SourceWidth
            : EmulatorConstants.VideoWidth;
        var sourceHeight = _machine?.Frame.SourceHeight > 0
            ? _machine.Frame.SourceHeight
            : EmulatorConstants.VideoHeight;
        var (width, height) = ResolveOutputSize(sourceWidth, sourceHeight);
        RendererSettings rendererSettings = _settings.ToRendererSettings(_gameId);

        if (allowVulkan && _rendererKind == VideoRendererKind.Vulkan)
        {
            try
            {
                VulkanVideo.Settings = rendererSettings;
                if (!VulkanVideo.Start())
                {
                    throw new InvalidOperationException("The embedded Vulkan surface is unavailable.");
                }

                _vulkanPresenter = true;
                _cpuVideo = null;
                _cpuBitmap = null;
                _cpuPixels = null;
                CpuVideo.IsVisible = false;
                Video.IsVisible = false;
                VulkanVideo.IsVisible = true;
                VulkanVideo.SetPresentationVisible(EmulationView.IsVisible);
                RendererText.Text = "Vulkan";
                StatusText.Text = "Vulkan renderer active.";
                if (VulkanVideo.DeviceName is { } deviceName)
                {
                    _logger.ForCategory(LogCategory.Video).Information(
                        "Vulkan renderer ready: {Device}.",
                        deviceName);
                }
                else
                {
                    _logger.ForCategory(LogCategory.Video).Information(
                        "Vulkan surface initializing; waiting for the control to attach.");
                }

                return;
            }
            catch (Exception vulkanException) when (vulkanException is not OutOfMemoryException)
            {
                VulkanVideo.Stop();
                VulkanVideo.IsVisible = false;
                exception = vulkanException;
                _logger.ForCategory(LogCategory.Video).Warning(
                    vulkanException,
                    "Vulkan is unavailable; falling back to the CPU presenter.");
            }
        }

        var renderer = new CpuVideoRenderer { Settings = rendererSettings };
        renderer.Initialize(width, height);
        _cpuVideo = renderer;
        _cpuPixels = new byte[width * height * 4];
        _cpuBitmap = CreateBitmap(width, height);
        CpuVideo.Source = _cpuBitmap;
        CpuVideo.IsVisible = true;
        Video.IsVisible = false;
        VulkanVideo.IsVisible = false;
        _cpuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _cpuTimer.Tick += (_, _) => RenderCpuFrame();
        _cpuTimer.Start();
        _logger.ForCategory(LogCategory.Video).Information(
            "CPU presenter active ({Reason}).",
            _rendererKind == VideoRendererKind.Software
                ? "software renderer selected"
                : exception.Message);
        var softwareSelected = _rendererKind == VideoRendererKind.Software;
        var vulkanSelected = _rendererKind == VideoRendererKind.Vulkan;
        RendererText.Text = softwareSelected ? "Software (CPU)" : "CPU fallback";
        StatusText.Text = softwareSelected
            ? "Software renderer active (CPU)."
            : vulkanSelected
                ? $"Vulkan unavailable ({exception.Message}); using the CPU renderer."
                : $"OpenGL unavailable ({exception.Message}); using the CPU renderer.";
    }

    private void StopCpuFallback(bool showVideo = true)
    {
        var hadPresenter = _cpuVideo is not null || _vulkanPresenter;
        _cpuTimer?.Stop();
        _cpuTimer = null;
        CpuVideo.IsVisible = false;
        VulkanVideo.SetPresentationVisible(false);
        VulkanVideo.IsVisible = false;
        VulkanVideo.Stop();
        Video.IsVisible = showVideo;
        _cpuVideo?.Dispose();
        _cpuVideo = null;
        _vulkanPresenter = false;
        _cpuBitmap = null;
        _cpuPixels = null;
        if (hadPresenter && showVideo)
        {
            _logger.ForCategory(LogCategory.Video).Information("OpenGL presenter restored.");
        }
    }

    private void RenderCpuFrame()
    {
        if (_cpuVideo is null || _cpuBitmap is null || _cpuPixels is null)
        {
            return;
        }

        var sourceWidth = _machine?.Frame.SourceWidth ?? 0;
        var sourceHeight = _machine?.Frame.SourceHeight ?? 0;
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            sourceWidth = EmulatorConstants.VideoWidth;
            sourceHeight = EmulatorConstants.VideoHeight;
        }

        var (width, height) = ResolveOutputSize(sourceWidth, sourceHeight);
        if (_cpuVideo.Frame.Width != width || _cpuVideo.Frame.Height != height)
        {
            _cpuVideo.Resize(width, height);
            _cpuPixels = new byte[width * height * 4];
            _cpuBitmap = CreateBitmap(width, height);
            CpuVideo.Source = _cpuBitmap;
        }

        Video.RenderSnapshot(_cpuVideo, width, height);
        var pixels = _cpuVideo.Frame.PixelSpan;
        if (pixels.Length != _cpuPixels.Length)
        {
            _cpuPixels = new byte[pixels.Length];
        }

        pixels.CopyTo(_cpuPixels);
        using var locked = _cpuBitmap.Lock();
        var rowBytes = width * 4;
        if (locked.RowBytes == rowBytes)
        {
            Marshal.Copy(_cpuPixels, 0, locked.Address, _cpuPixels.Length);
            return;
        }

        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(
                _cpuPixels,
                y * rowBytes,
                IntPtr.Add(locked.Address, y * locked.RowBytes),
                rowBytes);
        }
    }

    private (int Width, int Height) ResolveOutputSize(int fallbackWidth, int fallbackHeight)
    {
        return OutputResolutionSizes.TryGetSize(_settings.OutputResolution, out var width, out var height)
            ? (width, height)
            : (fallbackWidth, fallbackHeight);
    }

    private static WriteableBitmap CreateBitmap(int width, int height)
    {
        return new WriteableBitmap(
            new global::Avalonia.PixelSize(width, height),
            new Vector(96, 96),
            AvaloniaPixelFormat.Bgra8888,
            AlphaFormat.Premul);
    }

    private RewindOptions CreateRewindOptions()
    {
        try
        {
            return _settings.ToRewindOptions();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _logger.ForCategory(LogCategory.Host).Warning(
                exception,
                "Invalid rewind settings; using the defaults.");
            return RewindOptions.Default;
        }
    }

    private void ApplyCheats()
    {
        if (_machine is not null)
        {
            _cheats?.Apply(_machine);
        }
    }

    private void ReloadCheats()
    {
        try
        {
            _cheatPath = CheatStore.ResolvePath(_settings.ResolveCheatsDirectory(), _gameId);
            CheatDocument document = CheatStore.Load(_cheatPath, _gameId);
            _cheats = CheatSet.FromDocument(document);
            CheatEntry? invalid = _cheats.Entries.FirstOrDefault(
                entry => entry.Enabled && entry.HasError);
            if (invalid is not null)
            {
                _logger.ForCategory(LogCategory.Host).Warning(
                    "Cheat {Name} in {Path} is invalid and will not run: {Error}",
                    invalid.Name,
                    _cheatPath,
                    invalid.Error);
            }

            _logger.ForCategory(LogCategory.Host).Information(
                "Loaded {Count} cheat(s) from {Path}.",
                _cheats.ActiveCount,
                _cheatPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            _cheats = new CheatSet();
            _logger.ForCategory(LogCategory.Host).Warning(
                exception,
                "Cheat file {Path} could not be loaded.",
                _cheatPath);
        }
    }

    private void OnCheatsAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ShowCheatsAsync, "showing cheats");
    }

    private async Task ShowCheatsAsync()
    {
        if (_machine is null || _cheats is null)
        {
            StatusText.Text = "Open a BIOS and disc before editing cheats.";
            return;
        }

        ReloadCheats();
        var window = new CheatsWindow(
            _gameId,
            _gameName,
            _cheatPath,
            CheatDocument.FromSet(_gameId, _cheats),
            () =>
            {
                ReloadCheats();
                _runner?.ResetRewind();
            });
        await window.ShowDialog(this).ConfigureAwait(true);
        ReloadCheats();
        StatusText.Text = $"Cheats: {_cheats.ActiveCount} enabled ({_cheatPath}).";
    }

    private FiveDoMachine CreateMachine()
    {
        if (_machine is null)
        {
            _machine = new FiveDoMachine(new EmulatorTuning
            {
                HighResolution = _settings.HighResolution,
                ArmTiming = _settings.ArmTiming,
                ClioTimers = _settings.ClioTimers,
                CelTiming = _settings.CelTiming,
                CdromTiming = _settings.CdromTiming,
                CdSpeed = _settings.CdSpeed,
                ArmClock = _settings.ResolveArmClock(),
                RandomSeed = _settings.RandomSeed,
            })
            {
                PbusSource = _input,
                AudioSink = _audio,
                BeforeFrame = ApplyCheats,
                CdAudioVolume = _settings.CdAudioVolume,
                CdAudioMuted = _settings.CdAudioMuted,
            };
            _runner = new CoreRunner(
                _machine,
                frame => SubmitFrameToActiveRenderer(frame),
                status => Dispatcher.UIThread.Post(() => OnCoreStatus(status)),
                CreateRewindOptions())
            {
                FrameSkip = _settings.FrameSkip,
            };
            _runner.Faulted += (_, args) => Dispatcher.UIThread.Post(() =>
            {
                var exception = args.Exception;
                _running = false;
                SetPlayState(running: false);
                StatusText.Text = $"Emulation error: {exception.Message}";
                _logger.ForCategory(LogCategory.Machine).Error(exception, "Emulation fault.");
                ShowLibraryView();
                ShowError("Emulation error", exception.Message);
            });
            _runner.GuestDiscEjected += (_, _) => Dispatcher.UIThread.Post(HandleGuestDiscEjection);
            ReloadCheats();
        }

        return _machine;
    }

    private void SubmitFrameToActiveRenderer(VdlFrame frame)
    {
        if (_vulkanPresenter)
        {
            VulkanVideo.SubmitFrame(frame);
        }
        else
        {
            Video.SubmitFrame(frame);
        }
    }

    private NvramStore CreateNvramStore(string? gameId)
    {
        try
        {
            return _settings.CreateNvramStore(gameId);
        }
        catch (ArgumentException)
        {
            // Per-game mode without a selected game: keep the shared image as the
            // placeholder until a disc supplies a game ID.
            return new NvramStore(
                Path.Combine(_settings.ResolveNvramDirectory(), NvramLayout.LegacyFileName));
        }
    }

    private void FlushNvram()
    {
        if (_machine is not null)
        {
            PersistNvram(_machine);
        }
    }

    private void PersistNvram(FiveDoMachine machine)
    {
        var buffer = new byte[EmulatorConstants.NvramSize];
        machine.CopyNvram(buffer);
        _nvramStore.MarkChanged(buffer);
        _nvramStore.Flush();
    }

    private void ReplaceNvramStore(string? gameId)
    {
        NvramStore previous = _nvramStore;
        _nvramStore = CreateNvramStore(gameId);
        _nvramGameId = gameId ?? string.Empty;
        _nvramMode = _settings.NvramStorageMode;
        _nvramVersion = _settings.NvramVersion;
        _nvramDirectory = _settings.NvramDirectory;
        previous.Dispose();
    }

    private void Shutdown()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        _libraryScanCts?.Cancel();
        _playtime.Stop();
        StopInputRecording(save: true);
        _logTimer.Stop();
        _runner?.Dispose();
        _runner = null;
        FlushNvram();
        AppSettingsStore.Save(_settings);
        if (_audio is not null)
        {
            _logger.ForCategory(LogCategory.Audio).Information(
                "OpenAL statistics: pushed {Pushed}, dropped {Dropped}, underruns {Underruns}, "
                + "buffered {Buffered} frames, queued {Queued} frames, OpenAL errors {Errors} ({LastError}), "
                + "tap {Tap} ({TapError}).",
                _audio.SamplesPushed,
                _audio.SamplesDropped,
                _audio.UnderrunBuffers,
                _audio.BufferedFrames,
                _audio.QueuedFrames,
                _audio.AlErrors,
                _audio.LastAlError ?? "none",
                _audio.TapEnabled ? _audio.TapPath : "off",
                _audio.TapError ?? "none");
        }

        _disc?.Dispose();
        _cpuTimer?.Stop();
        _cpuTimer = null;
        _cpuVideo?.Dispose();
        _cpuVideo = null;
        VulkanVideo.Dispose();
        // VideoSurface owns GL resources until Avalonia detaches it. Disposing it from
        // Closing runs before the GL context is current and crashes in Silk.NET.
        _nvramStore.Dispose();
        _audio?.Dispose();
        _gamepad?.Dispose();
        ClearLibraryItems();
        _logger.ForCategory(LogCategory.Host).Information("Desktop shell shut down.");
    }

    private void OnVideoPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_quickMenuOpen)
        {
            return;
        }

        UpdatePointerState(e.GetPosition(VideoHost));
    }

    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_quickMenuOpen)
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(VideoHost).Properties;
        _pointerLeftDown = properties.IsLeftButtonPressed;
        _pointerMiddleDown = properties.IsMiddleButtonPressed;
        _pointerRightDown = properties.IsRightButtonPressed;
        UpdatePointerState(e.GetPosition(VideoHost));
        e.Handled = true;
    }

    private void OnVideoPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_quickMenuOpen)
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(VideoHost).Properties;
        _pointerLeftDown = properties.IsLeftButtonPressed;
        _pointerMiddleDown = properties.IsMiddleButtonPressed;
        _pointerRightDown = properties.IsRightButtonPressed;
        UpdatePointerState(e.GetPosition(VideoHost));
        e.Handled = true;
    }

    private void OnVideoPointerExited(object? sender, PointerEventArgs e)
    {
        _lastPointerPosition = null;
        _pointerLeftDown = false;
        _pointerMiddleDown = false;
        _pointerRightDown = false;
        for (var port = 0; port < PbusEncoder.MaxControllers; port++)
        {
            if (_input.Devices.GetDevice(port) == InputDeviceType.Lightgun)
            {
                _input.Devices.SetLightgun(
                    port,
                    _input.Devices.GetLightgun(port) with { Trigger = false, Reload = false, OffScreen = true });
            }
            else if (_input.Devices.GetDevice(port) == InputDeviceType.ArcadeLightgun)
            {
                _input.Devices.SetArcadeLightgun(
                    port,
                    _input.Devices.GetArcadeLightgun(port) with { Trigger = false, Holster = false, OffScreen = true });
            }
            else if (_input.Devices.GetDevice(port) == InputDeviceType.Mouse)
            {
                _input.Devices.SetMouse(port, _input.Devices.GetMouse(port) with { DeltaX = 0, DeltaY = 0 });
            }
        }

        LightgunOverlay.IsVisible = false;
    }

    private void UpdatePointerState(Point position)
    {
        double width = VideoHost.Bounds.Width;
        double height = VideoHost.Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var screenX = (int)Math.Clamp(position.X * EmulatorConstants.VideoWidth / width, 0, EmulatorConstants.VideoWidth - 1);
        var screenY = (int)Math.Clamp(position.Y * EmulatorConstants.VideoHeight / height, 0, EmulatorConstants.VideoHeight - 1);
        var deltaX = 0;
        var deltaY = 0;
        if (_lastPointerPosition is { } last)
        {
            deltaX = (int)Math.Round((position.X - last.X) * EmulatorConstants.VideoWidth / width);
            deltaY = (int)Math.Round((position.Y - last.Y) * EmulatorConstants.VideoHeight / height);
        }

        _lastPointerPosition = position;
        var lightgunVisible = false;
        for (var port = 0; port < PbusEncoder.MaxControllers; port++)
        {
            switch (_input.Devices.GetDevice(port))
            {
                case InputDeviceType.Mouse:
                    MouseState mouse = _input.Devices.GetMouse(port);
                    _input.Devices.SetMouse(
                        port,
                        new MouseState(
                            mouse.DeltaX + deltaX,
                            mouse.DeltaY + deltaY,
                            _pointerLeftDown,
                            _pointerMiddleDown,
                            _pointerRightDown,
                            false));
                    break;
                case InputDeviceType.Lightgun:
                    _input.Devices.SetLightgun(
                        port,
                        new LightgunState(
                            screenX,
                            screenY,
                            _pointerLeftDown,
                            _pointerRightDown,
                            _pointerMiddleDown,
                            OffScreen: false));
                    lightgunVisible = true;
                    break;
                case InputDeviceType.ArcadeLightgun:
                    _input.Devices.SetArcadeLightgun(
                        port,
                        new ArcadeLightgunState(
                            screenX,
                            screenY,
                            Trigger: _pointerLeftDown,
                            Service: false,
                            Coins: _pointerRightDown,
                            Start: _pointerMiddleDown,
                            Holster: false,
                            OffScreen: false));
                    lightgunVisible = true;
                    break;
                case InputDeviceType.OrbatakTrackball:
                    OrbatakTrackballState trackball = _input.Devices.GetTrackball(port);
                    _input.Devices.SetTrackball(
                        port,
                        new OrbatakTrackballState(
                            trackball.DeltaX + Math.Clamp(deltaX, -24, 24),
                            trackball.DeltaY + Math.Clamp(deltaY, -24, 24)));
                    break;
            }
        }

        if (lightgunVisible)
        {
            LightgunOverlay.IsVisible = true;
            Canvas.SetLeft(LightgunDot, position.X - 3);
            Canvas.SetTop(LightgunDot, position.Y - 3);
            LightgunHorizontal.StartPoint = new Point(position.X - 8, position.Y);
            LightgunHorizontal.EndPoint = new Point(position.X + 8, position.Y);
            LightgunVertical.StartPoint = new Point(position.X, position.Y - 8);
            LightgunVertical.EndPoint = new Point(position.X, position.Y + 8);
        }
        else
        {
            LightgunOverlay.IsVisible = false;
        }

    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var keyName = e.Key.ToString();
        HotkeyAction hotkey = HotkeyRouter.Resolve(_settings.Input, keyName);

        if (_quickMenuOpen)
        {
            if (hotkey == HotkeyAction.QuickMenu || e.Key == Key.Escape)
            {
                CloseQuickMenu();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                MoveQuickMenuFocus(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                MoveQuickMenuFocus(1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Home)
            {
                _quickMenuButtons[0].Focus();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.End)
            {
                _quickMenuButtons[^1].Focus();
                e.Handled = true;
                return;
            }

            // Let the focused button handle Enter, Space, and Tab. Never feed a
            // quick-menu keystroke to the emulated pad.
            base.OnKeyDown(e);
            return;
        }

        // Fullscreen works from the library and game views; Escape always leaves fullscreen.
        if (hotkey == HotkeyAction.Fullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        // Do not feed browser keystrokes to the emulated pad.
        if (LibraryView.IsVisible || FocusManager.GetFocusedElement() is TextBox)
        {
            base.OnKeyDown(e);
            return;
        }

        // Shift+F9 is a frame-by-frame rewind companion to F9 frame advance.
        if (e.Key == Key.F9 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            RewindFrames(1);
            e.Handled = true;
            return;
        }

        if (hotkey != HotkeyAction.None)
        {
            HandleHotkey(hotkey);
            e.Handled = true;
            return;
        }

        if (_input.Keyboard.SetKey(keyName, pressed: true))
        {
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void HandleHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.SaveState:
                SaveStateToCurrentSlot();
                break;
            case HotkeyAction.LoadState:
                LoadStateFromCurrentSlot();
                break;
            case HotkeyAction.Screenshot:
                CaptureScreenshot();
                break;
            case HotkeyAction.Fullscreen:
                ToggleFullscreen();
                break;
            case HotkeyAction.Pause:
                TogglePlayPause();
                break;
            case HotkeyAction.Reset:
                ResetMachine();
                break;
            case HotkeyAction.SlotPrevious:
                SelectPreviousSlot();
                break;
            case HotkeyAction.SlotNext:
                SelectNextSlot();
                break;
            case HotkeyAction.FrameAdvance:
                AdvanceFrame();
                break;
            case HotkeyAction.Rewind:
                RewindSeconds(1);
                break;
            case HotkeyAction.FastForward:
                StartFastForward();
                break;
            case HotkeyAction.QuickMenu:
                ToggleQuickMenu();
                break;
            case HotkeyAction.SwapDisc:
                AsyncEvent.Run(SwapDiscAsync, "swapping discs");
                break;
            case HotkeyAction.ToggleInputDisplay:
                ToggleInputDisplay();
                break;
        }
    }

    private void AdvanceFrame()
    {
        if (_machine is null || _runner is null)
        {
            StatusText.Text = "Open a 3DO BIOS before advancing a frame.";
            return;
        }

        if (_running)
        {
            PauseEmulation(showLibrary: false);
        }

        _runner.StepFrame();
        StatusText.Text = $"Advanced to frame {_machine.FramesExecuted}.";
    }

    private void RewindSeconds(int seconds)
    {
        RewindFrames(seconds * 60);
    }

    private void RewindFrames(int frames)
    {
        if (_machine is null || _runner is null)
        {
            StatusText.Text = "Open a 3DO BIOS before rewinding.";
            ShowOsd("Boot a disc before rewinding.", OsdSeverity.Warning);
            return;
        }

        CancelFastForward();
        if (_runner.Rewind(frames))
        {
            StatusText.Text = $"Rewound to frame {_machine.FramesExecuted}.";
            ShowOsd($"Rewound to frame {_machine.FramesExecuted}");
        }
        else
        {
            StatusText.Text = "No rewind history is available yet.";
            ShowOsd("No rewind history is available yet.", OsdSeverity.Warning);
        }
    }

    private void StartFastForward()
    {
        if (_fastForwardHeld)
        {
            return;
        }

        if (_runner is null)
        {
            StatusText.Text = "Open a 3DO BIOS before fast-forwarding.";
            return;
        }

        _fastForwardHeld = true;
        try
        {
            _runner.SpeedMultiplier = _settings.ResolveFastForwardMultiplier();
        }
        catch (ArgumentOutOfRangeException)
        {
            _runner.SpeedMultiplier = 4;
        }

        if (FastForwardMenuItem is not null)
        {
            FastForwardMenuItem.IsChecked = true;
        }

        StatusText.Text = $"Fast-forward {_runner.SpeedMultiplier}x.";
        ShowOsd($"Fast-forward {_runner.SpeedMultiplier}x");
    }

    private void StopFastForward()
    {
        CancelFastForward();
        if (_running)
        {
            StatusText.Text = "Emulation running.";
            ShowOsd("Fast-forward off.");
        }
    }

    private void CancelFastForward()
    {
        _fastForwardHeld = false;
        if (FastForwardMenuItem is not null)
        {
            FastForwardMenuItem.IsChecked = false;
        }

        if (_runner is not null)
        {
            _runner.SpeedMultiplier = 1;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (_quickMenuOpen)
        {
            base.OnKeyUp(e);
            return;
        }

        if (LibraryView.IsVisible || FocusManager.GetFocusedElement() is TextBox)
        {
            CancelFastForward();
            base.OnKeyUp(e);
            return;
        }

        HotkeyAction hotkey = HotkeyRouter.Resolve(_settings.Input, e.Key.ToString());
        if (hotkey == HotkeyAction.FastForward)
        {
            StopFastForward();
            e.Handled = true;
            return;
        }

        if (_input.Keyboard.SetKey(e.Key.ToString(), pressed: false))
        {
            e.Handled = true;
        }

        base.OnKeyUp(e);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        try
        {
            _libraryScanCts?.Cancel();
            _logTimer.Stop();
            _movieTimer?.Stop();
            StopMovie(save: true);
            _runner?.Dispose();
            _runner = null;
            _disc?.Dispose();
            _cpuTimer?.Stop();
            _cpuTimer = null;
            _cpuVideo?.Dispose();
            _cpuVideo = null;
            _nvramStore.Dispose();
            _audio?.Dispose();
            _gamepad?.Dispose();
            ClearLibraryItems();
        }
        catch (Exception exception)
        {
            _logger.ForCategory(LogCategory.Host).Error(exception, "Desktop shell dispose failed.");
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }
}
