using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FiveDo.Cheats;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Lists and edits the per-game cheat document. Changes are saved atomically as they are
/// made, and the owner is notified so the running machine can pick up the new set.
/// </summary>
public partial class CheatsWindow : Window
{
    private readonly string _gameId;
    private readonly string _path;
    private readonly CheatSet _set;
    private readonly Action? _changed;

    /// <summary>Creates an empty window for XAML preview and smoke tests.</summary>
    public CheatsWindow()
        : this(
            string.Empty,
            "No game",
            CheatStore.ResolvePath(CheatStore.DefaultDirectory, null),
            new CheatDocument(),
            null)
    {
    }

    /// <summary>Creates the cheat manager.</summary>
    /// <param name="gameId">4DO game ID, or an empty string for the shared file.</param>
    /// <param name="gameName">Game name shown in the header.</param>
    /// <param name="path">Cheat document path.</param>
    /// <param name="document">Loaded document.</param>
    /// <param name="changed">Optional callback invoked after a successful save.</param>
    public CheatsWindow(
        string gameId,
        string gameName,
        string path,
        CheatDocument document,
        Action? changed)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(document);
        InitializeComponent();
        _gameId = gameId;
        _path = path;
        _set = CheatSet.FromDocument(document);
        _changed = changed;
        HeaderText.Text = string.IsNullOrWhiteSpace(gameName)
            ? "Cheats"
            : $"Cheats - {gameName}";
        PathText.Text = path;
        RefreshList();
        RefreshStatus();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CheatList.SelectedItem is not CheatEntry entry)
        {
            return;
        }

        NameBox.Text = entry.Name;
        DescriptionBox.Text = entry.Description;
        CodeBox.Text = entry.Code;
        EnabledCheck.IsChecked = entry.Enabled;
    }

    private void OnCheatToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: CheatEntry entry } box)
        {
            return;
        }

        entry.Enabled = box.IsChecked == true;
        if (Save())
        {
            RefreshStatus();
        }
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        try
        {
            CheatEntry entry = _set.Add(
                NameBox.Text?.Trim() ?? string.Empty,
                DescriptionBox.Text?.Trim() ?? string.Empty,
                CodeBox.Text?.Trim() ?? string.Empty,
                EnabledCheck.IsChecked == true);
            if (Save())
            {
                RefreshList(entry.Name);
                RefreshStatus();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            Serilog.Log.Debug(exception, "Cheat add validation failed.");
            StatusText.Text = exception.Message;
        }
    }

    private void OnUpdate(object? sender, RoutedEventArgs e)
    {
        if (CheatList.SelectedItem is not CheatEntry entry)
        {
            StatusText.Text = "Select a cheat to update.";
            return;
        }

        try
        {
            var name = NameBox.Text?.Trim() ?? string.Empty;
            if (!_set.Update(
                    entry.Name,
                    name,
                    DescriptionBox.Text?.Trim() ?? string.Empty,
                    CodeBox.Text?.Trim() ?? string.Empty,
                    EnabledCheck.IsChecked == true))
            {
                StatusText.Text = "The selected cheat no longer exists.";
                return;
            }

            if (Save())
            {
                RefreshList(name);
                RefreshStatus();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            Serilog.Log.Debug(exception, "Cheat update validation failed.");
            StatusText.Text = exception.Message;
        }
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (CheatList.SelectedItem is not CheatEntry entry)
        {
            StatusText.Text = "Select a cheat to delete.";
            return;
        }

        _set.Remove(entry.Name);
        if (!Save())
        {
            return;
        }

        NameBox.Text = string.Empty;
        DescriptionBox.Text = string.Empty;
        CodeBox.Text = string.Empty;
        EnabledCheck.IsChecked = false;
        RefreshList();
        RefreshStatus();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        if (Save())
        {
            Close();
        }
    }

    private bool Save()
    {
        try
        {
            CheatStore.Save(_path, CheatDocument.FromSet(_gameId, _set));
            _changed?.Invoke();
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "Cheat save failed for {Path}.", _path);
            StatusText.Text = $"Cheat save failed: {exception.Message}";
            return false;
        }
    }

    private void RefreshList(string? selectedName = null)
    {
        List<CheatEntry> entries = _set.Entries.ToList();
        CheatList.ItemsSource = entries;
        CheatEntry? selected = selectedName is null
            ? entries.FirstOrDefault()
            : entries.FirstOrDefault(
                entry => string.Equals(entry.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                ?? entries.FirstOrDefault();
        CheatList.SelectedItem = selected;
        if (selected is null)
        {
            NameBox.Text = string.Empty;
            DescriptionBox.Text = string.Empty;
            CodeBox.Text = string.Empty;
            EnabledCheck.IsChecked = false;
        }
    }

    private void RefreshStatus()
    {
        var errors = _set.Entries.Count(entry => entry.HasError);
        StatusText.Text =
            $"{_set.Count} cheat(s), {_set.ActiveCount} enabled"
            + (errors == 0 ? "." : $", {errors} need attention.")
            + $" Saved to {_path}.";
    }
}
