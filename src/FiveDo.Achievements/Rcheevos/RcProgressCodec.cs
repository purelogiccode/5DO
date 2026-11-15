using System;
using System.Collections.Generic;
using System.Linq;
using FiveDo.Core;
using Serilog;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>
/// Serializes and restores the parts of an <see cref="RcRuntime"/> that must survive a
/// save state: achievement hit counts and trigger state, per-trigger memref values used
/// by delta/prior conditions, and leaderboard attempt state.
/// </summary>
/// <remarks>
/// This is a 5DO-owned format, separate from rcheevos' progress format. It deliberately
/// keys achievements and leaderboards by their stable IDs and definition hashes so a
/// state written by an older game definition set is discarded rather than applied to
/// incompatible conditions.
/// </remarks>
internal static class RcProgressCodec
{
    internal const uint Magic = 0x31504152; // "RAP1" little-endian.
    internal const ushort Version = 1;

    private const int MaxEntries = 1_000_000;

    public static byte[] Serialize(string gameHash, RcRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(gameHash);
        ArgumentNullException.ThrowIfNull(runtime);

        var writer = new StateWriter(4096);
        writer.WriteUInt32(Magic);
        writer.WriteUInt16(Version);
        writer.WriteString(gameHash);

        writer.WriteInt32(runtime.Achievements.Count);
        foreach (RcAchievement achievement in runtime.Achievements)
        {
            writer.WriteUInt32(achievement.Id);
            writer.WriteUInt32(HashDefinition(achievement.Definition));
            WriteTrigger(writer, achievement.Trigger, includeMemRefs: true);
        }

        writer.WriteInt32(runtime.Leaderboards.Count);
        foreach (RcLeaderboardEntry leaderboard in runtime.Leaderboards)
        {
            writer.WriteUInt32(leaderboard.Id);
            writer.WriteUInt32(HashDefinition(leaderboard.Definition));
            writer.WriteByte((byte)leaderboard.Leaderboard.State);
            writer.WriteInt32(leaderboard.Value);
            WriteTrigger(writer, leaderboard.Leaderboard.Start, includeMemRefs: false);
            WriteTrigger(writer, leaderboard.Leaderboard.Cancel, includeMemRefs: false);
            WriteTrigger(writer, leaderboard.Leaderboard.Submit, includeMemRefs: false);
            WriteValue(writer, leaderboard.Leaderboard.Value);
            writer.WriteBoolean(leaderboard.Leaderboard.Progress is not null);
            if (leaderboard.Leaderboard.Progress is not null)
            {
                WriteValue(writer, leaderboard.Leaderboard.Progress);
            }

            WriteMemRefs(writer, leaderboard.Leaderboard.MemRefs);
        }

        return writer.ToArray();
    }

    public static bool TryDeserialize(
        ReadOnlySpan<byte> payload,
        string expectedGameHash,
        RcRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(expectedGameHash);
        ArgumentNullException.ThrowIfNull(runtime);

        try
        {
            byte[] bytes = payload.ToArray();
            var reader = new StateReader(bytes);
            if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != Version)
            {
                return false;
            }

            if (!string.Equals(reader.ReadString(), expectedGameHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int achievementCount = reader.ReadInt32();
            if (achievementCount < 0 || achievementCount != runtime.Achievements.Count)
            {
                return false;
            }

            var achievements = new List<AchievementSnapshot>(achievementCount);
            for (var index = 0; index < achievementCount; index++)
            {
                uint id = reader.ReadUInt32();
                uint definitionHash = reader.ReadUInt32();
                RcAchievement? achievement = runtime.GetAchievement(id);
                if (achievement is null
                    || HashDefinition(achievement.Definition) != definitionHash)
                {
                    return false;
                }

                TriggerSnapshot trigger = ReadTrigger(reader, includeMemRefs: true);
                if (!ValidateTrigger(achievement.Trigger, trigger))
                {
                    return false;
                }

                achievements.Add(new AchievementSnapshot(id, trigger));
            }

            int leaderboardCount = reader.ReadInt32();
            if (leaderboardCount < 0 || leaderboardCount != runtime.Leaderboards.Count)
            {
                return false;
            }

            var leaderboards = new List<LeaderboardSnapshot>(leaderboardCount);
            for (var index = 0; index < leaderboardCount; index++)
            {
                uint id = reader.ReadUInt32();
                uint definitionHash = reader.ReadUInt32();
                RcLeaderboardEntry? leaderboard = runtime.GetLeaderboard(id);
                if (leaderboard is null
                    || HashDefinition(leaderboard.Definition) != definitionHash)
                {
                    return false;
                }

                byte stateValue = reader.ReadByte();
                if (stateValue > (byte)RcLeaderboardState.Disabled)
                {
                    return false;
                }

                int entryValue = reader.ReadInt32();
                TriggerSnapshot start = ReadTrigger(reader, includeMemRefs: false);
                TriggerSnapshot cancel = ReadTrigger(reader, includeMemRefs: false);
                TriggerSnapshot submit = ReadTrigger(reader, includeMemRefs: false);
                ValueSnapshot value = ReadValue(reader);
                bool hasProgress = reader.ReadBoolean();
                ValueSnapshot? progress = hasProgress ? ReadValue(reader) : null;
                MemRefSnapshot[] memrefs = ReadMemRefs(reader);

                if (!ValidateTrigger(leaderboard.Leaderboard.Start, start)
                    || !ValidateTrigger(leaderboard.Leaderboard.Cancel, cancel)
                    || !ValidateTrigger(leaderboard.Leaderboard.Submit, submit)
                    || !ValidateValue(leaderboard.Leaderboard.Value, value)
                    || (progress is null) != (leaderboard.Leaderboard.Progress is null)
                    || (progress is not null
                        && !ValidateValue(leaderboard.Leaderboard.Progress!, progress))
                    || !ValidateMemRefs(leaderboard.Leaderboard.MemRefs, memrefs))
                {
                    return false;
                }

                leaderboards.Add(new LeaderboardSnapshot(
                    id,
                    (RcLeaderboardState)stateValue,
                    entryValue,
                    start,
                    cancel,
                    submit,
                    value,
                    progress,
                    memrefs));
            }

            if (reader.Remaining != 0)
            {
                return false;
            }

            foreach (AchievementSnapshot snapshot in achievements)
            {
                RcAchievement achievement = runtime.GetAchievement(snapshot.Id)!;
                ApplyTrigger(achievement.Trigger, snapshot.Trigger);
            }

            foreach (LeaderboardSnapshot snapshot in leaderboards)
            {
                RcLeaderboardEntry entry = runtime.GetLeaderboard(snapshot.Id)!;
                entry.Leaderboard.State = snapshot.State;
                entry.Value = snapshot.EntryValue;
                ApplyTrigger(entry.Leaderboard.Start, snapshot.Start);
                ApplyTrigger(entry.Leaderboard.Cancel, snapshot.Cancel);
                ApplyTrigger(entry.Leaderboard.Submit, snapshot.Submit);
                ApplyValue(entry.Leaderboard.Value, snapshot.Value);
                if (snapshot.Progress is not null)
                {
                    ApplyValue(entry.Leaderboard.Progress!, snapshot.Progress);
                }

                ApplyMemRefs(entry.Leaderboard.MemRefs, snapshot.MemRefs);
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidStateException
            or ArgumentException
            or FormatException
            or OverflowException
            or InvalidOperationException)
        {
            Log.Debug(exception, "RetroAchievements save-state progress could not be read");
            return false;
        }
    }

    private static void WriteTrigger(StateWriter writer, RcTrigger trigger, bool includeMemRefs)
    {
        RcCondition[] conditions = [.. EnumerateConditions(trigger)];
        writer.WriteByte((byte)trigger.State);
        writer.WriteUInt32(trigger.MeasuredValue);
        writer.WriteBoolean(trigger.HasHits);
        writer.WriteInt32(conditions.Length);
        foreach (RcCondition condition in conditions)
        {
            writer.WriteUInt32(condition.CurrentHits);
            writer.WriteByte(condition.IsTrue);
        }

        if (includeMemRefs)
        {
            WriteMemRefs(writer, trigger.MemRefs);
        }
    }

    private static TriggerSnapshot ReadTrigger(StateReader reader, bool includeMemRefs)
    {
        byte stateValue = reader.ReadByte();
        if (stateValue > (byte)RcTriggerState.Disabled)
        {
            throw new FormatException("Invalid trigger state.");
        }

        uint measuredValue = reader.ReadUInt32();
        bool hasHits = reader.ReadBoolean();
        int conditionCount = reader.ReadInt32();
        if (conditionCount < 0 || conditionCount > MaxEntries)
        {
            throw new FormatException("Invalid condition count.");
        }

        var hits = new uint[conditionCount];
        var flags = new byte[conditionCount];
        for (var index = 0; index < conditionCount; index++)
        {
            hits[index] = reader.ReadUInt32();
            flags[index] = reader.ReadByte();
        }

        MemRefSnapshot[] memrefs = includeMemRefs ? ReadMemRefs(reader) : [];
        return new TriggerSnapshot((RcTriggerState)stateValue, measuredValue, hasHits, hits, flags, includeMemRefs, memrefs);
    }

    private static void WriteMemRefs(StateWriter writer, List<RcMemRefBase> memrefs)
    {
        writer.WriteInt32(memrefs.Count);
        foreach (RcMemRefBase memref in memrefs)
        {
            writer.WriteUInt32(memref.Address);
            writer.WriteByte((byte)memref.Value.Size);
            writer.WriteByte((byte)memref.Value.Type);
            writer.WriteByte((byte)memref.Value.MemRefType);
            writer.WriteUInt32(memref.Value.Value);
            writer.WriteUInt32(memref.Value.Prior);
            writer.WriteBoolean(memref.Value.Changed);
        }
    }

    private static MemRefSnapshot[] ReadMemRefs(StateReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaxEntries)
        {
            throw new FormatException("Invalid memory-reference count.");
        }

        var memrefs = new MemRefSnapshot[count];
        for (var index = 0; index < count; index++)
        {
            memrefs[index] = new MemRefSnapshot(
                reader.ReadUInt32(),
                (RcMemSize)reader.ReadByte(),
                (RcValueType)reader.ReadByte(),
                (RcMemRefType)reader.ReadByte(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadBoolean());
        }

        return memrefs;
    }

    private static void WriteValue(StateWriter writer, RcValue value)
    {
        RcCondition[] conditions = [.. EnumerateConditions(value.Conditions)];
        writer.WriteUInt32(value.Value.Value);
        writer.WriteUInt32(value.Value.Prior);
        writer.WriteBoolean(value.Value.Changed);
        writer.WriteInt32(conditions.Length);
        foreach (RcCondition condition in conditions)
        {
            writer.WriteUInt32(condition.CurrentHits);
            writer.WriteByte(condition.IsTrue);
        }
    }

    private static ValueSnapshot ReadValue(StateReader reader)
    {
        uint value = reader.ReadUInt32();
        uint prior = reader.ReadUInt32();
        bool changed = reader.ReadBoolean();
        int conditionCount = reader.ReadInt32();
        if (conditionCount < 0 || conditionCount > MaxEntries)
        {
            throw new FormatException("Invalid value condition count.");
        }

        var hits = new uint[conditionCount];
        var flags = new byte[conditionCount];
        for (var index = 0; index < conditionCount; index++)
        {
            hits[index] = reader.ReadUInt32();
            flags[index] = reader.ReadByte();
        }

        return new ValueSnapshot(value, prior, changed, hits, flags);
    }

    private static bool ValidateTrigger(RcTrigger trigger, TriggerSnapshot snapshot)
    {
        RcCondition[] conditions = [.. EnumerateConditions(trigger)];
        if (conditions.Length != snapshot.Hits.Length)
        {
            return false;
        }

        return !snapshot.IncludesMemRefs || ValidateMemRefs(trigger.MemRefs, snapshot.MemRefs);
    }

    private static bool ValidateValue(RcValue value, ValueSnapshot snapshot)
    {
        return EnumerateConditions(value.Conditions).Count() == snapshot.Hits.Length;
    }

    private static bool ValidateMemRefs(
        List<RcMemRefBase> actual,
        IReadOnlyList<MemRefSnapshot> snapshot)
    {
        if (actual.Count != snapshot.Count)
        {
            return false;
        }

        for (var index = 0; index < actual.Count; index++)
        {
            RcMemRefBase memref = actual[index];
            MemRefSnapshot saved = snapshot[index];
            if (memref.Address != saved.Address
                || memref.Value.Size != saved.Size
                || memref.Value.Type != saved.Type
                || memref.Value.MemRefType != saved.MemRefType)
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyTrigger(RcTrigger trigger, TriggerSnapshot snapshot)
    {
        trigger.State = snapshot.State;
        trigger.MeasuredValue = snapshot.MeasuredValue;
        trigger.HasHits = snapshot.HasHits;

        RcCondition[] conditions = [.. EnumerateConditions(trigger)];
        for (var index = 0; index < conditions.Length; index++)
        {
            conditions[index].CurrentHits = snapshot.Hits[index];
            conditions[index].IsTrue = snapshot.Flags[index];
        }

        if (snapshot.IncludesMemRefs)
        {
            ApplyMemRefs(trigger.MemRefs, snapshot.MemRefs);
        }
    }

    private static void ApplyValue(RcValue value, ValueSnapshot snapshot)
    {
        value.Value.Value = snapshot.Value;
        value.Value.Prior = snapshot.Prior;
        value.Value.Changed = snapshot.Changed;

        RcCondition[] conditions = [.. EnumerateConditions(value.Conditions)];
        for (var index = 0; index < conditions.Length; index++)
        {
            conditions[index].CurrentHits = snapshot.Hits[index];
            conditions[index].IsTrue = snapshot.Flags[index];
        }
    }

    private static void ApplyMemRefs(
        List<RcMemRefBase> memrefs,
        IReadOnlyList<MemRefSnapshot> snapshots)
    {
        for (var index = 0; index < memrefs.Count; index++)
        {
            MemRefSnapshot snapshot = snapshots[index];
            memrefs[index].Value.Value = snapshot.Value;
            memrefs[index].Value.Prior = snapshot.Prior;
            memrefs[index].Value.Changed = snapshot.Changed;
        }
    }

    private static IEnumerable<RcCondition> EnumerateConditions(RcTrigger trigger)
    {
        if (trigger.Requirement is not null)
        {
            foreach (RcCondition condition in trigger.Requirement.AllConditions())
            {
                yield return condition;
            }
        }

        for (RcCondSet? condset = trigger.Alternative; condset is not null; condset = condset.Next)
        {
            foreach (RcCondition condition in condset.AllConditions())
            {
                yield return condition;
            }
        }
    }

    private static IEnumerable<RcCondition> EnumerateConditions(RcCondSet? condset)
    {
        for (; condset is not null; condset = condset.Next)
        {
            foreach (RcCondition condition in condset.AllConditions())
            {
                yield return condition;
            }
        }
    }

    private static uint HashDefinition(string text)
    {
        var hash = 2166136261u;
        foreach (char character in text)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash;
    }

    private sealed record AchievementSnapshot(uint Id, TriggerSnapshot Trigger);

    private sealed record TriggerSnapshot(
        RcTriggerState State,
        uint MeasuredValue,
        bool HasHits,
        uint[] Hits,
        byte[] Flags,
        bool IncludesMemRefs,
        MemRefSnapshot[] MemRefs);

    private sealed record ValueSnapshot(
        uint Value,
        uint Prior,
        bool Changed,
        uint[] Hits,
        byte[] Flags);

    private sealed record MemRefSnapshot(
        uint Address,
        RcMemSize Size,
        RcValueType Type,
        RcMemRefType MemRefType,
        uint Value,
        uint Prior,
        bool Changed);

    private sealed record LeaderboardSnapshot(
        uint Id,
        RcLeaderboardState State,
        int EntryValue,
        TriggerSnapshot Start,
        TriggerSnapshot Cancel,
        TriggerSnapshot Submit,
        ValueSnapshot Value,
        ValueSnapshot? Progress,
        MemRefSnapshot[] MemRefs);
}
