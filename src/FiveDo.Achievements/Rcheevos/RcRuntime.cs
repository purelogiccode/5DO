// Ported from rcheevos (MIT) - src/rcheevos/runtime.c.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FiveDo.Achievements.Rcheevos;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RcRuntimeEvent(uint Id, int Value, RcRuntimeEventType Type);

internal sealed class RcAchievement
{
    public required uint Id { get; init; }

    public required string Definition { get; init; }

    public required RcTrigger Trigger { get; init; }
}

internal sealed class RcLeaderboardEntry
{
    public required uint Id { get; init; }

    public required string Definition { get; init; }

    public required RcLeaderboard Leaderboard { get; init; }

    public int Value { get; set; }
}

/// <summary>
/// The runtime container for achievements, leaderboards, and rich presence. This is a
/// managed equivalent of <c>rc_runtime_t</c> with one memref set per parsed definition.
/// </summary>
internal sealed class RcRuntime
{
    private readonly List<RcAchievement> _achievements = [];
    private readonly List<RcLeaderboardEntry> _leaderboards = [];
    private RcRichPresence? _richPresence;
    private string? _richPresenceDefinition;

    public IReadOnlyList<RcAchievement> Achievements => _achievements;

    public IReadOnlyList<RcLeaderboardEntry> Leaderboards => _leaderboards;

    public RcError ActivateAchievement(uint id, string memaddr)
    {
        foreach (RcAchievement achievement in _achievements)
        {
            if (achievement.Id != id)
            {
                continue;
            }

            if (string.Equals(achievement.Definition, memaddr, StringComparison.Ordinal))
            {
                achievement.Trigger.ResetTrigger();
                return RcError.Ok;
            }

            DeactivateAchievement(id);
            break;
        }

        try
        {
            RcTrigger trigger = RcTrigger.Parse(memaddr);
            _achievements.Add(new RcAchievement
            {
                Id = id,
                Definition = memaddr,
                Trigger = trigger,
            });

            trigger.ResetTrigger();
            return RcError.Ok;
        }
        catch (RcParseException exception)
        {
            return exception.Error;
        }
    }

    public void DeactivateAchievement(uint id)
    {
        for (int index = _achievements.Count - 1; index >= 0; index--)
        {
            if (_achievements[index].Id == id)
            {
                _achievements.RemoveAt(index);
            }
        }
    }

    public RcAchievement? GetAchievement(uint id)
    {
        foreach (RcAchievement achievement in _achievements)
        {
            if (achievement.Id == id)
            {
                return achievement;
            }
        }

        return null;
    }

    public bool GetAchievementMeasured(uint id, out uint measuredValue, out uint measuredTarget)
    {
        RcAchievement? achievement = GetAchievement(id);
        if (achievement is null)
        {
            measuredValue = 0;
            measuredTarget = 0;
            return false;
        }

        RcTrigger trigger = achievement.Trigger;
        if (RcTrigger.IsActiveState(trigger.State))
        {
            measuredValue = trigger.MeasuredValue == RcTrigger.MeasuredUnknown ? 0 : trigger.MeasuredValue;
            measuredTarget = trigger.MeasuredTarget;
        }
        else
        {
            measuredValue = 0;
            measuredTarget = 0;
        }

        return true;
    }

    public string FormatAchievementMeasured(uint id) =>
        GetAchievement(id)?.Trigger.FormatMeasured() ?? string.Empty;

    public RcError ActivateLeaderboard(uint id, string memaddr)
    {
        if (string.IsNullOrEmpty(memaddr))
        {
            return RcError.InvalidMemoryOperand;
        }

        foreach (RcLeaderboardEntry entry in _leaderboards)
        {
            if (entry.Id != id)
            {
                continue;
            }

            if (string.Equals(entry.Definition, memaddr, StringComparison.Ordinal))
            {
                entry.Leaderboard.Reset();
                return RcError.Ok;
            }

            DeactivateLeaderboard(id);
            break;
        }

        try
        {
            RcLeaderboard leaderboard = RcLeaderboard.Parse(memaddr);
            _leaderboards.Add(new RcLeaderboardEntry
            {
                Id = id,
                Definition = memaddr,
                Leaderboard = leaderboard,
            });

            leaderboard.Reset();
            return RcError.Ok;
        }
        catch (RcParseException exception)
        {
            return exception.Error;
        }
    }

    public void DeactivateLeaderboard(uint id)
    {
        for (int index = _leaderboards.Count - 1; index >= 0; index--)
        {
            if (_leaderboards[index].Id == id)
            {
                _leaderboards.RemoveAt(index);
            }
        }
    }

    public RcLeaderboardEntry? GetLeaderboard(uint id)
    {
        foreach (RcLeaderboardEntry entry in _leaderboards)
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }

        return null;
    }

    public RcError ActivateRichPresence(string script)
    {
        if (string.Equals(_richPresenceDefinition, script, StringComparison.Ordinal))
        {
            _richPresence?.Reset();
            return RcError.Ok;
        }

        try
        {
            _richPresence = RcRichPresence.Parse(script);
            _richPresenceDefinition = script;
            _richPresence.Reset();
            return RcError.Ok;
        }
        catch (RcParseException exception)
        {
            _richPresence = null;
            _richPresenceDefinition = null;
            return exception.Error;
        }
    }

    public string GetRichPresence(RcPeek? peek, object? userData) =>
        _richPresence?.GetDisplayString(peek, userData) ?? string.Empty;

    public string? RichPresenceDefinition => _richPresenceDefinition;

    public void DoFrame(RcPeek? peek, object? userData, Action<RcRuntimeEvent>? eventHandler)
    {
        for (int index = _achievements.Count - 1; index >= 0; index--)
        {
            RcAchievement achievement = _achievements[index];
            RcTrigger trigger = achievement.Trigger;

            uint oldMeasuredValue = trigger.MeasuredValue;
            RcTriggerState oldState = trigger.State;
            RcTriggerState newState = trigger.Evaluate(peek, userData);

            if (newState == RcTriggerState.Reset)
            {
                eventHandler?.Invoke(new RcRuntimeEvent(
                    achievement.Id,
                    0,
                    RcRuntimeEventType.AchievementReset));

                newState = trigger.State;
            }

            if (trigger.MeasuredValue != oldMeasuredValue
                && oldMeasuredValue != RcTrigger.MeasuredUnknown
                && trigger.MeasuredTarget != 0
                && trigger.MeasuredValue <= trigger.MeasuredTarget
                && newState is not (RcTriggerState.Triggered or RcTriggerState.Inactive or RcTriggerState.Waiting))
            {
                if (trigger.MeasuredAsPercent)
                {
                    uint oldPercent = (uint)(((ulong)oldMeasuredValue * 100) / trigger.MeasuredTarget);
                    uint newPercent = (uint)(((ulong)trigger.MeasuredValue * 100) / trigger.MeasuredTarget);
                    if (oldPercent != newPercent)
                    {
                        eventHandler?.Invoke(new RcRuntimeEvent(
                            achievement.Id,
                            (int)newPercent,
                            RcRuntimeEventType.AchievementProgressUpdated));
                    }
                }
                else
                {
                    eventHandler?.Invoke(new RcRuntimeEvent(
                        achievement.Id,
                        (int)trigger.MeasuredValue,
                        RcRuntimeEventType.AchievementProgressUpdated));
                }
            }

            if (newState == oldState)
            {
                continue;
            }

            if (oldState == RcTriggerState.Primed)
            {
                eventHandler?.Invoke(new RcRuntimeEvent(
                    achievement.Id,
                    0,
                    RcRuntimeEventType.AchievementUnprimed));
            }

            switch (newState)
            {
                case RcTriggerState.Triggered:
                    eventHandler?.Invoke(new RcRuntimeEvent(
                        achievement.Id,
                        0,
                        RcRuntimeEventType.AchievementTriggered));
                    break;

                case RcTriggerState.Paused:
                    eventHandler?.Invoke(new RcRuntimeEvent(
                        achievement.Id,
                        0,
                        RcRuntimeEventType.AchievementPaused));
                    break;

                case RcTriggerState.Primed:
                    eventHandler?.Invoke(new RcRuntimeEvent(
                        achievement.Id,
                        0,
                        RcRuntimeEventType.AchievementPrimed));
                    break;

                case RcTriggerState.Active when oldState is RcTriggerState.Waiting or RcTriggerState.Paused:
                    eventHandler?.Invoke(new RcRuntimeEvent(
                        achievement.Id,
                        0,
                        RcRuntimeEventType.AchievementActivated));
                    break;
            }
        }

        for (int index = _leaderboards.Count - 1; index >= 0; index--)
        {
            RcLeaderboardEntry entry = _leaderboards[index];
            RcLeaderboardState oldState = entry.Leaderboard.State;
            RcLeaderboardState newState = entry.Leaderboard.Evaluate(out int value, peek, userData);

            switch (newState)
            {
                case RcLeaderboardState.Started:
                    if (oldState != RcLeaderboardState.Started)
                    {
                        entry.Value = value;
                        eventHandler?.Invoke(new RcRuntimeEvent(
                            entry.Id,
                            value,
                            RcRuntimeEventType.LeaderboardStarted));
                    }
                    else if (value != entry.Value)
                    {
                        entry.Value = value;
                        eventHandler?.Invoke(new RcRuntimeEvent(
                            entry.Id,
                            value,
                            RcRuntimeEventType.LeaderboardUpdated));
                    }

                    break;

                case RcLeaderboardState.Canceled:
                    if (oldState != RcLeaderboardState.Canceled)
                    {
                        eventHandler?.Invoke(new RcRuntimeEvent(
                            entry.Id,
                            value,
                            RcRuntimeEventType.LeaderboardCanceled));
                    }

                    break;

                case RcLeaderboardState.Triggered:
                    if (oldState != RcLeaderboardState.Triggered)
                    {
                        eventHandler?.Invoke(new RcRuntimeEvent(
                            entry.Id,
                            value,
                            RcRuntimeEventType.LeaderboardTriggered));
                    }

                    break;
            }
        }

        _richPresence?.Update(peek, userData);
    }

    public void Reset()
    {
        foreach (RcAchievement achievement in _achievements)
        {
            achievement.Trigger.ResetTrigger();
        }

        foreach (RcLeaderboardEntry entry in _leaderboards)
        {
            entry.Leaderboard.Reset();
        }

        _richPresence?.Reset();
    }
}
