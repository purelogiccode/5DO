using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// A small picker for attached optical drives. Kept in code so the drive list can be
/// refreshed from the host without a second XAML surface.
/// </summary>
internal sealed class OpticalDriveWindow : Window
{
    private readonly ListBox _drives;

    /// <summary>Creates the picker.</summary>
    /// <param name="drives">Optical drive paths that currently contain media.</param>
    public OpticalDriveWindow(IReadOnlyList<string> drives)
    {
        ArgumentNullException.ThrowIfNull(drives);
        Title = "Optical Drive";
        Width = 460;
        Height = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _drives = new ListBox
        {
            ItemsSource = drives,
            SelectedIndex = drives.Count > 0 ? 0 : -1,
            MinHeight = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTip.SetTip(_drives, "Choose the drive that contains the 3DO disc.");

        var hint = new TextBlock
        {
            Text = "Select an optical drive with a 3DO disc and press Start.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
        };
        ToolTip.SetTip(hint, "The drive is read through the host block interface and never written.");

        var start = new Button
        {
            Content = "Start",
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = drives.Count > 0,
        };
        ToolTip.SetTip(start, "Boot the selected drive.");
        start.Click += (_, _) => CloseSelected();

        var cancel = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTip.SetTip(cancel, "Close without booting a drive.");
        cancel.Click += (_, _) => Close(null);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(start);

        var panel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
        };
        panel.Children.Add(hint);
        panel.Children.Add(_drives);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private void CloseSelected()
    {
        if (_drives.SelectedItem is string drive)
        {
            Close(drive);
        }
    }
}
