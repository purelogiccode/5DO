using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Shows the volume, game-database, and compatibility metadata for a disc image.
/// </summary>
public partial class DiscInfoWindow : Window
{
    /// <summary>Creates an empty window for XAML preview and smoke tests.</summary>
    public DiscInfoWindow()
        : this(new DiscInfo(
            string.Empty,
            "Disc",
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            0,
            null,
            null,
            null,
            null,
            FixMode.None))
    {
    }

    /// <summary>Creates the information dialog.</summary>
    /// <param name="info">Disc metadata.</param>
    public DiscInfoWindow(DiscInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        InitializeComponent();
        SourceText.Text = string.IsNullOrWhiteSpace(info.SourcePath) ? "(none)" : info.SourcePath;
        FormatText.Text = info.Format;
        LabelText.Text = string.IsNullOrWhiteSpace(info.Label) ? "(none)" : info.Label;
        CommentText.Text = string.IsNullOrWhiteSpace(info.Comment) ? "(none)" : info.Comment;
        GameIdText.Text = string.IsNullOrWhiteSpace(info.GameId) ? "(unknown)" : info.GameId;
        GameNameText.Text = string.IsNullOrWhiteSpace(info.GameName) ? "(not in the 4DO database)" : info.GameName;
        RegionText.Text = string.IsNullOrWhiteSpace(info.Regions) ? "(unknown)" : info.Regions;
        PublisherText.Text = string.IsNullOrWhiteSpace(info.Publisher) ? "(unknown)" : info.Publisher;
        ReleaseYearText.Text = string.IsNullOrWhiteSpace(info.ReleaseYear) ? "(unknown)" : info.ReleaseYear;
        ContentsText.Text =
            $"{info.FileCount.ToString("N0", CultureInfo.InvariantCulture)} files, "
            + $"{info.DirectoryCount.ToString("N0", CultureInfo.InvariantCulture)} directories, "
            + DiscBrowserNode.FormatBytes((ulong)info.TotalFileBytes);
        SectorsText.Text = info.SectorCount.ToString("N0", CultureInfo.InvariantCulture);
        FixesText.Text = info.Fixes == FixMode.None
            ? "None"
            : $"0x{(uint)info.Fixes:x8}";
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
