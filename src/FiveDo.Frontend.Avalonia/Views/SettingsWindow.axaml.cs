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
        IntegerCheck.IsChecked = settings.IntegerScale;
        VsyncCheck.IsChecked = settings.Vsync;
        OverlayCheck.IsChecked = settings.ShowOverlay;
        NativeScreenshotCheck.IsChecked = settings.SaveNativeScreenshot;
        BloomSlider.Value = settings.Bloom;
        AudioCheck.IsChecked = settings.AudioEnabled;
        VolumeSlider.Value = settings.Volume;
        AutoFixesCheck.IsChecked = settings.AutoGameFixes;
        HighResCheck.IsChecked = settings.HighResolution;
        SaveDirectoryBox.Text = settings.SaveDirectory;
        ScreenshotDirectoryBox.Text = settings.ScreenshotDirectory;
        GameText.Text = string.IsNullOrWhiteSpace(gameId)
            ? "No disc loaded."
            : $"{gameName ?? "Unknown"} ({gameId[..Math.Min(8, gameId.Length)]})";

        FixMode? overrideMode = settings.GetFixModeOverride(gameId);
        FixModeBox.Text = overrideMode is null ? string.Empty : $"0x{(uint)overrideMode.Value:x}";
    }

    private async void OnTestAudioClick(object? sender, RoutedEventArgs e)
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
                        output.PushSample(new AudioSample(value, value).Packed);
                    }

                    Thread.Sleep(16);
                }
            }).ConfigureAwait(false);
            TestAudioText.Text = "Test tone finished.";
        }
        catch (Exception exception)
        {
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
        settings.IntegerScale = IntegerCheck.IsChecked == true;
        settings.Vsync = VsyncCheck.IsChecked == true;
        settings.ShowOverlay = OverlayCheck.IsChecked == true;
        settings.SaveNativeScreenshot = NativeScreenshotCheck.IsChecked == true;
        settings.Bloom = (float)BloomSlider.Value;
        settings.AudioEnabled = AudioCheck.IsChecked == true;
        settings.Volume = (float)VolumeSlider.Value;
        settings.AutoGameFixes = AutoFixesCheck.IsChecked == true;
        settings.HighResolution = HighResCheck.IsChecked == true;
        settings.SaveDirectory = SaveDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.ScreenshotDirectory = ScreenshotDirectoryBox.Text?.Trim() ?? string.Empty;

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
            ValidationText.Text = exception.Message;
            return;
        }

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnBrowseSaveDirectory(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolder("Select the save-state directory", SaveDirectoryBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            SaveDirectoryBox.Text = path;
        }
    }

    private async void OnBrowseScreenshotDirectory(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolder("Select the screenshot directory", ScreenshotDirectoryBox.Text).ConfigureAwait(true);
        if (path is not null)
        {
            ScreenshotDirectoryBox.Text = path;
        }
    }

    private async System.Threading.Tasks.Task<string?> PickFolder(string title, string? current)
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
}
