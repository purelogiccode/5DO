using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FiveDo.Achievements.Models;

/// <summary>Result of a RetroAchievements login.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Public API data-transfer properties are consumed by callers and tests.")]
public sealed class RaLoginResult
{
    /// <summary>Gets a value indicating whether the login succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the server error message when <see cref="Success"/> is false.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>Gets the case-corrected display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets the API token used for subsequent requests.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Gets the hardcore score.</summary>
    public uint Score { get; init; }

    /// <summary>Gets the softcore score.</summary>
    public uint SoftcoreScore { get; init; }

    /// <summary>Gets the number of unread messages.</summary>
    public uint UnreadMessages { get; init; }

    /// <summary>Gets the avatar URL.</summary>
    public string AvatarUrl { get; init; } = string.Empty;
}

/// <summary>A RetroAchievements achievement definition.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Public API data-transfer properties are consumed by callers and tests.")]
public sealed class RaAchievement
{
    /// <summary>Gets the achievement ID.</summary>
    public uint Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the server flags (3 = core, 5 = unofficial).</summary>
    public uint Flags { get; init; }

    /// <summary>Gets the point value.</summary>
    public uint Points { get; init; }

    /// <summary>Gets the rcheevos condition definition.</summary>
    public string MemAddr { get; init; } = string.Empty;

    /// <summary>Gets the author display name.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Gets the unlocked badge URL.</summary>
    public string BadgeUrl { get; init; } = string.Empty;

    /// <summary>Gets the locked badge URL.</summary>
    public string BadgeLockedUrl { get; init; } = string.Empty;

    /// <summary>Gets the achievement type (standard, progression, missable, win).</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the softcore rarity percentage.</summary>
    public double Rarity { get; init; } = 100.0;

    /// <summary>Gets the hardcore rarity percentage.</summary>
    public double RarityHardcore { get; init; } = 100.0;

    /// <summary>Gets a value indicating whether the achievement is an official core achievement.</summary>
    public bool IsOfficial => Flags == 3;
}

/// <summary>A RetroAchievements leaderboard definition.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Public API data-transfer properties are consumed by callers and tests.")]
public sealed class RaLeaderboardDefinition
{
    /// <summary>Gets the leaderboard ID.</summary>
    public uint Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the rcheevos definition.</summary>
    public string Mem { get; init; } = string.Empty;

    /// <summary>Gets the display format.</summary>
    public string Format { get; init; } = "VALUE";

    /// <summary>Gets a value indicating whether lower scores are better.</summary>
    public bool LowerIsBetter { get; init; }

    /// <summary>Gets a value indicating whether the leaderboard is hidden.</summary>
    public bool Hidden { get; init; }
}

/// <summary>The achievements, leaderboards, and rich presence for a game.</summary>
public sealed class RaGameData
{
    /// <summary>Gets the RetroAchievements game ID.</summary>
    public uint GameId { get; init; }

    /// <summary>Gets the game title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the console ID (43 for 3DO).</summary>
    public uint ConsoleId { get; init; }

    /// <summary>Gets the game icon URL.</summary>
    public string ImageUrl { get; init; } = string.Empty;

    /// <summary>Gets the rich-presence script, or an empty string.</summary>
    public string RichPresence { get; init; } = string.Empty;

    /// <summary>Gets the achievement definitions.</summary>
    public IReadOnlyList<RaAchievement> Achievements { get; init; } = [];

    /// <summary>Gets the leaderboard definitions.</summary>
    public IReadOnlyList<RaLeaderboardDefinition> Leaderboards { get; init; } = [];
}

/// <summary>A previously unlocked achievement returned when a session starts.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct RaUnlock(uint AchievementId);

/// <summary>Result of starting a play session.</summary>
public sealed class RaSessionResult
{
    /// <summary>Gets a value indicating whether the session started.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the server error message.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>Gets the softcore unlocks.</summary>
    public IReadOnlyList<RaUnlock> Unlocks { get; init; } = [];

    /// <summary>Gets the hardcore unlocks.</summary>
    public IReadOnlyList<RaUnlock> HardcoreUnlocks { get; init; } = [];

    /// <summary>Gets the server time at the start of the session.</summary>
    public long ServerNow { get; init; }
}

/// <summary>Result of awarding an achievement.</summary>
public sealed class RaAwardResult
{
    /// <summary>Gets a value indicating whether the award was accepted or already present.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the server error message.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>Gets the new hardcore score.</summary>
    public uint NewScore { get; init; }

    /// <summary>Gets the new softcore score.</summary>
    public uint NewSoftcoreScore { get; init; }

    /// <summary>Gets the awarded achievement ID.</summary>
    public uint AwardedAchievementId { get; init; }

    /// <summary>Gets the number of achievements remaining, or <see cref="uint.MaxValue"/> when unknown.</summary>
    public uint AchievementsRemaining { get; init; } = uint.MaxValue;
}

/// <summary>Result of a rich-presence ping.</summary>
public sealed class RaPingResult
{
    /// <summary>Gets a value indicating whether the ping succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the server error message.</summary>
    public string Error { get; init; } = string.Empty;
}

/// <summary>Result of fetching the current user's unlocks for a game.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Public API data-transfer properties are consumed by callers and tests.")]
public sealed class RaUserUnlocksResult
{
    /// <summary>Gets a value indicating whether the request succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the server error message.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>Gets the unlocked achievement IDs.</summary>
    public IReadOnlyList<uint> AchievementIds { get; init; } = [];
}

/// <summary>Result of submitting a leaderboard score.</summary>
[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Public API data-transfer properties are consumed by callers and tests.")]
public sealed class RaLeaderboardSubmitResult
{
    /// <summary>Gets a value indicating whether the submission succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the server error message.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>Gets the submitted score.</summary>
    public int SubmittedScore { get; init; }

    /// <summary>Gets the player's best score.</summary>
    public int BestScore { get; init; }

    /// <summary>Gets the new rank.</summary>
    public uint NewRank { get; init; }

    /// <summary>Gets the number of ranked entries.</summary>
    public uint NumEntries { get; init; }
}

/// <summary>Identifies a game from a disc hash.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct RaGameIdResult(bool Success, string Error, uint GameId);
