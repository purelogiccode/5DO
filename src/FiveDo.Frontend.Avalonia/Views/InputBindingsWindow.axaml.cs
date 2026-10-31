using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Captures keyboard bindings for the six player profiles and returns the edited profile.
/// </summary>
public partial class InputBindingsWindow : Window
{
    private static readonly ControllerButton[] GamepadChoices =
    [
        ControllerButton.None,
        ControllerButton.A,
        ControllerButton.B,
        ControllerButton.C,
        ControllerButton.P,
        ControllerButton.X,
        ControllerButton.L,
        ControllerButton.R,
        ControllerButton.Up,
        ControllerButton.Down,
        ControllerButton.Left,
        ControllerButton.Right,
    ];

    private readonly InputProfile _profile;
    private KeyboardBindingEditor _editor;
    private int _playerIndex;
    private ControllerButton? _pendingButton;
    private readonly bool _initializing;
    private bool _refreshingGamepad;

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
        _initializing = true;
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

        ControllerButton button = _pendingButton.Value;
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
        foreach (ControllerButton button in KeyboardBindingEditor.ControllerButtons)
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
            ToolTip.SetTip(label, $"The 3DO {button} button.");
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var key = new TextBox
            {
                Text = _editor.GetKey(button) ?? "(unbound)",
                IsReadOnly = true,
                Margin = new global::Avalonia.Thickness(0, 0, 6, 0),
            };
            ToolTip.SetTip(key, $"Current keyboard binding for {button}. Select Rebind to change it.");
            Grid.SetColumn(key, 1);
            row.Children.Add(key);

            var rebind = new Button
            {
                Content = _pendingButton == button ? "Press a key..." : "Rebind",
                Tag = button,
            };
            ToolTip.SetTip(
                rebind,
                _pendingButton == button
                    ? $"Press the new key for {button}, or Escape to cancel."
                    : $"Bind {button} to a new keyboard key.");
            rebind.Click += OnRebind;
            Grid.SetColumn(rebind, 2);
            row.Children.Add(rebind);

            RowsPanel.Children.Add(row);
        }

        RefreshGamepadRows();
    }

    private void RefreshGamepadRows()
    {
        _refreshingGamepad = true;
        try
        {
            GamepadRowsPanel.Children.Clear();
            PlayerProfile profile = _profile.Players[_playerIndex];
            foreach (var name in GamepadMapper.BindingNames)
            {
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("150,*"),
                    Margin = new global::Avalonia.Thickness(0, 2),
                };

                var label = new TextBlock
                {
                    Text = name,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ToolTip.SetTip(label, $"Physical gamepad control: {name}.");
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

                var combo = new ComboBox
                {
                    ItemsSource = GamepadChoices,
                    SelectedItem = profile.Gamepad.TryGetValue(name, out ControllerButton button)
                        ? button
                        : ControllerButton.None,
                    Tag = name,
                };
                ToolTip.SetTip(combo, $"Choose the 3DO button mapped to {name}.");
                combo.SelectionChanged += OnGamepadBindingChanged;
                Grid.SetColumn(combo, 1);
                row.Children.Add(combo);
                GamepadRowsPanel.Children.Add(row);
            }
        }
        finally
        {
            _refreshingGamepad = false;
        }
    }

    private void OnGamepadBindingChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingGamepad || sender is not ComboBox { Tag: string name } combo)
        {
            return;
        }

        PlayerProfile profile = _profile.Players[_playerIndex];
        if (combo.SelectedItem is ControllerButton button && button != ControllerButton.None)
        {
            profile.Gamepad[name] = button;
            StatusText.Text = $"Bound player {_playerIndex + 1} {name} to {button}.";
        }
        else
        {
            profile.Gamepad.Remove(name);
            StatusText.Text = $"Unbound player {_playerIndex + 1} {name}.";
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
        var defaults = PlayerProfile.CreateDefault(_playerIndex + 1);
        _profile.Players[_playerIndex].Gamepad = new Dictionary<string, ControllerButton>(
            defaults.Gamepad,
            StringComparer.OrdinalIgnoreCase);
        _pendingButton = null;
        StatusText.Text = $"Restored the default keyboard and gamepad bindings for player {_playerIndex + 1}.";
        RefreshRows();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _editor.ApplyTo(_profile.Players[_playerIndex]);
        Close(_profile);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
