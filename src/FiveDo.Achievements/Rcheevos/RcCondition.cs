// Ported from rcheevos (MIT) - src/rcheevos/condition.c.
namespace FiveDo.Achievements.Rcheevos;

/// <summary>A single condition in a condition set.</summary>
internal sealed class RcCondition
{
    public RcOperand Operand1 = new();

    public RcOperand Operand2 = new();

    public uint RequiredHits;

    public uint CurrentHits;

    public RcCondition? Next;

    public RcConditionType Type;

    public RcOperator Oper;

    /// <summary>
    /// Non-zero when the condition evaluated true last time. Bit zero is the condition
    /// result; bit one marks a ResetIf that caused the trigger's hit counts to reset.
    /// </summary>
    public byte IsTrue;

    public bool IsCombining => Type is RcConditionType.AddSource
        or RcConditionType.SubSource
        or RcConditionType.AddAddress
        or RcConditionType.Remember
        or RcConditionType.AddHits
        or RcConditionType.SubHits
        or RcConditionType.ResetNextIf
        or RcConditionType.AndNext
        or RcConditionType.OrNext;

    internal static RcCondition Parse(RcCursor cursor, RcParseState parse)
    {
        var condition = new RcCondition();
        ParseInternal(condition, cursor, parse);
        return condition;
    }

    internal static void ParseInternal(RcCondition condition, RcCursor cursor, RcParseState parse)
    {
        bool canModify = false;
        condition.CurrentHits = 0;
        condition.IsTrue = 0;

        if (!cursor.End && cursor.CharAt(1) == ':')
        {
            switch (char.ToLowerInvariant(cursor.Current))
            {
                case 'p':
                    condition.Type = RcConditionType.PauseIf;
                    break;
                case 'r':
                    condition.Type = RcConditionType.ResetIf;
                    break;
                case 'a':
                    condition.Type = RcConditionType.AddSource;
                    canModify = true;
                    break;
                case 'b':
                    condition.Type = RcConditionType.SubSource;
                    canModify = true;
                    break;
                case 'c':
                    condition.Type = RcConditionType.AddHits;
                    break;
                case 'd':
                    condition.Type = RcConditionType.SubHits;
                    break;
                case 'n':
                    condition.Type = RcConditionType.AndNext;
                    break;
                case 'o':
                    condition.Type = RcConditionType.OrNext;
                    break;
                case 'm':
                    condition.Type = RcConditionType.Measured;
                    break;
                case 'q':
                    condition.Type = RcConditionType.MeasuredIf;
                    break;
                case 'i':
                    condition.Type = RcConditionType.AddAddress;
                    canModify = true;
                    break;
                case 't':
                    condition.Type = RcConditionType.Trigger;
                    break;
                case 'k':
                    condition.Type = RcConditionType.Remember;
                    canModify = true;
                    break;
                case 'z':
                    condition.Type = RcConditionType.ResetNextIf;
                    break;
                case 'g':
                    parse.MeasuredAsPercent = true;
                    condition.Type = RcConditionType.Measured;
                    break;
                default:
                    throw new RcParseException(RcError.InvalidConditionType);
            }

            cursor.Skip(2);
        }
        else
        {
            condition.Type = RcConditionType.Standard;
        }

        condition.Operand1 = RcOperand.Parse(cursor, parse);

        condition.Oper = ParseOperator(cursor);

        if (condition.Oper == RcOperator.None)
        {
            if (!canModify)
            {
                if (condition.Type != RcConditionType.Measured && !parse.IgnoreNonParseErrors)
                {
                    throw new RcParseException(RcError.InvalidOperator);
                }
            }

            condition.Operand2 = RcOperand.Const(1);
            condition.RequiredHits = 0;
            return;
        }

        if (canModify && !IsModifying(condition.Oper))
        {
            switch (condition.Type)
            {
                case RcConditionType.AddSource:
                case RcConditionType.SubSource:
                case RcConditionType.AddAddress:
                    condition.Oper = RcOperator.None;
                    break;

                default:
                    if (!parse.IgnoreNonParseErrors)
                    {
                        throw new RcParseException(RcError.InvalidOperator);
                    }

                    break;
            }
        }

        condition.Operand2 = RcOperand.Parse(cursor, parse);

        if (condition.Oper == RcOperator.None)
        {
            condition.Operand2 = RcOperand.Const(0);
        }

        if (cursor.Current == '(')
        {
            cursor.Advance();
            if (!RcText.TryParseUInt32(cursor, 10, out uint hits, out int end) || cursor.Text.Length <= end || cursor.Text[end] != ')')
            {
                throw new RcParseException(RcError.InvalidRequiredHits);
            }

            condition.RequiredHits = condition.Oper == RcOperator.None ? 0 : hits;
            if (condition.Oper != RcOperator.None)
            {
                parse.HasRequiredHits = true;
            }

            cursor.Position = end + 1;
        }
        else if (cursor.Current == '.')
        {
            cursor.Advance();
            if (!RcText.TryParseUInt32(cursor, 10, out uint hits, out int end) || cursor.Text.Length <= end || cursor.Text[end] != '.')
            {
                throw new RcParseException(RcError.InvalidRequiredHits);
            }

            condition.RequiredHits = condition.Oper == RcOperator.None ? 0 : hits;
            if (condition.Oper != RcOperator.None)
            {
                parse.HasRequiredHits = true;
            }

            cursor.Position = end + 1;
        }
        else
        {
            condition.RequiredHits = 0;
        }
    }

    internal static bool IsModifying(RcOperator oper) => oper is RcOperator.And
        or RcOperator.Xor
        or RcOperator.Div
        or RcOperator.Mult
        or RcOperator.Mod
        or RcOperator.Add
        or RcOperator.Sub
        or RcOperator.None;

    internal static bool Test(RcCondition condition, RcEvalState evalState)
    {
        RcOperand.Evaluate(out RcTypedValue value1, condition.Operand1, evalState);
        RcOperand.Evaluate(out RcTypedValue value2, condition.Operand2, evalState);
        return RcTypedValue.Compare(value1, value2, condition.Oper);
    }

    internal static void EvaluateConditionValue(out RcTypedValue value, RcCondition condition, RcEvalState evalState)
    {
        RcOperand.Evaluate(out value, condition.Operand1, evalState);
        RcOperand.Evaluate(out RcTypedValue amount, condition.Operand2, evalState);
        RcTypedValue.Combine(ref value, ref amount, condition.Oper);
    }

    /// <summary>Mutates the parser state after a condition is parsed.</summary>
    internal static void UpdateParseState(RcCondition condition, RcParseState parse)
    {
        switch (condition.Type)
        {
            case RcConditionType.AddAddress:
                if (condition.Oper != RcOperator.None)
                {
                    RcOperand.FromCondition(condition, parse.IndirectParent, parse);
                }
                else
                {
                    RcOperand.Copy(condition.Operand1, parse.IndirectParent);
                }

                break;

            case RcConditionType.AddSource:
                if (parse.AddSourceParent.Type == RcOperandType.None)
                {
                    RcOperand.FromCondition(condition, parse.AddSourceParent, parse);
                }
                else
                {
                    RcMemSize newSize = parse.AddSourceParent.IsFloat ? RcMemSize.Float : RcMemSize.Bits32;
                    var conditionOperand = new RcOperand();
                    RcOperand.FromCondition(condition, conditionOperand, parse);
                    RcOperand.AddSource(conditionOperand, parse, newSize);
                    RcOperand.Copy(conditionOperand, parse.AddSourceParent);
                }

                parse.AddSourceOper = RcOperator.AddAccumulator;
                parse.IndirectParent.Type = RcOperandType.None;
                break;

            case RcConditionType.SubSource:
                if (parse.AddSourceParent.Type == RcOperandType.None)
                {
                    RcOperand.FromCondition(condition, parse.AddSourceParent, parse);
                    parse.AddSourceOper = RcOperator.SubParent;
                }
                else
                {
                    RcMemSize newSize = parse.AddSourceParent.IsFloat ? RcMemSize.Float : RcMemSize.Bits32;

                    if (parse.AddSourceOper == RcOperator.AddAccumulator && !parse.AddSourceParent.IsMemRef)
                    {
                        RcOperand zero = RcOperand.Const(0);
                        RcModifiedMemRef memref = parse.AllocModifiedMemRef(
                            parse.AddSourceParent.Size,
                            parse.AddSourceParent,
                            RcOperator.AddAccumulator,
                            zero);
                        parse.AddSourceParent.MemRef = memref;
                        parse.AddSourceParent.Type = RcOperandType.Address;
                    }
                    else if (parse.AddSourceOper == RcOperator.SubParent)
                    {
                        RcOperand zero = parse.AddSourceParent.IsFloat
                            ? RcOperand.FloatConst(0.0)
                            : RcOperand.Const(0);

                        RcModifiedMemRef negate = parse.AllocModifiedMemRef(
                            newSize,
                            parse.AddSourceParent,
                            RcOperator.SubParent,
                            zero);
                        parse.AddSourceParent.MemRef = negate;
                        parse.AddSourceParent.Size = zero.Size;

                        if (parse.AddSourceParent.Type == RcOperandType.Const)
                        {
                            parse.AddSourceParent.Num = RcMemRefHelpers.GetModifiedValue(negate, null, null);
                        }
                    }

                    parse.AddSourceOper = RcOperator.SubAccumulator;
                    var conditionOperand = new RcOperand();
                    RcOperand.FromCondition(condition, conditionOperand, parse);
                    RcOperand.AddSource(conditionOperand, parse, newSize);
                    RcOperand.Copy(conditionOperand, parse.AddSourceParent);
                    parse.AddSourceOper = RcOperator.AddAccumulator;
                }

                parse.IndirectParent.Type = RcOperandType.None;
                break;

            case RcConditionType.Remember:
                if (condition.Operand1.Type == RcOperandType.Recall
                    && condition.Oper == RcOperator.None
                    && parse.AddSourceParent.Type == RcOperandType.None
                    && parse.IndirectParent.Type == RcOperandType.None)
                {
                    break;
                }

                RcOperand.FromCondition(condition, condition.Operand1, parse);

                if (parse.AddSourceParent.Type != RcOperandType.None)
                {
                    RcOperand.AddSource(condition.Operand1, parse, condition.Operand1.Size);
                    condition.Operand1.IsCombining = true;
                }

                RcOperand.Copy(condition.Operand1, parse.Remember);
                parse.AddSourceParent.Type = RcOperandType.None;
                parse.IndirectParent.Type = RcOperandType.None;
                break;

            case RcConditionType.Measured:
                if (parse.IsValue)
                {
                    switch (condition.Oper)
                    {
                        case RcOperator.And:
                        case RcOperator.Xor:
                        case RcOperator.Div:
                        case RcOperator.Mult:
                        case RcOperator.Mod:
                        case RcOperator.Add:
                        case RcOperator.Sub:
                            RcOperand.FromCondition(condition, condition.Operand1, parse);
                            break;
                    }
                }

                goto default;

            default:
                if (parse.AddSourceParent.Type != RcOperandType.None)
                {
                    if (parse.AddSourceOper == RcOperator.AddAccumulator)
                    {
                        parse.AddSourceOper = RcOperator.Add;
                    }

                    RcOperand.AddSource(condition.Operand1, parse, condition.Operand1.Size);
                    condition.Operand1.IsCombining = true;
                }

                parse.AddSourceParent.Type = RcOperandType.None;
                parse.IndirectParent.Type = RcOperandType.None;
                break;
        }
    }

    private static RcOperator ParseOperator(RcCursor cursor)
    {
        char current = cursor.Current;
        switch (current)
        {
            case '=':
                cursor.Advance();
                if (cursor.Current == '=')
                {
                    cursor.Advance();
                }

                return RcOperator.Eq;

            case '!':
                if (cursor.CharAt(1) == '=')
                {
                    cursor.Skip(2);
                    return RcOperator.Ne;
                }

                throw new RcParseException(RcError.InvalidOperator);

            case '<':
                if (cursor.CharAt(1) == '=')
                {
                    cursor.Skip(2);
                    return RcOperator.Le;
                }

                cursor.Advance();
                return RcOperator.Lt;

            case '>':
                if (cursor.CharAt(1) == '=')
                {
                    cursor.Skip(2);
                    return RcOperator.Ge;
                }

                cursor.Advance();
                return RcOperator.Gt;

            case '*':
                cursor.Advance();
                return RcOperator.Mult;

            case '/':
                cursor.Advance();
                return RcOperator.Div;

            case '&':
                cursor.Advance();
                return RcOperator.And;

            case '^':
                cursor.Advance();
                return RcOperator.Xor;

            case '%':
                cursor.Advance();
                return RcOperator.Mod;

            case '+':
                cursor.Advance();
                return RcOperator.Add;

            case '-':
                cursor.Advance();
                return RcOperator.Sub;

            case '\0':
            case '_':
            case 'S':
            case ')':
            case '$':
                return RcOperator.None;

            default:
                throw new RcParseException(RcError.InvalidOperator);
        }
    }
}
