using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Browses screenshots in the configured directory with filters, thumbnails, compare,
/// export, and file actions.
/// </summary>
public partial class ScreenshotGalleryWindow : Window
{
    private static readonly DateFilterOption[] DateFilters =
    [
        new("All dates", null),
        new("Today", 0),
        new("Last 7 days", 7),
        new("Last 30 days", 30),
    ];

    private readonly ObservableCollection<ScreenshotRow> _rows = [];
    private readonly string _directory;
    private readonly DispatcherTimer _filterTimer;
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
        DateFilterCombo.ItemsSource = DateFilters;
        DateFilterCombo.SelectedIndex = 0;
        GameFilterBox.TextChanged += OnGameFilterChanged;
        DateFilterCombo.SelectionChanged += OnDateFilterChanged;
        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            Refresh(ScreenshotList.SelectedItem is ScreenshotRow row ? row.Entry.Path : null);
        };
        Closed += (_, _) =>
        {
            _filterTimer.Stop();
            DisposeRows();
        };
        Refresh();
    }

    private void OnGameFilterChanged(object? sender, TextChangedEventArgs e)
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void OnDateFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DateFilterCombo.SelectedItem is DateFilterOption)
        {
            Refresh();
        }
    }

    private void OnClearFiltersClick(object? sender, RoutedEventArgs e)
    {
        GameFilterBox.Text = string.Empty;
        DateFilterCombo.SelectedIndex = 0;
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
        UpdateSelectionButtons();
        ScreenshotRow[] selected = SelectedRows();
        if (selected.Length == 1)
        {
            var metadata = ScreenshotMetadata.Load(selected[0].Entry.Path);
            StatusText.Text = metadata is null
                ? "Selected. The first Delete click asks for confirmation."
                : $"Selected. {metadata.Summary()}";
        }
        else if (selected.Length > 1)
        {
            StatusText.Text = $"{selected.Length} selected. Select exactly two to compare.";
        }
    }

    private void OnDoubleTapped(object? sender, RoutedEventArgs e) => OpenSelected();

    private void OnOpenClick(object? sender, RoutedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (SelectedRows().FirstOrDefault() is not { } row)
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
            Serilog.Log.Debug(exception, "Screenshot {Path} could not be opened.", row.Entry.Path);
            StatusText.Text = $"Could not open the screenshot: {exception.Message}";
        }
    }

    private void OnCopyPathClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedRows().FirstOrDefault() is not { } row)
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

    private async void OnExportClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (SelectedRows().FirstOrDefault() is not { } row)
            {
                return;
            }

            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Export screenshot",
                    SuggestedFileName = row.Entry.FileName,
                    DefaultExtension = "png",
                    FileTypeChoices =
                    [
                        new FilePickerFileType("PNG image") { Patterns = ["*.png"] },
                    ],
                }).ConfigureAwait(true);
            var destination = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(destination))
            {
                return;
            }

            var exported = ScreenshotLibrary.Export(row.Entry.Path, destination);
            StatusText.Text = $"Exported to {exported}.";
        }
        catch (Exception exception)
        {
            // An exception escaping an async void handler terminates the process, so
            // every failure (including picker and platform errors) must be contained.
            Serilog.Log.Error(exception, "Screenshot export failed.");
            StatusText.Text = $"Could not export the screenshot: {exception.Message}";
        }
    }

    private async void OnCompareClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            ScreenshotRow[] selected = SelectedRows();
            if (selected.Length != 2)
            {
                return;
            }

            var window = new ScreenshotCompareWindow(selected[0].Entry, selected[1].Entry);
            await window.ShowDialog(this).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            // An exception escaping an async void handler terminates the process, so
            // every failure (including window lifetime errors) must be contained.
            Serilog.Log.Error(exception, "Screenshot comparison failed.");
            StatusText.Text = $"Could not compare the screenshots: {exception.Message}";
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedRows().FirstOrDefault() is not { } row)
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
            Serilog.Log.Debug(exception, "Screenshot {Path} could not be deleted.", row.Entry.Path);
            _pendingDeletePath = null;
            StatusText.Text = $"Could not delete the screenshot: {exception.Message}";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void Refresh(string? selectPath = null)
    {
        DisposeRows();
        var option = DateFilterCombo.SelectedItem as DateFilterOption ?? DateFilters[0];
        DateTimeOffset? from = option.Days switch
        {
            null => null,
            0 => new DateTimeOffset(DateTime.Today),
            var days => DateTimeOffset.Now.AddDays(-days.Value),
        };

        foreach (ScreenshotEntry entry in ScreenshotLibrary.List(
                     _directory,
                     GameFilterBox.Text,
                     from,
                     to: null))
        {
            _rows.Add(new ScreenshotRow(entry));
        }

        DirectoryText.Text = _directory;
        StatusText.Text = _rows.Count switch
        {
            0 => "No screenshots match the current filters.",
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

        UpdateSelectionButtons();
    }

    private void UpdateSelectionButtons()
    {
        ScreenshotRow[] selected = SelectedRows();
        var any = selected.Length > 0;
        OpenButton.IsEnabled = any;
        CopyPathButton.IsEnabled = any;
        ExportButton.IsEnabled = any;
        DeleteButton.IsEnabled = any;
        CompareButton.IsEnabled = selected.Length == 2;
    }

    private ScreenshotRow[] SelectedRows()
    {
        return ScreenshotList.SelectedItems?.OfType<ScreenshotRow>().ToArray() ?? [];
    }

    private void DisposeRows()
    {
        foreach (ScreenshotRow row in _rows)
        {
            row.Dispose();
        }

        _rows.Clear();
    }

    private sealed record DateFilterOption(string Label, int? Days)
    {
        public override string ToString() => Label;
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
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "Screenshot thumbnail {Path} could not be decoded.", entry.Path);
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
