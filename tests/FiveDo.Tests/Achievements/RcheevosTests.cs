using System;
using System.Collections.Generic;
using FiveDo.Achievements.Rcheevos;

namespace FiveDo.Tests.Achievements;

/// <summary>
/// Ported coverage for the managed rcheevos port. The vectors mirror the upstream
/// MIT test suite (test_operand.c, test_condition.c, test_condset.c, test_trigger.c,
/// test_value.c, test_lboard.c, test_richpresence.c, and test_format.c).
/// </summary>
public sealed class RcheevosTests
{
    [Theory]
    [InlineData("0xH1234", 0, 0x1234u)]
    [InlineData("0x 1234", 1, 0x1234u)]
    [InlineData("0x1234", 1, 0x1234u)]
    [InlineData("0xW1234", 2, 0x1234u)]
    [InlineData("0xX1234", 3, 0x1234u)]
    [InlineData("0xL1234", 4, 0x1234u)]
    [InlineData("0xU1234", 5, 0x1234u)]
    [InlineData("0xM1234", 6, 0x1234u)]
    [InlineData("0xN1234", 7, 0x1234u)]
    [InlineData("0xK1234", 14, 0x1234u)]
    [InlineData("0xI1234", 15, 0x1234u)]
    [InlineData("0xJ1234", 16, 0x1234u)]
    [InlineData("0xG1234", 17, 0x1234u)]
    [InlineData("fF1234", 18, 0x1234u)]
    [InlineData("fB1234", 21, 0x1234u)]
    [InlineData("fM1234", 19, 0x1234u)]
    [InlineData("fL1234", 20, 0x1234u)]
    public void ParsesMemoryOperandSizes(string text, int sizeValue, uint address)
    {
        RcOperand operand = ParseOperand(text);
        Assert.Equal(RcOperandType.Address, operand.Type);
        Assert.Equal((RcMemSize)sizeValue, operand.Size);
        Assert.Equal(address, operand.MemRef?.Address);
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("1234", 1234u)]
    [InlineData("H1234", 0x1234u)]
    [InlineData("hABCD", 0xABCDu)]
    [InlineData("+123", 123u)]
    [InlineData("-1", 0xFFFFFFFFu)]
    [InlineData("v1234", 1234u)]
    [InlineData("v-1", 0xFFFFFFFFu)]
    public void ParsesIntegerConstants(string text, uint value)
    {
        RcOperand operand = ParseOperand(text);
        Assert.Equal(RcOperandType.Const, operand.Type);
        Assert.Equal(value, operand.Num);
    }

    [Theory]
    [InlineData("f1.5", 1.5)]
    [InlineData("f-1.5", -1.5)]
    [InlineData("f0.25", 0.25)]
    public void ParsesFloatingPointConstants(string text, double value)
    {
        RcOperand operand = ParseOperand(text);
        Assert.Equal(RcOperandType.Float, operand.Type);
        Assert.Equal(value, operand.Double, 6);
    }

    [Fact]
    public void AlternativeGroupsAccumulateHits()
    {
        var memory = new TestMemory([0x00, 0x12, 0x34, 0xAB, 0x56]);
        RcTrigger trigger = RcTrigger.Parse("0xH0001=16S0xH0002=52S0xL0004=6");

        Assert.False(trigger.Test(memory.Peek, memory));
        AssertHit(trigger, 0, 0, 0);
        AssertHit(trigger, 1, 0, 1);
        AssertHit(trigger, 2, 0, 1);

        memory.Bytes[1] = 16;
        Assert.True(trigger.Test(memory.Peek, memory));
        AssertHit(trigger, 0, 0, 1);
        AssertHit(trigger, 1, 0, 2);
        AssertHit(trigger, 2, 0, 2);

        memory.Bytes[4] = 0;
        Assert.True(trigger.Test(memory.Peek, memory));
        AssertHit(trigger, 0, 0, 2);
        AssertHit(trigger, 1, 0, 3);
        AssertHit(trigger, 2, 0, 2);
    }

    [Fact]
    public void ResetIfClearsHitCounts()
    {
        var memory = new TestMemory([0x00, 0x12, 0x34, 0xAB, 0x56]);
        RcTrigger trigger = RcTrigger.Parse("0xH0001=18(2)_R:0xH0000=1");

        memory.Bytes[1] = 18;
        Assert.False(trigger.Test(memory.Peek, memory));
        AssertHit(trigger, 0, 0, 1);

        Assert.True(trigger.Test(memory.Peek, memory));
        AssertHit(trigger, 0, 0, 2);

        memory.Bytes[0] = 1;
        Assert.False(trigger.Test(memory.Peek, memory));
        AssertHit(trigger, 0, 0, 0);
    }

    [Fact]
    public void PauseIfPreventsEvaluation()
    {
        var memory = new TestMemory([0x00, 0x12, 0x34, 0xAB, 0x56]);
        RcTrigger trigger = RcTrigger.Parse("P:0xH0000=1_0xH0001=18");

        memory.Bytes[1] = 18;
        Assert.True(trigger.Test(memory.Peek, memory));

        memory.Bytes[0] = 1;
        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.Equal(RcTriggerState.Paused, trigger.State);
    }

    [Fact]
    public void AddSourceCombinesBytes()
    {
        var memory = new TestMemory([0x00, 0x12, 0x04, 0xAB, 0x56]);
        RcTrigger trigger = RcTrigger.Parse("A:0xH0001=0_0xH0002=22");

        Assert.True(trigger.Test(memory.Peek, memory));
        AssertHit(trigger, 0, 1, 1);
    }

    [Fact]
    public void SubSourceSubtractsFirstValue()
    {
        var memory = new TestMemory([0x00, 0x12, 0x04, 0xAB, 0x56]);
        RcTrigger trigger = RcTrigger.Parse("B:0xH0002=0_0xH0001=14");

        Assert.True(trigger.Test(memory.Peek, memory));
        AssertHit(trigger, 0, 1, 1);
    }

    [Fact]
    public void AddAddressRedirectsNextRead()
    {
        var memory = new TestMemory([0x02, 0x11, 0x16, 0xAB, 0x56]);
        RcTrigger trigger = RcTrigger.Parse("I:0xH0000=0_0xH0000=22");

        // 0x0000 holds 2, so the next byte(0x0000) reads byte(0x0000 + 2) = 0x22.
        Assert.True(trigger.Test(memory.Peek, memory));

        memory.Bytes[0] = 1;
        Assert.False(trigger.Test(memory.Peek, memory));
    }

    [Fact]
    public void AddSourceMultiplyAndDivide()
    {
        var multiplyMemory = new TestMemory([0x00, 0x06, 0x04, 0x00, 0x00]);
        RcTrigger multiply = RcTrigger.Parse("A:0xH0001*3_0xH0002=22");
        Assert.True(multiply.Test(multiplyMemory.Peek, multiplyMemory));

        multiplyMemory.Bytes[1] = 1;
        Assert.False(multiply.Test(multiplyMemory.Peek, multiplyMemory));

        multiplyMemory.Bytes[2] = 19;
        Assert.True(multiply.Test(multiplyMemory.Peek, multiplyMemory));

        var divideMemory = new TestMemory([0x00, 0x06, 20, 0x00, 0x00]);
        RcTrigger divide = RcTrigger.Parse("A:0xH0001/3_0xH0002=22");
        Assert.True(divide.Test(divideMemory.Peek, divideMemory));

        divideMemory.Bytes[1] = 14;
        Assert.False(divide.Test(divideMemory.Peek, divideMemory));

        divideMemory.Bytes[2] = 18;
        Assert.True(divide.Test(divideMemory.Peek, divideMemory));
    }

    [Fact]
    public void AddSourceAndSubSourceChain()
    {
        var memory = new TestMemory([0x00, 0x0C, 0x34, 0xAB, 0x56]);
        RcTrigger trigger = RcTrigger.Parse("A:0xH0001=0_B:0xL0002=0_0xL0004=14");

        // 0x0C - 4 + 6 = 14.
        Assert.True(trigger.Test(memory.Peek, memory));
    }

    [Fact]
    public void AndNextChainsConditions()
    {
        var memory = new TestMemory([0x00, 20, 20, 20]);
        RcTrigger trigger = RcTrigger.Parse("N:0xH0001=20_N:0xH0002=20_0xH0003=20.3.");

        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.True(trigger.Test(memory.Peek, memory));
    }

    [Fact]
    public void DeltaAndPriorTrackChanges()
    {
        var memory = new TestMemory([0x00, 0x12, 0x34, 0xAB, 0x56]);
        RcTrigger delta = RcTrigger.Parse("0xH0001>d0xH0001");

        Assert.True(delta.Test(memory.Peek, memory));
        Assert.False(delta.Test(memory.Peek, memory));

        memory.Bytes[1] = 0x11;
        Assert.False(delta.Test(memory.Peek, memory));
        memory.Bytes[1] = 0x12;
        Assert.True(delta.Test(memory.Peek, memory));

        var priorMemory = new TestMemory([0x01]);
        RcTrigger prior = RcTrigger.Parse("p0xH0000=0");

        Assert.True(prior.Test(priorMemory.Peek, priorMemory));
        Assert.True(prior.Test(priorMemory.Peek, priorMemory));

        priorMemory.Bytes[0] = 2;
        Assert.False(prior.Test(priorMemory.Peek, priorMemory));
    }

    [Fact]
    public void MeasuredTracksProgress()
    {
        var memory = new TestMemory([0x00, 0x12, 0x34, 0xAB, 0x56]);
        RcTrigger trigger = RcTrigger.Parse("M:0xH0002=52(3)_P:0xH0001=1SR:0xH0003=1");

        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.Equal(1u, trigger.MeasuredValue);
        Assert.Equal(3u, trigger.MeasuredTarget);

        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.Equal(2u, trigger.MeasuredValue);

        memory.Bytes[1] = 1;
        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.Equal(2u, trigger.MeasuredValue);

        memory.Bytes[3] = 1;
        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.Equal(0u, trigger.MeasuredValue);

        memory.Bytes[1] = 2;
        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.Equal(0u, trigger.MeasuredValue);

        memory.Bytes[3] = 0;
        Assert.False(trigger.Test(memory.Peek, memory));
        Assert.Equal(1u, trigger.MeasuredValue);
    }

    [Fact]
    public void BcdAndInvertedTransforms()
    {
        var memory = new TestMemory([0x12, 0x87]);
        RcTrigger bcd = RcTrigger.Parse("b0xH0000=12");
        Assert.True(bcd.Test(memory.Peek, memory));

        RcTrigger inverted = RcTrigger.Parse("~0xH0001=120");
        Assert.True(inverted.Test(memory.Peek, memory));
    }

    [Fact]
    public void BcdOperandTransforms()
    {
        var memory = new TestMemory([0x12, 0x87]);
        RcOperand operand = ParseOperand("b0xH0000");
        List<RcMemRefBase> memrefs = operand.MemRef is null ? [] : [operand.MemRef];
        RcMemRefHelpers.UpdateMemRefs(memrefs, memory.Peek, memory);
        RcOperand.Evaluate(out RcTypedValue value, operand, null);

        Assert.Equal(RcValueType.Unsigned, value.Type);
        Assert.Equal(12u, value.U32);
    }

    [Fact]
    public void RememberAndRecallWork()
    {
        var memory = new TestMemory([0x12, 0x00]);
        RcTrigger trigger = RcTrigger.Parse("K:0xH0000_0xH0001={recall}");

        // Stored value is 0x12; the second condition compares 0x00 to 0x12.
        Assert.False(trigger.Test(memory.Peek, memory));

        memory.Bytes[1] = 0x12;
        Assert.True(trigger.Test(memory.Peek, memory));
    }

    [Fact]
    public void AddHitsCombinesTwoConditions()
    {
        var memory = new TestMemory([0x01, 0x01, 0x01]);
        RcTrigger trigger = RcTrigger.Parse("C:0xH0000=1_C:0xH0001=1_0xH0002=1.2.");

        Assert.True(trigger.Test(memory.Peek, memory));
        Assert.True(trigger.Test(memory.Peek, memory));
        Assert.True(trigger.Test(memory.Peek, memory));
    }

    [Fact]
    public void LegacyValueEvaluates()
    {
        var memory = new TestMemory([0x34, 0x12, 0x00]);
        RcValue value = RcValue.Parse("0x 0000");

        Assert.Equal(0x1234, value.Evaluate(memory.Peek, memory));
    }

    [Fact]
    public void ConditionValueEvaluates()
    {
        var memory = new TestMemory([0x02, 0x03, 0x04]);
        RcValue value = RcValue.Parse("M:0xH0000*0xH0001");

        Assert.Equal(6, value.Evaluate(memory.Peek, memory));
    }

    [Fact]
    public void LeaderboardStartsSubmitsAndResets()
    {
        var memory = new TestMemory([0x00, 0x00, 0x05, 0x00]);
        RcLeaderboard leaderboard = RcLeaderboard.Parse(
            "STA:0xH0000=1::CAN:0xH0001=1::SUB:0xH0002=99::VAL:0xH0002");

        Assert.Equal(RcLeaderboardState.Active, leaderboard.Evaluate(out _, memory.Peek, memory));

        memory.Bytes[0] = 1;
        Assert.Equal(RcLeaderboardState.Started, leaderboard.Evaluate(out int progress, memory.Peek, memory));
        Assert.Equal(5, progress);

        memory.Bytes[0] = 0;
        Assert.Equal(RcLeaderboardState.Started, leaderboard.Evaluate(out _, memory.Peek, memory));

        memory.Bytes[2] = 6;
        Assert.Equal(RcLeaderboardState.Started, leaderboard.Evaluate(out progress, memory.Peek, memory));
        Assert.Equal(6, progress);

        memory.Bytes[1] = 1;
        Assert.Equal(RcLeaderboardState.Canceled, leaderboard.Evaluate(out _, memory.Peek, memory));

        var submitMemory = new TestMemory([0, 0, 5]);
        RcLeaderboard submit = RcLeaderboard.Parse(
            "STA:0xH0000=1::CAN:0xH0001=1::SUB:0xH0002=99::VAL:0xH0002");
        Assert.Equal(RcLeaderboardState.Active, submit.Evaluate(out _, submitMemory.Peek, submitMemory));

        submitMemory.Bytes[0] = 1;
        Assert.Equal(RcLeaderboardState.Started, submit.Evaluate(out _, submitMemory.Peek, submitMemory));

        submitMemory.Bytes[2] = 99;
        Assert.Equal(RcLeaderboardState.Triggered, submit.Evaluate(out int submitted, submitMemory.Peek, submitMemory));
        Assert.Equal(99, submitted);
    }

    [Fact]
    public void RichPresenceDisplaysLookupAndValues()
    {
        const string script =
            "Lookup:Mode\n0=Menu\n1=Playing\n2=Menu\n\n"
            + "Display:\n"
            + "?0xH0000=2?Secret @Mode(0xH0000)\n"
            + "@Mode(0xH0000) @Number(0xH0001)";

        var memory = new TestMemory([1, 42, 0]);
        RcRichPresence presence = RcRichPresence.Parse(script);

        Assert.Equal("Playing 42", presence.Evaluate(memory.Peek, memory));

        memory.Bytes[0] = 2;
        Assert.Equal("Secret Menu", presence.Evaluate(memory.Peek, memory));

        memory.Bytes[0] = 0;
        Assert.Equal("Menu 42", presence.Evaluate(memory.Peek, memory));
    }

    [Theory]
    [InlineData((int)RcFormat.Value, 123456, "123,456")]
    [InlineData((int)RcFormat.Score, 123, "000,123")]
    [InlineData((int)RcFormat.Seconds, 65, "1:05")]
    [InlineData((int)RcFormat.Seconds, 3665, "1h01:05")]
    [InlineData((int)RcFormat.Centisecs, 12345, "2:03.45")]
    [InlineData((int)RcFormat.Minutes, 65, "1h05")]
    [InlineData((int)RcFormat.Frames, 3600, "1:00.00")]
    [InlineData((int)RcFormat.Fixed2, 12345, "123.45")]
    [InlineData((int)RcFormat.Tens, 12, "120")]
    [InlineData((int)RcFormat.UnsignedValue, 42, "42")]
    public void FormatsValues(int formatValue, int value, string expected)
    {
        Assert.Equal(expected, RcFormatter.FormatValue(value, (RcFormat)formatValue));
    }

    [Fact]
    public void ProgressRoundTripRestoresHitCounts()
    {
        var runtime = new RcRuntime();
        Assert.Equal(RcError.Ok, runtime.ActivateAchievement(1, "0xH0000=1.4."));
        var memory = new TestMemory([1]);
        RcAchievement achievement = runtime.Achievements[0];

        // Three true frames count as three hits; the fourth triggers.
        _ = achievement.Trigger.Evaluate(memory.Peek, memory);
        _ = achievement.Trigger.Evaluate(memory.Peek, memory);
        _ = achievement.Trigger.Evaluate(memory.Peek, memory);
        Assert.Equal(3u, achievement.Trigger.Requirement!.Conditions!.CurrentHits);

        byte[] progress = RcProgressCodec.Serialize("game-hash", runtime);
        runtime.Reset();
        Assert.Equal(0u, achievement.Trigger.Requirement!.Conditions!.CurrentHits);
        Assert.True(RcProgressCodec.TryDeserialize(progress, "game-hash", runtime));
        Assert.Equal(3u, achievement.Trigger.Requirement!.Conditions!.CurrentHits);

        Assert.Equal(
            RcTriggerState.Triggered,
            achievement.Trigger.Evaluate(memory.Peek, memory));
    }

    [Fact]
    public void ProgressRoundTripRestoresLeaderboardAttempt()
    {
        var runtime = new RcRuntime();
        Assert.Equal(
            RcError.Ok,
            runtime.ActivateLeaderboard(20, "STA:0xH0000=1::CAN:0xH0001=1::SUB:0xH0002=1::VAL:0xH0003"));
        var memory = new TestMemory([0, 0, 0, 5]);

        runtime.DoFrame(memory.Peek, memory, null);
        memory.Bytes[0] = 1;
        runtime.DoFrame(memory.Peek, memory, null);
        Assert.Equal(RcLeaderboardState.Started, runtime.Leaderboards[0].Leaderboard.State);
        Assert.Equal(5, runtime.Leaderboards[0].Value);

        byte[] progress = RcProgressCodec.Serialize("game-hash", runtime);
        runtime.Reset();
        Assert.True(RcProgressCodec.TryDeserialize(progress, "game-hash", runtime));
        Assert.Equal(RcLeaderboardState.Started, runtime.Leaderboards[0].Leaderboard.State);
        Assert.Equal(5, runtime.Leaderboards[0].Value);
    }

    [Fact]
    public void ProgressRoundTripRejectsAnotherGame()
    {
        var runtime = new RcRuntime();
        Assert.Equal(RcError.Ok, runtime.ActivateAchievement(1, "0xH0000=1"));
        byte[] progress = RcProgressCodec.Serialize("game-a", runtime);

        Assert.False(RcProgressCodec.TryDeserialize(progress, "game-b", runtime));
    }

    private static RcOperand ParseOperand(string text)
    {
        var cursor = new RcCursor(text);
        var parse = new RcParseState();
        RcOperand operand = RcOperand.Parse(cursor, parse);
        Assert.True(cursor.End, $"Unexpected trailing input: {cursor}");
        return operand;
    }

    private static void AssertHit(RcTrigger trigger, int group, int index, uint expected)
    {
        RcCondition condition = GetCondition(trigger, group, index);
        Assert.Equal(expected, condition.CurrentHits);
    }

    private static RcCondition GetCondition(RcTrigger trigger, int group, int index)
    {
        RcCondSet? condset;
        if (group == 0)
        {
            condset = trigger.Requirement;
        }
        else
        {
            condset = trigger.Alternative;
            for (int i = 1; i < group; i++)
            {
                condset = condset?.Next;
            }
        }

        Assert.NotNull(condset);
        int current = 0;
        foreach (RcCondition condition in condset.AllConditions())
        {
            if (current++ == index)
            {
                return condition;
            }
        }

        throw new InvalidOperationException($"Condition {group}/{index} was not found.");
    }

    private sealed class TestMemory(byte[] bytes)
    {
        public byte[] Bytes { get; } = bytes;

        public uint Peek(uint address, uint numBytes, object? userData)
        {
            uint value = 0;
            for (uint index = 0; index < numBytes; index++)
            {
                uint offset = address + index;
                if (offset < Bytes.Length)
                {
                    value |= (uint)Bytes[offset] << (int)(index * 8);
                }
            }

            return value;
        }
    }
}


