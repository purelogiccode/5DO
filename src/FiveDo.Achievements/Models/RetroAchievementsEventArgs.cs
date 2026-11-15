using System;

namespace FiveDo.Achievements.Models;

/// <summary>Event data for a RetroAchievements state change.</summary>
public sealed class RetroAchievementsStateEventArgs(RetroAchievementsState state) : EventArgs
{
    /// <summary>Gets the state snapshot.</summary>
    public RetroAchievementsState State { get; } = state;
}

/// <summary>Event data for a RetroAchievements notification.</summary>
public sealed class AchievementNotificationEventArgs(AchievementNotification notification) : EventArgs
{
    /// <summary>Gets the notification.</summary>
    public AchievementNotification Notification { get; } = notification;
}

/// <summary>Event data for a completed login.</summary>
public sealed class RaLoginEventArgs(RaLoginResult result) : EventArgs
{
    /// <summary>Gets the login result.</summary>
    public RaLoginResult Result { get; } = result;
}
