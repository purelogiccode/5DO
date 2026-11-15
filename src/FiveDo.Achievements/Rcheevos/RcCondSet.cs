// Ported from rcheevos (MIT) - src/rcheevos/condset.c.
using System.Collections.Generic;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>A group of conditions that must be satisfied together.</summary>
internal sealed class RcCondSet
{
    public RcCondSet? Next;

    public RcCondition? Conditions;

    public int NumPauseConditions;

    public int NumResetConditions;

    public int NumHitTargetConditions;

    public int NumMeasuredConditions;

    public int NumOtherConditions;

    public int NumIndirectConditions;

    public bool IsPaused;

    internal List<RcCondition> PauseConditions { get; } = [];

    internal List<RcCondition> ResetConditions { get; } = [];

    internal List<RcCondition> HitTargetConditions { get; } = [];

    internal List<RcCondition> MeasuredConditions { get; } = [];

    internal List<RcCondition> OtherConditions { get; } = [];

    internal List<RcCondition> IndirectConditions { get; } = [];

    public static RcCondSet Parse(RcCursor cursor, RcParseState parse)
    {
        if (cursor.Current is 'S' or 's' || cursor.End)
        {
            return new RcCondSet();
        }

        var source = new List<RcCondition>();
        uint measuredTarget = 0;
        parse.AddSourceOper = RcOperator.None;
        parse.AddSourceParent.Type = RcOperandType.None;
        parse.IndirectParent.Type = RcOperandType.None;
        parse.Remember.Type = RcOperandType.None;

        while (true)
        {
            RcCondition condition = RcCondition.Parse(cursor, parse);

            if (condition.Oper == RcOperator.None && !parse.IgnoreNonParseErrors)
            {
                switch (condition.Type)
                {
                    case RcConditionType.AddAddress:
                    case RcConditionType.AddSource:
                    case RcConditionType.SubSource:
                    case RcConditionType.Remember:
                        break;

                    case RcConditionType.Measured when parse.IsValue:
                        break;

                    default:
                        throw new RcParseException(RcError.InvalidOperator);
                }
            }

            if (condition.Type == RcConditionType.Measured)
            {
                if (measuredTarget != 0)
                {
                    if (!parse.IgnoreNonParseErrors)
                    {
                        throw new RcParseException(RcError.MultipleMeasured);
                    }
                }
                else if (parse.IsValue)
                {
                    measuredTarget = uint.MaxValue;
                    if (!RcCondition.IsModifying(condition.Oper))
                    {
                        // Measuring a comparison in a value produces a tally.
                        condition.RequiredHits = measuredTarget;
                    }
                }
                else if (condition.RequiredHits != 0)
                {
                    measuredTarget = condition.RequiredHits;
                }
                else if (condition.Operand2.Type == RcOperandType.Const)
                {
                    measuredTarget = condition.Operand2.Num;
                }
                else if (condition.Operand2.Type == RcOperandType.Float)
                {
                    measuredTarget = (uint)condition.Operand2.Double;
                }
                else if (!parse.IgnoreNonParseErrors)
                {
                    throw new RcParseException(RcError.InvalidMeasuredTarget);
                }

                if (parse.MeasuredTarget != 0 && measuredTarget != parse.MeasuredTarget
                    && !parse.IgnoreNonParseErrors)
                {
                    throw new RcParseException(RcError.MultipleMeasured);
                }

                parse.MeasuredTarget = measuredTarget;
            }

            if (parse.IsValue && condition.Type is RcConditionType.Standard or RcConditionType.Trigger
                && !parse.IgnoreNonParseErrors)
            {
                throw new RcParseException(RcError.InvalidValueFlag);
            }

            RcCondition.UpdateParseState(condition, parse);
            source.Add(condition);

            if (cursor.Current != '_')
            {
                break;
            }

            cursor.Advance();
        }

        var result = new RcCondSet();

        // rcheevos reorders conditions into evaluation buckets. Combining conditions
        // (AddHits/AndNext/OrNext/...) belong to the bucket of the next real condition.
        var pendingCombining = new List<RcCondition>();
        foreach (RcCondition condition in source)
        {
            RcConditionClassification classification = Classify(condition);
            if (classification is RcConditionClassification.Combining or RcConditionClassification.Indirect)
            {
                if (classification == RcConditionClassification.Combining)
                {
                    pendingCombining.Add(condition);
                }
                else
                {
                    AddToBucket(result, [condition], RcConditionClassification.Indirect);
                }

                continue;
            }

            AddToBucket(result, pendingCombining, classification);
            pendingCombining.Clear();
            AddToBucket(result, [condition], classification);
        }

        if (pendingCombining.Count > 0)
        {
            AddToBucket(result, pendingCombining, RcConditionClassification.Other);
        }

        result.NumPauseConditions = result.PauseConditions.Count;
        result.NumResetConditions = result.ResetConditions.Count;
        result.NumHitTargetConditions = result.HitTargetConditions.Count;
        result.NumMeasuredConditions = result.MeasuredConditions.Count;
        result.NumOtherConditions = result.OtherConditions.Count;
        result.NumIndirectConditions = result.IndirectConditions.Count;

        result.Conditions = BuildLinkedList(source);
        if (result.NumPauseConditions > 0 && parse.Remember.Type != RcOperandType.None)
        {
            result.UpdatePauseRemember();
        }

        return result;
    }

    public static RcCondSet ParseEmpty() => new();

    /// <summary>Builds a value condition set from the legacy value parser's conditions.</summary>
    public static RcCondSet FromLegacyValue(IReadOnlyList<RcCondition> conditions)
    {
        var result = new RcCondSet();
        result.MeasuredConditions.AddRange(conditions);
        result.NumMeasuredConditions = conditions.Count;
        result.Conditions = BuildLinkedList(conditions);
        return result;
    }

    public void Reset()
    {
        foreach (RcCondition condition in AllConditions())
        {
            condition.CurrentHits = 0;
        }
    }

    public IEnumerable<RcCondition> AllConditions()
    {
        if (Conditions is not null)
        {
            for (RcCondition? condition = Conditions; condition is not null; condition = condition.Next)
            {
                yield return condition;
            }
        }
    }

    public static bool Test(RcCondSet self, RcEvalState evalState)
    {
        evalState.MeasuredValue.Type = RcValueType.None;
        evalState.AddHits = 0;
        evalState.IsTrue = true;
        evalState.IsPrimed = true;
        evalState.IsPaused = false;
        evalState.CanMeasure = true;
        evalState.MeasuredFromHits = false;
        evalState.AndNext = true;
        evalState.OrNext = false;
        evalState.ResetNext = false;
        evalState.StopProcessing = false;

        if (self.NumPauseConditions > 0)
        {
            TestInternal(self.PauseConditions, evalState, canShortCircuit: true);
            self.IsPaused = evalState.IsPaused;
            if (self.IsPaused)
            {
                return false;
            }
        }

        if (self.NumResetConditions > 0)
        {
            TestInternal(self.ResetConditions, evalState, canShortCircuit: false);
        }

        if (self.NumHitTargetConditions > 0)
        {
            if (!evalState.WasReset)
            {
                TestInternal(self.HitTargetConditions, evalState, canShortCircuit: false);
            }
        }

        if (self.NumMeasuredConditions > 0)
        {
            if (evalState.WasReset)
            {
                foreach (RcCondition condition in self.MeasuredConditions)
                {
                    condition.CurrentHits = 0;
                }
            }

            TestInternal(self.MeasuredConditions, evalState, canShortCircuit: false);

            if (evalState.MeasuredValue.Type != RcValueType.None)
            {
                if (!evalState.CanMeasure || (evalState.MeasuredFromHits && evalState.WasReset))
                {
                    evalState.MeasuredValue.Type = RcValueType.Unsigned;
                    evalState.MeasuredValue.U32 = 0;
                }
            }
        }

        if (self.NumOtherConditions > 0)
        {
            if (evalState.IsTrue)
            {
                TestInternal(self.OtherConditions, evalState, canShortCircuit: false);
            }
            else if (!evalState.WasReset)
            {
                TestInternal(self.OtherConditions, evalState, canShortCircuit: false);
            }
        }

        return evalState.IsTrue;
    }

    private static void AddToBucket(
        RcCondSet condset,
        IReadOnlyList<RcCondition> conditions,
        RcConditionClassification classification)
    {
        List<RcCondition> bucket = classification switch
        {
            RcConditionClassification.Pause => condset.PauseConditions,
            RcConditionClassification.Reset => condset.ResetConditions,
            RcConditionClassification.HitTarget => condset.HitTargetConditions,
            RcConditionClassification.Measured => condset.MeasuredConditions,
            RcConditionClassification.Indirect => condset.IndirectConditions,
            _ => condset.OtherConditions,
        };

        bucket.AddRange(conditions);
    }

    private static RcCondition? BuildLinkedList(IReadOnlyList<RcCondition> conditions)
    {
        RcCondition? head = null;
        RcCondition? previous = null;

        foreach (RcCondition condition in conditions)
        {
            if (previous is null)
            {
                head = condition;
            }
            else
            {
                previous.Next = condition;
            }

            previous = condition;
        }

        if (previous is not null)
        {
            previous.Next = null;
        }

        return head;
    }

    private static RcConditionClassification Classify(RcCondition condition)
    {
        switch (condition.Type)
        {
            case RcConditionType.PauseIf:
                return RcConditionClassification.Pause;

            case RcConditionType.ResetIf:
                return RcConditionClassification.Reset;

            case RcConditionType.AddAddress:
            case RcConditionType.AddSource:
            case RcConditionType.SubSource:
                return RcConditionClassification.Indirect;

            case RcConditionType.AddHits:
            case RcConditionType.AndNext:
            case RcConditionType.OrNext:
            case RcConditionType.Remember:
            case RcConditionType.ResetNextIf:
            case RcConditionType.SubHits:
                return RcConditionClassification.Combining;

            case RcConditionType.Measured:
            case RcConditionType.MeasuredIf:
                return RcConditionClassification.Measured;

            default:
                return condition.RequiredHits != 0
                    ? RcConditionClassification.HitTarget
                    : RcConditionClassification.Other;
        }
    }

    private static void TestInternal(
        IReadOnlyList<RcCondition> conditions,
        RcEvalState evalState,
        bool canShortCircuit)
    {
        foreach (RcCondition condition in conditions)
        {
            switch (condition.Type)
            {
                case RcConditionType.Standard:
                    EvaluateStandard(condition, evalState);
                    break;

                case RcConditionType.PauseIf:
                    EvaluatePauseIf(condition, evalState);
                    break;

                case RcConditionType.ResetIf:
                    EvaluateResetIf(condition, evalState);
                    break;

                case RcConditionType.Trigger:
                    EvaluateTrigger(condition, evalState);
                    break;

                case RcConditionType.Measured:
                    EvaluateMeasured(condition, evalState);
                    break;

                case RcConditionType.MeasuredIf:
                    EvaluateMeasuredIf(condition, evalState);
                    break;

                case RcConditionType.AddSource:
                case RcConditionType.SubSource:
                case RcConditionType.AddAddress:
                case RcConditionType.Remember:
                    break;

                case RcConditionType.AddHits:
                    EvaluateAddHits(condition, evalState);
                    break;

                case RcConditionType.SubHits:
                    EvaluateSubHits(condition, evalState);
                    break;

                case RcConditionType.ResetNextIf:
                    evalState.ResetNext = EvaluateConditionNoAddHits(condition, evalState);
                    break;

                case RcConditionType.AndNext:
                    evalState.AndNext = EvaluateConditionNoAddHits(condition, evalState);
                    break;

                case RcConditionType.OrNext:
                    evalState.OrNext = EvaluateConditionNoAddHits(condition, evalState);
                    break;

                default:
                    evalState.StopProcessing = true;
                    evalState.IsTrue = false;
                    evalState.IsPrimed = false;
                    break;
            }

            if (evalState.StopProcessing && canShortCircuit)
            {
                break;
            }
        }
    }

    private static bool EvaluateConditionNoAddHits(RcCondition condition, RcEvalState evalState)
    {
        bool condValid = RcCondition.Test(condition, evalState);
        condition.IsTrue = condValid ? (byte)1 : (byte)0;

        if (evalState.ResetNext)
        {
            evalState.WasCondReset |= condition.CurrentHits != 0;
            condition.CurrentHits = 0;
            condValid = false;
        }
        else
        {
            condValid &= evalState.AndNext;
            condValid |= evalState.OrNext;

            if (condValid)
            {
                evalState.HasHits = true;

                if (condition.RequiredHits == 0)
                {
                    ++condition.CurrentHits;
                }
                else if (condition.CurrentHits < condition.RequiredHits)
                {
                    ++condition.CurrentHits;
                    condValid = condition.CurrentHits == condition.RequiredHits;
                }
            }
            else if (condition.CurrentHits > 0)
            {
                evalState.HasHits = true;
                condValid = condition.CurrentHits == condition.RequiredHits;
            }
        }

        evalState.AndNext = true;
        evalState.OrNext = false;

        return condValid;
    }

    private static uint EvaluateTotalHits(RcCondition condition, RcEvalState evalState)
    {
        uint totalHits = condition.CurrentHits;

        if (condition.RequiredHits != 0)
        {
            int signedHits = (int)condition.CurrentHits + evalState.AddHits;
            totalHits = signedHits >= 0 ? (uint)signedHits : 0;
        }

        evalState.AddHits = 0;
        return totalHits;
    }

    private static bool EvaluateCondition(RcCondition condition, RcEvalState evalState)
    {
        bool condValid = EvaluateConditionNoAddHits(condition, evalState);

        if (evalState.AddHits != 0 && condition.RequiredHits != 0)
        {
            uint totalHits = EvaluateTotalHits(condition, evalState);
            condValid = totalHits >= condition.RequiredHits;
        }

        evalState.ResetNext = false;
        return condValid;
    }

    private static void EvaluateStandard(RcCondition condition, RcEvalState evalState)
    {
        bool condValid = EvaluateCondition(condition, evalState);

        evalState.IsTrue &= condValid;
        evalState.IsPrimed &= condValid;
    }

    private static void EvaluatePauseIf(RcCondition condition, RcEvalState evalState)
    {
        bool condValid = EvaluateCondition(condition, evalState);

        if (condValid)
        {
            evalState.IsPaused = true;
            evalState.IsTrue = evalState.IsPrimed = false;
            evalState.StopProcessing = true;
        }
        else if (condition.RequiredHits == 0)
        {
            condition.CurrentHits = 0;
        }
    }

    private static void EvaluateResetIf(RcCondition condition, RcEvalState evalState)
    {
        bool condValid = EvaluateCondition(condition, evalState);

        if (condValid)
        {
            condition.IsTrue |= 0x02;
            evalState.IsTrue = evalState.IsPrimed = false;
            evalState.WasReset = true;
            evalState.StopProcessing = true;
        }
    }

    private static void EvaluateTrigger(RcCondition condition, RcEvalState evalState)
    {
        bool condValid = EvaluateCondition(condition, evalState);
        evalState.IsTrue &= condValid;
    }

    private static void EvaluateMeasured(RcCondition condition, RcEvalState evalState)
    {
        if (condition.RequiredHits == 0)
        {
            EvaluateStandard(condition, evalState);
            RcOperand.Evaluate(out evalState.MeasuredValue, condition.Operand1, evalState);
            evalState.MeasuredFromHits = false;
        }
        else
        {
            _ = EvaluateConditionNoAddHits(condition, evalState);
            uint totalHits = EvaluateTotalHits(condition, evalState);

            bool condValid = totalHits >= condition.RequiredHits;
            evalState.IsTrue &= condValid;
            evalState.IsPrimed &= condValid;

            evalState.MeasuredValue = RcTypedValue.FromUnsigned(totalHits);
            evalState.MeasuredFromHits = true;
            evalState.ResetNext = false;
        }
    }

    private static void EvaluateMeasuredIf(RcCondition condition, RcEvalState evalState)
    {
        bool condValid = EvaluateCondition(condition, evalState);

        evalState.IsTrue &= condValid;
        evalState.IsPrimed &= condValid;
        evalState.CanMeasure &= condValid;
    }

    private static void EvaluateAddHits(RcCondition condition, RcEvalState evalState)
    {
        _ = EvaluateConditionNoAddHits(condition, evalState);
        evalState.AddHits += (int)condition.CurrentHits;
        evalState.ResetNext = false;
    }

    private static void EvaluateSubHits(RcCondition condition, RcEvalState evalState)
    {
        _ = EvaluateConditionNoAddHits(condition, evalState);
        evalState.AddHits -= (int)condition.CurrentHits;
        evalState.ResetNext = false;
    }

    private void UpdatePauseRemember()
    {
        RcOperand? pauseRemember = null;
        for (int index = 0; index < PauseConditions.Count; index++)
        {
            RcCondition condition = PauseConditions[index];
            if (condition.Type == RcConditionType.Remember)
            {
                pauseRemember = condition.Operand1;
            }
            else if (pauseRemember is null)
            {
                ClearUnboundRecall(condition.Operand1);
                ClearUnboundRecall(condition.Operand2);
            }
        }

        if (pauseRemember is null)
        {
            return;
        }

        bool stop = false;
        foreach (RcCondition condition in AllConditions())
        {
            if (!stop)
            {
                if (!PauseConditions.Contains(condition))
                {
                    UpdateRecallOperand(condition.Operand1, pauseRemember);
                    UpdateRecallOperand(condition.Operand2, pauseRemember);
                }

                if (condition.Type == RcConditionType.Remember)
                {
                    stop = true;
                }
            }
        }
    }

    private static void ClearUnboundRecall(RcOperand operand)
    {
        if (operand.Type == RcOperandType.Recall
            && RcOperand.IsMemRefType(operand.MemRefAccessType))
        {
            operand.MemRef = null;
        }
    }

    private static void UpdateRecallOperand(RcOperand operand, RcOperand remember)
    {
        if (operand.Type == RcOperandType.Recall)
        {
            if (RcOperand.IsMemRefType(operand.MemRefAccessType) && operand.MemRef is null)
            {
                RcOperand.Copy(remember, operand);
                operand.MemRefAccessType = operand.Type;
                operand.Type = RcOperandType.Recall;
            }
        }
        else if (operand.IsMemRef && operand.MemRef is RcModifiedMemRef modified)
        {
            UpdateRecallOperand(modified.Parent, remember);
            UpdateRecallOperand(modified.Modifier, remember);
        }
    }

    private enum RcConditionClassification
    {
        Combining,
        Pause,
        Reset,
        HitTarget,
        Measured,
        Other,
        Indirect,
    }
}
