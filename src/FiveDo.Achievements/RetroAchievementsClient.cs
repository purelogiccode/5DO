using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Achievements.Models;

namespace FiveDo.Achievements;

/// <summary>
/// A small managed RetroAchievements Web API client. It covers login, game
/// identification, game data, sessions, achievement awards, pings, user unlocks,
/// and leaderboard submissions.
/// </summary>
/// <remarks>
/// The request shapes mirror rcheevos' <c>rc_api_*</c> helpers. Responses are parsed
/// with <see cref="JsonDocument"/> so the client is trim/AOT friendly.
/// </remarks>
public sealed class RetroAchievementsClient : IDisposable
{
    /// <summary>The default RetroAchievements web host.</summary>
    public const string DefaultHost = "https://retroachievements.org";

    private const string DefaultMediaHost = "https://media.retroachievements.org";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _userAgent;

    /// <summary>Initializes a new instance of the <see cref="RetroAchievementsClient"/> class.</summary>
    /// <param name="host">Web host, or <see langword="null"/> for <see cref="DefaultHost"/>.</param>
    /// <param name="httpClient">Optional HTTP client. The caller owns it.</param>
    /// <param name="userAgent">Optional user agent.</param>
    public RetroAchievementsClient(string? host = null, HttpClient? httpClient = null, string? userAgent = null)
    {
        Host = NormalizeHost(host);
        _http = httpClient ?? new HttpClient();
        _ownsHttp = httpClient is null;
        _userAgent = string.IsNullOrWhiteSpace(userAgent)
            ? RetroAchievementsCompliance.UserAgent
            : userAgent;
    }

    /// <summary>Gets the normalized web host without a trailing slash.</summary>
    public string Host { get; }

    /// <summary>Logs in with a username and either a password or an existing API token.</summary>
    /// <param name="username">RetroAchievements username.</param>
    /// <param name="secret">Password or API token.</param>
    /// <param name="isToken">Whether <paramref name="secret"/> is an API token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The login result.</returns>
    public async Task<RaLoginResult> LoginAsync(
        string username,
        string secret,
        bool isToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["r"] = "login2",
            ["u"] = username,
            [isToken ? "t" : "p"] = secret,
        };

        using JsonDocument? document = await PostAsync("/dorequest.php", parameters, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return new RaLoginResult { Success = false, Error = "Unable to contact RetroAchievements." };
        }

        JsonElement root = document.RootElement;
        if (!GetBool(root, "Success", false))
        {
            return new RaLoginResult { Success = false, Error = GetString(root, "Error") };
        }

        return new RaLoginResult
        {
            Success = true,
            DisplayName = GetString(root, "User"),
            Token = GetString(root, "Token"),
            Score = GetUInt32(root, "Score"),
            SoftcoreScore = GetUInt32(root, "SoftcoreScore"),
            UnreadMessages = GetUInt32(root, "Messages"),
            AvatarUrl = GetString(root, "AvatarUrl"),
        };
    }

    /// <summary>Resolves a game hash to a RetroAchievements game ID.</summary>
    /// <param name="gameHash">The 32-character disc hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The game ID, or zero when the hash is not registered.</returns>
    public async Task<RaGameIdResult> ResolveHashAsync(
        string gameHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameHash);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["r"] = "gameid",
            ["m"] = gameHash,
        };

        using JsonDocument? document = await PostAsync("/dorequest.php", parameters, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return new RaGameIdResult(false, "Unable to contact RetroAchievements.", 0);
        }

        JsonElement root = document.RootElement;
        return new RaGameIdResult(
            GetBool(root, "Success", false),
            GetString(root, "Error"),
            GetUInt32(root, "GameID"));
    }

    /// <summary>Fetches achievements, leaderboards, and the rich-presence script for a game.</summary>
    /// <param name="gameId">RetroAchievements game ID.</param>
    /// <param name="username">Current username.</param>
    /// <param name="token">Current API token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The game data, or <see langword="null"/> when the request failed.</returns>
    public async Task<RaGameData?> FetchGameAsync(
        uint gameId,
        string username,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(gameId);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["r"] = "patch",
            ["u"] = username,
            ["t"] = token,
            ["g"] = gameId.ToString(CultureInfo.InvariantCulture),
        };

        using JsonDocument? document = await PostAsync("/dorequest.php", parameters, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        JsonElement root = document.RootElement;
        if (!GetBool(root, "Success", false)
            || !root.TryGetProperty("PatchData", out JsonElement patch)
            || patch.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string imageName = StripImagePath(GetString(patch, "ImageIcon"));
        string imageUrl = GetString(patch, "ImageIconURL");
        if (string.IsNullOrEmpty(imageUrl) && !string.IsNullOrEmpty(imageName))
        {
            imageUrl = $"{DefaultMediaHost}/Images/{imageName}.png";
        }

        var achievements = new List<RaAchievement>();
        if (patch.TryGetProperty("Achievements", out JsonElement achievementArray)
            && achievementArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in achievementArray.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string badgeName = GetString(entry, "BadgeName");
                string badgeUrl = GetString(entry, "BadgeURL");
                if (string.IsNullOrEmpty(badgeUrl) && !string.IsNullOrEmpty(badgeName))
                {
                    badgeUrl = $"{DefaultMediaHost}/Badge/{badgeName}.png";
                }

                string badgeLockedUrl = GetString(entry, "BadgeLockedURL");
                if (string.IsNullOrEmpty(badgeLockedUrl) && !string.IsNullOrEmpty(badgeName))
                {
                    badgeLockedUrl = $"{DefaultMediaHost}/Badge/{badgeName}_lock.png";
                }

                achievements.Add(new RaAchievement
                {
                    Id = GetUInt32(entry, "ID"),
                    Title = NormalizeMissableTitle(GetString(entry, "Title")),
                    Description = GetString(entry, "Description"),
                    Flags = GetUInt32(entry, "Flags"),
                    Points = GetUInt32(entry, "Points"),
                    MemAddr = GetString(entry, "MemAddr"),
                    Author = GetString(entry, "Author"),
                    BadgeUrl = badgeUrl,
                    BadgeLockedUrl = badgeLockedUrl,
                    Type = GetString(entry, "Type"),
                    Rarity = GetDouble(entry, "Rarity", 100.0),
                    RarityHardcore = GetDouble(entry, "RarityHardcore", 100.0),
                });
            }
        }

        var leaderboards = new List<RaLeaderboardDefinition>();
        if (patch.TryGetProperty("Leaderboards", out JsonElement leaderboardArray)
            && leaderboardArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in leaderboardArray.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                leaderboards.Add(new RaLeaderboardDefinition
                {
                    Id = GetUInt32(entry, "ID"),
                    Title = GetString(entry, "Title"),
                    Description = GetString(entry, "Description"),
                    Mem = GetString(entry, "Mem"),
                    Format = GetString(entry, "Format"),
                    LowerIsBetter = GetBool(entry, "LowerIsBetter", false),
                    Hidden = GetBool(entry, "Hidden", false),
                });
            }
        }

        return new RaGameData
        {
            GameId = GetUInt32(patch, "ID"),
            Title = GetString(patch, "Title"),
            ConsoleId = GetUInt32(patch, "ConsoleID"),
            ImageUrl = imageUrl,
            RichPresence = GetString(patch, "RichPresencePatch"),
            Achievements = achievements,
            Leaderboards = leaderboards,
        };
    }

    /// <summary>Starts a play session, returning achievements already unlocked by the user.</summary>
    /// <param name="gameId">Game ID.</param>
    /// <param name="gameHash">Disc hash.</param>
    /// <param name="username">Username.</param>
    /// <param name="token">API token.</param>
    /// <param name="hardcore">Whether hardcore mode is active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session result.</returns>
    public async Task<RaSessionResult> StartSessionAsync(
        uint gameId,
        string gameHash,
        string username,
        string token,
        bool hardcore,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["r"] = "startsession",
            ["u"] = username,
            ["t"] = token,
            ["g"] = gameId.ToString(CultureInfo.InvariantCulture),
            ["h"] = hardcore ? "1" : "0",
            ["m"] = gameHash,
            ["l"] = _userAgent,
        };

        using JsonDocument? document = await PostAsync("/dorequest.php", parameters, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return new RaSessionResult { Success = false, Error = "Unable to contact RetroAchievements." };
        }

        JsonElement root = document.RootElement;
        if (!GetBool(root, "Success", false))
        {
            return new RaSessionResult { Success = false, Error = GetString(root, "Error") };
        }

        return new RaSessionResult
        {
            Success = true,
            Unlocks = ParseUnlocks(root, "Unlocks"),
            HardcoreUnlocks = ParseUnlocks(root, "HardcoreUnlocks"),
            ServerNow = GetInt64(root, "ServerNow"),
        };
    }

    /// <summary>Awards an achievement to the current user.</summary>
    /// <param name="achievementId">Achievement ID.</param>
    /// <param name="username">Username.</param>
    /// <param name="token">API token.</param>
    /// <param name="hardcore">Whether hardcore mode is active.</param>
    /// <param name="gameHash">Disc hash.</param>
    /// <param name="secondsSinceUnlock">Optional offline-unlock age in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The award result.</returns>
    public async Task<RaAwardResult> AwardAchievementAsync(
        uint achievementId,
        string username,
        string token,
        bool hardcore,
        string gameHash,
        uint secondsSinceUnlock = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(achievementId);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["r"] = "awardachievement",
            ["u"] = username,
            ["t"] = token,
            ["a"] = achievementId.ToString(CultureInfo.InvariantCulture),
            ["h"] = hardcore ? "1" : "0",
            ["v"] = BuildAchievementSignature(achievementId, username, hardcore, secondsSinceUnlock),
        };

        if (!string.IsNullOrEmpty(gameHash))
        {
            parameters["m"] = gameHash;
        }

        if (secondsSinceUnlock != 0)
        {
            parameters["o"] = secondsSinceUnlock.ToString(CultureInfo.InvariantCulture);
        }

        using JsonDocument? document = await PostAsync("/dorequest.php", parameters, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return new RaAwardResult { Success = false, Error = "Unable to contact RetroAchievements." };
        }

        JsonElement root = document.RootElement;
        bool success = GetBool(root, "Success", false);
        string error = GetString(root, "Error");
        if (!success && error.StartsWith("User already has", StringComparison.Ordinal))
        {
            success = true;
        }

        return new RaAwardResult
        {
            Success = success,
            Error = success ? string.Empty : error,
            NewScore = GetUInt32(root, "Score"),
            NewSoftcoreScore = GetUInt32(root, "SoftcoreScore"),
            AwardedAchievementId = GetUInt32(root, "AchievementID"),
            AchievementsRemaining = root.TryGetProperty("AchievementsRemaining", out _)
                ? GetUInt32(root, "AchievementsRemaining")
                : uint.MaxValue,
        };
    }

    /// <summary>Updates the user's rich presence and session heartbeat.</summary>
    /// <param name="gameId">Game ID.</param>
    /// <param name="gameHash">Disc hash.</param>
    /// <param name="username">Username.</param>
    /// <param name="token">API token.</param>
    /// <param name="hardcore">Whether hardcore mode is active.</param>
    /// <param name="richPresence">Rich-presence display string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ping result.</returns>
    public async Task<RaPingResult> PingAsync(
        uint gameId,
        string gameHash,
        string username,
        string token,
        bool hardcore,
        string? richPresence,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["r"] = "ping",
            ["u"] = username,
            ["t"] = token,
            ["g"] = gameId.ToString(CultureInfo.InvariantCulture),
            ["h"] = hardcore ? "1" : "0",
            ["x"] = gameHash,
        };

        if (!string.IsNullOrEmpty(richPresence))
        {
            parameters["m"] = richPresence;
        }

        using JsonDocument? document = await PostAsync("/dorequest.php", parameters, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return new RaPingResult { Success = false, Error = "Unable to contact RetroAchievements." };
        }

        return new RaPingResult
        {
            Success = GetBool(document.RootElement, "Success", false),
            Error = GetString(document.RootElement, "Error"),
        };
    }

    /// <summary>Fetches the current user's unlocked achievement IDs for a game.</summary>
    /// <param name="gameId">Game ID.</param>
    /// <param name="username">Username.</param>
    /// <param name="token">API token.</param>
    /// <param name="hardcore">Whether hardcore unlocks are requested.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unlock result.</returns>
    public async Task<RaUserUnlocksResult> FetchUserUnlocksAsync(
        uint gameId,
        string username,
        string token,
        bool hardcore,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["r"] = "unlocks",
            ["u"] = username,
            ["t"] = token,
            ["g"] = gameId.ToString(CultureInfo.InvariantCulture),
            ["h"] = hardcore ? "1" : "0",
        };

        using JsonDocument? document = await PostAsync("/dorequest.php", parameters, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return new RaUserUnlocksResult { Success = false, Error = "Unable to contact RetroAchievements." };
        }

        JsonElement root = document.RootElement;
        if (!GetBool(root, "Success", false))
        {
            return new RaUserUnlocksResult { Success = false, Error = GetString(root, "Error") };
        }

        var ids = new List<uint>();
        if (root.TryGetProperty("UserUnlocks", out JsonElement unlocks)
            && unlocks.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in unlocks.EnumerateArray())
            {
                if (entry.TryGetUInt32(out uint id))
                {
                    ids.Add(id);
                }
            }
        }

        return new RaUserUnlocksResult { Success = true, AchievementIds = ids };
    }

    /// <summary>Submits a leaderboard score.</summary>
    /// <param name="leaderboardId">Leaderboard ID.</param>
    /// <param name="score">Score to submit.</param>
    /// <param name="username">Username.</param>
    /// <param name="token">API token.</param>
    /// <param name="gameHash">Disc hash.</param>
    /// <param name="secondsSinceCompletion">Optional age of an offline completion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The submission result.</returns>
    public async Task<RaLeaderboardSubmitResult> SubmitLeaderboardAsync(
        uint leaderboardId,
        int score,
        string username,
        string token,
        string gameHash,
        uint secondsSinceCompletion = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(leaderboardId);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["r"] = "submitlbentry",
            ["u"] = username,
            ["t"] = token,
            ["i"] = leaderboardId.ToString(CultureInfo.InvariantCulture),
            ["s"] = score.ToString(CultureInfo.InvariantCulture),
            ["v"] = BuildLeaderboardSignature(leaderboardId, username, score, secondsSinceCompletion),
        };

        if (!string.IsNullOrEmpty(gameHash))
        {
            parameters["m"] = gameHash;
        }

        if (secondsSinceCompletion != 0)
        {
            parameters["o"] = secondsSinceCompletion.ToString(CultureInfo.InvariantCulture);
        }

        using JsonDocument? document = await PostAsync("/dorequest.php", parameters, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return new RaLeaderboardSubmitResult { Success = false, Error = "Unable to contact RetroAchievements." };
        }

        JsonElement root = document.RootElement;
        if (!GetBool(root, "Success", false))
        {
            return new RaLeaderboardSubmitResult { Success = false, Error = GetString(root, "Error") };
        }

        JsonElement response = root.TryGetProperty("Response", out JsonElement responseElement)
            ? responseElement
            : default;

        return new RaLeaderboardSubmitResult
        {
            Success = true,
            SubmittedScore = GetInt32(response, "Score"),
            BestScore = GetInt32(response, "BestScore"),
            NewRank = response.ValueKind == JsonValueKind.Object
                && response.TryGetProperty("RankInfo", out JsonElement rank)
                ? GetUInt32(rank, "Rank")
                : 0,
            NumEntries = response.ValueKind == JsonValueKind.Object
                && response.TryGetProperty("RankInfo", out JsonElement rankInfo)
                ? GetUInt32(rankInfo, "NumEntries")
                : 0,
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private async Task<JsonDocument?> PostAsync(
        string path,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, Host + path);
        request.Content = content;
        request.Headers.UserAgent.ParseAdd(_userAgent);

        try
        {
            using HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return JsonDocument.Parse(body);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static List<RaUnlock> ParseUnlocks(JsonElement root, string propertyName)
    {
        var unlocks = new List<RaUnlock>();
        if (!root.TryGetProperty(propertyName, out JsonElement array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return unlocks;
        }

        foreach (JsonElement entry in array.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object)
            {
                unlocks.Add(new RaUnlock(GetUInt32(entry, "ID")));
            }
        }

        return unlocks;
    }

    private static string BuildAchievementSignature(
        uint achievementId,
        string username,
        bool hardcore,
        uint secondsSinceUnlock)
    {
        var text = new StringBuilder()
            .Append(achievementId.ToString(CultureInfo.InvariantCulture))
            .Append(username)
            .Append(hardcore ? "1" : "0");

        if (secondsSinceUnlock != 0)
        {
            text.Append(achievementId.ToString(CultureInfo.InvariantCulture))
                .Append(secondsSinceUnlock.ToString(CultureInfo.InvariantCulture));
        }

        return Sign(text.ToString());
    }

    private static string BuildLeaderboardSignature(
        uint leaderboardId,
        string username,
        int score,
        uint secondsSinceCompletion)
    {
        var text = new StringBuilder()
            .Append(leaderboardId.ToString(CultureInfo.InvariantCulture))
            .Append(username)
            .Append(score.ToString(CultureInfo.InvariantCulture));

        if (secondsSinceCompletion != 0)
        {
            text.Append(secondsSinceCompletion.ToString(CultureInfo.InvariantCulture));
        }

        return Sign(text.ToString());
    }

    private static string Sign(string text)
    {
        // RetroAchievements requires an MD5 request signature. This is a protocol
        // checksum, not a security boundary; the API contract fixes the algorithm.
#pragma warning disable CA5351
        byte[] digest = MD5.HashData(Encoding.UTF8.GetBytes(text));
#pragma warning restore CA5351
        return Convert.ToHexStringLower(digest);
    }

    private static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return DefaultHost;
        }

        host = host.Trim().TrimEnd('/');
        if (!host.Contains("://", StringComparison.Ordinal))
        {
            host = "https://" + host;
        }

        return host;
    }

    private static string StripImagePath(string value)
    {
        int slash = value.LastIndexOf('/');
        if (slash < 0)
        {
            return value;
        }

        string name = value[(slash + 1)..];
        int dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private static string NormalizeMissableTitle(string title)
    {
        if (title.StartsWith("[m]", StringComparison.Ordinal))
        {
            return title[3..].TrimStart();
        }

        if (title.EndsWith("[m]", StringComparison.Ordinal))
        {
            return title[..^3].TrimEnd();
        }

        return title;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.ToString(),
            _ => string.Empty,
        };
    }

    private static uint GetUInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetUInt32(out uint number))
        {
            return number;
        }

        return uint.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed)
            ? parsed
            : 0;
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int number))
        {
            return number;
        }

        return int.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
    }

    private static long GetInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long number))
        {
            return number;
        }

        return long.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : 0;
    }

    private static bool GetBool(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => property.TryGetInt32(out int number) && number != 0,
            JsonValueKind.String => bool.TryParse(property.GetString(), out bool parsed) ? parsed : defaultValue,
            _ => defaultValue,
        };
    }

    private static double GetDouble(JsonElement element, string propertyName, double defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return defaultValue;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out double number)
            ? number
            : defaultValue;
    }
}


