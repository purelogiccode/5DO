using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Message box shown when the startup update check finds a newer GitHub release.
/// </summary>
public partial class UpdateAvailableWindow : Window
{
    private readonly string? _downloadUrl;

    /// <summary>Creates the dialog.</summary>
    public UpdateAvailableWindow()
        : this(null)
    {
    }

    /// <summary>Creates the dialog for one release.</summary>
    /// <param name="info">Release information, or <see langword="null"/> for XAML tooling.</param>
    public UpdateAvailableWindow(UpdateInfo? info)
    {
        InitializeComponent();
        _downloadUrl = info?.DownloadUrl;
        if (info is not null)
        {
            HeadingText.Text = $"{FiveDoVersion.ProductName} {info.LatestVersion} is available.";
            VersionText.Text = $"You have {info.CurrentVersion}. The newer release is {info.LatestVersion}.";
            ReleaseText.Text = info.ReleaseName;
            UrlText.Text = info.DownloadUrl;
        }
    }

    /// <summary>Shows the update dialog for a release.</summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="info">Release information.</param>
    /// <returns>A task that completes when the dialog closes.</returns>
    public static async Task ShowAsync(Window owner, UpdateInfo info)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(info);
        var dialog = new UpdateAvailableWindow(info);
        await dialog.ShowDialog(owner).ConfigureAwait(true);
    }

    private void OnDownload(object? sender, RoutedEventArgs e)
    {
        if (!UpdateLauncher.TryOpen(_downloadUrl))
        {
            UrlText.Text = $"{_downloadUrl} (could not open a browser; copy this URL)";
            return;
        }

        Close();
    }

    private void OnLater(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
