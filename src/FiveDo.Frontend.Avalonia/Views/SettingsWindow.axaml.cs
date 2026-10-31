using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FiveDo.Audio;
using FiveDo.Core;
using FiveDo.Host;
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
        SaveDirectoryBox.Text = settings.SaveDirectory;
        ScreenshotDirectoryBox.Text = settings.ScreenshotDirectory;
        CoverDirectoryBox.Text = settings.CoverDirectory;
        GameText.Text = string.IsNullOrWhiteSpace(gameId)
            ? "No disc loaded."
            : $"{gameName ?? "Unknown"} ({gameId[..Math.Min(8, gameId.Length)]})";

        FixMode? overrideMode = settings.GetFixModeOverride(gameId);
        FixModeBox.Text = overrideMode is null ? string.Empty : $"0x{(uint)overrideMode.Value:x}";
    }

    private void OnTestAudioClickAsync(object? sender, RoutedEventArgs e) =>
        AsyncEvent.Run(TestAudioAsync, "testing audio output");

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
        settings.SaveDirectory = SaveDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.ScreenshotDirectory = ScreenshotDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.CoverDirectory = CoverDirectoryBox.Text?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_gameId))
        {
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

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private void OnBrowseSaveDirectoryAsync(object? sender, RoutedEventArgs e) =>
        AsyncEvent.Run(BrowseSaveDirectoryAsync, "selecting the save-state directory");

    private async Task BrowseSaveDirectoryAsync()
    {
        var path = await PickFolderAsync("Select the save-state directory", SaveDirectoryBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            SaveDirectoryBox.Text = path;
        }
    }

    private void OnBrowseScreenshotDirectoryAsync(object? sender, RoutedEventArgs e) =>
        AsyncEvent.Run(BrowseScreenshotDirectoryAsync, "selecting the screenshot directory");

    private async Task BrowseScreenshotDirectoryAsync()
    {
        var path = await PickFolderAsync("Select the screenshot directory", ScreenshotDirectoryBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            ScreenshotDirectoryBox.Text = path;
        }
    }

    private void OnBrowseCoverDirectoryAsync(object? sender, RoutedEventArgs e) =>
        AsyncEvent.Run(BrowseCoverDirectoryAsync, "selecting the cover-art directory");

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
        if (!string.IsNullOrWhiteSpace(current) && System.IO.Directory.Exists(current))
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
        public override string ToString() => Label;
    }

    private sealed record ResolutionOption(OutputResolution Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record FrameSkipOption(FrameSkipMode Value, string Label)
    {
        public override string ToString() => Label;
    }
}
