using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly LibraryState _libraryState;
    private LibraryMetadataCatalog _libraryMetadata;
    private CancellationTokenSource? _scanCts;
    private int _scanGeneration;

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
        _libraryState = LibraryStateStore.Load();
        _libraryMetadata = LibraryMetadataCatalog.Load(_settings.ResolveLibraryMetadataPath());
        Closed += (_, _) =>
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
            _scanCts = null;
        };
        AsyncEvent.Run(RefreshAsync, "loading the game library");
    }

    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(RefreshAsync, "refreshing the game library");
    }

    private void OnAddFolderAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(AddFolderAsync, "adding a game folder");
    }

    private async Task AddFolderAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add a game folder",
            AllowMultiple = true,
        }).ConfigureAwait(true);

        var added = false;
        foreach (IStorageFolder folder in folders)
        {
            var path = folder.TryGetLocalPath();
            if (path is null || _settings.LibraryFolders.Any(existing =>
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
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    private void OnOpenSelected(object? sender, RoutedEventArgs e)
    {
        OpenSelected();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        OpenSelected();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateCover();
    }

    private void OnToggleFavorite(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path })
        {
            return;
        }

        GameLibraryEntry? entry = LibraryList.ItemsSource?
            .OfType<GameLibraryEntry>()
            .FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return;
        }

        var favorite = _libraryState.ToggleFavorite(entry.GameId, entry.Path);
        LibraryStateStore.Save(_libraryState);
        var selectedPath = (LibraryList.SelectedItem as GameLibraryEntry)?.Path;
        var decorated = LibraryList.ItemsSource?
            .OfType<GameLibraryEntry>()
            .Select(DecorateEntry)
            .ToList() ?? [];
        LibraryList.ItemsSource = decorated;
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            LibraryList.SelectedItem = decorated.FirstOrDefault(candidate =>
                string.Equals(candidate.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
        }

        UpdateCover();
        StatusText.Text = favorite
            ? $"{entry.Name} added to favorites."
            : $"{entry.Name} removed from favorites.";
        e.Handled = true;
    }

    private GameLibraryEntry DecorateEntry(GameLibraryEntry entry)
    {
        return _libraryMetadata.Apply(_libraryState.Apply(entry));
    }

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
            Serilog.Log.Debug(exception, "Cover art could not be loaded for {Path}.", entry.CoverPath);
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

    private async Task RefreshAsync()
    {
        _libraryMetadata = LibraryMetadataCatalog.Load(_settings.ResolveLibraryMetadataPath());
        var generation = ++_scanGeneration;
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        var scanCts = new CancellationTokenSource();
        _scanCts = scanCts;
        IList<string> folders = _settings.LibraryFolders.Count > 0
            ? _settings.LibraryFolders
            : RecentFolders();
        var coverDirectory = _settings.CoverDirectory;
        StatusText.Text = folders.Count == 0
            ? "Add a folder that contains ISO, BIN, CUE, or CHD images."
            : "Scanning game folders...";

        var scanToken = scanCts.Token;
        IReadOnlyList<GameLibraryEntry> finalEntries;
        try
        {
            GameLibraryScanResult initial = folders.Count == 0
                ? new GameLibraryScanResult([], [])
                : await Task.Run(
                    () => GameLibraryScanner.BeginScan(
                        folders,
                        cachePath: GameLibraryScanner.DefaultCachePath,
                        cancellationToken: scanToken,
                        coverDirectory: coverDirectory),
                    scanToken).ConfigureAwait(true);
            if (generation != _scanGeneration)
            {
                return;
            }

            LibraryList.ItemsSource = initial.Entries.Select(DecorateEntry).ToList();
            UpdateCover();
            finalEntries = initial.Entries;
            if (initial.PendingPaths.Count > 0)
            {
                var pendingCount = initial.PendingPaths.Count;
                StatusText.Text = $"{CountLabel(initial.Entries.Count)}; scanning metadata... 0 of {pendingCount}";
                var metadataProgress = new Progress<int>(count =>
                {
                    if (generation == _scanGeneration && _scanCts is not null)
                    {
                        StatusText.Text =
                            $"{CountLabel(initial.Entries.Count)}; scanning metadata... {count} of {pendingCount}";
                    }
                });
                var selectedPath = (LibraryList.SelectedItem as GameLibraryEntry)?.Path;
                IReadOnlyList<GameLibraryEntry> enriched = await Task.Run(
                    () => GameLibraryScanner.InspectPending(
                        initial.PendingPaths,
                        cachePath: GameLibraryScanner.DefaultCachePath,
                        progress: metadataProgress,
                        coverDirectory: coverDirectory,
                        cancellationToken: scanToken),
                    scanToken).ConfigureAwait(true);
                if (generation != _scanGeneration)
                {
                    return;
                }

                finalEntries = initial.Entries
                    .Where(entry => !entry.Pending)
                    .Concat(enriched)
                    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                List<GameLibraryEntry> decorated = finalEntries.Select(DecorateEntry).ToList();
                LibraryList.ItemsSource = decorated;
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    LibraryList.SelectedItem = decorated.FirstOrDefault(entry =>
                        string.Equals(entry.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
                }

                UpdateCover();
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = $"Library scan failed: {exception.Message}";
            Serilog.Log.Debug(exception, "Library scan failed.");
            return;
        }
        finally
        {
            if (ReferenceEquals(_scanCts, scanCts))
            {
                _scanCts = null;
            }

            scanCts.Dispose();
        }

        if (generation != _scanGeneration)
        {
            return;
        }

        if (finalEntries.Count == 0)
        {
            StatusText.Text = folders.Count == 0
                ? "Add a folder that contains ISO, BIN, CUE, or CHD images."
                : "No supported disc images were found in the configured folders.";
            return;
        }

        var known = 0;
        foreach (GameLibraryEntry entry in finalEntries)
        {
            if (entry.Known)
            {
                known++;
            }
        }

        StatusText.Text = $"{finalEntries.Count} disc(s), {known} recognized by the 4DO database.";
    }

    private static string CountLabel(int count)
    {
        return count == 1 ? "1 game" : $"{count} games";
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
