// Ported from rcheevos (MIT) - src/rcheevos/lboard.c.
using System.Collections.Generic;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>A parsed leaderboard definition.</summary>
internal sealed class RcLeaderboard
{
    public RcTrigger Start = new();

    public RcTrigger Cancel = new();

    public RcTrigger Submit = new();

    public RcValue Value = new();

    public RcValue? Progress;

    public RcLeaderboardState State = RcLeaderboardState.Waiting;

    internal List<RcMemRefBase> MemRefs { get; private set; } = [];

    public static RcLeaderboard Parse(string memaddr)
    {
        var cursor = new RcCursor(memaddr);
        var parse = new RcParseState();
        return Parse(cursor, parse);
    }

    public static RcLeaderboard Parse(RcCursor cursor, RcParseState parse)
    {
        var leaderboard = new RcLeaderboard();
        ParseInternal(leaderboard, cursor, parse);
        leaderboard.MemRefs = parse.MemRefs;
        leaderboard.Start.UseMemRefs(parse.MemRefs);
        leaderboard.Cancel.UseMemRefs(parse.MemRefs);
        leaderboard.Submit.UseMemRefs(parse.MemRefs);
        leaderboard.Value.UseMemRefs(parse.MemRefs);
        leaderboard.Progress?.UseMemRefs(parse.MemRefs);

        leaderboard.Start.UpdateMemRefsEnabled = false;
        leaderboard.Cancel.UpdateMemRefsEnabled = false;
        leaderboard.Submit.UpdateMemRefsEnabled = false;
        leaderboard.Value.UpdateMemRefsEnabled = false;
        if (leaderboard.Progress is not null)
        {
            leaderboard.Progress.UpdateMemRefsEnabled = false;
        }

        return leaderboard;
    }

    private static void ParseInternal(RcLeaderboard self, RcCursor cursor, RcParseState parse)
    {
        int found = 0;
        const int StartFlag = 1 << 0;
        const int CancelFlag = 1 << 1;
        const int SubmitFlag = 1 << 2;
        const int ValueFlag = 1 << 3;
        const int ProgressFlag = 1 << 4;
        const int Complete = StartFlag | CancelFlag | SubmitFlag | ValueFlag;

        while (true)
        {
            if (StartsWith(cursor, "sta:"))
            {
                if ((found & StartFlag) != 0)
                {
                    throw new RcParseException(RcError.DuplicatedStart);
                }

                cursor.Skip(4);
                if (!cursor.End && cursor.Current != ':')
                {
                    found |= StartFlag;
                    self.Start = RcTrigger.Parse(cursor, parse);
                }
            }
            else if (StartsWith(cursor, "can:"))
            {
                if ((found & CancelFlag) != 0)
                {
                    throw new RcParseException(RcError.DuplicatedCancel);
                }

                cursor.Skip(4);
                if (!cursor.End && cursor.Current != ':')
                {
                    found |= CancelFlag;
                    self.Cancel = RcTrigger.Parse(cursor, parse);
                }
            }
            else if (StartsWith(cursor, "sub:"))
            {
                if ((found & SubmitFlag) != 0)
                {
                    throw new RcParseException(RcError.DuplicatedSubmit);
                }

                cursor.Skip(4);
                if (!cursor.End && cursor.Current != ':')
                {
                    found |= SubmitFlag;
                    self.Submit = RcTrigger.Parse(cursor, parse);
                }
            }
            else if (StartsWith(cursor, "val:"))
            {
                if ((found & ValueFlag) != 0)
                {
                    throw new RcParseException(RcError.DuplicatedValue);
                }

                cursor.Skip(4);
                if (!cursor.End && cursor.Current != ':')
                {
                    found |= ValueFlag;
                    self.Value = RcValue.Parse(cursor, parse);
                }
            }
            else if (StartsWith(cursor, "pro:"))
            {
                if ((found & ProgressFlag) != 0)
                {
                    throw new RcParseException(RcError.DuplicatedProgress);
                }

                cursor.Skip(4);
                if (!cursor.End && cursor.Current != ':')
                {
                    found |= ProgressFlag;
                    self.Progress = RcValue.Parse(cursor, parse);
                }
            }

            if (cursor.End || cursor.Current == '"')
            {
                break;
            }

            if (cursor.Current != ':' || cursor.CharAt(1) != ':')
            {
                throw new RcParseException(RcError.InvalidLboardField);
            }

            cursor.Skip(2);
        }

        if ((found & Complete) != Complete)
        {
            if ((found & StartFlag) == 0)
            {
                throw new RcParseException(RcError.MissingStart);
            }

            if ((found & CancelFlag) == 0)
            {
                throw new RcParseException(RcError.MissingCancel);
            }

            if ((found & SubmitFlag) == 0)
            {
                throw new RcParseException(RcError.MissingSubmit);
            }

            throw new RcParseException(RcError.MissingValue);
        }

        self.State = RcLeaderboardState.Waiting;
    }

    public RcLeaderboardState Evaluate(out int value, RcPeek? peek, object? userData)
    {
        RcMemRefHelpers.UpdateMemRefs(MemRefs, peek, userData);

        if (State is RcLeaderboardState.Inactive or RcLeaderboardState.Disabled)
        {
            value = 0;
            return RcLeaderboardState.Inactive;
        }

        // These are always tested once per frame so hit counts stay correct.
        bool startOk = Start.Test(peek, userData);
        bool cancelOk = Cancel.Test(peek, userData);
        bool submitOk = Submit.Test(peek, userData);

        switch (State)
        {
            case RcLeaderboardState.Waiting:
            case RcLeaderboardState.Triggered:
            case RcLeaderboardState.Canceled:
                if (startOk)
                {
                    value = 0;
                    return RcLeaderboardState.Inactive;
                }

                State = RcLeaderboardState.Active;
                break;

            case RcLeaderboardState.Active:
                if (startOk && !cancelOk)
                {
                    if (submitOk)
                    {
                        State = RcLeaderboardState.Triggered;
                    }
                    else if (Start.IsEmpty)
                    {
                        // Empty start trigger: leaderboard is in development.
                    }
                    else
                    {
                        State = RcLeaderboardState.Started;
                    }

                    Progress?.Reset();
                    Value.Reset();
                }

                break;

            case RcLeaderboardState.Started:
                if (cancelOk)
                {
                    State = RcLeaderboardState.Canceled;
                }
                else if (submitOk)
                {
                    State = RcLeaderboardState.Triggered;
                }

                break;
        }

        switch (State)
        {
            case RcLeaderboardState.Started:
                if (Progress is not null)
                {
                    value = Progress.Evaluate(peek, userData);
                    break;
                }

                goto case RcLeaderboardState.Triggered;

            case RcLeaderboardState.Triggered:
                value = Value.Evaluate(peek, userData);
                break;

            default:
                value = 0;
                break;
        }

        return State;
    }

    public void Reset()
    {
        State = RcLeaderboardState.Waiting;
        Start.ResetTrigger();
        Submit.ResetTrigger();
        Cancel.ResetTrigger();
        Progress?.Reset();
        Value.Reset();
    }

    public static bool IsActiveState(RcLeaderboardState state) =>
        state is not (RcLeaderboardState.Disabled or RcLeaderboardState.Inactive);

    private static bool StartsWith(RcCursor cursor, string prefix)
    {
        if (cursor.Position + prefix.Length > cursor.Text.Length)
        {
            return false;
        }

        for (int index = 0; index < prefix.Length; index++)
        {
            if (char.ToLowerInvariant(cursor.Text[cursor.Position + index]) != prefix[index])
            {
                return false;
            }
        }

        return true;
    }
}
