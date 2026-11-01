using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>Edits the offline library metadata record for one disc.</summary>
public partial class GameMetadataWindow : Window
{
    private readonly string _gameId;
    private readonly string _discPath;
    private readonly string _metadataPath;

    /// <summary>Creates an empty editor for XAML preview and smoke tests.</summary>
    public GameMetadataWindow()
        : this(string.Empty, string.Empty, LibraryMetadataStore.DefaultPath, string.Empty)
    {
    }

    /// <summary>Creates the metadata editor.</summary>
    /// <param name="gameId">4DO game ID, or empty.</param>
    /// <param name="discPath">Disc path, or empty.</param>
    /// <param name="metadataPath">Metadata JSON path.</param>
    /// <param name="fallbackTitle">Title to show when no record exists.</param>
    public GameMetadataWindow(string gameId, string discPath, string metadataPath, string fallbackTitle)
    {
        ArgumentNullException.ThrowIfNull(metadataPath);
        InitializeComponent();
        _gameId = gameId;
        _discPath = discPath;
        _metadataPath = metadataPath;
        PathText.Text = metadataPath;
        LibraryMetadataRecord? record = LibraryMetadataCatalog.Load(metadataPath).Find(gameId, discPath);
        TitleBox.Text = record?.Title ?? fallbackTitle;
        DeveloperBox.Text = record?.Developer ?? string.Empty;
        PublisherBox.Text = record?.Publisher ?? string.Empty;
        YearBox.Text = record?.ReleaseYear ?? string.Empty;
        GenreBox.Text = record?.Genre ?? string.Empty;
        RegionsBox.Text = record?.Regions ?? string.Empty;
        PlayersBox.Text = (record?.Players ?? 0).ToString(CultureInfo.InvariantCulture);
        DescriptionBox.Text = record?.Description ?? string.Empty;
        StatusText.Text = record is null
            ? "No metadata record; saving adds one."
            : "Editing an existing metadata record.";
        SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(gameId) || !string.IsNullOrWhiteSpace(discPath);
        ClearButton.IsEnabled = record is not null;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(
                PlayersBox.Text?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var players)
            || players is < 0 or > 8)
        {
            StatusText.Text = "Players must be a number from 0 to 8.";
            return;
        }

        try
        {
            var record = new LibraryMetadataRecord
            {
                GameId = _gameId.Trim(),
                Path = _discPath.Trim(),
                Title = TitleBox.Text?.Trim() ?? string.Empty,
                Developer = DeveloperBox.Text?.Trim() ?? string.Empty,
                Publisher = PublisherBox.Text?.Trim() ?? string.Empty,
                ReleaseYear = YearBox.Text?.Trim() ?? string.Empty,
                Genre = GenreBox.Text?.Trim() ?? string.Empty,
                Regions = RegionsBox.Text?.Trim() ?? string.Empty,
                Players = players,
                Description = DescriptionBox.Text?.Trim() ?? string.Empty,
            };
            LibraryMetadataStore.Upsert(record, _metadataPath);
            Close(true);
        }
        catch (Exception exception) when (exception is ArgumentException or System.IO.IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Could not save metadata: {exception.Message}";
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            LibraryMetadataStore.Remove(_gameId, _discPath, _metadataPath);
            Close(true);
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Could not clear metadata: {exception.Message}";
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
