using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FiveDo.Host;
using FiveDo.Storage;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Lists the BIOS library and the NVRAM images owned by the host, and exposes the
/// per-game BIOS assignment plus NVRAM backup/import/reset/delete operations.
/// </summary>
/// <remarks>
/// The window edits <see cref="AppSettings"/> in place and returns <see langword="true"/>
/// from <see cref="Window.ShowDialog{T}(Window)"/> when a setting changed. The caller persists the
/// settings and recreates the machine when required, matching the settings dialog.
/// </remarks>
public partial class BiosNvramManagerWindow : Window
{
    private readonly AppSettings _settings;
    private readonly string _gameId;
    private readonly string _gameName;
    private bool _biosFolderDerived;

    private IReadOnlyList<BiosProfile> _biosProfiles = [];
    private IReadOnlyList<NvramInventoryEntry> _nvramEntries = [];
    private string? _pendingConfirmation;
    private string? _pendingImportSource;

    /// <summary>Creates a window for XAML preview and the trimmed frontend smoke test.</summary>
    public BiosNvramManagerWindow()
        : this(new AppSettings(), string.Empty, string.Empty, scanOnLoad: false)
    {
    }

    /// <summary>Creates the BIOS/NVRAM manager.</summary>
    /// <param name="settings">Settings edited in place.</param>
    /// <param name="gameId">Current 4DO game ID, or an empty string.</param>
    /// <param name="gameName">Current disc name, or an empty string.</param>
    public BiosNvramManagerWindow(AppSettings settings, string? gameId, string? gameName)
        : this(settings, gameId ?? string.Empty, gameName ?? string.Empty, scanOnLoad: true)
    {
    }

    private BiosNvramManagerWindow(
        AppSettings settings,
        string gameId,
        string gameName,
        bool scanOnLoad)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();
        _settings = settings;
        _gameId = gameId;
        _gameName = gameName;

        GameText.Text = string.IsNullOrWhiteSpace(_gameId)
            ? "No game is loaded. Open a disc to assign a BIOS to that game."
            : $"Current game: {(_gameName.Length > 0 ? _gameName : _gameId)} ({_gameId}). "
                + "BIOS assignments and NVRAM paths apply to this disc.";
        var biosFolder = settings.BiosFolder;
        if (string.IsNullOrWhiteSpace(biosFolder) && !string.IsNullOrWhiteSpace(settings.BiosPath))
        {
            try
            {
                biosFolder = Path.GetDirectoryName(Path.GetFullPath(settings.BiosPath)) ?? string.Empty;
                _biosFolderDerived = !string.IsNullOrWhiteSpace(biosFolder);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                Serilog.Log.Debug(exception, "The configured BIOS path could not be resolved in the manager.");
            }
        }

        BiosFolderBox.Text = biosFolder;
        NvramFolderBox.Text = settings.ResolveNvramDirectory();
        UpdateAssignmentButtons();

        if (scanOnLoad)
        {
            RefreshBios(selectPath: settings.BiosPath);
            RefreshNvram(selectPath: null);
        }
        else
        {
            BiosSummaryText.Text = "Choose a BIOS folder to scan.";
            NvramSummaryText.Text = "Choose the NVRAM root folder to list images.";
            FooterText.Text = "BIOS and NVRAM manager.";
        }
    }

    /// <summary>Gets a value indicating whether the window changed persisted settings.</summary>
    public bool SettingsChanged { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an NVRAM image was imported, reset, or deleted.
    /// </summary>
    public bool NvramFilesChanged { get; private set; }

    private void OnBrowseBiosFolderAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseBiosFolderAsync, "selecting the BIOS folder");
    }

    private async Task BrowseBiosFolderAsync()
    {
        var path = await PickFolderAsync("Select the BIOS folder", BiosFolderBox.Text).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        BiosFolderBox.Text = path;
        _settings.BiosFolder = path;
        _biosFolderDerived = false;
        SettingsChanged = true;
        RefreshBios(selectPath: _settings.BiosPath);
    }

    private void OnScanBiosAsync(object? sender, RoutedEventArgs e)
    {
        RefreshBios(selectPath: SelectedBios?.Path ?? _settings.BiosPath);
    }

    private void OnImportBiosAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ImportBiosAsync, "importing a BIOS image");
    }

    private async Task ImportBiosAsync()
    {
        var folder = BiosFolderBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            BiosStatusText.Text = "Choose a BIOS folder before importing an image.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import a 3DO BIOS image",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("BIOS ROM") { Patterns = ["*.bin", "*.rom"] }],
            }).ConfigureAwait(true);
        if (files.Count == 0)
        {
            return;
        }

        var source = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(source))
        {
            BiosStatusText.Text = "The selected file is not a local path.";
            return;
        }

        try
        {
            BiosImportResult result = await Task.Run(
                () => BiosInventory.Import(source, folder)).ConfigureAwait(true);
            _settings.BiosFolder = Path.GetFullPath(folder);
            _biosFolderDerived = false;
            SettingsChanged = true;
            RefreshBios(selectPath: result.Path);
            BiosStatusText.Text = result.Copied
                ? $"Imported {result.Profile.Name} as {Path.GetFileName(result.Path)}."
                : result.Note ?? "The BIOS image is already present.";
            FooterText.Text = $"BIOS library: {Path.GetFullPath(folder)}";
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            BiosStatusText.Text = $"BIOS import failed: {exception.Message}";
            Serilog.Log.Debug(exception, "BIOS import failed.");
        }
    }

    private void OnBiosSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _pendingConfirmation = null;
        BiosProfile? profile = SelectedBios;
        if (profile is null)
        {
            DetailBiosName.Text = "Select a BIOS image";
            DetailBiosKind.Text = string.Empty;
            DetailBiosRegion.Text = string.Empty;
            DetailBiosMd5.Text = string.Empty;
            DetailBiosSize.Text = string.Empty;
            DetailBiosFlags.Text = string.Empty;
            DetailBiosPath.Text = string.Empty;
            DetailBiosNotes.Text = string.Empty;
            UpdateAssignmentButtons();
            return;
        }

        DetailBiosName.Text = profile.Name;
        DetailBiosKind.Text = profile.Kind.ToString();
        DetailBiosRegion.Text = profile.RegionLabel;
        DetailBiosMd5.Text = profile.Md5;
        DetailBiosSize.Text = $"{profile.Size:N0} bytes";
        DetailBiosFlags.Text = string.Join(
            ", ",
            new[]
            {
                profile.IsKanji ? "Kanji" : null,
                profile.IsAnvil ? "Anvil" : null,
                profile.IsPatched ? "Patched/arcade" : null,
            }.Where(flag => flag is not null));
        if (string.IsNullOrEmpty(DetailBiosFlags.Text))
        {
            DetailBiosFlags.Text = "retail";
        }

        DetailBiosPath.Text = profile.Path;
        DetailBiosNotes.Text = profile.Notes;
        UpdateAssignmentButtons();
    }

    private void OnUseBios1(object? sender, RoutedEventArgs e)
    {
        if (SelectedBios is not { IsPrimary: true } profile)
        {
            BiosStatusText.Text = "Select a primary 1 MB BIOS image first.";
            return;
        }

        _settings.BiosPath = profile.Path;
        if (string.IsNullOrWhiteSpace(_settings.BiosFolder))
        {
            _settings.BiosFolder = Path.GetDirectoryName(profile.Path) ?? string.Empty;
            _biosFolderDerived = false;
        }

        SettingsChanged = true;
        BiosStatusText.Text =
            $"BIOS1 set to {profile.Name}. Restart the game to apply the new BIOS.";
        FooterText.Text = $"BIOS1: {profile.Path}";
    }

    private void OnUseBios2(object? sender, RoutedEventArgs e)
    {
        if (SelectedBios is not { IsSecondary: true } profile)
        {
            BiosStatusText.Text = "Select a BIOS2, Kanji font, or arcade support ROM first.";
            return;
        }

        _settings.Bios2Path = profile.Path;
        if (string.IsNullOrWhiteSpace(_settings.BiosFolder))
        {
            _settings.BiosFolder = Path.GetDirectoryName(profile.Path) ?? string.Empty;
            _biosFolderDerived = false;
        }

        SettingsChanged = true;
        BiosStatusText.Text =
            $"BIOS2/font set to {profile.Name}. Restart the game to apply the new ROM.";
        FooterText.Text = $"BIOS2: {profile.Path}";
    }

    private void OnAssignBios(object? sender, RoutedEventArgs e)
    {
        if (SelectedBios is not { IsPrimary: true } profile)
        {
            BiosStatusText.Text = "Select a primary 1 MB BIOS image to assign.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_gameId))
        {
            BiosStatusText.Text = "Open a disc before assigning a BIOS to a game.";
            return;
        }

        _settings.SetBiosOverride(_gameId, profile.Md5);
        SettingsChanged = true;
        BiosStatusText.Text =
            $"{profile.Name} is now pinned to {(_gameName.Length > 0 ? _gameName : _gameId)}. "
            + "Restart the game to apply it.";
        FooterText.Text = $"BIOS override: {_gameId} -> {profile.Md5[..Math.Min(8, profile.Md5.Length)]}";
    }

    private void OnClearBiosAssignment(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            BiosStatusText.Text = "Open a disc before clearing a BIOS assignment.";
            return;
        }

        _settings.SetBiosOverride(_gameId, null);
        SettingsChanged = true;
        BiosStatusText.Text =
            $"Automatic BIOS selection restored for {(_gameName.Length > 0 ? _gameName : _gameId)}. "
            + "Restart the game to apply it.";
        FooterText.Text = $"BIOS override cleared for {_gameId}.";
    }

    private void OnOpenBiosFolder(object? sender, RoutedEventArgs e)
    {
        OpenFolder(BiosFolderBox.Text?.Trim(), BiosStatusText);
    }

    private void OnBrowseNvramFolderAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BrowseNvramFolderAsync, "selecting the NVRAM folder");
    }

    private async Task BrowseNvramFolderAsync()
    {
        var path = await PickFolderAsync("Select the NVRAM root folder", NvramFolderBox.Text).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        NvramFolderBox.Text = path;
        _settings.NvramDirectory = path;
        SettingsChanged = true;
        RefreshNvram(selectPath: null);
    }

    private void OnRefreshNvram(object? sender, RoutedEventArgs e)
    {
        RefreshNvram(selectPath: SelectedNvram?.File.Path);
    }

    private void OnNvramSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _pendingConfirmation = null;
        _pendingImportSource = null;
        NvramInventoryEntry? entry = SelectedNvram;
        if (entry is null)
        {
            DetailNvramTitle.Text = "Select an NVRAM image";
            DetailNvramMode.Text = string.Empty;
            DetailNvramVersion.Text = string.Empty;
            DetailNvramGame.Text = string.Empty;
            DetailNvramSize.Text = string.Empty;
            DetailNvramModified.Text = string.Empty;
            DetailNvramState.Text = string.Empty;
            DetailNvramPath.Text = string.Empty;
            UpdateNvramButtons();
            return;
        }

        DetailNvramTitle.Text = entry.GameLabel;
        DetailNvramMode.Text = entry.ModeLabel;
        DetailNvramVersion.Text = entry.File.Version.ToString(CultureInfo.InvariantCulture);
        DetailNvramGame.Text = string.IsNullOrEmpty(entry.File.GameId)
            ? "Shared image"
            : $"{entry.GameLabel} ({entry.File.GameId})";
        DetailNvramSize.Text = entry.SizeLabel;
        DetailNvramModified.Text = entry.ModifiedLabel;
        DetailNvramState.Text = entry.StateLabel;
        DetailNvramPath.Text = entry.File.Path;
        UpdateNvramButtons();
    }

    private void OnBackupNvramAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BackupNvramAsync, "backing up an NVRAM image");
    }

    private async Task BackupNvramAsync()
    {
        NvramInventoryEntry? entry = SelectedNvram;
        if (entry is null)
        {
            NvramStatusText.Text = "Select an NVRAM image to back up.";
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Back up NVRAM",
                SuggestedFileName = NvramInventory.SuggestBackupFileName(entry.File, DateTimeOffset.Now),
                DefaultExtension = "ram",
                FileTypeChoices = [new FilePickerFileType("NVRAM image") { Patterns = ["*.ram"] }],
            }).ConfigureAwait(true);
        var destination = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        try
        {
            var path = await Task.Run(
                () => NvramInventory.Backup(entry.File.Path, destination)).ConfigureAwait(true);
            NvramStatusText.Text = $"Backed up {entry.GameLabel} to {path}.";
            FooterText.Text = $"NVRAM backup: {path}";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            NvramStatusText.Text = $"NVRAM backup failed: {exception.Message}";
            Serilog.Log.Debug(exception, "NVRAM backup failed.");
        }
    }

    private void OnImportNvramAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ImportNvramAsync, "importing an NVRAM image");
    }

    private async Task ImportNvramAsync()
    {
        NvramInventoryEntry? entry = SelectedNvram;
        if (entry is null)
        {
            NvramStatusText.Text = "Select the NVRAM image to replace.";
            return;
        }

        if (string.Equals(
                _pendingConfirmation,
                ConfirmationKey("nvram-import", entry.File.Path),
                StringComparison.Ordinal)
            && _pendingImportSource is { } pendingSource)
        {
            _pendingConfirmation = null;
            _pendingImportSource = null;
            try
            {
                var path = await Task.Run(
                    () => NvramInventory.Import(pendingSource, entry.File.Path)).ConfigureAwait(true);
                NvramFilesChanged = true;
                RefreshNvram(selectPath: path);
                NvramStatusText.Text = $"Imported {Path.GetFileName(pendingSource)} into {entry.GameLabel}.";
                FooterText.Text = $"NVRAM import: {path}";
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                NvramStatusText.Text = $"NVRAM import failed: {exception.Message}";
                Serilog.Log.Debug(exception, "NVRAM import failed.");
            }

            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import an NVRAM image",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("NVRAM image") { Patterns = ["*.ram"] }],
            }).ConfigureAwait(true);
        if (files.Count == 0)
        {
            return;
        }

        var source = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(source))
        {
            NvramStatusText.Text = "The selected file is not a local path.";
            return;
        }

        _pendingConfirmation = ConfirmationKey("nvram-import", entry.File.Path);
        _pendingImportSource = source;
        NvramStatusText.Text =
            $"Import {Path.GetFileName(source)} over {entry.GameLabel}? "
            + "Click Import Into Selected again to confirm.";
    }

    private void OnResetNvram(object? sender, RoutedEventArgs e)
    {
        NvramInventoryEntry? entry = SelectedNvram;
        if (entry is null)
        {
            NvramStatusText.Text = "Select an NVRAM image to reset.";
            return;
        }

        var key = ConfirmationKey("nvram-reset", entry.File.Path);
        if (!Confirm(key, $"Reset {entry.GameLabel} to a fresh volume?", NvramStatusText))
        {
            return;
        }

        try
        {
            var path = NvramInventory.Reset(entry.File.Path);
            NvramFilesChanged = true;
            RefreshNvram(selectPath: path);
            NvramStatusText.Text = $"Reset {entry.GameLabel} to a fresh NVRAM volume.";
            FooterText.Text = $"NVRAM reset: {path}";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            NvramStatusText.Text = $"NVRAM reset failed: {exception.Message}";
            Serilog.Log.Debug(exception, "NVRAM reset failed.");
        }
    }

    private void OnDeleteNvram(object? sender, RoutedEventArgs e)
    {
        NvramInventoryEntry? entry = SelectedNvram;
        if (entry is null)
        {
            NvramStatusText.Text = "Select an NVRAM image to delete.";
            return;
        }

        var key = ConfirmationKey("nvram-delete", entry.File.Path);
        if (!Confirm(key, $"Delete {entry.GameLabel} ({entry.File.Path})?", NvramStatusText))
        {
            return;
        }

        try
        {
            var deleted = NvramInventory.Delete(entry.File.Path);
            if (deleted)
            {
                NvramFilesChanged = true;
            }

            RefreshNvram(selectPath: null);
            NvramStatusText.Text = deleted
                ? $"Deleted {entry.GameLabel}."
                : "The selected image no longer exists.";
            FooterText.Text = deleted ? $"NVRAM deleted: {entry.File.Path}" : "NVRAM delete skipped.";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            NvramStatusText.Text = $"NVRAM delete failed: {exception.Message}";
            Serilog.Log.Debug(exception, "NVRAM delete failed.");
        }
    }

    private void OnBackupAllNvramAsync(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(BackupAllNvramAsync, "backing up all NVRAM images");
    }

    private async Task BackupAllNvramAsync()
    {
        var root = NvramFolderBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            NvramStatusText.Text = "Choose an existing NVRAM folder first.";
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select the NVRAM backup folder",
                AllowMultiple = false,
            }).ConfigureAwait(true);
        if (folders.Count == 0)
        {
            return;
        }

        var destination = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destination))
        {
            NvramStatusText.Text = "The selected folder is not a local path.";
            return;
        }

        var target = Path.Combine(destination, NvramInventory.SuggestBackupFolderName(DateTimeOffset.Now));
        try
        {
            var count = await Task.Run(
                () => NvramInventory.BackupAll(root, target)).ConfigureAwait(true);
            NvramStatusText.Text = count == 0
                ? "No recognized NVRAM images were found."
                : $"Backed up {count} NVRAM image(s) to {target}.";
            FooterText.Text = $"NVRAM backup: {target}";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            NvramStatusText.Text = $"NVRAM backup failed: {exception.Message}";
            Serilog.Log.Debug(exception, "NVRAM backup-all failed.");
        }
    }

    private void OnOpenNvramFolder(object? sender, RoutedEventArgs e)
    {
        OpenFolder(NvramFolderBox.Text?.Trim(), NvramStatusText);
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close(SettingsChanged || NvramFilesChanged);
    }

    private BiosProfile? SelectedBios => BiosList.SelectedItem as BiosProfile;

    private NvramInventoryEntry? SelectedNvram => NvramList.SelectedItem as NvramInventoryEntry;

    private void RefreshBios(string? selectPath)
    {
        var folder = BiosFolderBox.Text?.Trim();
        _biosProfiles = [];
        BiosList.ItemsSource = null;
        _pendingConfirmation = null;
        if (string.IsNullOrWhiteSpace(folder))
        {
            BiosSummaryText.Text = "Choose a BIOS folder to scan.";
            BiosStatusText.Text = "No BIOS folder is configured.";
            UpdateAssignmentButtons();
            return;
        }

        if (!Directory.Exists(folder))
        {
            BiosSummaryText.Text = $"The BIOS folder '{folder}' does not exist.";
            BiosStatusText.Text = "Choose an existing folder or import an image into a new one.";
            UpdateAssignmentButtons();
            return;
        }

        try
        {
            BiosLibrary library = BiosInventory.Scan(folder);
            _biosProfiles = library.Profiles;
            BiosList.ItemsSource = _biosProfiles;
            BiosSummaryText.Text =
                $"{library.Primaries.Count} primary, {library.Secondaries.Count} secondary, "
                + $"{library.Errors.Count} error(s). Scanned {Path.GetFullPath(folder)}.";
            BiosStatusText.Text = library.Errors.Count == 0
                ? "Select an image to use, import, or assign."
                : string.Join("  ", library.Errors);
            var normalizedFolder = Path.GetFullPath(folder);
            if (!_biosFolderDerived
                && !string.Equals(_settings.BiosFolder, normalizedFolder, StringComparison.OrdinalIgnoreCase))
            {
                _settings.BiosFolder = normalizedFolder;
                SettingsChanged = true;
            }
            BiosProfile? selected = _biosProfiles.FirstOrDefault(
                    profile => string.Equals(profile.Path, selectPath, StringComparison.OrdinalIgnoreCase))
                ?? _biosProfiles.FirstOrDefault(profile => string.Equals(
                    profile.Path,
                    _settings.BiosPath,
                    StringComparison.OrdinalIgnoreCase))
                ?? (_biosProfiles.Count > 0 ? _biosProfiles[0] : null);
            BiosList.SelectedItem = selected;
            FooterText.Text = $"BIOS library: {Path.GetFullPath(folder)}";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            BiosSummaryText.Text = $"BIOS scan failed: {exception.Message}";
            BiosStatusText.Text = exception.Message;
            Serilog.Log.Debug(exception, "BIOS folder scan failed.");
        }

        UpdateAssignmentButtons();
    }

    private void RefreshNvram(string? selectPath)
    {
        var root = NvramFolderBox.Text?.Trim();
        _nvramEntries = [];
        NvramList.ItemsSource = null;
        _pendingConfirmation = null;
        _pendingImportSource = null;
        if (string.IsNullOrWhiteSpace(root))
        {
            NvramSummaryText.Text = "Choose the NVRAM root folder to list images.";
            UpdateNvramButtons();
            return;
        }

        try
        {
            _nvramEntries = NvramInventory.Enumerate(root);
            NvramList.ItemsSource = _nvramEntries;
            var invalid = _nvramEntries.Count(entry => !entry.IsValid);
            NvramSummaryText.Text = _nvramEntries.Count == 0
                ? $"No recognized NVRAM images in {Path.GetFullPath(root)}."
                : $"{_nvramEntries.Count} image(s), {_nvramEntries.Count - invalid} valid"
                    + (invalid == 0 ? "." : $", {invalid} invalid.");
            NvramInventoryEntry? selected = _nvramEntries.FirstOrDefault(
                    entry => string.Equals(entry.File.Path, selectPath, StringComparison.OrdinalIgnoreCase))
                ?? (_nvramEntries.Count > 0 ? _nvramEntries[0] : null);
            NvramList.SelectedItem = selected;
            FooterText.Text = $"NVRAM library: {Path.GetFullPath(root)}";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            NvramSummaryText.Text = $"NVRAM scan failed: {exception.Message}";
            NvramStatusText.Text = exception.Message;
            Serilog.Log.Debug(exception, "NVRAM folder scan failed.");
        }

        UpdateNvramButtons();
    }

    private void UpdateAssignmentButtons()
    {
        BiosProfile? profile = SelectedBios;
        UseBios1Button.IsEnabled = profile?.IsPrimary == true;
        UseBios2Button.IsEnabled = profile?.IsSecondary == true;
        AssignBiosButton.IsEnabled = profile?.IsPrimary == true && _gameId.Length > 0;
        ClearBiosAssignmentButton.IsEnabled = _gameId.Length > 0;
        OpenBiosFolderButton.IsEnabled = !string.IsNullOrWhiteSpace(BiosFolderBox.Text);
    }

    private void UpdateNvramButtons()
    {
        var hasSelection = SelectedNvram is not null;
        BackupNvramButton.IsEnabled = hasSelection;
        ImportNvramButton.IsEnabled = hasSelection;
        ResetNvramButton.IsEnabled = hasSelection;
        DeleteNvramButton.IsEnabled = hasSelection;
        OpenNvramFolderButton.IsEnabled = !string.IsNullOrWhiteSpace(NvramFolderBox.Text);
    }

    private bool Confirm(string key, string message, TextBlock status)
    {
        if (string.Equals(_pendingConfirmation, key, StringComparison.Ordinal))
        {
            _pendingConfirmation = null;
            return true;
        }

        _pendingConfirmation = key;
        status.Text = message + " Click the same button again to confirm.";
        return false;
    }

    private static string ConfirmationKey(string action, string path)
    {
        return $"{action}:{Path.GetFullPath(path)}";
    }

    private async Task<string?> PickFolderAsync(string title, string? current)
    {
        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            try
            {
                start = await StorageProvider.TryGetFolderFromPathAsync(current).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                Serilog.Log.Debug(exception, "The current folder could not be used as a picker start location.");
            }
        }

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                SuggestedStartLocation = start,
            }).ConfigureAwait(true);
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private static void OpenFolder(string? folder, TextBlock status)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            status.Text = "The folder does not exist.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            status.Text = $"The folder could not be opened: {exception.Message}";
            Serilog.Log.Debug(exception, "Opening the BIOS/NVRAM folder failed.");
        }
    }
}
