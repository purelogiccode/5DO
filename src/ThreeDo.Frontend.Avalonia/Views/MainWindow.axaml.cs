using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ThreeDo.Audio;
using ThreeDo.Core;
using ThreeDo.Diagnostics;
using ThreeDo.Host;
using ThreeDo.Input;
using ThreeDo.Machine;
using ThreeDo.Storage;
using ThreeDo.Video;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;

namespace ThreeDo.Frontend.Avalonia.Views;

/// <summary>
/// The minimal desktop shell: video surface, file loading, play/pause, reset, save/load,
/// screenshots, and keyboard input. The machine runs on <see cref="CoreRunner"/>'s dedicated
/// 60 Hz thread; the video surface renders the latest mailbox frame on the UI thread.
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private readonly NvramStore _nvramStore;
    private readonly AppSettings _settings;
    private readonly InputManager _input;
    private readonly string _statePath;
    private readonly OpenAlAudioOutput? _audio;
    private ThreeDoMachine? _machine;
    private CoreRunner? _runner;
    private IDiscSource? _disc;
    private CpuVideoRenderer? _cpuVideo;
    private WriteableBitmap? _cpuBitmap;
    private DispatcherTimer? _cpuTimer;
    private byte[]? _cpuPixels;
    private bool _running;
    private bool _shuttingDown;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettingsStore.Load();
        if (string.IsNullOrWhiteSpace(_settings.SaveDirectory))
        {
            _settings.SaveDirectory = Path.Combine(AppSettingsStore.DefaultDirectory, "states");
        }

        _statePath = Path.Combine(_settings.SaveDirectory, "quick.tdos");
        _input = new InputManager(_settings.Input);
        _audio = _settings.AudioEnabled ? OpenAlAudioOutput.TryCreate() : null;
        _nvramStore = new NvramStore(Path.Combine(AppSettingsStore.DefaultDirectory, "nvram.ram"));
        HighResCheck.IsChecked = _settings.HighResolution;
        Video.Settings = _settings.ToRendererSettings();
        Video.OpenGlReady += OnOpenGlReady;
        Video.OpenGlFailed += OnOpenGlFailed;
        Deactivated += (_, _) => _input.Keyboard.Reset();
        Closing += (_, _) => Shutdown();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (File.Exists(_settings.BiosPath))
        {
            OpenBiosPath(_settings.BiosPath);
        }

        if (File.Exists(_settings.DiscPath))
        {
            OpenDiscPath(_settings.DiscPath);
        }

        await Task.CompletedTask;
    }

    private async void OnOpenBios(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open 3DO BIOS",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("BIOS ROM") { Patterns = ["*.bin", "*.rom"] }],
        });
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
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
        {
            OpenDiscPath(path);
        }
    }

    private void OpenBiosPath(string path)
    {
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
            _settings.BiosPath = path;
            AppSettingsStore.Save(_settings);
            StatusText.Text = $"BIOS {bios.Md5[..8]} loaded{(bios.IsAnvil ? " (Anvil)" : string.Empty)}.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            StatusText.Text = $"BIOS error: {exception.Message}";
        }
    }

    private void OpenDiscPath(string path)
    {
        try
        {
            var machine = _machine ?? CreateMachine();
            var disc = DiscImageFactory.Open(path);
            _runner!.Invoke(() =>
            {
                _disc?.Dispose();
                _disc = disc;
                machine.InsertDisc(disc);
            });
            _settings.DiscPath = path;
            _settings.RecentDiscs.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
            _settings.RecentDiscs.Insert(0, path);
            if (_settings.RecentDiscs.Count > 10)
            {
                _settings.RecentDiscs.RemoveRange(10, _settings.RecentDiscs.Count - 10);
            }

            AppSettingsStore.Save(_settings);
            StatusText.Text = $"{Path.GetFileName(path)}: {disc.SectorCount} sectors.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or DiscImageException or ArgumentException)
        {
            StatusText.Text = $"Disc error: {exception.Message}";
        }
    }

    private void OnPlayPause(object? sender, RoutedEventArgs e)
    {
        if (_machine is null)
        {
            StatusText.Text = "Load a BIOS first.";
            return;
        }

        _running = !_running;
        PlayButton.Content = _running ? "Pause" : "Play";
        if (_running)
        {
            _runner!.Start();
        }
        else
        {
            _runner!.Pause();
            FlushNvram();
        }
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        _runner?.Invoke(() => _machine?.Reset());
        StatusText.Text = "Reset.";
    }

    private void OnSaveState(object? sender, RoutedEventArgs e)
    {
        if (_machine is null)
        {
            StatusText.Text = "Nothing to save.";
            return;
        }

        Directory.CreateDirectory(_settings.SaveDirectory);
        _runner!.Invoke(() =>
        {
            var discHash = _disc is null ? string.Empty : GameId.Compute(_disc);
            _machine.SaveStateToFile(_statePath, discHash);
        });
        StatusText.Text = $"State saved: {_statePath}";
    }

    private void OnLoadState(object? sender, RoutedEventArgs e)
    {
        if (_machine is null || !File.Exists(_statePath))
        {
            StatusText.Text = "No quick state to load.";
            return;
        }

        _runner!.Invoke(() => _machine.LoadStateFromFile(_statePath));
        StatusText.Text = $"State loaded: {_statePath}";
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
        var path = Path.Combine(directory, $"3do-{DateTime.Now:yyyyMMdd-HHmmss}.bmp");
        if (Video.IsOpenGlAvailable)
        {
            void OnCaptured(byte[] pixels, int width, int height)
            {
                Video.CaptureCompleted -= OnCaptured;
                FrameDumper.WriteBmp(pixels, width, height, path);
                StatusText.Text = $"Screenshot: {path}";
            }

            Video.CaptureCompleted += OnCaptured;
            Video.RequestCapture();
            StatusText.Text = "Capturing screenshot...";
            return;
        }

        _runner!.Invoke(() =>
        {
            var frame = new FrameBuffer(_machine.Frame.SourceWidth, _machine.Frame.SourceHeight);
            FrameAssembler.Assemble(_machine.Frame, frame, frame.Width, frame.Height);
            FrameDumper.WriteBmp(frame, path);
        });
        StatusText.Text = $"Screenshot: {path}";
    }

    private void OnFullscreen(object? sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        WindowState = WindowState == WindowState.FullScreen
            ? WindowState.Normal
            : WindowState.FullScreen;
    }

    private void OnAbout(object? sender, RoutedEventArgs e)
    {
        var text = new TextBlock
        {
            Text = $"{ThreeDoVersion.ProductName} {ThreeDoVersion.Version}\n\n"
                + "A pure C# / .NET 3DO emulator with an Avalonia frontend and OpenGL presenter.\n\n"
                + "FreeDO/4DO are not standard OSI-licensed projects. See THIRD-PARTY-NOTICES.md "
                + "and docs/Licensing.md before distributing builds.\n\n"
                + "This project does not include BIOS ROMs or game images.",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
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
            Title = "About 3DOEmulator",
            Width = 460,
            Height = 300,
            Content = panel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        close.Click += (_, _) => window.Close();
        window.ShowDialog(this);
    }

    private void OnHighResChanged(object? sender, RoutedEventArgs e)
    {
        var enabled = HighResCheck.IsChecked == true;
        if (_settings.HighResolution == enabled)
        {
            return;
        }

        _settings.HighResolution = enabled;
        AppSettingsStore.Save(_settings);
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
        PlayButton.Content = "Play";
        _runner?.Dispose();
        _runner = null;
        _machine = null;
        _disc?.Dispose();
        _disc = null;
        OpenBiosPath(biosPath);
        if (!string.IsNullOrWhiteSpace(discPath) && File.Exists(discPath))
        {
            OpenDiscPath(discPath);
        }

        StatusText.Text = highResolution
            ? "High-resolution mode enabled; machine restarted."
            : "High-resolution mode disabled; machine restarted.";
    }

    private void OnCoreStatus(CoreStatus status)
    {
        StatusText.Text =
            $"Frame {status.Frames}  PC 0x{status.ProgramCounter:x8}  cycles {status.Cycles}  {status.FramesPerSecond:F1} fps";
    }

    private void OnOpenGlReady(RendererCapabilities capabilities)
    {
        StopCpuFallback();
        StatusText.Text =
            $"{capabilities.Name} {capabilities.MajorVersion}.{capabilities.MinorVersion} renderer ready.";
    }

    private void OnOpenGlFailed(Exception exception)
    {
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

        _cpuVideo = new CpuVideoRenderer { Settings = _settings.ToRendererSettings() };
        _cpuVideo.Initialize(width, height);
        _cpuPixels = new byte[width * height * 4];
        _cpuBitmap = CreateBitmap(width, height);
        CpuVideo.Source = _cpuBitmap;
        CpuVideo.IsVisible = true;
        Video.IsVisible = false;
        _cpuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _cpuTimer.Tick += (_, _) => RenderCpuFrame();
        _cpuTimer.Start();
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

    private ThreeDoMachine CreateMachine()
    {
        if (_machine is null)
        {
            _machine = new ThreeDoMachine(new EmulatorTuning { HighResolution = _settings.HighResolution })
            {
                PbusSource = _input,
                AudioSink = _audio,
            };
            _runner = new CoreRunner(
                _machine,
                frame => Video.SubmitFrame(frame),
                status => Dispatcher.UIThread.Post(() => OnCoreStatus(status)));
            _runner.Faulted += exception => Dispatcher.UIThread.Post(() =>
            {
                _running = false;
                PlayButton.Content = "Play";
                StatusText.Text = $"Emulation error: {exception.Message}";
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
        _shuttingDown = true;
        _runner?.Dispose();
        _runner = null;
        FlushNvram();
        AppSettingsStore.Save(_settings);
        _disc?.Dispose();
        _cpuTimer?.Stop();
        _cpuTimer = null;
        _cpuVideo?.Dispose();
        _cpuVideo = null;
        Video.Dispose();
        _nvramStore.Dispose();
        _audio?.Dispose();
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
            WindowState = WindowState.Normal;
            e.Handled = true;
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
        if (_input.Keyboard.SetKey(e.Key.ToString(), pressed: false))
        {
            e.Handled = true;
        }

        base.OnKeyUp(e);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _shuttingDown = true;
        _runner?.Dispose();
        _runner = null;
        _disc?.Dispose();
        _cpuTimer?.Stop();
        _cpuTimer = null;
        _cpuVideo?.Dispose();
        _cpuVideo = null;
        Video.Dispose();
        _nvramStore.Dispose();
        _audio?.Dispose();
        GC.SuppressFinalize(this);
    }
}
