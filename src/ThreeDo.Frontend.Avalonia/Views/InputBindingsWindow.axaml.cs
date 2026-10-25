using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using ThreeDo.Input;

namespace ThreeDo.Frontend.Avalonia.Views;

/// <summary>
/// Captures keyboard bindings for player 1 and returns the edited profile.
/// </summary>
public partial class InputBindingsWindow : Window
{
    private readonly PlayerProfile _profile;
    private readonly KeyboardBindingEditor _editor;
    private ControllerButton? _pendingButton;

    /// <summary>Creates the dialog with the default player profile.</summary>
    public InputBindingsWindow()
        : this(PlayerProfile.CreateDefault(1))
    {
    }

    /// <summary>Creates the dialog for a profile.</summary>
    /// <param name="profile">Profile to edit.</param>
    public InputBindingsWindow(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        InitializeComponent();
        _profile = profile;
        _editor = new KeyboardBindingEditor(profile);
        RefreshRows();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_pendingButton is null)
        {
            base.OnKeyDown(e);
            return;
        }

        if (e.Key == Key.Escape)
        {
            _pendingButton = null;
            StatusText.Text = "Rebind cancelled.";
            RefreshRows();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.None or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt)
        {
            return;
        }

        var button = _pendingButton.Value;
        var key = e.Key.ToString();
        _editor.Rebind(button, key);
        _pendingButton = null;
        StatusText.Text = $"Bound {button} to {key}.";
        RefreshRows();
        e.Handled = true;
    }

    private void RefreshRows()
    {
        RowsPanel.Children.Clear();
        foreach (var button in KeyboardBindingEditor.ControllerButtons)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("110,*,Auto"),
                Margin = new global::Avalonia.Thickness(0, 2),
            };

            var label = new TextBlock
            {
                Text = button.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var key = new TextBox
            {
                Text = _editor.GetKey(button) ?? "(unbound)",
                IsReadOnly = true,
                Margin = new global::Avalonia.Thickness(0, 0, 6, 0),
            };
            Grid.SetColumn(key, 1);
            row.Children.Add(key);

            var rebind = new Button
            {
                Content = _pendingButton == button ? "Press a key..." : "Rebind",
                Tag = button,
            };
            rebind.Click += OnRebind;
            Grid.SetColumn(rebind, 2);
            row.Children.Add(rebind);

            RowsPanel.Children.Add(row);
        }
    }

    private void OnRebind(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ControllerButton button })
        {
            return;
        }

        _pendingButton = button;
        StatusText.Text = $"Press a key for {button} (Escape cancels).";
        RefreshRows();
        Focus();
        e.Handled = true;
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        _editor.ResetDefaults();
        _pendingButton = null;
        StatusText.Text = "Restored the default keyboard bindings.";
        RefreshRows();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var result = new PlayerProfile
        {
            Name = _profile.Name,
            Gamepad = new Dictionary<string, ControllerButton>(_profile.Gamepad),
        };
        _editor.ApplyTo(result);
        Close(result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
