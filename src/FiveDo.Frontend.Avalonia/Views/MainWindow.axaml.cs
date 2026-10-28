using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
    private readonly InMemoryLogSink _logSink = new(4000);
    private readonly Log _log;
    private readonly PlatformGamepadBackend? _gamepad;
    private readonly ObservableCollection<LibraryItem> _visibleGames = [];
    private readonly List<LibraryItem> _allGames = [];
    private readonly DispatcherTimer _logTimer;
    private InputManager _input;
    private readonly SaveStateSlotManager _slots;
    private OpenAlAudioOutput? _audio;
    private float _meterPeak;
    private FiveDoMachine? _machine;
    private CoreRunner? _runner;
    private IDiscSource? _disc;
    private string _gameId = string.Empty;
    private string _gameName = string.Empty;
    private CpuVideoRenderer? _cpuVideo;
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

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettingsStore.Load();
        _log = new Log(_logSink);
        _log.Write(LogLevel.Info, LogCategory.Host, $"Desktop shell starting ({FiveDoVersion.Version}).");
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
            _log.Write(
                LogLevel.Info,
                LogCategory.Audio,
                $"OpenAL audio ready ({OpenAlAudioOutput.OutputSampleRate} Hz stereo).");
        }
        else if (_settings.AudioEnabled)
        {
            _log.Write(
                LogLevel.Warning,
                LogCategory.Audio,
                $"OpenAL audio is unavailable; emulation continues silently ({audioError ?? "unknown error"}).");
        }

        _nvramStore = new NvramStore(Path.Combine(AppSettingsStore.DefaultDirectory, "nvram.ram"));
        HighResMenuItem.IsChecked = _settings.HighResolution;
        OverlayPanel.IsVisible = _settings.ShowOverlay;
        Video.Settings = _settings.ToRendererSettings(_gameId);
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

        Video.OpenGlReady += OnOpenGlReady;
        Video.OpenGlFailed += OnOpenGlFailed;
        Deactivated += (_, _) => _input.Keyboard.Reset();
        Closing += (_, _) => Shutdown();
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

        if (!_settings.FirstRunCompleted && string.IsNullOrWhiteSpace(_settings.BiosPath))
        {
            ShowWelcome(firstRun: true);
            return;
        }

        LoadConfiguredPaths();
        RefreshLibrary();
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
                        OnOpenBios(null, new RoutedEventArgs());
                        break;
                    case WelcomeAction.OpenDisc:
                        OnOpenDisc(null, new RoutedEventArgs());
                        break;
                }

                if (Program.StartupDiscPath is not null && _disc is null)
                {
                    LoadConfiguredPaths();
                }

                RefreshLibrary();
            };
            IsEnabled = false;
            welcome.Show(this);
        }
        catch (Exception exception)
        {
            IsEnabled = true;
            StatusText.Text = $"Welcome error: {exception.Message}";
            Console.Error.WriteLine($"Welcome error: {exception}");
        }
    }

    private void OnWelcome(object? sender, RoutedEventArgs e) => ShowWelcome(firstRun: false);

    private void LoadConfiguredPaths()
    {
        if (File.Exists(_settings.BiosPath))
        {
            OpenBiosPath(_settings.BiosPath);
        }

        var startupDisc = Program.StartupDiscPath;
        var discPath = startupDisc ?? _settings.DiscPath;
        if (File.Exists(discPath))
        {
            OpenDiscPath(discPath, autoStart: startupDisc is not null);
        }
    }

    private async void OnOpenBios(object? sender, RoutedEventArgs e)
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

    private async void OnOpenDisc(object? sender, RoutedEventArgs e)
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
                machine.Reset();
                var nvram = _nvramStore.LoadOrCreate();
                machine.LoadNvram(nvram.Data.Span);
            });
            _biosLoaded = true;
            _settings.BiosPath = path;
            AppSettingsStore.Save(_settings);
            StatusText.Text = $"BIOS {bios.Md5[..8]} loaded{(bios.IsAnvil ? " (Anvil)" : string.Empty)}.";
            _log.Write(
                LogLevel.Info,
                LogCategory.Storage,
                $"BIOS loaded: {path} ({bios.Md5}, {(bios.IsAnvil ? "Anvil" : "retail")}).");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            StatusText.Text = $"BIOS error: {exception.Message}";
            _log.Write(LogLevel.Error, LogCategory.Storage, $"BIOS load failed: {exception.Message}");
            ShowError("BIOS error", exception.Message);
        }
    }

    private void OpenDiscPath(string path, bool autoStart = false)
    {
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
            _log.Write(
                LogLevel.Info,
                LogCategory.Storage,
                $"Disc opened: {_gameName} ({gameId[..Math.Min(8, gameId.Length)]}), "
                + $"{disc.SectorCount} sectors, fixes 0x{(uint)fixMode:x8}.");

            if (autoStart)
            {
                StartEmulation(showGameView: true);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or DiscImageException or ArgumentException)
        {
            StatusText.Text = $"Disc error: {exception.Message}";
            _log.Write(LogLevel.Error, LogCategory.Storage, $"Disc open failed: {exception.Message}");
            ShowError("Disc error", exception.Message);
        }
    }

    private void OnPlayPause(object? sender, RoutedEventArgs e)
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

            _log.Write(LogLevel.Info, LogCategory.Host, "Emulation started.");
        }

        SetPlayState(running: true);
        if (showGameView)
        {
            ShowEmulationView();
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
            _log.Write(LogLevel.Info, LogCategory.Host, "Emulation paused.");
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

    private void OnStop(object? sender, RoutedEventArgs e) => PauseEmulation(showLibrary: true);

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
        _runner?.Invoke(() => _machine?.Reset());
        StatusText.Text = "Reset.";
        _log.Write(LogLevel.Info, LogCategory.Machine, "Machine reset.");
    }

    private void OnSaveState(object? sender, RoutedEventArgs e)
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
        _log.Write(LogLevel.Info, LogCategory.State, $"State saved to slot {slot + 1} ({_gameName}).");
    }

    private void OnLoadState(object? sender, RoutedEventArgs e)
    {
        var slot = _slots.CurrentSlot;
        if (_machine is null || !_slots.Exists(slot))
        {
            StatusText.Text = $"Slot {slot + 1} is empty.";
            return;
        }

        _runner!.Invoke(() => _slots.Load(_machine, slot));
        StatusText.Text = $"State loaded: slot {slot + 1}.";
        _log.Write(LogLevel.Info, LogCategory.State, $"State loaded from slot {slot + 1}.");
    }

    private void OnPreviousSlot(object? sender, RoutedEventArgs e)
    {
        _slots.Previous();
        PersistSlot();
        UpdateSlotText();
        StatusText.Text = $"Selected slot {_slots.CurrentSlot + 1}.";
    }

    private void OnNextSlot(object? sender, RoutedEventArgs e)
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

            void OnCaptured(object? sender, Controls.CaptureCompletedEventArgs e)
            {
                Video.CaptureCompleted -= OnCaptured;
                PngDumper.WriteBgra(e.Pixels, e.Width, e.Height, path);
                if (_settings.SaveNativeScreenshot)
                {
                    SaveNativeScreenshot(nativePath);
                }

                StatusText.Text = $"Screenshot: {path}";
                _log.Write(LogLevel.Info, LogCategory.Host, $"Screenshot written: {path}.");
            }
        }

        SaveNativeScreenshot(path);
        StatusText.Text = $"Screenshot: {path}";
        _log.Write(LogLevel.Info, LogCategory.Host, $"Screenshot written from the CPU frame: {path}.");
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
        var close = new Button
        {
            Content = "Close",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
        };
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

    private void OnShowLibrary(object? sender, RoutedEventArgs e) => ShowLibraryView();

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
        text.AppendLine("A pure C# / .NET 3DO emulator with an Avalonia frontend and OpenGL presenter.");
        if (Video.Capabilities is { } capabilities)
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
        text.AppendLine("FreeDO/4DO are not standard OSI-licensed projects. See THIRD-PARTY-NOTICES.md "
            + "and docs/Licensing.md before distributing builds.");
        text.AppendLine();
        text.AppendLine("This project does not include BIOS ROMs or game images.");

        var textBlock = new TextBlock
        {
            Text = text.ToString(),
            TextWrapping = TextWrapping.Wrap,
        };
        var close = new Button
        {
            Content = "Close",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
        };
        var panel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
        };
        panel.Children.Add(textBlock);
        panel.Children.Add(close);
        var window = new Window
        {
            Title = "About 5DO",
            Width = 460,
            Height = 300,
            Content = panel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        close.Click += (_, _) => window.Close();
        _ = window.ShowDialog(this);
    }

    private async void OnAddFolder(object? sender, RoutedEventArgs e)
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
            RefreshLibrary();
        }
        else if (folders.Count > 0)
        {
            StatusText.Text = "That folder is already in the game library.";
        }
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => RefreshLibrary();

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => ApplyLibraryFilter();

    private void OnIconSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        var width = Math.Round(e.NewValue);
        Resources["LibraryCardWidth"] = width;
        Resources["LibraryCardImageHeight"] = Math.Max(80, width - 14);
    }

    private void OnGridView(object? sender, RoutedEventArgs e) => SetLibraryMode(grid: true);

    private void OnListView(object? sender, RoutedEventArgs e) => SetLibraryMode(grid: false);

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

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e) =>
        BootLibrarySelection(GridLibraryList.SelectedItem as LibraryItem);

    private void OnListDoubleTapped(object? sender, TappedEventArgs e) =>
        BootLibrarySelection(ListLibraryList.SelectedItem as LibraryItem);

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

    private static string CountLabel(int count) => count == 1 ? "1 game" : $"{count} games";

    private async void RefreshLibrary()
    {
        if (Program.SmokeTest)
        {
            return;
        }

        var generation = ++_scanGeneration;
        var folders = _settings.LibraryFolders.Count > 0
            ? _settings.LibraryFolders.ToList()
            : RecentFolders();
        StatusText.Text = folders.Count == 0
            ? "No game folders configured. Use File > Add Game Folder."
            : "Scanning game folders...";

        IReadOnlyList<GameLibraryEntry> entries = [];
        try
        {
            if (folders.Count > 0)
            {
                entries = await Task.Run(() => GameLibraryScanner.Scan(folders)).ConfigureAwait(true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = $"Library scan failed: {exception.Message}";
            _log.Write(LogLevel.Error, LogCategory.Storage, $"Library scan failed: {exception.Message}");
            return;
        }

        if (_shuttingDown)
        {
            return;
        }

        if (generation != _scanGeneration)
        {
            return;
        }

        var selectedPath = SelectedLibraryItem()?.Path;
        ClearLibraryItems();
        foreach (var entry in entries)
        {
            _allGames.Add(new LibraryItem(entry));
        }

        ApplyLibraryFilter();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            var reselect = _visibleGames.FirstOrDefault(item =>
                string.Equals(item.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
            if (reselect is not null)
            {
                SelectLibraryItem(reselect);
            }
        }

        StatusText.Text = entries.Count == 0
            ? "No discs found. Add a game folder from the File menu."
            : $"{CountLabel(entries.Count)} in the library.";
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
            RecentMenu.Items.Add(new MenuItem { Header = "No recent discs", IsEnabled = false });
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
        clear.Click += (_, _) =>
        {
            _settings.RecentDiscs.Clear();
            AppSettingsStore.Save(_settings);
            UpdateRecentMenu();
        };
        RecentMenu.Items.Add(clear);
    }

    private void OnToggleLog(object? sender, RoutedEventArgs e) => SetLogDockVisible(!_logDockVisible);

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
            lines.Add($"{time}[{entry.Level,-8}] {entry.Category,-12} {entry.Message}");
        }

        LogList.ItemsSource = lines;
        LogSummaryText.Text = entries.Count == 1 ? "1 entry" : $"{entries.Count} entries";
        if (lines.Count > 0)
        {
            LogList.ScrollIntoView(lines[^1]);
        }
    }

    private async void OnSettings(object? sender, RoutedEventArgs e)
    {
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
            return;
        }

        AppSettingsStore.Save(_settings);
        Video.Settings = _settings.ToRendererSettings(_gameId);
        OverlayPanel.IsVisible = _settings.ShowOverlay;
        if (_cpuVideo is not null)
        {
            _cpuVideo.Settings = _settings.ToRendererSettings(_gameId);
        }

        ApplyAudioSettings();
        HighResMenuItem.IsChecked = _settings.HighResolution;
        UpdateEmulationHeader();
        if (_machine is not null && _settings.HighResolution != _machine.Tuning.HighResolution)
        {
            RecreateMachine(_settings.HighResolution);
            return;
        }

        StatusText.Text = "Settings saved.";
        _log.Write(LogLevel.Info, LogCategory.Host, "Settings saved.");
    }

    private async void OnSaveSlots(object? sender, RoutedEventArgs e)
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
                    _runner!.Invoke(() => _slots.Load(_machine, slot));
                }
            },
            slot => _slots.Delete(slot));
        await window.ShowDialog(this).ConfigureAwait(true);
        PersistSlot();
        UpdateSlotText();
    }

    private async void OnInputBindings(object? sender, RoutedEventArgs e)
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
        _log.Write(
            LogLevel.Info,
            LogCategory.Input,
            $"Input bindings saved ({conflicts.Count} conflict(s)); {_gamepad?.ConnectedControllers ?? 0} gamepad(s).");
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

                _log.Write(
                    _audio is null ? LogLevel.Warning : LogLevel.Info,
                    LogCategory.Audio,
                    _audio is null
                        ? $"OpenAL audio is unavailable; emulation continues silently ({audioError ?? "unknown error"})."
                        : $"OpenAL audio ready ({OpenAlAudioOutput.OutputSampleRate} Hz stereo).");
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
        _log.Write(
            LogLevel.Info,
            LogCategory.Video,
            enabled ? "High-resolution mode enabled." : "High-resolution mode disabled.");
        if (_machine is null)
        {
            StatusText.Text = enabled
                ? "High-resolution mode enabled for the next game."
                : "High-resolution mode disabled.";
            return;
        }

        RecreateMachine(enabled);
    }

    private void RecreateMachine(bool highResolution)
    {
        var biosPath = _settings.BiosPath;
        var discPath = _settings.DiscPath;
        if (string.IsNullOrWhiteSpace(biosPath) || !File.Exists(biosPath))
        {
            StatusText.Text = "Reload the BIOS to apply the video mode.";
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
        StatusText.Text = highResolution
            ? "High-resolution mode enabled; machine restarted."
            : "High-resolution mode disabled; machine restarted.";
    }

    private void OnCoreStatus(CoreStatus status)
    {
        StatsText.Text =
            $"frame {status.Frames} · PC 0x{status.ProgramCounter:x8} · {status.Cycles} cyc";
        FpsText.Text = $"{status.FramesPerSecond,5:F1} fps";
        if (OverlayPanel.IsVisible)
        {
            var gpu = Video.GpuMilliseconds;
            var timing = gpu is null
                ? $"present {Video.PresentMilliseconds:F2} ms"
                : $"gpu {gpu.Value:F2} ms  present {Video.PresentMilliseconds:F2} ms";
            var audio = _audio is null
                ? string.Empty
                : $"  audio {_audio.BufferedFrames}f q{_audio.QueuedBuffers} u{_audio.UnderrunBuffers} "
                    + $"d{_audio.SamplesDropped} {FormatAudioMeter(_audio.TakePeakLevel())}";
            OverlayText.Text =
                $"{status.FramesPerSecond,5:F1}/{status.TargetFramesPerSecond:F2} fps  "
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

    private void OnOpenGlReady(object? sender, Controls.OpenGlReadyEventArgs e)
    {
        var capabilities = e.Capabilities;
        StopCpuFallback();
        RendererText.Text = $"{capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion}";
        StatusText.Text =
            $"{capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion} renderer ready.";
        _log.Write(
            LogLevel.Info,
            LogCategory.Video,
            $"OpenGL renderer ready: {capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion} "
            + $"({capabilities.Api}), max texture {capabilities.MaxTextureSize}.");
    }

    private void OnOpenGlFailed(object? sender, Controls.OpenGlFailedEventArgs e)
    {
        var exception = e.Exception;
        Console.Error.WriteLine($"OpenGL unavailable: {exception.Message}");
        _log.Write(LogLevel.Warning, LogCategory.Video, $"OpenGL unavailable: {exception.Message}");
        StartCpuFallback(exception);
    }

    private void StartCpuFallback(Exception exception)
    {
        if (_cpuVideo is not null || _shuttingDown)
        {
            return;
        }

        var width = _machine?.Frame.SourceWidth > 0
            ? _machine.Frame.SourceWidth
            : EmulatorConstants.VideoWidth;
        var height = _machine?.Frame.SourceHeight > 0
            ? _machine.Frame.SourceHeight
            : EmulatorConstants.VideoHeight;

        _cpuVideo = new CpuVideoRenderer { Settings = _settings.ToRendererSettings(_gameId) };
        _cpuVideo.Initialize(width, height);
        _cpuPixels = new byte[width * height * 4];
        _cpuBitmap = CreateBitmap(width, height);
        CpuVideo.Source = _cpuBitmap;
        CpuVideo.IsVisible = true;
        Video.IsVisible = false;
        _cpuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _cpuTimer.Tick += (_, _) => RenderCpuFrame();
        _cpuTimer.Start();
        RendererText.Text = "CPU fallback";
        StatusText.Text = $"OpenGL unavailable ({exception.Message}); using the CPU renderer.";
    }

    private void StopCpuFallback()
    {
        _cpuTimer?.Stop();
        _cpuTimer = null;
        CpuVideo.IsVisible = false;
        Video.IsVisible = true;
        _cpuVideo?.Dispose();
        _cpuVideo = null;
        _cpuBitmap = null;
        _cpuPixels = null;
    }

    private void RenderCpuFrame()
    {
        if (_cpuVideo is null || _cpuBitmap is null || _cpuPixels is null)
        {
            return;
        }

        var width = _machine?.Frame.SourceWidth ?? 0;
        var height = _machine?.Frame.SourceHeight ?? 0;
        if (width <= 0 || height <= 0)
        {
            width = EmulatorConstants.VideoWidth;
            height = EmulatorConstants.VideoHeight;
        }

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

    private static WriteableBitmap CreateBitmap(int width, int height)
    {
        return new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            AvaloniaPixelFormat.Bgra8888,
            AlphaFormat.Premul);
    }

    private FiveDoMachine CreateMachine()
    {
        if (_machine is null)
        {
            _machine = new FiveDoMachine(new EmulatorTuning { HighResolution = _settings.HighResolution })
            {
                PbusSource = _input,
                AudioSink = _audio,
            };
            _runner = new CoreRunner(
                _machine,
                frame => Video.SubmitFrame(frame),
                status => Dispatcher.UIThread.Post(() => OnCoreStatus(status)));
            _runner.Faulted += (_, args) => Dispatcher.UIThread.Post(() =>
            {
                var exception = args.Exception;
                _running = false;
                SetPlayState(running: false);
                StatusText.Text = $"Emulation error: {exception.Message}";
                _log.Write(LogLevel.Error, LogCategory.Machine, $"Emulation fault: {exception}");
                ShowLibraryView();
                ShowError("Emulation error", exception.Message);
            });
        }

        return _machine;
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
        _logTimer.Stop();
        _runner?.Dispose();
        _runner = null;
        FlushNvram();
        AppSettingsStore.Save(_settings);
        if (_audio is not null)
        {
            _log.Write(
                LogLevel.Info,
                LogCategory.Audio,
                $"OpenAL statistics: pushed {_audio.SamplesPushed}, dropped {_audio.SamplesDropped}, "
                + $"underruns {_audio.UnderrunBuffers}, buffered {_audio.BufferedFrames} frames, "
                + $"queued {_audio.QueuedFrames} frames, OpenAL errors {_audio.AlErrors}"
                + (_audio.LastAlError is null ? string.Empty : $" ({_audio.LastAlError})")
                + $", tap {(_audio.TapEnabled ? _audio.TapPath : "off")}"
                + (_audio.TapError is null ? string.Empty : $" ({_audio.TapError})")
                + ".");
        }

        _disc?.Dispose();
        _cpuTimer?.Stop();
        _cpuTimer = null;
        _cpuVideo?.Dispose();
        _cpuVideo = null;
        // VideoSurface owns GL resources until Avalonia detaches it. Disposing it from
        // Closing runs before the GL context is current and crashes in Silk.NET.
        _nvramStore.Dispose();
        _audio?.Dispose();
        _gamepad?.Dispose();
        ClearLibraryItems();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11)
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

        if (_input.Keyboard.SetKey(e.Key.ToString(), pressed: true))
        {
            e.Handled = true;
        }

        base.OnKeyDown(e);
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
        GC.SuppressFinalize(this);
    }
}
