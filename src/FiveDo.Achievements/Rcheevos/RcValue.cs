// Ported from rcheevos (MIT) - src/rcheevos/value.c (value parser and evaluator).
using System.Collections.Generic;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>A named or anonymous value expression, also usable as a memory reference.</summary>
internal sealed class RcValue : RcMemRefBase
{
    public RcCondSet? Conditions;

    public string Name = string.Empty;

    public RcValue? Next;

    internal List<RcMemRefBase> MemRefs { get; private set; } = [];

    /// <summary>Gets or sets a value indicating whether this value refreshes its own memrefs.</summary>
    internal bool UpdateMemRefsEnabled { get; set; } = true;

    internal void UseMemRefs(List<RcMemRefBase> memrefs) => MemRefs = memrefs;

    public static RcValue Parse(string memaddr)
    {
        var cursor = new RcCursor(memaddr);
        var parse = new RcParseState();
        return Parse(cursor, parse);
    }

    public static RcValue Parse(RcCursor cursor, RcParseState parse)
    {
        var value = new RcValue();
        ParseInternal(value, cursor, parse);
        value.MemRefs = parse.MemRefs;
        return value;
    }

    private static void ParseInternal(RcValue self, RcCursor cursor, RcParseState parse)
    {
        bool wasValue = parse.IsValue;
        parse.IsValue = true;

        if (cursor.CharAt(1) == ':')
        {
            ParseCondValue(self, cursor, parse);
        }
        else
        {
            ParseLegacyValue(self, cursor, parse);
        }

        if (self.Conditions is not null)
        {
            self.Name = "(unnamed)";
            self.Value.Value = 0;
            self.Value.Prior = 0;
            self.Value.MemRefType = RcMemRefType.Value;
            self.Value.Changed = false;

            for (RcCondSet? condset = self.Conditions; condset is not null; condset = condset.Next)
            {
                foreach (RcCondition condition in condset.AllConditions())
                {
                    if (condition.Type == RcConditionType.Measured)
                    {
                        if (condition.Operand1.IsFloat)
                        {
                            self.Value.Size = RcMemSize.Float;
                            self.Value.Type = RcValueType.Float;
                        }
                        else
                        {
                            self.Value.Size = RcMemSize.Bits32;
                            self.Value.Type = RcValueType.Unsigned;
                        }

                        break;
                    }
                }
            }
        }

        parse.IsValue = wasValue;
    }

    private static void ParseCondValue(RcValue self, RcCursor cursor, RcParseState parse)
    {
        RcCondSet? last = null;

        while (true)
        {
            parse.MeasuredTarget = 0;
            RcCondSet condset = RcCondSet.Parse(cursor, parse);

            if (last is null)
            {
                self.Conditions = condset;
            }
            else
            {
                last.Next = condset;
            }

            if (cursor.Current is 'S' or 's')
            {
                throw new RcParseException(RcError.InvalidValueFlag);
            }

            if (parse.MeasuredTarget == 0)
            {
                throw new RcParseException(RcError.MissingValueMeasured);
            }

            if (cursor.Current == '$')
            {
                cursor.Advance();
                last = condset;
                continue;
            }

            break;
        }

        if (last is not null)
        {
            last.Next = null;
        }
    }

    private static void ParseLegacyValue(RcValue self, RcCursor cursor, RcParseState parse)
    {
        RcCondSet? lastClause = null;

        do
        {
            var conditions = new List<RcCondition>();
            var buffer = new char[64];
            buffer[0] = 'A';
            buffer[1] = ':';

            while (true)
            {
                buffer[0] = 'A';
                int length = 2;

                while (true)
                {
                    if (length == buffer.Length)
                    {
                        throw new RcParseException(RcError.InvalidValue);
                    }

                    char current = cursor.Current;
                    switch (current)
                    {
                        case '_':
                            break;

                        case '$':
                        case '\0':
                        case ':':
                        case ')':
                            if (buffer[0] == 'A')
                            {
                                buffer[0] = 'M';
                            }

                            break;

                        case '*':
                            buffer[length++] = '*';
                            int lookahead = cursor.Position + 1;
                            char next = index(cursor, lookahead);
                            if (next == '-')
                            {
                                buffer[0] = 'B';
                                cursor.Advance();
                                ++lookahead;
                            }
                            else if (next == '+')
                            {
                                ++lookahead;
                            }

                            while (RcText.IsDigit(index(cursor, lookahead)))
                            {
                                ++lookahead;
                            }

                            if (index(cursor, lookahead) == '.')
                            {
                                buffer[length++] = 'f';
                            }

                            cursor.Advance();
                            continue;

                        default:
                            buffer[length++] = current;
                            cursor.Advance();
                            continue;
                    }

                    break;
                }

                string conditionText = new(buffer, 0, length);
                var conditionCursor = new RcCursor(conditionText);
                RcCondition condition = RcCondition.Parse(conditionCursor, parse);

                if (conditionCursor.Position != conditionText.Length)
                {
                    throw new RcParseException(RcError.InvalidValue);
                }

                if (condition.Type == RcConditionType.Measured && !RcCondition.IsModifying(condition.Oper))
                {
                    condition.Oper = RcOperator.None;
                }

                RcCondition.UpdateParseState(condition, parse);
                conditions.Add(condition);

                if (cursor.Current != '_')
                {
                    break;
                }

                cursor.Advance();
            }

            if (conditions.Count == 0 || conditions[^1].Type != RcConditionType.Measured)
            {
                var measuredCursor = new RcCursor("M:0");
                conditions.Add(RcCondition.Parse(measuredCursor, parse));
            }

            RcCondSet condset = RcCondSet.FromLegacyValue(conditions);
            if (lastClause is null)
            {
                self.Conditions = condset;
            }
            else
            {
                lastClause.Next = condset;
            }

            lastClause = condset;

            if (cursor.Current != '$')
            {
                break;
            }

            cursor.Advance();
        }
        while (true);

        lastClause.Next = null;

        return;

        static char index(RcCursor cursor, int position) =>
            (uint)position < (uint)cursor.Text.Length ? cursor.Text[position] : '\0';
    }

    public bool EvaluateTyped(out RcTypedValue result, RcPeek? peek, object? userData)
    {
        if (UpdateMemRefsEnabled)
        {
            RcMemRefHelpers.UpdateMemRefs(MemRefs, peek, userData);
        }

        result = RcTypedValue.FromSigned(0);
        bool valid = false;

        for (RcCondSet? condset = Conditions; condset is not null; condset = condset.Next)
        {
            var evalState = new RcEvalState
            {
                Peek = peek,
                PeekUserData = userData,
            };

            _ = RcCondSet.Test(condset, evalState);

            if (condset.IsPaused)
            {
                continue;
            }

            if (evalState.WasReset)
            {
                condset.Reset();
            }

            if (evalState.MeasuredValue.Type != RcValueType.None)
            {
                if (!valid)
                {
                    result = evalState.MeasuredValue;
                    valid = true;
                }
                else if (RcTypedValue.Compare(evalState.MeasuredValue, result, RcOperator.Gt))
                {
                    result = evalState.MeasuredValue;
                }
            }
        }

        return valid;
    }

    public int Evaluate(RcPeek? peek, object? userData)
    {
        if (EvaluateTyped(out RcTypedValue result, peek, userData))
        {
            RcTypedValue.Convert(ref result, RcValueType.Unsigned);
            RcMemRefHelpers.UpdateValue(ref Value, result.U32);
        }
        else
        {
            result = RcTypedValue.FromUnsigned(Value.Value);
        }

        RcTypedValue.Convert(ref result, RcValueType.Signed);
        return result.I32;
    }

    public void Reset()
    {
        for (RcCondSet? condset = Conditions; condset is not null; condset = condset.Next)
        {
            condset.Reset();
        }

        Value.Value = 0;
        Value.Prior = 0;
        Value.Changed = false;
    }

    public bool IsMeasuredFromHits()
    {
        for (RcCondSet? condset = Conditions; condset is not null; condset = condset.Next)
        {
            foreach (RcCondition condition in condset.AllConditions())
            {
                if (condition.Type == RcConditionType.Measured)
                {
                    return condition.RequiredHits != 0;
                }
            }
        }

        return false;
    }
}
