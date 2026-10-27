using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FiveDo.Core;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Displays the in-memory diagnostic log with level, category, and text filters.
/// </summary>
public partial class LogsWindow : Window
{
    private readonly InMemoryLogSink _sink;
    private readonly DispatcherTimer _timer;
    private bool _initializing;

    /// <summary>Creates the dialog with an empty log.</summary>
    public LogsWindow()
        : this(new InMemoryLogSink())
    {
    }

    /// <summary>Creates the dialog.</summary>
    /// <param name="sink">Log sink owned by the shell.</param>
    public LogsWindow(InMemoryLogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _initializing = true;
        InitializeComponent();

        LevelCombo.ItemsSource = Enum.GetValues<LogLevel>()
            .Where(level => level != LogLevel.None)
            .ToArray();
        LevelCombo.SelectedItem = LogLevel.Info;
        CategoryCombo.ItemsSource = Enum.GetValues<LogCategory>()
            .Where(category => category != LogCategory.None)
            .ToArray();
        CategoryCombo.SelectedItem = LogCategory.All;
        _initializing = false;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshIfAuto();
        _timer.Start();
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initializing)
        {
            Refresh();
        }
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_initializing)
        {
            Refresh();
        }
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => Refresh();

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        _sink.Clear();
        Refresh();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void RefreshIfAuto()
    {
        if (AutoRefreshCheck.IsChecked == true)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var filter = new LogViewFilter
        {
            MinimumLevel = LevelCombo.SelectedItem is LogLevel level ? level : LogLevel.Info,
            Category = CategoryCombo.SelectedItem is LogCategory category ? category : LogCategory.All,
            Search = SearchBox.Text?.Trim() ?? string.Empty,
        };

        IReadOnlyList<LogEntry> entries = _sink.Entries;
        IReadOnlyList<LogEntry> filtered = filter.Apply(entries);
        var lines = new List<string>(filtered.Count);
        foreach (LogEntry entry in filtered)
        {
            var time = entry.TimestampUtc == default
                ? string.Empty
                : entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + " ";
            lines.Add($"{time}[{entry.Level,-8}] {entry.Category,-12} {entry.Message}");
        }

        LogList.ItemsSource = lines;
        if (lines.Count > 0)
        {
            LogList.ScrollIntoView(lines[^1]);
        }

        StatusText.Text = $"{filtered.Count} of {entries.Count} entries.";
    }
}
