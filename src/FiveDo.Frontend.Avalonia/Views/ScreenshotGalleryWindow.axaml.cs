using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Browses screenshots in the configured directory with thumbnails and file actions.
/// </summary>
public partial class ScreenshotGalleryWindow : Window
{
    private readonly ObservableCollection<ScreenshotRow> _rows = [];
    private readonly string _directory;
    private string? _pendingDeletePath;

    /// <summary>Creates the window for the default screenshot directory.</summary>
    public ScreenshotGalleryWindow()
        : this(new AppSettings().ResolveScreenshotDirectory())
    {
    }

    /// <summary>Creates the window for a screenshot directory.</summary>
    /// <param name="directory">Directory to scan.</param>
    public ScreenshotGalleryWindow(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        InitializeComponent();
        _directory = directory;
        ScreenshotList.ItemsSource = _rows;
        Closed += (_, _) => DisposeRows();
        Refresh();
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        _pendingDeletePath = null;
        Refresh(ScreenshotList.SelectedItem is ScreenshotRow row ? row.Entry.Path : null);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _pendingDeletePath = null;
        bool selected = ScreenshotList.SelectedItem is ScreenshotRow;
        OpenButton.IsEnabled = selected;
        CopyPathButton.IsEnabled = selected;
        DeleteButton.IsEnabled = selected;
        if (selected)
        {
            StatusText.Text = "Selected. The first Delete click asks for confirmation.";
        }
    }

    private void OnDoubleTapped(object? sender, RoutedEventArgs e) => OpenSelected();

    private void OnOpenClick(object? sender, RoutedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (ScreenshotList.SelectedItem is not ScreenshotRow row)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(row.Entry.Path) { UseShellExecute = true });
            StatusText.Text = $"Opened {row.Entry.FileName}.";
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = $"Could not open the screenshot: {exception.Message}";
        }
    }

    private void OnCopyPathClick(object? sender, RoutedEventArgs e)
    {
        if (ScreenshotList.SelectedItem is not ScreenshotRow row)
        {
            return;
        }

        _ = CopyPathAsync(row.Entry.Path);
        StatusText.Text = $"Copied {row.Entry.FileName}.";
    }

    private async Task CopyPathAsync(string path)
    {
        if (Clipboard is not null)
        {
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(path));
            await Clipboard.SetDataAsync(transfer).ConfigureAwait(true);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (ScreenshotList.SelectedItem is not ScreenshotRow row)
        {
            return;
        }

        if (!string.Equals(_pendingDeletePath, row.Entry.Path, StringComparison.Ordinal))
        {
            _pendingDeletePath = row.Entry.Path;
            StatusText.Text = $"Click Delete again to remove {row.Entry.FileName}.";
            return;
        }

        try
        {
            ScreenshotLibrary.Delete(row.Entry.Path);
            _pendingDeletePath = null;
            Refresh();
            StatusText.Text = $"Deleted {row.Entry.FileName}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _pendingDeletePath = null;
            StatusText.Text = $"Could not delete the screenshot: {exception.Message}";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void Refresh(string? selectPath = null)
    {
        DisposeRows();
        foreach (ScreenshotEntry entry in ScreenshotLibrary.List(_directory))
        {
            _rows.Add(new ScreenshotRow(entry));
        }

        DirectoryText.Text = _directory;
        StatusText.Text = _rows.Count switch
        {
            0 => "No screenshots yet. Press the screenshot hotkey while a game is running.",
            1 => "1 screenshot.",
            _ => $"{_rows.Count} screenshots.",
        };
        if (selectPath is not null)
        {
            foreach (ScreenshotRow row in _rows)
            {
                if (string.Equals(row.Entry.Path, selectPath, StringComparison.OrdinalIgnoreCase))
                {
                    ScreenshotList.SelectedItem = row;
                    break;
                }
            }
        }

        if (ScreenshotList.SelectedItem is null)
        {
            OpenButton.IsEnabled = false;
            CopyPathButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
        }
    }

    private void DisposeRows()
    {
        foreach (ScreenshotRow row in _rows)
        {
            row.Dispose();
        }

        _rows.Clear();
    }
}

/// <summary>One gallery row with a decoded thumbnail.</summary>
public sealed class ScreenshotRow : IDisposable
{
    /// <summary>Initializes a row and decodes its thumbnail.</summary>
    /// <param name="entry">Screenshot metadata.</param>
    public ScreenshotRow(ScreenshotEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        try
        {
            using FileStream stream = File.OpenRead(entry.Path);
            Thumbnail = Bitmap.DecodeToWidth(stream, 320);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Thumbnail = null;
        }
    }

    /// <summary>Gets the screenshot metadata.</summary>
    public ScreenshotEntry Entry { get; }

    /// <summary>Gets the decoded thumbnail, or <see langword="null"/> when the PNG is unreadable.</summary>
    public Bitmap? Thumbnail { get; }

    /// <inheritdoc />
    public void Dispose() => Thumbnail?.Dispose();
}
