using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ThreeDo.Host;

namespace ThreeDo.Frontend.Avalonia.Views;

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
