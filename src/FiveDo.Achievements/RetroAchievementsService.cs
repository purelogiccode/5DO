using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Achievements.Models;
using FiveDo.Achievements.Rcheevos;
using FiveDo.Core;
using Serilog;

namespace FiveDo.Achievements;

/// <summary>
/// Owns the RetroAchievements runtime for one emulator session. Call <see cref="Tick"/>
/// once per emulated frame on the emulator thread, and marshal the events to the UI thread.
/// </summary>
/// <remarks>
/// Network work runs on the thread pool. The rcheevos evaluation itself never runs off
/// the emulator thread: the game definitions are parsed on the pool, published as a
/// pending runtime, and consumed by the next <see cref="Tick"/>.
/// </remarks>
public sealed class RetroAchievementsService : IDisposable
{
    private const int PingIntervalSeconds = 120;
    private const int AwardAttempts = 5;
    private const int AwardRetryDelayMilliseconds = 5_000;
    private const uint WarningAchievementId = 101_000_000;

    private readonly IAchievementMemory _memory;
    private readonly HttpClient? _httpClient;
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _awardGate = new(1, 1);
    private readonly SemaphoreSlim _queueFlushGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly PendingUnlockQueue _pendingUnlocks;
    private readonly Task _queueTask;
    private readonly List<AchievementEntry> _entries = [];
    private readonly List<LeaderboardProgress> _leaderboards = [];
    private readonly HashSet<uint> _unlocked = [];
    private RetroAchievementsClient _client;
    private RetroAchievementsSettings _settings;
    private RetroAchievementsState _state = new();
    private RuntimeGame? _game;
    private RuntimeGame? _pendingGame;
    private string _gameHash = string.Empty;
    private string _pendingGameTitle = string.Empty;
    private string _richPresence = string.Empty;
    private DateTime _nextPingUtc = DateTime.MaxValue;
    private int _frameCounter;
    private int _loadGeneration;
    private DateTime _lastStateRaiseUtc = DateTime.MinValue;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="RetroAchievementsService"/> class.</summary>
    /// <param name="memory">The 3DO main-RAM view.</param>
    /// <param name="settings">Initial settings.</param>
    /// <param name="httpClient">Optional HTTP client for tests.</param>
    /// <param name="pendingUnlockPath">
    /// Optional path for the durable offline-unlock queue. When omitted, unlocks are only
    /// retried for the lifetime of this service instance.
    /// </param>
    public RetroAchievementsService(
        IAchievementMemory memory,
        RetroAchievementsSettings settings,
        HttpClient? httpClient = null,
        string? pendingUnlockPath = null)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(settings);

        _memory = memory;
        _settings = settings;
        _httpClient = httpClient;
        _client = new RetroAchievementsClient(settings.Host, httpClient);
        _pendingUnlocks = new PendingUnlockQueue(pendingUnlockPath);
        UpdateState(state => state with
        {
            Enabled = settings.Enabled,
            Hardcore = settings.Hardcore,
            LoggedIn = !string.IsNullOrWhiteSpace(settings.ApiToken),
            Username = settings.Username,
            PendingUnlocks = _pendingUnlocks.Count,
            Status = settings.Enabled ? "RetroAchievements ready." : "RetroAchievements disabled.",
        });

        _queueTask = Task.Run(() => RunUnlockQueueAsync(_lifetime.Token), _lifetime.Token);
    }

    /// <summary>Raised when the service state changes.</summary>
    /// <remarks>Handlers run on the emulator thread unless the change came from a network completion.</remarks>
    public event EventHandler<RetroAchievementsStateEventArgs>? StateChanged;

    /// <summary>Raised for unlock, progress, leaderboard, and error notifications.</summary>
    public event EventHandler<AchievementNotificationEventArgs>? Notification;

    /// <summary>Raised when a successful login returns a token the host should persist.</summary>
    public event EventHandler<RaLoginEventArgs>? LoginCompleted;

    /// <summary>Gets a snapshot of the current state.</summary>
    public RetroAchievementsState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Gets a copy of the current settings.</summary>
    public RetroAchievementsSettings Settings
    {
        get
        {
            lock (_gate)
            {
                return CloneSettings(_settings);
            }
        }
    }

    /// <summary>Gets a value indicating whether a game is loaded and being evaluated.</summary>
    public bool HasGame
    {
        get
        {
            lock (_gate)
            {
                return _game is not null;
            }
        }
    }

    /// <summary>Applies settings and, when needed, reconnects or restarts the session.</summary>
    /// <param name="settings">New settings.</param>
    /// <param name="restartGame">Whether to reload the current game when the host or hardcore mode changed.</param>
    public void ApplySettings(RetroAchievementsSettings settings, bool restartGame = true)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string oldHost;
        bool oldHardcore;
        bool oldEnabled;
        string oldHash;
        string oldTitle;
        lock (_gate)
        {
            oldHost = _settings.Host;
            oldHardcore = _settings.Hardcore;
            oldEnabled = _settings.Enabled;
            oldHash = _gameHash;
            oldTitle = _pendingGameTitle;
            _settings = CloneSettings(settings);
        }

        if (!string.Equals(oldHost, settings.Host, StringComparison.OrdinalIgnoreCase))
        {
            _client.Dispose();
            _client = new RetroAchievementsClient(settings.Host, _httpClient);
        }

        UpdateState(state => state with
        {
            Enabled = settings.Enabled,
            Hardcore = settings.Hardcore,
            Username = settings.Username,
            Status = settings.Enabled ? state.Status : "RetroAchievements disabled.",
        });

        if (oldEnabled && !settings.Enabled)
        {
            EndGame();
            return;
        }

        if (!oldEnabled && settings.Enabled && !string.IsNullOrWhiteSpace(oldHash))
        {
            BeginGame(oldHash, oldTitle);
            ScheduleUnlockFlush();
            return;
        }

        if (restartGame
            && settings.Enabled
            && oldEnabled
            && !string.IsNullOrWhiteSpace(oldHash)
            && (!string.Equals(oldHost, settings.Host, StringComparison.OrdinalIgnoreCase)
                || oldHardcore != settings.Hardcore))
        {
            BeginGame(oldHash, oldTitle);
        }

        if (settings.Enabled)
        {
            ScheduleUnlockFlush();
        }
    }

    /// <summary>Logs in and stores the returned token in the service settings.</summary>
    /// <param name="username">Username.</param>
    /// <param name="secret">Password or API token.</param>
    /// <param name="isToken">Whether the secret is a token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The login result.</returns>
    public async Task<RaLoginResult> LoginAsync(
        string username,
        string secret,
        bool isToken,
        CancellationToken cancellationToken = default)
    {
        RaLoginResult result = await _client
            .LoginAsync(username, secret, isToken, cancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            lock (_gate)
            {
                _settings.Username = string.IsNullOrEmpty(result.DisplayName) ? username : result.DisplayName;
                _settings.ApiToken = result.Token;
            }

            UpdateState(state => state with
            {
                LoggedIn = true,
                Username = string.IsNullOrWhiteSpace(result.DisplayName) ? username : result.DisplayName,
                Score = result.Score,
                SoftcoreScore = result.SoftcoreScore,
                Status = "Logged in to RetroAchievements.",
                Error = string.Empty,
            });

            EventHandler<RaLoginEventArgs>? loginHandler = LoginCompleted;
            if (loginHandler is not null)
            {
                var loginArgs = new RaLoginEventArgs(result);
                foreach (Delegate subscriber in loginHandler.GetInvocationList())
                {
                    try
                    {
                        ((EventHandler<RaLoginEventArgs>)subscriber).Invoke(this, loginArgs);
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        Log.Warning(exception, "RetroAchievements login handler failed");
                    }
                }
            }

            ScheduleUnlockFlush();

            string hash;
            string title;
            lock (_gate)
            {
                hash = _gameHash;
                title = _pendingGameTitle;
            }

            if (_settings.Enabled && !string.IsNullOrWhiteSpace(hash))
            {
                BeginGame(hash, title);
            }
        }
        else
        {
            UpdateState(state => state with
            {
                LoggedIn = false,
                Status = "RetroAchievements login failed.",
                Error = result.Error,
            });

            Notify(new AchievementNotification
            {
                Kind = AchievementNotificationKind.Error,
                Title = "RetroAchievements",
                Message = string.IsNullOrWhiteSpace(result.Error) ? "Login failed." : result.Error,
            });
        }

        return result;
    }

    /// <summary>Clears the session token and stops evaluating the current game.</summary>
    public void Logout()
    {
        lock (_gate)
        {
            ++_loadGeneration;
            _settings.ApiToken = string.Empty;
            _game = null;
            _pendingGame = null;
            _entries.Clear();
            _leaderboards.Clear();
            _unlocked.Clear();
        }

        UpdateState(state => state with
        {
            LoggedIn = false,
            GameLoaded = false,
            GameId = 0,
            GameTitle = string.Empty,
            RichPresence = string.Empty,
            Achievements = [],
            Leaderboards = [],
            TotalAchievements = 0,
            UnlockedAchievements = 0,
            TotalPoints = 0,
            UnlockedPoints = 0,
            Status = "Logged out of RetroAchievements.",
            Error = string.Empty,
        });
    }

    /// <summary>Identifies a disc and begins a session when logged in.</summary>
    /// <param name="disc">The loaded disc source.</param>
    /// <param name="title">Optional game title used for status messages.</param>
    /// <returns><see langword="true"/> when a 3DO hash was computed.</returns>
    public bool BeginGame(IDiscSource disc, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(disc);

        if (!ThreeDoHasher.TryHash(disc, out string hash))
        {
            UpdateState(state => state with
            {
                GameLoaded = false,
                GameHash = string.Empty,
                GameTitle = title ?? string.Empty,
                Status = "This disc is not recognized as a 3DO game.",
                Error = "3DO hash could not be computed.",
            });

            return false;
        }

        BeginGame(hash, title);
        return true;
    }

    /// <summary>Begins a session for a precomputed 3DO hash.</summary>
    /// <param name="gameHash">32-character lowercase hash.</param>
    /// <param name="title">Optional game title.</param>
    public void BeginGame(string gameHash, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameHash);

        bool enabled;
        bool loggedIn;
        int generation;
        lock (_gate)
        {
            generation = ++_loadGeneration;
            _gameHash = gameHash;
            _pendingGameTitle = title ?? string.Empty;
            _game = null;
            _pendingGame = null;
            _entries.Clear();
            _leaderboards.Clear();
            _unlocked.Clear();
            _richPresence = string.Empty;
            enabled = _settings.Enabled;
            loggedIn = !string.IsNullOrWhiteSpace(_settings.ApiToken);
        }

        UpdateState(state => state with
        {
            GameLoaded = false,
            GameId = 0,
            GameTitle = title ?? string.Empty,
            GameHash = gameHash,
            RichPresence = string.Empty,
            Achievements = [],
            Leaderboards = [],
            TotalAchievements = 0,
            UnlockedAchievements = 0,
            TotalPoints = 0,
            UnlockedPoints = 0,
            Status = enabled
                ? loggedIn ? "Identifying game..." : "Log in to load achievements."
                : "RetroAchievements disabled.",
            Error = string.Empty,
        });

        if (enabled && loggedIn)
        {
            _ = LoadGameAsync(gameHash, title ?? string.Empty, generation, _lifetime.Token);
        }
    }

    /// <summary>Stops evaluating the current game.</summary>
    public void EndGame()
    {
        lock (_gate)
        {
            ++_loadGeneration;
            _game = null;
            _pendingGame = null;
            _entries.Clear();
            _leaderboards.Clear();
            _unlocked.Clear();
            _richPresence = string.Empty;
            _nextPingUtc = DateTime.MaxValue;
        }

        UpdateState(state => state with
        {
            GameLoaded = false,
            GameId = 0,
            GameTitle = string.Empty,
            RichPresence = string.Empty,
            Achievements = [],
            Leaderboards = [],
            TotalAchievements = 0,
            UnlockedAchievements = 0,
            TotalPoints = 0,
            UnlockedPoints = 0,
            Status = "No game loaded.",
        });
    }

    /// <summary>Resets all runtime hit counts and tracked progress. Call after a save-state load or rewind.</summary>
    public void ResetRuntime()
    {
        lock (_gate)
        {
            if (_game is null)
            {
                return;
            }

            _game.Runtime.Reset();
            foreach (AchievementEntry entry in _entries)
            {
                if (!entry.Unlocked)
                {
                    entry.MeasuredValue = 0;
                }
            }

            for (int index = 0; index < _leaderboards.Count; index++)
            {
                _leaderboards[index] = new LeaderboardProgress
                {
                    Id = _leaderboards[index].Id,
                    Title = _leaderboards[index].Title,
                    Value = 0,
                    IsActive = false,
                };
            }
        }

        RaiseAchievementState(force: true);
    }

    /// <summary>
    /// Captures the current achievement and leaderboard progress for a 5DO save state.
    /// </summary>
    /// <returns>The progress payload, or an empty array when no game is loaded.</returns>
    /// <remarks>Call on the emulation thread; the runtime is not thread-safe.</remarks>
    public byte[] ExportProgress()
    {
        lock (_gate)
        {
            return _game is null
                ? []
                : RcProgressCodec.Serialize(_game.Hash, _game.Runtime);
        }
    }

    /// <summary>
    /// Restores achievement and leaderboard progress from a 5DO save state.
    /// </summary>
    /// <param name="progress">The section payload captured by <see cref="ExportProgress"/>.</param>
    /// <returns><see langword="true"/> when the progress matched the loaded game and was applied.</returns>
    /// <remarks>
    /// The payload is discarded when the game hash or any definition differs. Call on the
    /// emulation thread and use <see cref="ResetRuntime"/> when this returns false.
    /// </remarks>
    public bool ImportProgress(ReadOnlySpan<byte> progress)
    {
        RuntimeGame? game;
        lock (_gate)
        {
            game = _game;
        }

        if (game is null
            || progress.IsEmpty
            || !RcProgressCodec.TryDeserialize(progress, game.Hash, game.Runtime))
        {
            return false;
        }

        lock (_gate)
        {
            foreach (AchievementEntry entry in _entries)
            {
                RcAchievement? achievement = game.Runtime.GetAchievement(entry.Definition.Id);
                if (achievement is null)
                {
                    continue;
                }

                if (achievement.Trigger.State == RcTriggerState.Triggered)
                {
                    entry.Unlocked = true;
                    _unlocked.Add(entry.Definition.Id);
                    entry.MeasuredValue = achievement.Trigger.MeasuredTarget;
                }
                else if (!entry.Unlocked)
                {
                    entry.MeasuredValue = achievement.Trigger.MeasuredTarget == 0
                        || achievement.Trigger.MeasuredValue == RcTrigger.MeasuredUnknown
                        ? 0
                        : achievement.Trigger.MeasuredValue;
                }
            }

            for (int index = 0; index < _leaderboards.Count; index++)
            {
                RcLeaderboardEntry? leaderboard = game.Runtime.GetLeaderboard(_leaderboards[index].Id);
                if (leaderboard is null)
                {
                    continue;
                }

                _leaderboards[index] = new LeaderboardProgress
                {
                    Id = _leaderboards[index].Id,
                    Title = _leaderboards[index].Title,
                    Value = leaderboard.Value,
                    IsActive = leaderboard.Leaderboard.State == RcLeaderboardState.Started,
                };
            }
        }

        RaiseAchievementState(force: true);
        return true;
    }

    /// <summary>Evaluates achievements, leaderboards, and rich presence for one frame.</summary>
    public void Tick()
    {
        RuntimeGame? published = null;
        RuntimeGame? runtimeGame;
        lock (_gate)
        {
            if (_pendingGame is not null)
            {
                published = _pendingGame;
                _pendingGame = null;
                _game = published;
            }

            runtimeGame = _game;
        }

        if (published is not null)
        {
            OnGamePublished(published);
            runtimeGame = published;
        }

        if (runtimeGame is null || _disposed)
        {
            return;
        }

        try
        {
            runtimeGame.Runtime.DoFrame(
                (address, numBytes, _) => _memory.Peek(address, numBytes),
                null,
                OnRuntimeEvent);

            if (++_frameCounter % 30 == 0)
            {
                string richPresence = runtimeGame.Runtime.GetRichPresence(
                    (address, numBytes, _) => _memory.Peek(address, numBytes),
                    null);

                if (!string.Equals(richPresence, _richPresence, StringComparison.Ordinal))
                {
                    _richPresence = richPresence;
                    UpdateState(state => state with { RichPresence = richPresence });
                }
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "RetroAchievements evaluation failed; detaching the runtime");

            lock (_gate)
            {
                _game = null;
                _entries.Clear();
                _leaderboards.Clear();
                _richPresence = string.Empty;
            }

            UpdateState(state => state with
            {
                GameLoaded = false,
                GameId = 0,
                Achievements = [],
                Leaderboards = [],
                TotalAchievements = 0,
                UnlockedAchievements = 0,
                TotalPoints = 0,
                UnlockedPoints = 0,
                Status = "RetroAchievements stopped after an evaluation error.",
                Error = exception.Message,
            });

            Notify(new AchievementNotification
            {
                Kind = AchievementNotificationKind.Error,
                Title = "RetroAchievements",
                Message = "Achievement evaluation failed. Emulation continues without achievements.",
            });
            return;
        }

        if (DateTime.UtcNow >= _nextPingUtc)
        {
            _nextPingUtc = DateTime.UtcNow.AddSeconds(PingIntervalSeconds);
            _ = PingAsync(runtimeGame, _lifetime.Token);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _client.Dispose();
        try
        {
            _queueTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _lifetime.Dispose();
        _awardGate.Dispose();
        _queueFlushGate.Dispose();
    }

    private async Task LoadGameAsync(
        string hash,
        string title,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!IsCurrentLoad(generation, hash))
            {
                return;
            }

            UpdateState(state => state with { Status = "Identifying game..." });

            (string username, string token, bool hardcore) = SnapshotCredentials();
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            RaGameIdResult identity = await _client
                .ResolveHashAsync(hash, cancellationToken)
                .ConfigureAwait(false);

            if (!IsCurrentLoad(generation, hash))
            {
                return;
            }

            if (!identity.Success)
            {
                Fail(string.IsNullOrWhiteSpace(identity.Error)
                    ? "RetroAchievements could not identify this game."
                    : identity.Error);
                return;
            }

            if (identity.GameId == 0)
            {
                UpdateState(state => state with
                {
                    GameLoaded = false,
                    Status = "No achievements for this disc.",
                });

                Notify(new AchievementNotification
                {
                    Kind = AchievementNotificationKind.GameNotIdentified,
                    Title = "RetroAchievements",
                    Message = string.IsNullOrWhiteSpace(title)
                        ? "This disc has no RetroAchievements set."
                        : $"{title} has no RetroAchievements set.",
                });

                return;
            }

            RaGameData? game = await _client
                .FetchGameAsync(identity.GameId, username, token, cancellationToken)
                .ConfigureAwait(false);

            if (!IsCurrentLoad(generation, hash))
            {
                return;
            }

            if (game is null)
            {
                Fail("RetroAchievements game data could not be loaded.");
                return;
            }

            RaSessionResult session = await _client
                .StartSessionAsync(identity.GameId, hash, username, token, hardcore, cancellationToken)
                .ConfigureAwait(false);

            if (!IsCurrentLoad(generation, hash))
            {
                return;
            }

            if (!session.Success)
            {
                Fail(string.IsNullOrWhiteSpace(session.Error)
                    ? "RetroAchievements session could not be started."
                    : session.Error);
                return;
            }

            // Hardcore and casual unlocks are tracked separately by RetroAchievements.
            // A casual-only unlock must stay active in a hardcore session so the player
            // can earn the hardcore version, while a hardcore unlock counts for both.
            var unlocked = new HashSet<uint>();
            if (hardcore)
            {
                foreach (RaUnlock unlock in session.HardcoreUnlocks)
                {
                    unlocked.Add(unlock.AchievementId);
                }
            }
            else
            {
                foreach (RaUnlock unlock in session.Unlocks.Concat(session.HardcoreUnlocks))
                {
                    unlocked.Add(unlock.AchievementId);
                }
            }

            // A queued award may already have reached the server through another client.
            foreach (RaUnlock unlock in session.Unlocks)
            {
                _pendingUnlocks.Remove(unlock.AchievementId, username, hardcore: false);
            }

            foreach (RaUnlock unlock in session.HardcoreUnlocks)
            {
                _pendingUnlocks.Remove(unlock.AchievementId, username, hardcore: false);
                _pendingUnlocks.Remove(unlock.AchievementId, username, hardcore: true);
            }

            UpdateState(state => state with { PendingUnlocks = _pendingUnlocks.Count });

            var runtime = new RcRuntime();
            var entries = new List<AchievementEntry>();
            foreach (RaAchievement achievement in game.Achievements)
            {
                // Warning achievements are client-generated, zero-point notices. rcheevos'
                // rc_client ignores them for the summary and never activates them.
                if (achievement.Id >= WarningAchievementId)
                {
                    continue;
                }

                bool isUnlocked = unlocked.Contains(achievement.Id);
                bool unsupported = false;

                if (achievement.IsOfficial && !isUnlocked)
                {
                    RcError error = runtime.ActivateAchievement(achievement.Id, achievement.MemAddr);
                    if (error != RcError.Ok)
                    {
                        unsupported = true;
                        Log.Warning(
                            "RetroAchievements achievement {AchievementId} has an unsupported definition: {Error}",
                            achievement.Id,
                            error);
                    }
                }

                entries.Add(new AchievementEntry
                {
                    Definition = achievement,
                    Unlocked = isUnlocked,
                    Unsupported = unsupported,
                    Hardcore = hardcore,
                });
            }

            var leaderboardDefinitions = new Dictionary<uint, RaLeaderboardDefinition>();
            foreach (RaLeaderboardDefinition leaderboard in game.Leaderboards)
            {
                if (runtime.ActivateLeaderboard(leaderboard.Id, leaderboard.Mem) == RcError.Ok)
                {
                    leaderboardDefinitions[leaderboard.Id] = leaderboard;
                }
            }

            if (!string.IsNullOrWhiteSpace(game.RichPresence))
            {
                RcError richPresenceError = runtime.ActivateRichPresence(game.RichPresence);
                if (richPresenceError != RcError.Ok)
                {
                    Log.Warning(
                        "RetroAchievements rich-presence script for game {GameId} failed to parse: {Error}",
                        game.GameId,
                        richPresenceError);
                }
            }

            var runtimeGame = new RuntimeGame
            {
                Game = game,
                Runtime = runtime,
                Entries = entries,
                LeaderboardDefinitions = leaderboardDefinitions,
                Hardcore = hardcore,
                Hash = hash,
            };

            lock (_gate)
            {
                if (generation != _loadGeneration
                    || !string.Equals(_gameHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _pendingGame = runtimeGame;
                _pendingGameTitle = game.Title;
                _nextPingUtc = DateTime.UtcNow.AddSeconds(5);
            }

            Log.Information(
                "RetroAchievements loaded {GameTitle} ({GameId}): {AchievementCount} achievements, {LeaderboardCount} leaderboards",
                game.Title,
                game.GameId,
                game.Achievements.Count,
                game.Leaderboards.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.Error(exception, "RetroAchievements failed to load the current game");
            Fail("RetroAchievements failed to load the current game.");
        }
    }

    private void OnGamePublished(RuntimeGame game)
    {
        lock (_gate)
        {
            _entries.Clear();
            _entries.AddRange(game.Entries);
            _leaderboards.Clear();
            foreach ((uint id, RaLeaderboardDefinition definition) in game.LeaderboardDefinitions)
            {
                _leaderboards.Add(new LeaderboardProgress
                {
                    Id = id,
                    Title = definition.Title,
                    IsActive = false,
                });
            }

            _unlocked.Clear();
            foreach (AchievementEntry entry in _entries)
            {
                if (entry.Unlocked)
                {
                    _unlocked.Add(entry.Definition.Id);
                }
            }
        }

        UpdateState(state => state with
        {
            GameLoaded = true,
            GameId = game.Game.GameId,
            GameTitle = game.Game.Title,
            RichPresence = string.Empty,
            Status = "Achievements loaded.",
            Error = string.Empty,
        });

        RaiseAchievementState(force: true);

        Notify(new AchievementNotification
        {
            Kind = AchievementNotificationKind.GameIdentified,
            Title = game.Game.Title,
            Message = $"{_entries.Count(entry => entry.Definition.IsOfficial)} achievements loaded.",
            Hardcore = game.Hardcore,
        });

        // A session is now active, so any queued awards for this game can be retried.
        ScheduleUnlockFlush();
    }

    private void OnRuntimeEvent(RcRuntimeEvent runtimeEvent)
    {
        RuntimeGame? game;
        lock (_gate)
        {
            game = _game;
        }

        if (game is null)
        {
            return;
        }

        switch (runtimeEvent.Type)
        {
            case RcRuntimeEventType.AchievementTriggered:
                OnAchievementUnlocked(game, runtimeEvent.Id);
                break;

            case RcRuntimeEventType.AchievementProgressUpdated:
            case RcRuntimeEventType.AchievementReset:
                UpdateAchievementMeasurement(game, runtimeEvent.Id);
                break;

            case RcRuntimeEventType.LeaderboardStarted:
                UpdateLeaderboard(runtimeEvent.Id, runtimeEvent.Value, active: true);
                if (game.Hardcore && _settings.ShowLeaderboardNotifications)
                {
                    string title = GetLeaderboardTitle(game, runtimeEvent.Id);
                    Notify(new AchievementNotification
                    {
                        Kind = AchievementNotificationKind.LeaderboardStarted,
                        Title = title,
                        Message = "Leaderboard attempt started.",
                        Hardcore = true,
                    });
                }

                break;

            case RcRuntimeEventType.LeaderboardUpdated:
                UpdateLeaderboard(runtimeEvent.Id, runtimeEvent.Value, active: true);
                break;

            case RcRuntimeEventType.LeaderboardCanceled:
                UpdateLeaderboard(runtimeEvent.Id, 0, active: false);
                if (game.Hardcore && _settings.ShowLeaderboardNotifications)
                {
                    Notify(new AchievementNotification
                    {
                        Kind = AchievementNotificationKind.LeaderboardCanceled,
                        Title = GetLeaderboardTitle(game, runtimeEvent.Id),
                        Message = "Leaderboard attempt canceled.",
                        Hardcore = true,
                    });
                }

                break;

            case RcRuntimeEventType.LeaderboardTriggered:
                UpdateLeaderboard(runtimeEvent.Id, 0, active: false);

                // RetroAchievements only accepts leaderboard entries from hardcore
                // sessions. Keep tracking in casual mode without submitting.
                if (!game.Hardcore)
                {
                    break;
                }

                _ = SubmitLeaderboardAsync(game, runtimeEvent.Id, runtimeEvent.Value, _lifetime.Token);
                break;
        }
    }

    private void OnAchievementUnlocked(RuntimeGame game, uint achievementId)
    {
        AchievementEntry? entry;
        lock (_gate)
        {
            if (!_unlocked.Add(achievementId))
            {
                return;
            }

            entry = _entries.FirstOrDefault(candidate => candidate.Definition.Id == achievementId);
            if (entry is not null)
            {
                entry.Unlocked = true;
                entry.MeasuredValue = entry.MeasuredTarget;
            }
        }

        if (entry is null)
        {
            return;
        }

        UpdateAchievementMeasurement(game, achievementId);
        RaiseAchievementState(force: true);

        Notify(new AchievementNotification
        {
            Kind = AchievementNotificationKind.AchievementUnlocked,
            Title = entry.Definition.Title,
            Message = entry.Definition.Description,
            BadgeUrl = entry.Definition.BadgeUrl,
            AchievementId = achievementId,
            Points = entry.Definition.Points,
            Hardcore = game.Hardcore,
        });

        (string username, _, _) = SnapshotCredentials();
        var pending = new PendingUnlock(
            achievementId,
            game.Game.GameId,
            game.Hash,
            username,
            game.Hardcore,
            DateTimeOffset.UtcNow,
            entry.Definition.Title,
            entry.Definition.Points);

        _pendingUnlocks.Add(pending);
        UpdateState(state => state with { PendingUnlocks = _pendingUnlocks.Count });
        _ = AwardAsync(pending, _lifetime.Token);
    }

    private void UpdateAchievementMeasurement(RuntimeGame game, uint achievementId)
    {
        if (!game.Runtime.GetAchievementMeasured(achievementId, out uint measuredValue, out uint measuredTarget))
        {
            return;
        }

        AchievementEntry? entry;
        lock (_gate)
        {
            entry = _entries.FirstOrDefault(candidate => candidate.Definition.Id == achievementId);
            if (entry is null)
            {
                return;
            }

            entry.MeasuredValue = measuredValue;
            entry.MeasuredTarget = measuredTarget;
        }

        if (_settings.ShowProgressNotifications
            && measuredTarget != 0
            && !entry.Unlocked)
        {
            Notify(new AchievementNotification
            {
                Kind = AchievementNotificationKind.AchievementProgress,
                Title = entry.Definition.Title,
                Message = $"{measuredValue}/{measuredTarget}",
                BadgeUrl = entry.Definition.BadgeLockedUrl,
                AchievementId = achievementId,
                Hardcore = game.Hardcore,
            });
        }

        RaiseAchievementState();
    }

    private void UpdateLeaderboard(uint leaderboardId, int value, bool active)
    {
        lock (_gate)
        {
            for (int index = 0; index < _leaderboards.Count; index++)
            {
                if (_leaderboards[index].Id != leaderboardId)
                {
                    continue;
                }

                _leaderboards[index] = new LeaderboardProgress
                {
                    Id = leaderboardId,
                    Title = _leaderboards[index].Title,
                    Value = value,
                    IsActive = active,
                };
                break;
            }
        }

        UpdateState(state => state with { Leaderboards = BuildLeaderboardState() });
    }

    private async Task AwardAsync(
        PendingUnlock pending,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return;
        }

        (string username, string token, _) = SnapshotCredentials();
        if (string.IsNullOrEmpty(token)
            || !string.Equals(username, pending.Username, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _awardGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_pendingUnlocks.Contains(pending.AchievementId, pending.Username, pending.Hardcore))
            {
                return;
            }

            string error = string.Empty;
            for (int attempt = 0; attempt < AwardAttempts; attempt++)
            {
                RaAwardResult result = await _client
                    .AwardAchievementAsync(
                        pending.AchievementId,
                        pending.Username,
                        token,
                        pending.Hardcore,
                        pending.GameHash,
                        SecondsSince(pending.UnlockedAtUtc),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (result.Success)
                {
                    _pendingUnlocks.Remove(pending.AchievementId, pending.Username, pending.Hardcore);
                    UpdateState(state => state with
                    {
                        Score = result.NewScore != 0 ? result.NewScore : state.Score,
                        SoftcoreScore = result.NewSoftcoreScore != 0 ? result.NewSoftcoreScore : state.SoftcoreScore,
                        PendingUnlocks = _pendingUnlocks.Count,
                    });
                    return;
                }

                error = result.Error;
                if (!IsTransientAwardError(error))
                {
                    if (IsPermanentAwardError(error))
                    {
                        _pendingUnlocks.Remove(pending.AchievementId, pending.Username, pending.Hardcore);
                        UpdateState(state => state with { PendingUnlocks = _pendingUnlocks.Count });
                    }

                    break;
                }

                await Task.Delay(AwardRetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            Log.Warning(
                "RetroAchievements could not award achievement {AchievementId}; it remains queued: {Error}",
                pending.AchievementId,
                error);

            Notify(new AchievementNotification
            {
                Kind = AchievementNotificationKind.Error,
                Title = "RetroAchievements",
                Message = string.IsNullOrWhiteSpace(error)
                    ? $"Achievement {pending.AchievementId} could not be synced and remains queued."
                    : $"{error} The unlock remains queued.",
                AchievementId = pending.AchievementId,
                Hardcore = pending.Hardcore,
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.Error(exception, "RetroAchievements award request failed; the unlock remains queued");
        }
        finally
        {
            _awardGate.Release();
        }
    }

    private async Task SubmitLeaderboardAsync(
        RuntimeGame game,
        uint leaderboardId,
        int value,
        CancellationToken cancellationToken)
    {
        if (!game.Hardcore || !IsEnabled())
        {
            return;
        }

        (string username, string token, _) = SnapshotCredentials();
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        try
        {
            RaLeaderboardSubmitResult result = await _client
                .SubmitLeaderboardAsync(
                    leaderboardId,
                    value,
                    username,
                    token,
                    game.Hash,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                Log.Warning(
                    "RetroAchievements leaderboard {LeaderboardId} submission failed: {Error}",
                    leaderboardId,
                    result.Error);
            }
            else
            {
                Log.Information(
                    "RetroAchievements leaderboard {LeaderboardId} submitted {Score} (best {BestScore}, rank {Rank}/{Entries})",
                    leaderboardId,
                    result.SubmittedScore,
                    result.BestScore,
                    result.NewRank,
                    result.NumEntries);

                if (_settings.ShowLeaderboardNotifications)
                {
                    Notify(new AchievementNotification
                    {
                        Kind = AchievementNotificationKind.LeaderboardSubmitted,
                        Title = GetLeaderboardTitle(game, leaderboardId),
                        Message = result.NumEntries > 0
                            ? $"Score {result.SubmittedScore} submitted (rank {result.NewRank}/{result.NumEntries})."
                            : $"Score {result.SubmittedScore} submitted.",
                        Hardcore = true,
                    });
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.Error(exception, "RetroAchievements leaderboard submission failed");
        }
    }

    private async Task PingAsync(RuntimeGame game, CancellationToken cancellationToken)
    {
        (string username, string token, bool hardcore) = SnapshotCredentials();
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        string richPresence;
        lock (_gate)
        {
            // Hardcore compliance requires rich presence to be submitted even when the
            // user has hidden the local display.
            richPresence = _settings.Hardcore || _settings.ShowRichPresence
                ? _richPresence
                : string.Empty;
        }

        try
        {
            RaPingResult result = await _client
                .PingAsync(game.Game.GameId, game.Hash, username, token, hardcore, richPresence, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
            {
                Log.Debug("RetroAchievements ping failed: {Error}", result.Error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "RetroAchievements ping failed");
        }
    }

    private void Fail(string message)
    {
        UpdateState(state => state with
        {
            GameLoaded = false,
            Status = "RetroAchievements unavailable.",
            Error = message,
        });

        Notify(new AchievementNotification
        {
            Kind = AchievementNotificationKind.Error,
            Title = "RetroAchievements",
            Message = message,
        });
    }

    private void RaiseAchievementState(bool force = false)
    {
        DateTime now = DateTime.UtcNow;
        if (!force && (now - _lastStateRaiseUtc).TotalMilliseconds < 250)
        {
            return;
        }

        uint totalPoints = 0;
        uint unlockedPoints = 0;
        foreach (AchievementEntry entry in _entries)
        {
            if (!entry.Definition.IsOfficial)
            {
                continue;
            }

            totalPoints += entry.Definition.Points;
            if (entry.Unlocked)
            {
                unlockedPoints += entry.Definition.Points;
            }
        }

        _lastStateRaiseUtc = now;
        UpdateState(state => state with
        {
            Achievements = BuildAchievementState(),
            Leaderboards = BuildLeaderboardState(),
            TotalAchievements = _entries.Count(entry => entry.Definition.IsOfficial),
            UnlockedAchievements = _entries.Count(entry => entry.Definition.IsOfficial && entry.Unlocked),
            TotalPoints = totalPoints,
            UnlockedPoints = unlockedPoints,
        });
    }

    private List<AchievementProgress> BuildAchievementState()
    {
        var achievements = new List<AchievementProgress>(_entries.Count);
        foreach (AchievementEntry entry in _entries)
        {
            string progressText = string.Empty;
            if (!entry.Unlocked && entry.MeasuredTarget != 0)
            {
                progressText = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entry.MeasuredValue}/{entry.MeasuredTarget}");
            }

            achievements.Add(new AchievementProgress
            {
                Id = entry.Definition.Id,
                Title = entry.Definition.Title,
                Description = entry.Definition.Description,
                Points = entry.Definition.Points,
                BadgeUrl = entry.Definition.BadgeUrl,
                BadgeLockedUrl = entry.Definition.BadgeLockedUrl,
                IsOfficial = entry.Definition.IsOfficial,
                Unlocked = entry.Unlocked,
                IsUnsupported = entry.Unsupported,
                MeasuredValue = entry.MeasuredValue,
                MeasuredTarget = entry.MeasuredTarget,
                ProgressText = progressText,
                Rarity = entry.Hardcore ? entry.Definition.RarityHardcore : entry.Definition.Rarity,
            });
        }

        return achievements;
    }

    private List<LeaderboardProgress> BuildLeaderboardState()
    {
        var leaderboards = new List<LeaderboardProgress>(_leaderboards.Count);
        foreach (LeaderboardProgress leaderboard in _leaderboards)
        {
            leaderboards.Add(new LeaderboardProgress
            {
                Id = leaderboard.Id,
                Title = leaderboard.Title,
                Value = leaderboard.Value,
                IsActive = leaderboard.IsActive,
            });
        }

        return leaderboards;
    }

    private bool IsCurrentLoad(int generation, string hash)
    {
        lock (_gate)
        {
            return generation == _loadGeneration
                && string.Equals(_gameHash, hash, StringComparison.OrdinalIgnoreCase);
        }
    }

    private (string Username, string Token, bool Hardcore) SnapshotCredentials()
    {
        lock (_gate)
        {
            return (_settings.Username, _settings.ApiToken, _settings.Hardcore);
        }
    }

    private bool IsEnabled()
    {
        lock (_gate)
        {
            return _settings.Enabled;
        }
    }

    private string GetActiveGameHash()
    {
        lock (_gate)
        {
            return _game is null ? string.Empty : _gameHash;
        }
    }

    private string GetLeaderboardTitle(RuntimeGame game, uint leaderboardId) =>
        game.LeaderboardDefinitions.TryGetValue(leaderboardId, out RaLeaderboardDefinition? definition)
            ? definition.Title
            : $"Leaderboard {leaderboardId}";

    private void UpdateState(Func<RetroAchievementsState, RetroAchievementsState> update)
    {
        RetroAchievementsState state;
        lock (_gate)
        {
            _state = update(_state);
            state = _state;
        }

        EventHandler<RetroAchievementsStateEventArgs>? handler = StateChanged;
        if (handler is null)
        {
            return;
        }

        var args = new RetroAchievementsStateEventArgs(state);
        foreach (Delegate subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<RetroAchievementsStateEventArgs>)subscriber).Invoke(this, args);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Achievement state must never fault emulation or the network loop.
                Log.Warning(exception, "RetroAchievements state-change handler failed");
            }
        }
    }

    private void Notify(AchievementNotification notification)
    {
        EventHandler<AchievementNotificationEventArgs>? handler = Notification;
        if (handler is null)
        {
            return;
        }

        var args = new AchievementNotificationEventArgs(notification);
        foreach (Delegate subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<AchievementNotificationEventArgs>)subscriber).Invoke(this, args);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // A notification handler (for example a closing UI window) must not
                // interrupt frame evaluation.
                Log.Debug(exception, "RetroAchievements notification handler failed");
            }
        }
    }

    private void ScheduleUnlockFlush()
    {
        if (_disposed)
        {
            return;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await FlushPendingUnlocksOnceAsync(_lifetime.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    Log.Debug(exception, "RetroAchievements unlock-queue flush failed");
                }
            },
            _lifetime.Token);
    }

    private async Task RunUnlockQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await FlushPendingUnlocksOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Log.Debug(exception, "RetroAchievements pending-unlock queue flush failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>Attempts to submit every queued unlock for the signed-in user.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>Exposed internally for deterministic tests; production uses the 30-second loop.</remarks>
    internal async Task FlushPendingUnlocksOnceAsync(CancellationToken cancellationToken)
    {
        _pendingUnlocks.Prune();
        if (_pendingUnlocks.Count == 0)
        {
            if (State.PendingUnlocks != 0)
            {
                UpdateState(state => state with { PendingUnlocks = 0 });
            }

            return;
        }

        if (!IsEnabled())
        {
            return;
        }

        string activeHash = GetActiveGameHash();
        if (string.IsNullOrEmpty(activeHash))
        {
            // Awards require an active game session. Queued entries stay durable until
            // the matching game is started again.
            return;
        }

        (string username, string token, _) = SnapshotCredentials();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token))
        {
            return;
        }

        if (!await _queueFlushGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            foreach (PendingUnlock pending in _pendingUnlocks.Snapshot())
            {
                if (!string.Equals(pending.GameHash, activeHash, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(pending.Username, username, StringComparison.OrdinalIgnoreCase)
                    || !_pendingUnlocks.Contains(pending.AchievementId, pending.Username, pending.Hardcore))
                {
                    continue;
                }

                await _awardGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (!_pendingUnlocks.Contains(pending.AchievementId, pending.Username, pending.Hardcore))
                    {
                        continue;
                    }

                    RaAwardResult result = await _client
                        .AwardAchievementAsync(
                            pending.AchievementId,
                            pending.Username,
                            token,
                            pending.Hardcore,
                            pending.GameHash,
                            SecondsSince(pending.UnlockedAtUtc),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (result.Success)
                    {
                        _pendingUnlocks.Remove(pending.AchievementId, pending.Username, pending.Hardcore);
                        Log.Information(
                            "RetroAchievements synced queued unlock {AchievementId} ({Title})",
                            pending.AchievementId,
                            pending.Title);
                        UpdateState(state => state with
                        {
                            Score = result.NewScore != 0 ? result.NewScore : state.Score,
                            SoftcoreScore = result.NewSoftcoreScore != 0 ? result.NewSoftcoreScore : state.SoftcoreScore,
                            PendingUnlocks = _pendingUnlocks.Count,
                        });
                    }
                    else if (IsPermanentAwardError(result.Error))
                    {
                        _pendingUnlocks.Remove(pending.AchievementId, pending.Username, pending.Hardcore);
                        Log.Warning(
                            "RetroAchievements dropped rejected queued unlock {AchievementId}: {Error}",
                            pending.AchievementId,
                            result.Error);
                        UpdateState(state => state with { PendingUnlocks = _pendingUnlocks.Count });
                    }
                    else
                    {
                        Log.Debug(
                            "RetroAchievements queued unlock {AchievementId} will retry: {Error}",
                            pending.AchievementId,
                            result.Error);
                        break;
                    }
                }
                finally
                {
                    _awardGate.Release();
                }
            }
        }
        finally
        {
            _queueFlushGate.Release();
        }
    }

    private static uint SecondsSince(DateTimeOffset unlockedAt)
    {
        double seconds = (DateTimeOffset.UtcNow - unlockedAt).TotalSeconds;
        return seconds <= 0
            ? 0
            : seconds >= uint.MaxValue ? uint.MaxValue : (uint)seconds;
    }

    private static bool IsTransientAwardError(string error) =>
        string.IsNullOrWhiteSpace(error)
        || error.Contains("unable to contact", StringComparison.OrdinalIgnoreCase)
        || error.Contains("service unavailable", StringComparison.OrdinalIgnoreCase)
        || error.Contains("server error", StringComparison.OrdinalIgnoreCase)
        || error.Contains("bad gateway", StringComparison.OrdinalIgnoreCase)
        || error.Contains("timeout", StringComparison.OrdinalIgnoreCase)
        || error.Contains("temporar", StringComparison.OrdinalIgnoreCase)
        || error.Contains("try again", StringComparison.OrdinalIgnoreCase)
        || error.Contains("no session", StringComparison.OrdinalIgnoreCase)
        || error.Contains("invalid token", StringComparison.OrdinalIgnoreCase)
        || error.Contains("login", StringComparison.OrdinalIgnoreCase);

    private static bool IsPermanentAwardError(string error) =>
        error.Contains("invalid achievement", StringComparison.OrdinalIgnoreCase)
        || error.Contains("achievement not found", StringComparison.OrdinalIgnoreCase)
        || error.Contains("does not exist", StringComparison.OrdinalIgnoreCase);

    private static RetroAchievementsSettings CloneSettings(RetroAchievementsSettings settings) => new()
    {
        Enabled = settings.Enabled,
        Username = settings.Username,
        ApiToken = settings.ApiToken,
        Hardcore = settings.Hardcore,
        ShowNotifications = settings.ShowNotifications,
        ShowProgressNotifications = settings.ShowProgressNotifications,
        ShowRichPresence = settings.ShowRichPresence,
        ShowLeaderboardNotifications = settings.ShowLeaderboardNotifications,
        Host = settings.Host,
    };

    private sealed class AchievementEntry
    {
        public required RaAchievement Definition { get; init; }

        public bool Unlocked { get; set; }

        public bool Unsupported { get; set; }

        public bool Hardcore { get; init; }

        public uint MeasuredValue { get; set; }

        public uint MeasuredTarget { get; set; }
    }

    private sealed class RuntimeGame
    {
        public required RaGameData Game { get; init; }

        public required RcRuntime Runtime { get; init; }

        public required List<AchievementEntry> Entries { get; init; }

        public required Dictionary<uint, RaLeaderboardDefinition> LeaderboardDefinitions { get; init; }

        public required bool Hardcore { get; init; }

        public required string Hash { get; init; }
    }
}
