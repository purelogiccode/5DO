using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ThreeDo.Core;
using ThreeDo.Host;
using ThreeDo.Video;

namespace ThreeDo.Frontend.Avalonia.Views;

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
        AspectCombo.ItemsSource = Enum.GetValues<AspectMode>();
        AspectCombo.SelectedItem = settings.Aspect;
        IntegerCheck.IsChecked = settings.IntegerScale;
        VsyncCheck.IsChecked = settings.Vsync;
        AudioCheck.IsChecked = settings.AudioEnabled;
        VolumeSlider.Value = settings.Volume;
        AutoFixesCheck.IsChecked = settings.AutoGameFixes;
        HighResCheck.IsChecked = settings.HighResolution;
        SaveDirectoryBox.Text = settings.SaveDirectory;
        ScreenshotDirectoryBox.Text = settings.ScreenshotDirectory;
        GameText.Text = string.IsNullOrWhiteSpace(gameId)
            ? "No disc loaded."
            : $"{gameName ?? "Unknown"} ({gameId[..Math.Min(8, gameId.Length)]})";

        var overrideMode = settings.GetFixModeOverride(gameId);
        FixModeBox.Text = overrideMode is null ? string.Empty : $"0x{(uint)overrideMode.Value:x}";
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
        settings.AudioEnabled = AudioCheck.IsChecked == true;
        settings.Volume = (float)VolumeSlider.Value;
        settings.AutoGameFixes = AutoFixesCheck.IsChecked == true;
        settings.HighResolution = HighResCheck.IsChecked == true;
        settings.SaveDirectory = SaveDirectoryBox.Text?.Trim() ?? string.Empty;
        settings.ScreenshotDirectory = ScreenshotDirectoryBox.Text?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_gameId))
        {
            var text = FixModeBox.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                settings.SetFixModeOverride(_gameId, null);
            }
            else if (TryParseFixMode(text, out var mode))
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
        var path = await PickFolder("Select the save-state directory", SaveDirectoryBox.Text);
        if (path is not null)
        {
            SaveDirectoryBox.Text = path;
        }
    }

    private async void OnBrowseScreenshotDirectory(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolder("Select the screenshot directory", ScreenshotDirectoryBox.Text);
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
            var start = await StorageProvider.TryGetFolderFromPathAsync(current);
            if (start is not null)
            {
                options.SuggestedStartLocation = start;
            }
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(options);
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
