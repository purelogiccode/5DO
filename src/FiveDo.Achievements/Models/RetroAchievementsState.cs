using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FiveDo.Achievements.Models;

/// <summary>User-visible RetroAchievements configuration.</summary>
public sealed class RetroAchievementsSettings
{
    /// <summary>Gets or sets a value indicating whether RetroAchievements is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the login username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the API token returned by the login endpoint.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether hardcore mode is active.</summary>
    public bool Hardcore { get; set; }

    /// <summary>Gets or sets a value indicating whether unlock notifications are shown on the OSD.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether measured-progress updates are shown on the OSD.</summary>
    public bool ShowProgressNotifications { get; set; }

    /// <summary>Gets or sets a value indicating whether the rich-presence string is shown in the UI.</summary>
    public bool ShowRichPresence { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether leaderboard events are shown on the OSD.</summary>
    public bool ShowLeaderboardNotifications { get; set; } = true;

    /// <summary>Gets or sets the web host. An empty value uses the official host.</summary>
    public string Host { get; set; } = RetroAchievementsClient.DefaultHost;
}

/// <summary>A single achievement's runtime state.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "State properties are bound from XAML and exposed to consumers.")]
public sealed class AchievementProgress
{
    /// <summary>Gets the achievement ID.</summary>
    public uint Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the point value.</summary>
    public uint Points { get; init; }

    /// <summary>Gets the unlocked badge URL.</summary>
    public string BadgeUrl { get; init; } = string.Empty;

    /// <summary>Gets the locked badge URL.</summary>
    public string BadgeLockedUrl { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the achievement is an official core achievement.</summary>
    public bool IsOfficial { get; init; }

    /// <summary>Gets a value indicating whether the achievement is unlocked.</summary>
    public bool Unlocked { get; init; }

    /// <summary>Gets a value indicating whether the definition could not be parsed.</summary>
    public bool IsUnsupported { get; init; }

    /// <summary>Gets the current measured value.</summary>
    public uint MeasuredValue { get; init; }

    /// <summary>Gets the measured target, or zero when the achievement has no progress counter.</summary>
    public uint MeasuredTarget { get; init; }

    /// <summary>Gets the formatted progress text, such as <c>3/10</c>.</summary>
    public string ProgressText { get; init; } = string.Empty;

    /// <summary>Gets the rarity percentage.</summary>
    public double Rarity { get; init; } = 100.0;

    /// <summary>Gets a short display status: unlocked, locked, or unsupported.</summary>
    public string StatusText => IsUnsupported ? "Unsupported" : Unlocked ? "Unlocked" : "Locked";
}

/// <summary>A leaderboard's runtime state.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "State properties are bound from XAML and exposed to consumers.")]
public sealed class LeaderboardProgress
{
    /// <summary>Gets the leaderboard ID.</summary>
    public uint Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the active attempt's current value.</summary>
    public int Value { get; init; }

    /// <summary>Gets a value indicating whether an attempt is in progress.</summary>
    public bool IsActive { get; init; }
}

/// <summary>An immutable snapshot of the RetroAchievements service state.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "State properties are bound from XAML and exposed to consumers.")]
public sealed record RetroAchievementsState
{
    /// <summary>Gets a value indicating whether RetroAchievements is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets a value indicating whether a user is logged in.</summary>
    public bool LoggedIn { get; init; }

    /// <summary>Gets the current username.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether hardcore mode is active.</summary>
    public bool Hardcore { get; init; }

    /// <summary>Gets the hardcore score.</summary>
    public uint Score { get; init; }

    /// <summary>Gets the softcore score.</summary>
    public uint SoftcoreScore { get; init; }

    /// <summary>Gets a value indicating whether a supported game is loaded.</summary>
    public bool GameLoaded { get; init; }

    /// <summary>Gets the RetroAchievements game ID.</summary>
    public uint GameId { get; init; }

    /// <summary>Gets the game title.</summary>
    public string GameTitle { get; init; } = string.Empty;

    /// <summary>Gets the disc hash used for identification.</summary>
    public string GameHash { get; init; } = string.Empty;

    /// <summary>Gets the current rich-presence display string.</summary>
    public string RichPresence { get; init; } = string.Empty;

    /// <summary>Gets a human-readable status message.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets the last error message, or an empty string.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>Gets the number of queued unlocks waiting to sync.</summary>
    public int PendingUnlocks { get; init; }

    /// <summary>Gets the achievement states.</summary>
    public IReadOnlyList<AchievementProgress> Achievements { get; init; } = [];

    /// <summary>Gets the leaderboard states.</summary>
    public IReadOnlyList<LeaderboardProgress> Leaderboards { get; init; } = [];

    /// <summary>Gets the number of official achievements.</summary>
    public int TotalAchievements { get; init; }

    /// <summary>Gets the number of unlocked official achievements.</summary>
    public int UnlockedAchievements { get; init; }

    /// <summary>Gets the number of official achievement points available.</summary>
    public uint TotalPoints { get; init; }

    /// <summary>Gets the number of unlocked official achievement points.</summary>
    public uint UnlockedPoints { get; init; }
}

/// <summary>Kinds of RetroAchievements notifications surfaced to the frontend.</summary>
public enum AchievementNotificationKind
{
    /// <summary>An achievement was unlocked.</summary>
    AchievementUnlocked,

    /// <summary>An achievement's measured progress changed.</summary>
    AchievementProgress,

    /// <summary>A leaderboard attempt started.</summary>
    LeaderboardStarted,

    /// <summary>A leaderboard attempt was canceled.</summary>
    LeaderboardCanceled,

    /// <summary>A leaderboard score was submitted.</summary>
    LeaderboardSubmitted,

    /// <summary>A game was identified.</summary>
    GameIdentified,

    /// <summary>The disc hash is not registered.</summary>
    GameNotIdentified,

    /// <summary>A login or service operation failed.</summary>
    Error,
}

/// <summary>A notification for the on-screen display or notifications list.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Notification properties are exposed to consumers.")]
public sealed class AchievementNotification
{
    /// <summary>Gets the notification kind.</summary>
    public AchievementNotificationKind Kind { get; init; }

    /// <summary>Gets the notification title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the notification body.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the optional badge URL.</summary>
    public string BadgeUrl { get; init; } = string.Empty;

    /// <summary>Gets the optional achievement ID.</summary>
    public uint AchievementId { get; init; }

    /// <summary>Gets the optional point value.</summary>
    public uint Points { get; init; }

    /// <summary>Gets a value indicating whether the event happened in hardcore mode.</summary>
    public bool Hardcore { get; init; }
}
