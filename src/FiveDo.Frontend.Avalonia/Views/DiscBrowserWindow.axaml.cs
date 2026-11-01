using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FiveDo.Host;
using FiveDo.Storage;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Browses the Opera file system of the current disc and extracts files or directories.
/// </summary>
public partial class DiscBrowserWindow : Window
{
    private readonly string _discPath;
    private DiscBrowser? _browser;
    private string? _lastExtractionPath;
    private bool _closed;
    private bool _extracting;

    /// <summary>Creates an empty window for XAML preview and smoke tests.</summary>
    public DiscBrowserWindow()
        : this(string.Empty)
    {
    }

    /// <summary>Creates a browser for one disc image.</summary>
    /// <param name="discPath">ISO, BIN, CUE, or CHD path.</param>
    public DiscBrowserWindow(string discPath)
    {
        InitializeComponent();
        _discPath = discPath;
        Closed += (_, _) =>
        {
            _closed = true;
            if (!_extracting)
            {
                _browser?.Dispose();
                _browser = null;
            }
        };

        ExtractSelectedButton.IsEnabled = false;
        ExtractAllButton.IsEnabled = false;
        if (string.IsNullOrWhiteSpace(discPath))
        {
            StatusText.Text = "Boot a disc to browse its files.";
            return;
        }

        AsyncEvent.Run(LoadAsync, "reading the disc file system");
    }

    private async Task LoadAsync()
    {
        StatusText.Text = "Reading disc...";
        try
        {
            DiscBrowser browser = await Task.Run(() => DiscBrowser.Open(_discPath)).ConfigureAwait(true);
            if (_closed)
            {
                browser.Dispose();
                return;
            }

            _browser = browser;
            SourceText.Text = browser.SourcePath;
            VolumeText.Text =
                $"{browser.Label} ({browser.Format}, {browser.SectorCount.ToString("N0", CultureInfo.InvariantCulture)} sectors)";
            GameIdText.Text = string.IsNullOrWhiteSpace(browser.GameId) ? "(unknown)" : browser.GameId;
            ContentsText.Text =
                $"{browser.Info.FileCount.ToString("N0", CultureInfo.InvariantCulture)} files, "
                + $"{browser.Info.DirectoryCount.ToString("N0", CultureInfo.InvariantCulture)} directories, "
                + DiscBrowserNode.FormatBytes((ulong)browser.Info.TotalFileBytes);
            FooterText.Text = string.IsNullOrWhiteSpace(browser.Comment)
                ? "The disc is read-only. Extracted files are written below the folder you choose."
                : browser.Comment;
            EntryList.ItemsSource = browser.Nodes;
            if (browser.Nodes.Count > 0)
            {
                EntryList.SelectedItem = browser.Root;
            }

            ExtractSelectedButton.IsEnabled = true;
            ExtractAllButton.IsEnabled = true;
            StatusText.Text = "Ready.";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or DiscImageException
            or ArgumentException)
        {
            StatusText.Text = $"The disc could not be read: {exception.Message}";
            Serilog.Log.Error(exception, "Disc browser failed to read {Path}.", _discPath);
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (EntryList.SelectedItem is not DiscBrowserNode node)
        {
            DetailName.Text = "Select an entry";
            DetailPath.Text = string.Empty;
            DetailType.Text = string.Empty;
            DetailSize.Text = string.Empty;
            DetailLba.Text = string.Empty;
            DetailChildren.Text = string.Empty;
            return;
        }

        DetailName.Text = node.DisplayName;
        DetailPath.Text = node.Path;
        DetailType.Text = node.Kind;
        DetailSize.Text = node.IsDirectory
            ? node.SizeText
            : DiscBrowserNode.FormatBytes(node.Size);
        DetailLba.Text = node.Lba.ToString(CultureInfo.InvariantCulture);
        DetailChildren.Text = node.IsDirectory
            ? node.Children.Count.ToString(CultureInfo.InvariantCulture)
            : "-";
    }

    private void OnExtractSelectedAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ExtractSelectedAsync, "extracting the selected disc entry");
    }

    private void OnExtractAllAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ExtractAllAsync, "extracting the whole disc");
    }

    private async Task ExtractSelectedAsync()
    {
        if (_browser is null || EntryList.SelectedItem is not DiscBrowserNode node)
        {
            StatusText.Text = "Select a file or directory first.";
            return;
        }

        await ExtractAsync(node, node == _browser.Root ? "the whole disc" : node.Path)
            .ConfigureAwait(true);
    }

    private async Task ExtractAllAsync()
    {
        if (_browser is null)
        {
            StatusText.Text = "The disc is still loading.";
            return;
        }

        await ExtractAsync(_browser.Root, "the whole disc").ConfigureAwait(true);
    }

    private async Task ExtractAsync(DiscBrowserNode node, string label)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = $"Extract {label} to a folder",
                AllowMultiple = false,
            }).ConfigureAwait(true);
        if (folders.Count == 0)
        {
            return;
        }

        var destination = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destination))
        {
            StatusText.Text = "The selected folder has no local path.";
            return;
        }

        if (_closed || _browser is null)
        {
            return;
        }

        StatusText.Text = $"Extracting {label}...";
        _extracting = true;
        DiscBrowser browser = _browser;
        try
        {
            DiscExtractionResult result = await Task.Run(
                () => browser.Extract(node, destination)).ConfigureAwait(true);
            _lastExtractionPath = result.DestinationPath;
            OpenFolderButton.IsEnabled = true;
            StatusText.Text =
                $"Extracted {result.Files.ToString("N0", CultureInfo.InvariantCulture)} files "
                + $"({DiscBrowserNode.FormatBytes((ulong)result.Bytes)}) to {result.DestinationPath}.";
            FooterText.Text = StatusText.Text;
            Serilog.Log.Information(
                "Extracted {Files} disc files ({Bytes} bytes) to {Path}.",
                result.Files,
                result.Bytes,
                result.DestinationPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or DiscImageException
            or ArgumentException)
        {
            StatusText.Text = $"Extraction failed: {exception.Message}";
            Serilog.Log.Error(exception, "Disc extraction failed for {Path}.", _discPath);
        }
        finally
        {
            _extracting = false;
            if (_closed)
            {
                _browser?.Dispose();
                _browser = null;
            }
        }
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastExtractionPath)
            || !Directory.Exists(_lastExtractionPath))
        {
            StatusText.Text = "Extract files first.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _lastExtractionPath,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = $"The folder could not be opened: {exception.Message}";
            Serilog.Log.Debug(exception, "Opening the extraction folder failed.");
        }
    }
}
