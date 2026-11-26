using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Consent and progress dialog for the pinned FFmpeg download.
/// </summary>
internal sealed class FfmpegInstallWindow : Window
{
    private readonly FfmpegDownloadSource? _source;
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly Button _install;
    private readonly Button _cancel;
    private CancellationTokenSource? _downloadCancellation;

    /// <summary>Creates the dialog.</summary>
    /// <param name="autoStart">Whether to start the download immediately.</param>
    public FfmpegInstallWindow(bool autoStart = false)
    {
        _source = FfmpegProvisioner.CurrentSource;
        Title = "Install FFmpeg";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var heading = new TextBlock
        {
            Text = "FFmpeg is required for MP4 recording",
            FontSize = 19,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        string target = _source is null
            ? "No automatic download is available for this platform."
            : $"It will be saved to {FfmpegProvisioner.GetInstallDirectory()}.";
        var body = new TextBlock
        {
            Text = _source is null
                ? "Install FFmpeg manually, or set FIVEDO_FFMPEG to its executable path."
                : $"5DO can download FFmpeg {_source.Version} ({_source.SizeMiB:F0} MiB) "
                    + $"from GitHub for {_source.Platform}. The file is verified by SHA-256 "
                    + $"before it is used. {target}",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85,
        };

        var source = new TextBlock
        {
            Text = _source?.Url ?? string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.6,
            FontSize = 11,
        };

        var license = new TextBlock
        {
            Text = "FFmpeg is free software under the LGPL/GPL. 5DO does not bundle or "
                + "redistribute it; the download comes directly from the pinned upstream build.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.65,
            FontSize = 11,
        };

        _progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 8,
            IsVisible = false,
        };
        _status = new TextBlock
        {
            Text = string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            FontSize = 12,
        };

        _install = new Button
        {
            Content = "Download FFmpeg",
            IsDefault = true,
        };
        _install.Classes.Add("accent");
        _install.IsEnabled = _source is not null;
        _install.Click += OnInstall;

        _cancel = new Button
        {
            Content = "Cancel",
            IsCancel = true,
        };
        _cancel.Click += OnCancel;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(_cancel);
        buttons.Children.Add(_install);

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
        };
        panel.Children.Add(heading);
        panel.Children.Add(body);
        panel.Children.Add(source);
        panel.Children.Add(license);
        panel.Children.Add(_progress);
        panel.Children.Add(_status);
        panel.Children.Add(buttons);
        Content = panel;

        if (autoStart && _source is not null)
        {
            Opened += (_, _) => AsyncEvent.Run(InstallAsync, "installing FFmpeg");
        }
    }

    /// <summary>Shows the dialog and returns the installed executable.</summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="autoStart">Whether to start immediately after opening.</param>
    /// <returns>The installed path, or <see langword="null"/> when canceled.</returns>
    public static async Task<string?> ShowAsync(Window owner, bool autoStart = false)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dialog = new FfmpegInstallWindow(autoStart);
        return await dialog.ShowDialog<string?>(owner).ConfigureAwait(true);
    }

    private void OnInstall(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(InstallAsync, "installing FFmpeg");
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _downloadCancellation?.Cancel();
        Close(null);
    }

    private async Task InstallAsync()
    {
        if (_source is null || _downloadCancellation is not null)
        {
            return;
        }

        _downloadCancellation = new CancellationTokenSource();
        _install.IsEnabled = false;
        _progress.IsVisible = true;
        _progress.Value = 0;
        _status.Text = "Starting download...";
        try
        {
            var progress = new Progress<FfmpegInstallProgress>(value =>
            {
                _progress.Value = value.Percent;
                _status.Text = value.TotalBytes > 0
                    ? $"{value.Stage}: {value.BytesReceived / (1024.0 * 1024.0):F1} / "
                        + $"{value.TotalBytes / (1024.0 * 1024.0):F1} MiB"
                    : value.Stage;
            });
            FfmpegInstallResult result = await FfmpegProvisioner
                .InstallAsync(progress, cancellationToken: _downloadCancellation.Token)
                .ConfigureAwait(true);
            _progress.Value = 100;
            _status.Text = "Installed: " + result.Path;
            Close(result.Path);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Download canceled.";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _status.Text = exception.Message;
            Serilog.Log.Error(exception, "FFmpeg installation failed.");
        }
        finally
        {
            _downloadCancellation?.Dispose();
            _downloadCancellation = null;
            _install.IsEnabled = true;
        }
    }
}
