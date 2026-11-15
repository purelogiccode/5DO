// Ported from rcheevos (MIT) - src/rcheevos/trigger.c.
using System.Collections.Generic;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>A parsed achievement trigger (requirement plus alternative condition sets).</summary>
internal sealed class RcTrigger
{
    public const uint MeasuredUnknown = 0xFFFFFFFF;

    public RcCondSet? Requirement;

    public RcCondSet? Alternative;

    public uint MeasuredValue;

    public uint MeasuredTarget;

    public RcTriggerState State = RcTriggerState.Waiting;

    public bool HasHits;

    public bool MeasuredAsPercent;

    internal List<RcMemRefBase> MemRefs { get; private set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this trigger refreshes its own memrefs.
    /// Triggers owned by a leaderboard or rich-presence script share that owner's memref
    /// list, which is refreshed exactly once per frame by the owner.
    /// </summary>
    internal bool UpdateMemRefsEnabled { get; set; } = true;

    internal void UseMemRefs(List<RcMemRefBase> memrefs) => MemRefs = memrefs;

    public static RcTrigger Parse(string memaddr)
    {
        var cursor = new RcCursor(memaddr);
        var parse = new RcParseState();
        return Parse(cursor, parse);
    }

    public static RcTrigger Parse(RcCursor cursor, RcParseState parse)
    {
        var trigger = new RcTrigger();
        ParseInternal(trigger, cursor, parse);
        trigger.MemRefs = parse.MemRefs;
        return trigger;
    }

    private static void ParseInternal(RcTrigger trigger, RcCursor cursor, RcParseState parse)
    {
        parse.MeasuredTarget = 0;
        parse.HasRequiredHits = false;
        parse.MeasuredAsPercent = false;

        if (cursor.Current is 's' or 'S')
        {
            trigger.Requirement = null;
        }
        else
        {
            trigger.Requirement = RcCondSet.Parse(cursor, parse);
            trigger.Requirement.Next = null;
        }

        RcCondSet? lastAlternative = null;
        while (cursor.Current is 's' or 'S')
        {
            cursor.Advance();
            RcCondSet condset = RcCondSet.Parse(cursor, parse);
            if (lastAlternative is null)
            {
                trigger.Alternative = condset;
            }
            else
            {
                lastAlternative.Next = condset;
            }

            lastAlternative = condset;
        }

        if (lastAlternative is not null)
        {
            lastAlternative.Next = null;
        }

        trigger.MeasuredTarget = parse.MeasuredTarget;
        trigger.MeasuredValue = parse.MeasuredTarget != 0 ? MeasuredUnknown : 0;
        trigger.MeasuredAsPercent = parse.MeasuredAsPercent;
        trigger.State = RcTriggerState.Waiting;
        trigger.HasHits = false;
    }

    public RcTriggerState Evaluate(RcPeek? peek, object? userData)
    {
        switch (State)
        {
            case RcTriggerState.Triggered:
            case RcTriggerState.Disabled:
                return RcTriggerState.Inactive;

            case RcTriggerState.Inactive:
                if (UpdateMemRefsEnabled)
                {
                    UpdateMemRefs(peek, userData);
                }

                return RcTriggerState.Inactive;
        }

        if (UpdateMemRefsEnabled)
        {
            UpdateMemRefs(peek, userData);
        }

        var evalState = new RcEvalState
        {
            Peek = peek,
            PeekUserData = userData,
        };
        RcTypedValue measuredValue = default;
        bool measuredFromHits = false;
        bool ret;
        bool isPaused;
        bool isPrimed;

        if (Requirement is not null)
        {
            ret = RcCondSet.Test(Requirement, evalState);
            isPaused = evalState.IsPaused;
            isPrimed = evalState.IsPrimed;

            if (evalState.MeasuredValue.Type != RcValueType.None)
            {
                measuredValue = evalState.MeasuredValue;
                measuredFromHits = evalState.MeasuredFromHits;
            }
        }
        else
        {
            ret = true;
            isPaused = false;
            isPrimed = true;
        }

        RcCondSet? condset = Alternative;
        if (condset is not null)
        {
            bool sub = false;
            bool subPaused = true;
            bool subPrimed = false;

            do
            {
                sub |= RcCondSet.Test(condset, evalState);
                subPaused &= evalState.IsPaused;
                subPrimed |= evalState.IsPrimed;

                if (evalState.MeasuredValue.Type != RcValueType.None)
                {
                    if (measuredValue.Type == RcValueType.None
                        || RcTypedValue.Compare(evalState.MeasuredValue, measuredValue, RcOperator.Gt))
                    {
                        measuredValue = evalState.MeasuredValue;
                        measuredFromHits = evalState.MeasuredFromHits;
                    }
                }

                condset = condset.Next;
            }
            while (condset is not null);

            ret &= sub;
            isPrimed &= subPrimed;
            isPaused |= subPaused;
        }

        if (isPaused)
        {
            // A fully paused trigger ignores measured-value updates.
        }
        else if (measuredValue.Type == RcValueType.None)
        {
            // No measured value captured this frame; keep the previous one.
        }
        else
        {
            RcTypedValue.Convert(ref measuredValue, RcValueType.Unsigned);
            MeasuredValue = measuredValue.U32;
        }

        if (evalState.WasReset)
        {
            if (measuredFromHits)
            {
                MeasuredValue = 0;
            }
            else if (isPaused && MeasuredValue != 0)
            {
                if (Requirement is not null
                    && Requirement.IsPaused
                    && CondSetIsMeasuredFromHitCount(Requirement, MeasuredValue))
                {
                    MeasuredValue = 0;
                }
                else
                {
                    for (condset = Alternative; condset is not null; condset = condset.Next)
                    {
                        if (condset.IsPaused && CondSetIsMeasuredFromHitCount(condset, MeasuredValue))
                        {
                            MeasuredValue = 0;
                            break;
                        }
                    }
                }
            }

            ResetHitCounts();

            if (HasHits)
            {
                HasHits = false;

                if (State == RcTriggerState.Primed)
                {
                    State = RcTriggerState.Active;
                }

                return RcTriggerState.Reset;
            }

            evalState.HasHits = false;
            isPrimed = false;
        }
        else if (ret)
        {
            if (State == RcTriggerState.Waiting)
            {
                ResetTrigger();
                HasHits = false;
                return RcTriggerState.Waiting;
            }

            State = RcTriggerState.Triggered;
            return RcTriggerState.Triggered;
        }

        HasHits = evalState.HasHits;
        State = isPaused
            ? RcTriggerState.Paused
            : isPrimed
                ? RcTriggerState.Primed
                : RcTriggerState.Active;

        if (evalState.WasCondReset)
        {
            return RcTriggerState.Reset;
        }

        return State;
    }

    public bool Test(RcPeek? peek, object? userData)
    {
        State = RcTriggerState.Active;
        return Evaluate(peek, userData) == RcTriggerState.Triggered;
    }

    public void ResetTrigger()
    {
        ResetHitCounts();
        State = RcTriggerState.Waiting;

        if (MeasuredTarget != 0)
        {
            MeasuredValue = MeasuredUnknown;
        }

        HasHits = false;
    }

    public void UpdateMemRefs(RcPeek? peek, object? userData)
    {
        RcMemRefHelpers.UpdateMemRefs(MemRefs, peek, userData);
    }

    public bool IsEmpty => (Requirement is null || Requirement.Conditions is null) && Alternative is null;

    public bool ContainsMemRef(RcMemRefBase memref)
    {
        foreach (RcMemRefBase candidate in MemRefs)
        {
            if (ReferenceEquals(candidate, memref))
            {
                return true;
            }
        }

        return false;
    }

    private void ResetHitCounts()
    {
        Requirement?.Reset();

        for (RcCondSet? condset = Alternative; condset is not null; condset = condset.Next)
        {
            condset.Reset();
        }
    }

    private static bool CondSetIsMeasuredFromHitCount(RcCondSet condset, uint measuredValue)
    {
        foreach (RcCondition condition in condset.AllConditions())
        {
            if (condition.Type == RcConditionType.Measured
                && condition.RequiredHits != 0
                && condition.CurrentHits == measuredValue)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsActiveState(RcTriggerState state) => state is not (RcTriggerState.Disabled
        or RcTriggerState.Inactive
        or RcTriggerState.Triggered);

    public string FormatMeasured()
    {
        if (MeasuredTarget == 0 || !IsActiveState(State))
        {
            return string.Empty;
        }

        uint value = MeasuredValue == MeasuredUnknown ? 0 : MeasuredValue;
        if (value > MeasuredTarget)
        {
            value = MeasuredTarget;
        }

        return MeasuredAsPercent
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{((ulong)value * 100) / MeasuredTarget}%")
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value}/{MeasuredTarget}");
    }
}
