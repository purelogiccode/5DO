using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FiveDo.Achievements;
using FiveDo.Achievements.Models;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>Account, notification, and achievement progress for RetroAchievements.</summary>
public partial class RetroAchievementsWindow : Window
{
    private readonly RetroAchievementsService _service;

    /// <summary>Creates an empty dialog for the XAML smoke test.</summary>
    public RetroAchievementsWindow()
        : this(
            new RetroAchievementsService(new ZeroAchievementMemory(), new RetroAchievementsSettings()),
            new RetroAchievementsSettings(),
            _ => { })
    {
    }
    private readonly Action<RetroAchievementsSettings> _persist;
    private bool _initializing;
    private bool _closed;

    /// <summary>Creates the RetroAchievements dialog.</summary>
    /// <param name="service">Achievement service owned by the shell.</param>
    /// <param name="settings">Current settings.</param>
    /// <param name="persist">Callback that saves updated settings.</param>
    public RetroAchievementsWindow(
        RetroAchievementsService service,
        RetroAchievementsSettings settings,
        Action<RetroAchievementsSettings> persist)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(persist);

        _service = service;
        _persist = persist;
        _initializing = true;
        InitializeComponent();

        UsernameBox.Text = settings.Username;
        EnabledCheck.IsChecked = settings.Enabled;
        HardcoreCheck.IsChecked = settings.Hardcore;
        UnlockNotificationsCheck.IsChecked = settings.ShowNotifications;
        ProgressNotificationsCheck.IsChecked = settings.ShowProgressNotifications;
        LeaderboardNotificationsCheck.IsChecked = settings.ShowLeaderboardNotifications;
        RichPresenceCheck.IsChecked = settings.ShowRichPresence;
        UseTokenCheck.IsChecked = false;
        _initializing = false;
        UpdateHardcoreNote();

        _service.StateChanged += OnStateChanged;
        _service.Notification += OnNotification;
        Update(_service.State);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _service.StateChanged -= OnStateChanged;
        _service.Notification -= OnNotification;
        base.OnClosed(e);
    }

    private async void OnLogin(object? sender, RoutedEventArgs e)
    {
        string username = UsernameBox.Text?.Trim() ?? string.Empty;
        if (username.Length == 0)
        {
            StatusText.Text = "Enter a RetroAchievements username.";
            return;
        }

        string secret = SecretBox.Text ?? string.Empty;
        bool isToken = UseTokenCheck.IsChecked == true;
        if (secret.Length == 0)
        {
            RetroAchievementsSettings current = _service.Settings;
            if (string.IsNullOrEmpty(current.ApiToken))
            {
                StatusText.Text = "Enter a password or API token.";
                return;
            }

            secret = current.ApiToken;
            isToken = true;
        }

        LoginButton.IsEnabled = false;
        StatusText.Text = "Signing in...";
        try
        {
            RaLoginResult result = await _service.LoginAsync(username, secret, isToken).ConfigureAwait(true);
            if (result.Success)
            {
                SecretBox.Text = string.Empty;
                StatusText.Text = $"Signed in as {result.DisplayName}.";
                PersistFromInputs();
            }
            else
            {
                StatusText.Text = string.IsNullOrWhiteSpace(result.Error)
                    ? "Sign in failed."
                    : $"Sign in failed: {result.Error}";
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusText.Text = $"Sign in failed: {exception.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void OnLogout(object? sender, RoutedEventArgs e)
    {
        _service.Logout();
        RetroAchievementsSettings settings = _service.Settings;
        settings.ApiToken = string.Empty;
        _persist(settings);
        SecretBox.Text = string.Empty;
        StatusText.Text = "Signed out.";
    }

    private void OnOptionsChanged(object? sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        bool hardcore = HardcoreCheck.IsChecked == true;
        RichPresenceCheck.IsEnabled = !hardcore;
        if (hardcore)
        {
            RichPresenceCheck.IsChecked = true;
        }

        UpdateHardcoreNote();
        PersistFromInputs();
    }

    private void UpdateHardcoreNote()
    {
        HardcoreNote.IsVisible = HardcoreCheck.IsChecked == true
            && !RetroAchievementsCompliance.HardcoreApproved;
        HardcoreNote.Text = HardcoreNote.IsVisible
            ? "Hardcore is pending RetroAchievements client approval. Until 5DO is recognized, the server treats hardcore unlocks as casual. Enabling it resets the current game."
            : string.Empty;
    }

    private void PersistFromInputs()
    {
        RetroAchievementsSettings current = _service.Settings;
        bool hardcore = HardcoreCheck.IsChecked == true;
        var settings = new RetroAchievementsSettings
        {
            Enabled = EnabledCheck.IsChecked == true,
            Username = UsernameBox.Text?.Trim() ?? current.Username,
            ApiToken = current.ApiToken,
            Hardcore = hardcore,
            ShowNotifications = UnlockNotificationsCheck.IsChecked == true,
            ShowProgressNotifications = ProgressNotificationsCheck.IsChecked == true,
            ShowRichPresence = hardcore || RichPresenceCheck.IsChecked == true,
            ShowLeaderboardNotifications = LeaderboardNotificationsCheck.IsChecked == true,
            Host = current.Host,
        };

        _persist(settings);
    }

    private void OnStateChanged(object? sender, RetroAchievementsStateEventArgs e)
    {
        if (_closed)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => Update(e.State));
    }

    private void OnNotification(object? sender, AchievementNotificationEventArgs e)
    {
        if (_closed || e.Notification.Kind != AchievementNotificationKind.Error)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => StatusText.Text = e.Notification.Message);
    }

    private void Update(RetroAchievementsState state)
    {
        if (_closed)
        {
            return;
        }

        _initializing = true;
        EnabledCheck.IsChecked = state.Enabled;
        HardcoreCheck.IsChecked = state.Hardcore;
        RichPresenceCheck.IsEnabled = !state.Hardcore;
        if (state.Hardcore)
        {
            RichPresenceCheck.IsChecked = true;
        }

        UsernameBox.Text = string.IsNullOrWhiteSpace(state.Username) ? UsernameBox.Text : state.Username;
        _initializing = false;
        UpdateHardcoreNote();

        StatusText.Text = string.IsNullOrWhiteSpace(state.Error)
            ? state.Status
            : $"{state.Status} {state.Error}";

        string pending = state.PendingUnlocks > 0
            ? string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"\n{state.PendingUnlocks} pending sync")
            : string.Empty;
        TotalsText.Text = state.GameLoaded
            ? $"{state.UnlockedAchievements}/{state.TotalAchievements} unlocked\n{state.UnlockedPoints}/{state.TotalPoints} pts{pending}"
            : state.LoggedIn
                ? $"{state.Score} hardcore\n{state.SoftcoreScore} softcore{pending}"
                : string.Empty;

        RichPresenceText.Text = state.Enabled && RichPresenceCheck.IsChecked == true
            ? state.RichPresence
            : string.Empty;

        AchievementsList.ItemsSource = state.Achievements;
        FooterText.Text = state.GameLoaded
            ? $"Game {state.GameId}: {state.GameTitle} | hash {state.GameHash}"
            : string.IsNullOrEmpty(state.GameHash)
                ? "No game has been hashed yet."
                : $"Disc hash {state.GameHash}";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}

