using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

namespace ThreeDo.Frontend.Avalonia.Views;

/// <summary>
/// The minimal desktop shell: video surface, file loading, play/pause, reset, save/load,
/// screenshots, and keyboard input. One machine frame runs per UI timer tick for determinism;
/// a dedicated emulation thread is a later M9 optimization.
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly NvramStore _nvramStore;
    private readonly AppSettings _settings;
    private readonly InputManager _input;
    private readonly string _statePath;
    private readonly OpenAlAudioOutput? _audio;
    private ThreeDoMachine? _machine;
    private IDiscSource? _disc;
    private bool _running;

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
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, OnTick);
        Video.Settings = _settings.ToRendererSettings();
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
            machine.LoadBios(bios.Bios1.Span, bios.Bios2.Span);
            machine.Reset();
            var nvram = _nvramStore.LoadOrCreate();
            machine.LoadNvram(nvram.Data.Span);
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
            _disc?.Dispose();
            _disc = DiscImageFactory.Open(path);
            machine.InsertDisc(_disc);
            _settings.DiscPath = path;
            AppSettingsStore.Save(_settings);
            StatusText.Text = $"{Path.GetFileName(path)}: {_disc.SectorCount} sectors.";
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
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            FlushNvram();
        }
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        _machine?.Reset();
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
        var discHash = _disc is null ? string.Empty : GameId.Compute(_disc);
        _machine.SaveStateToFile(_statePath, discHash);
        StatusText.Text = $"State saved: {_statePath}";
    }

    private void OnLoadState(object? sender, RoutedEventArgs e)
    {
        if (_machine is null || !File.Exists(_statePath))
        {
            StatusText.Text = "No quick state to load.";
            return;
        }

        _machine.LoadStateFromFile(_statePath);
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
        var frame = new FrameBuffer(_machine.Frame.SourceWidth, _machine.Frame.SourceHeight);
        FrameAssembler.Assemble(_machine.Frame, frame, frame.Width, frame.Height);
        var path = Path.Combine(directory, $"3do-{DateTime.Now:yyyyMMdd-HHmmss}.bmp");
        FrameDumper.WriteBmp(frame, path);
        StatusText.Text = $"Screenshot: {path}";
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_machine is null)
        {
            return;
        }

        try
        {
            _machine.RunFrame();
            Video.SubmitFrame(_machine.Frame);
            StatusText.Text =
                $"Frame {_machine.FramesExecuted}  PC 0x{_machine.Cpu.ProgramCounter:x8}  cycles {_machine.LastFrameCycles}";
        }
        catch (Exception exception) when (exception is EmulationException or InvalidOperationException)
        {
            _running = false;
            _timer.Stop();
            StatusText.Text = $"Emulation error: {exception.Message}";
        }
    }

    private ThreeDoMachine CreateMachine()
    {
        _machine ??= new ThreeDoMachine { PbusSource = _input, AudioSink = _audio };
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
        _timer.Stop();
        FlushNvram();
        AppSettingsStore.Save(_settings);
        _disc?.Dispose();
        Video.Dispose();
        _nvramStore.Dispose();
        _audio?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
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
        _timer.Stop();
        _disc?.Dispose();
        Video.Dispose();
        _nvramStore.Dispose();
        _audio?.Dispose();
        GC.SuppressFinalize(this);
    }
}
