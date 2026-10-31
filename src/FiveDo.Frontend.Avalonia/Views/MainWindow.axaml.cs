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
using FiveDo.Core;
using FiveDo.Diagnostics;
using FiveDo.Diagnostics.Donations;
using FiveDo.Diagnostics.Logging;
using FiveDo.Frontend.Avalonia.Controls;
using FiveDo.Frontend.Avalonia.Input;
using FiveDo.Frontend.Avalonia.ViewModels;
using FiveDo.Host;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Storage;
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
    private readonly NvramStore _nvramStore;
    private readonly AppSettings _settings;
    private readonly InMemorySerilogSink _logSink;
    private readonly Serilog.ILogger _logger;
    private readonly PlatformGamepadBackend? _gamepad;
    private readonly ObservableCollection<LibraryItem> _visibleGames = [];
    private readonly List<LibraryItem> _allGames = [];
    private readonly DispatcherTimer _logTimer;
    private InputManager _input;
    private readonly SaveStateSlotManager _slots;
    private InputRecorder? _inputRecorder;
    private string? _inputRecordingPath;
    private OpenAlAudioOutput? _audio;
    private float _meterPeak;
    private FiveDoMachine? _machine;
    private CoreRunner? _runner;
    private IDiscSource? _disc;
    private string _gameId = string.Empty;
    private string _gameName = string.Empty;
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
    private bool _gridMode = true;
    private bool _syncingSelection;
    private bool _logDockVisible;
    private int _lastLogEntryCount = -1;
    private int _scanGeneration;
    private CancellationTokenSource? _libraryScanCts;
    private DateTime _lastDonationCheckUtc = DateTime.MinValue;
    private string? _donationBannerMessage;
    private bool _donationCheckInProgress;
    private bool _donationBannerDismissed;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettingsStore.Load();
        _lastDonationCheckUtc = _settings.DonationLastCheckedUtc ?? DateTime.MinValue;
        _rendererKind = ResolveRendererKind();
        _logSink = AppLogging.InMemorySink ?? new InMemorySerilogSink(4000);
        _logger = Serilog.Log.ForContext<MainWindow>();
        _logger.ForCategory(LogCategory.Host).Information(
            "Desktop shell starting ({Version}).",
            FiveDoVersion.Version);
        if (string.IsNullOrWhiteSpace(_settings.SaveDirectory))
        {
            _settings.SaveDirectory = Path.Combine(AppSettingsStore.DefaultDirectory, "states");
        }

        _settings.SaveSlotCount = Math.Clamp(_settings.SaveSlotCount, 1, 100);
        _slots = new SaveStateSlotManager(_settings.SaveDirectory, _settings.SaveSlotCount);
        _slots.Select(Math.Clamp(_settings.SaveSlot, 0, _slots.SlotCount - 1));
        _gamepad = PlatformGamepadBackend.TryCreate(_settings.Input);
        _input = new InputManager(_settings.Input, _gamepad);
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

        _nvramStore = new NvramStore(Path.Combine(AppSettingsStore.DefaultDirectory, "nvram.ram"));
        HighResMenuItem.IsChecked = _settings.HighResolution;
        OverlayPanel.IsVisible = _settings.ShowOverlay;
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

            // Construct the settings dialog so its XAML and named controls are also
            // validated by the trimmed smoke test. Do not close it: closing a window
            // before the main loop starts can shut down the dispatcher.
            _ = new SettingsWindow(_settings, _gameId, _gameName);
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
        };
        _logTimer.Start();
        SetLogDockVisible(false);
        SetLibraryMode(grid: true);
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
        var biosPath = Program.StartupBiosPath ?? _settings.BiosPath;
        if (File.Exists(biosPath))
        {
            OpenBiosPath(biosPath);
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
                    Patterns = ["*.iso", "*.bin", "*.cue", "*.chd"],
                },
            ],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
        {
            OpenDiscPath(path, autoStart: true);
        }
    }

    private void OpenBiosPath(string path)
    {
        StopInputRecording(save: true);
        if (_running)
        {
            PauseEmulation(showLibrary: false);
        }

        try
        {
            var bios = BiosImage.Load(path);
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
            _settings.BiosPath = path;
            AppSettingsStore.Save(_settings);
            StatusText.Text = $"BIOS {bios.Md5[..8]} loaded{(bios.IsAnvil ? " (Anvil)" : string.Empty)}.";
            _logger.ForCategory(LogCategory.Storage).Information(
                "BIOS loaded: {Path} ({Md5}, {Kind}).",
                path,
                bios.Md5,
                bios.IsAnvil ? "Anvil" : "retail");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            StatusText.Text = $"BIOS error: {exception.Message}";
            _logger.ForCategory(LogCategory.Storage).Error(exception, "BIOS load failed.");
            ShowError("BIOS error", exception.Message);
        }
    }

    private void OpenDiscPath(string path, bool autoStart = false)
    {
        StopInputRecording(save: true);
        if (autoStart && _running)
        {
            PauseEmulation(showLibrary: false);
        }

        try
        {
            var machine = _machine ?? CreateMachine();
            var disc = DiscImageFactory.Open(path);
            var gameId = disc.SectorCount >= 2 ? GameId.Compute(disc) : string.Empty;
            var record = gameId.Length == 0 ? null : GameDatabase.Default.FindByChecksum(gameId);
            var fixMode = _settings.ResolveFixMode(gameId);
            _runner!.Invoke(() =>
            {
                _disc?.Dispose();
                _disc = disc;
                machine.Compatibility.FixMode = fixMode;
                machine.InsertDisc(disc);
                if (autoStart)
                {
                    // A different game must boot from a cold RAM image; the previous
                    // game's leftovers can leave the BIOS waiting at PC 0x1658.
                    machine.Memory.ClearRam();
                    machine.Reset();
                }
            });
            _gameId = gameId;
            _gameName = record?.Name ?? Path.GetFileNameWithoutExtension(path);
            Video.Settings = _settings.ToRendererSettings(_gameId);
            if (_cpuVideo is not null)
            {
                _cpuVideo.Settings = _settings.ToRendererSettings(_gameId);
            }

            _settings.DiscPath = path;
            for (var i = _settings.RecentDiscs.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_settings.RecentDiscs[i], path, StringComparison.OrdinalIgnoreCase))
                {
                    _settings.RecentDiscs.RemoveAt(i);
                }
            }

            _settings.RecentDiscs.Insert(0, path);
            while (_settings.RecentDiscs.Count > 10)
            {
                _settings.RecentDiscs.RemoveAt(_settings.RecentDiscs.Count - 1);
            }

            AppSettingsStore.Save(_settings);
            UpdateRecentMenu();
            UpdateEmulationHeader();
            StatusText.Text = $"{_gameName}: {disc.SectorCount} sectors, fixes 0x{(uint)fixMode:x8}.";
            _logger.ForCategory(LogCategory.Storage).Information(
                "Disc opened: {Game} ({GameId}), {Sectors} sectors, fixes 0x{FixMode:x8}.",
                _gameName,
                gameId[..Math.Min(8, gameId.Length)],
                disc.SectorCount,
                (uint)fixMode);

            if (autoStart)
            {
                StartEmulation(showGameView: true);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or DiscImageException or ArgumentException)
        {
            StatusText.Text = $"Disc error: {exception.Message}";
            _logger.ForCategory(LogCategory.Storage).Error(exception, "Disc open failed.");
            ShowError("Disc error", exception.Message);
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
        if (!_biosLoaded || _machine is null || _runner is null)
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
            _runner?.Pause();
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
        StopInputRecording(save: true);

        // Stop, not pause: join the core thread and reset the machine so a later
        // Start (or a new game selection) begins from a known state.
        _runner?.Stop();
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

    private void ResetMachine()
    {
        _runner?.Invoke(() => _machine?.Reset());
        StatusText.Text = "Reset.";
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
            return;
        }

        _runner!.Invoke(() => _slots.Load(_machine, slot, _gameId));
        if (_machine.StateWarnings.Count > 0)
        {
            StatusText.Text = $"State loaded with warnings: {string.Join(" ", _machine.StateWarnings)}";
            _logger.ForCategory(LogCategory.State).Warning(
                "State loaded from slot {Slot} with warnings: {Warnings}",
                slot + 1,
                string.Join(" ", _machine.StateWarnings));
        }
        else
        {
            StatusText.Text = $"State loaded: slot {slot + 1}.";
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
                _logger.ForCategory(LogCategory.Host).Information("Screenshot written: {Path}.", path);
            }
        }

        SaveNativeScreenshot(path);
        StatusText.Text = $"Screenshot: {path}";
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
        FocusManager?.Focus(this, NavigationMethod.Unspecified, KeyModifiers.None);
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

    private void ApplyLibraryFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        var selected = SelectedLibraryItem();
        _visibleGames.Clear();
        foreach (var item in _allGames)
        {
            if (query.Length == 0
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.GameIdText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Format.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                _visibleGames.Add(item);
            }
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
            EmptyLibraryText.Text = $"No game matches \"{query}\".";
        }

        LibraryCountText.Text = !hasGames
            ? "0 games"
            : query.Length == 0
                ? CountLabel(_allGames.Count)
                : $"{_visibleGames.Count} of {CountLabel(_allGames.Count)}";
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
            _allGames.Add(new LibraryItem(entry));
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
        Video.Settings = _settings.ToRendererSettings(_gameId);
        if (_runner is not null)
        {
            _runner.FrameSkip = _settings.FrameSkip;
        }

        OverlayPanel.IsVisible = _settings.ShowOverlay;

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

        if (_machine is not null
            && (_settings.HighResolution != _machine.Tuning.HighResolution
                || _settings.ArmTiming != _machine.Tuning.ArmTiming
                || _settings.ClioTimers != _machine.Tuning.ClioTimers
                || _settings.CelTiming != _machine.Tuning.CelTiming
                || _settings.ResolveArmClock() != _machine.Tuning.ArmClock
                || _settings.RandomSeed != _machine.Tuning.RandomSeed))
        {
            RecreateMachine();
            return;
        }

        StatusText.Text = "Settings saved.";
        _logger.ForCategory(LogCategory.Host).Information("Settings saved.");
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
        _gamepad?.UpdateProfile(profile);
        _input = new InputManager(_settings.Input, _gamepad);
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

    private void RecreateMachine()
    {
        StopInputRecording(save: true);
        var biosPath = _settings.BiosPath;
        var discPath = _settings.DiscPath;
        if (string.IsNullOrWhiteSpace(biosPath) || !File.Exists(biosPath))
        {
            StatusText.Text = "Reload the BIOS to apply the emulation mode.";
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
        OpenBiosPath(biosPath);
        if (!string.IsNullOrWhiteSpace(discPath) && File.Exists(discPath))
        {
            OpenDiscPath(discPath);
        }

        ShowLibraryView();
        StatusText.Text = "Emulation options changed; machine restarted.";
    }

    private void OnCoreStatus(CoreStatus status)
    {
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
                ArmClock = _settings.ResolveArmClock(),
                RandomSeed = _settings.RandomSeed,
            })
            {
                PbusSource = _input,
                AudioSink = _audio,
            };
            _runner = new CoreRunner(
                _machine,
                frame => SubmitFrameToActiveRenderer(frame),
                status => Dispatcher.UIThread.Post(() => OnCoreStatus(status)))
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

    private void FlushNvram()
    {
        if (_machine is null)
        {
            return;
        }

        var buffer = new byte[EmulatorConstants.NvramSize];
        _machine.CopyNvram(buffer);
        _nvramStore.MarkChanged(buffer);
        _nvramStore.Flush();
    }

    private void Shutdown()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        _libraryScanCts?.Cancel();
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

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var keyName = e.Key.ToString();
        HotkeyAction hotkey = HotkeyRouter.Resolve(_settings.Input, keyName);

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

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (LibraryView.IsVisible || FocusManager.GetFocusedElement() is TextBox)
        {
            base.OnKeyUp(e);
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
