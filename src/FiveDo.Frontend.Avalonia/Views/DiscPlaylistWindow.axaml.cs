using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Edits the multi-disc playlist, including import, reordering, and extended-M3U save.
/// </summary>
public partial class DiscPlaylistWindow : Window
{
    private readonly ObservableCollection<DiscPlaylistEntry> _rows = [];
    private readonly DiscPlaylist _playlist;

    /// <summary>Creates an empty window for XAML preview and smoke tests.</summary>
    public DiscPlaylistWindow()
        : this([], sourcePath: null)
    {
    }

    /// <summary>Creates the playlist editor.</summary>
    /// <param name="entries">Initial entries.</param>
    /// <param name="sourcePath">Existing playlist path, or <see langword="null"/> for a new list.</param>
    public DiscPlaylistWindow(System.Collections.Generic.IEnumerable<DiscPlaylistEntry> entries, string? sourcePath)
    {
        ArgumentNullException.ThrowIfNull(entries);
        InitializeComponent();
        _playlist = DiscPlaylist.Create(entries, sourcePath);
        EntryList.ItemsSource = _rows;
        Refresh();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateButtons();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(AddDiscsAsync, "adding playlist discs");
    }

    private async Task AddDiscsAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add discs to the playlist",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("3DO discs and playlists")
                {
                    Patterns = ["*.iso", "*.bin", "*.cue", "*.chd", "*.m3u", "*.m3u8"],
                },
            ],
        }).ConfigureAwait(true);
        if (files.Count == 0)
        {
            return;
        }

        var added = 0;
        foreach (IStorageFile file in files)
        {
            var path = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (!IsPlaylistPath(path))
            {
                _playlist.Add(path);
                added++;
                continue;
            }

            try
            {
                DiscPlaylist imported = DiscPlaylist.Load(path);
                foreach (DiscPlaylistEntry entry in imported.Entries)
                {
                    _playlist.Add(entry.Path, entry.Title);
                    added++;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                StatusText.Text = $"Could not import {Path.GetFileName(path)}: {exception.Message}";
            }
        }

        Refresh(_playlist.Count - 1);
        StatusText.Text = added == 1 ? "Added 1 disc." : $"Added {added} discs.";
    }

    private void OnMoveUpClick(object? sender, RoutedEventArgs e)
    {
        int index = EntryList.SelectedIndex;
        if (_playlist.MoveEntryUp(index))
        {
            Refresh(index - 1);
        }
    }

    private void OnMoveDownClick(object? sender, RoutedEventArgs e)
    {
        int index = EntryList.SelectedIndex;
        if (_playlist.MoveEntryDown(index))
        {
            Refresh(index + 1);
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        int index = EntryList.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        _playlist.RemoveAt(index);
        Refresh(Math.Min(index, _playlist.Count - 1));
        StatusText.Text = _playlist.Count == 0
            ? "Add at least one disc before saving."
            : $"Removed 1 entry. {_playlist.Count} remaining.";
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(SaveAsync, "saving the playlist");
    }

    private async Task SaveAsync()
    {
        if (_playlist.Count == 0)
        {
            StatusText.Text = "Add at least one disc before saving.";
            return;
        }

        var path = _playlist.SourcePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save disc playlist",
                SuggestedFileName = "game.m3u8",
                DefaultExtension = "m3u8",
                FileTypeChoices =
                [
                    new FilePickerFileType("M3U playlist") { Patterns = ["*.m3u", "*.m3u8"] },
                ],
            }).ConfigureAwait(true);
            path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
        }

        try
        {
            string written = _playlist.Save(path);
            Close(written);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            StatusText.Text = $"Save failed: {exception.Message}";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Refresh(int? selection = null)
    {
        int target = selection ?? EntryList.SelectedIndex;
        _rows.Clear();
        foreach (DiscPlaylistEntry entry in _playlist.Entries)
        {
            _rows.Add(entry);
        }

        SourceText.Text = string.IsNullOrWhiteSpace(_playlist.SourcePath)
            ? "Unsaved playlist"
            : _playlist.SourcePath;
        if (_rows.Count == 0)
        {
            EntryList.SelectedIndex = -1;
        }
        else
        {
            EntryList.SelectedIndex = Math.Clamp(target, 0, _rows.Count - 1);
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        int index = EntryList.SelectedIndex;
        bool hasSelection = index >= 0;
        MoveUpButton.IsEnabled = index > 0;
        MoveDownButton.IsEnabled = hasSelection && index < _rows.Count - 1;
        RemoveButton.IsEnabled = hasSelection;
        SaveButton.IsEnabled = _rows.Count > 0;
        if (StatusText.Text is null or "")
        {
            StatusText.Text = _rows.Count == 1 ? "1 disc" : $"{_rows.Count} discs";
        }
    }

    private static bool IsPlaylistPath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".m3u", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".m3u8", StringComparison.OrdinalIgnoreCase);
    }
}
