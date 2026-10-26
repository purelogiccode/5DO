using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Browses configured folders with the 4DO game database and returns the selected disc path.
/// </summary>
public partial class GameLibraryWindow : Window
{
    private readonly AppSettings _settings;

    /// <summary>Creates the window with the persisted settings.</summary>
    public GameLibraryWindow()
        : this(AppSettingsStore.Load())
    {
    }

    /// <summary>Creates the window with explicit settings.</summary>
    /// <param name="settings">Settings that supply the configured library folders.</param>
    public GameLibraryWindow(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();
        _settings = settings;
        Refresh();
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => Refresh();

    private async void OnAddFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add a game folder",
            AllowMultiple = true,
        });

        var added = false;
        foreach (var folder in folders)
        {
            var path = folder.TryGetLocalPath();
            if (path is null || _settings.LibraryFolders.Exists(existing =>
                    string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _settings.LibraryFolders.Add(path);
            added = true;
        }

        if (added)
        {
            AppSettingsStore.Save(_settings);
            Refresh();
        }
    }

    private void OnOpenSelected(object? sender, RoutedEventArgs e) => OpenSelected();

    private void OnDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateCover();

    private void UpdateCover()
    {
        if (LibraryList.SelectedItem is not GameLibraryEntry entry)
        {
            CoverImage.Source = null;
            CoverText.Text = "Select a disc to see its cover.";
            return;
        }

        if (string.IsNullOrEmpty(entry.CoverPath))
        {
            CoverImage.Source = null;
            CoverText.Text = "No cover art. Add a PNG/JPG with the same name next to the disc.";
            return;
        }

        try
        {
            CoverImage.Source = new Bitmap(entry.CoverPath);
            CoverText.Text = Path.GetFileName(entry.CoverPath);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            CoverImage.Source = null;
            CoverText.Text = $"Cover art could not be loaded: {exception.Message}";
        }
    }

    private void OpenSelected()
    {
        if (LibraryList.SelectedItem is GameLibraryEntry entry)
        {
            Close(entry.Path);
        }
        else
        {
            StatusText.Text = "Select a disc first.";
        }
    }

    private void Refresh()
    {
        var folders = _settings.LibraryFolders.Count > 0
            ? _settings.LibraryFolders
            : RecentFolders();
        IReadOnlyList<GameLibraryEntry> entries = folders.Count == 0
            ? []
            : GameLibraryScanner.Scan(folders);
        LibraryList.ItemsSource = entries;
        UpdateCover();
        if (entries.Count == 0)
        {
            StatusText.Text = folders.Count == 0
                ? "Add a folder that contains ISO, BIN, CUE, or CHD images."
                : "No supported disc images were found in the configured folders.";
            return;
        }

        var known = 0;
        foreach (var entry in entries)
        {
            if (entry.Known)
            {
                known++;
            }
        }

        StatusText.Text = $"{entries.Count} disc(s), {known} recognized by the 4DO database.";
    }

    private List<string> RecentFolders()
    {
        var folders = new List<string>();
        foreach (var disc in _settings.RecentDiscs)
        {
            var folder = Path.GetDirectoryName(disc);
            if (folder is not null && !folders.Exists(existing =>
                    string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase)))
            {
                folders.Add(folder);
            }
        }

        return folders;
    }
}
