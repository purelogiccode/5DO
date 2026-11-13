using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Captures keyboard bindings for the eight player profiles and returns the edited profile.
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
    private readonly Func<GamepadState>? _gamepadState;
    private KeyboardBindingEditor _editor;
    private DispatcherTimer? _gamepadTimer;
    private int _playerIndex;
    private ControllerButton? _pendingButton;
    private ControllerButton _selectedButton;
    private ControllerButton _keyboardPressed;
    private ControllerButton _gamepadPressed;
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
        : this(profile, null)
    {
    }

    /// <summary>
    /// Creates the dialog for an input profile with a live gamepad sample provider for the
    /// controller preview. The provider is sampled on the UI thread; it must not poll a
    /// backend that the emulation thread is also polling.
    /// </summary>
    /// <param name="profile">Profile to edit. The profile is copied.</param>
    /// <param name="gamepadState">
    /// Optional callback returning the latest raw sample from the first connected
    /// controller, or <see langword="null"/> to disable live gamepad highlighting.
    /// </param>
    public InputBindingsWindow(InputProfile profile, Func<GamepadState>? gamepadState)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _initializing = true;
        InitializeComponent();
        _profile = profile.Clone();
        _gamepadState = gamepadState;
        _editor = new KeyboardBindingEditor(_profile.Players[0]);
        PlayerCombo.ItemsSource = Enumerable.Range(1, _profile.Players.Length).ToArray();
        PlayerCombo.SelectedIndex = 0;
        _initializing = false;
        RefreshRows();

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (_, _) =>
        {
            _keyboardPressed = ControllerButton.None;
            UpdatePreview();
        };

        if (gamepadState is not null)
        {
            _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _gamepadTimer.Tick += (_, _) => PollGamepadPreview();
            _gamepadTimer.Start();
        }

        Closed += (_, _) =>
        {
            _gamepadTimer?.Stop();
            _gamepadTimer = null;
        };
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
        _selectedButton = button;
        StatusText.Text = $"Bound player {_playerIndex + 1} {button} to {key}.";
        RefreshRows();
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_pendingButton is not null)
        {
            return;
        }

        ControllerButton button = ControllerPreview.FindKeyboardButton(
            _profile.Players[_playerIndex],
            e.Key.ToString());
        if (button == ControllerButton.None)
        {
            return;
        }

        _keyboardPressed |= button;
        UpdatePreview();
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        ControllerButton button = ControllerPreview.FindKeyboardButton(
            _profile.Players[_playerIndex],
            e.Key.ToString());
        if (button == ControllerButton.None)
        {
            return;
        }

        _keyboardPressed &= ~button;
        UpdatePreview();
    }

    private void PollGamepadPreview()
    {
        if (_gamepadState is null)
        {
            return;
        }

        ControllerState state = GamepadMapper.Map(_profile.Players[_playerIndex], _gamepadState());
        if (state.Buttons == _gamepadPressed)
        {
            return;
        }

        _gamepadPressed = state.Buttons;
        UpdatePreview();
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
        _selectedButton = ControllerButton.None;
        _keyboardPressed = ControllerButton.None;
        _gamepadPressed = ControllerButton.None;
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

            row.PointerEntered += (_, _) => SelectPreview(button);
            key.GotFocus += (_, _) => SelectPreview(button);
            RowsPanel.Children.Add(row);
        }

        RefreshGamepadRows();
        UpdatePreview();
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
                combo.GotFocus += (_, _) => SelectPreview(CurrentGamepadButton(name));
                Grid.SetColumn(combo, 1);
                row.Children.Add(combo);
                row.PointerEntered += (_, _) => SelectPreview(CurrentGamepadButton(name));
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
            SelectPreview(button);
        }
        else
        {
            profile.Gamepad.Remove(name);
            StatusText.Text = $"Unbound player {_playerIndex + 1} {name}.";
            SelectPreview(ControllerButton.None);
        }
    }

    private ControllerButton CurrentGamepadButton(string name)
    {
        return _profile.Players[_playerIndex].Gamepad.TryGetValue(name, out ControllerButton button)
            ? button
            : ControllerButton.None;
    }

    private void SelectPreview(ControllerButton button)
    {
        _selectedButton = button;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        PlayerProfile profile = _profile.Players[_playerIndex];
        PadPreview.MappedButtons = ControllerPreview.GetMappedButtons(profile);
        PadPreview.SelectedButton = _selectedButton;
        PadPreview.PressedButtons = _keyboardPressed | _gamepadPressed;
        UpdatePreviewText(profile);
    }

    private void UpdatePreviewText(PlayerProfile profile)
    {
        ControllerButton pressed = _keyboardPressed | _gamepadPressed;
        if (_selectedButton == ControllerButton.None)
        {
            PreviewText.Text = pressed == ControllerButton.None
                ? "Point at a binding row or press a mapped key to light the pad."
                : $"Pressed: {FormatButtons(pressed)}.";
            return;
        }

        var parts = new List<string>(2);
        string? key = ControllerPreview.FindKeyboardKey(profile, _selectedButton);
        if (key is not null)
        {
            parts.Add($"key {key}");
        }

        IReadOnlyList<string> gamepad = ControllerPreview.FindGamepadNames(profile, _selectedButton);
        if (gamepad.Count > 0)
        {
            parts.Add("gamepad " + string.Join(" / ", gamepad));
        }

        var binding = parts.Count == 0 ? "unbound" : string.Join(", ", parts);
        PreviewText.Text = (_selectedButton & pressed) != ControllerButton.None
            ? $"{_selectedButton} pressed ({binding})."
            : $"{_selectedButton} — {binding}.";
    }

    private static string FormatButtons(ControllerButton buttons)
    {
        var names = new List<string>();
        foreach (ControllerButton button in KeyboardBindingEditor.ControllerButtons)
        {
            if ((buttons & button) != ControllerButton.None)
            {
                names.Add(button.ToString());
            }
        }

        return names.Count == 0 ? "none" : string.Join(", ", names);
    }

    private void OnRebind(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ControllerButton button })
        {
            return;
        }

        _pendingButton = button;
        _keyboardPressed = ControllerButton.None;
        _selectedButton = button;
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
        _keyboardPressed = ControllerButton.None;
        _gamepadPressed = ControllerButton.None;
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
