using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using ThreeDo.Input;

namespace ThreeDo.Frontend.Avalonia.Views;

/// <summary>
/// Captures keyboard bindings for the six player profiles and returns the edited profile.
/// </summary>
public partial class InputBindingsWindow : Window
{
    private readonly InputProfile _profile;
    private KeyboardBindingEditor _editor;
    private int _playerIndex;
    private ControllerButton? _pendingButton;
    private bool _initializing = true;

    /// <summary>Creates the dialog with the default input profile.</summary>
    public InputBindingsWindow()
        : this(InputProfile.CreateDefault())
    {
    }

    /// <summary>Creates the dialog for an input profile. The profile is copied.</summary>
    /// <param name="profile">Profile to edit.</param>
    public InputBindingsWindow(InputProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        InitializeComponent();
        _profile = profile.Clone();
        _editor = new KeyboardBindingEditor(_profile.Players[0]);
        PlayerCombo.ItemsSource = Enumerable.Range(1, _profile.Players.Length).ToArray();
        PlayerCombo.SelectedIndex = 0;
        _initializing = false;
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
        StatusText.Text = $"Bound player {_playerIndex + 1} {button} to {key}.";
        RefreshRows();
        e.Handled = true;
    }

    private void OnPlayerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializing || PlayerCombo.SelectedIndex < 0)
        {
            return;
        }

        _editor.ApplyTo(_profile.Players[_playerIndex]);
        _playerIndex = PlayerCombo.SelectedIndex;
        _editor = new KeyboardBindingEditor(_profile.Players[_playerIndex]);
        _pendingButton = null;
        StatusText.Text = $"Editing player {_playerIndex + 1}.";
        RefreshRows();
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
        StatusText.Text = $"Press a key for player {_playerIndex + 1} {button} (Escape cancels).";
        RefreshRows();
        Focus();
        e.Handled = true;
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        _editor.ResetDefaults();
        _pendingButton = null;
        StatusText.Text = $"Restored the default keyboard bindings for player {_playerIndex + 1}.";
        RefreshRows();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _editor.ApplyTo(_profile.Players[_playerIndex]);
        Close(_profile);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
