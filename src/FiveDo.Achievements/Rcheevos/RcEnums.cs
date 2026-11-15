namespace FiveDo.Achievements.Rcheevos;

/// <summary>rcheevos memory sizes (<c>RC_MEMSIZE_*</c>).</summary>
internal enum RcMemSize : byte
{
    Bits8 = 0,
    Bits16 = 1,
    Bits24 = 2,
    Bits32 = 3,
    Low = 4,
    High = 5,
    Bit0 = 6,
    Bit1 = 7,
    Bit2 = 8,
    Bit3 = 9,
    Bit4 = 10,
    Bit5 = 11,
    Bit6 = 12,
    Bit7 = 13,
    Bitcount = 14,
    Bits16Be = 15,
    Bits24Be = 16,
    Bits32Be = 17,
    Float = 18,
    Mbf32 = 19,
    Mbf32Le = 20,
    FloatBe = 21,
    Double32 = 22,
    Double32Be = 23,
    Variable = 24,
}

/// <summary>rcheevos operand types (<c>RC_OPERAND_*</c>).</summary>
internal enum RcOperandType : byte
{
    Address = 0,
    Delta = 1,
    Const = 2,
    Float = 3,
    Func = 4,
    Prior = 5,
    Bcd = 6,
    Inverted = 7,
    Recall = 8,
    None = 0xFF,
}

/// <summary>rcheevos condition types (<c>RC_CONDITION_*</c>).</summary>
internal enum RcConditionType : byte
{
    Standard = 0,
    PauseIf = 1,
    ResetIf = 2,
    MeasuredIf = 3,
    Trigger = 4,
    Measured = 5,
    AddSource = 6,
    SubSource = 7,
    AddAddress = 8,
    Remember = 9,
    AddHits = 10,
    SubHits = 11,
    ResetNextIf = 12,
    AndNext = 13,
    OrNext = 14,
}

/// <summary>rcheevos comparison/combining operators (<c>RC_OPERATOR_*</c>).</summary>
internal enum RcOperator : byte
{
    Eq = 0,
    Lt = 1,
    Le = 2,
    Gt = 3,
    Ge = 4,
    Ne = 5,
    None = 6,
    Mult = 7,
    Div = 8,
    And = 9,
    Xor = 10,
    Mod = 11,
    Add = 12,
    Sub = 13,
    SubParent = 14,
    AddAccumulator = 15,
    SubAccumulator = 16,
    IndirectRead = 17,
}

/// <summary>rcheevos trigger states (<c>RC_TRIGGER_STATE_*</c>).</summary>
internal enum RcTriggerState : byte
{
    Inactive = 0,
    Waiting = 1,
    Active = 2,
    Paused = 3,
    Reset = 4,
    Triggered = 5,
    Primed = 6,
    Disabled = 7,
}

/// <summary>rcheevos leaderboard states (<c>RC_LBOARD_STATE_*</c>).</summary>
internal enum RcLeaderboardState : byte
{
    Inactive = 0,
    Waiting = 1,
    Active = 2,
    Started = 3,
    Canceled = 4,
    Triggered = 5,
    Disabled = 6,
}

/// <summary>rcheevos value formats (<c>RC_FORMAT_*</c>).</summary>
internal enum RcFormat : byte
{
    Frames = 0,
    Seconds = 1,
    Centisecs = 2,
    Score = 3,
    Value = 4,
    Minutes = 5,
    SecondsAsMinutes = 6,
    Float1 = 7,
    Float2 = 8,
    Float3 = 9,
    Float4 = 10,
    Float5 = 11,
    Float6 = 12,
    Fixed1 = 13,
    Fixed2 = 14,
    Fixed3 = 15,
    Tens = 16,
    Hundreds = 17,
    Thousands = 18,
    UnsignedValue = 19,
    Unformatted = 20,

    // rich-presence-only display types
    String = 101,
    Lookup = 102,
    UnknownMacro = 103,
    AsciiChar = 104,
    UnicodeChar = 105,
}

/// <summary>rcheevos runtime event types (<c>RC_RUNTIME_EVENT_*</c>).</summary>
internal enum RcRuntimeEventType : byte
{
    AchievementActivated = 0,
    AchievementPaused = 1,
    AchievementReset = 2,
    AchievementTriggered = 3,
    AchievementPrimed = 4,
    LeaderboardStarted = 5,
    LeaderboardCanceled = 6,
    LeaderboardUpdated = 7,
    LeaderboardTriggered = 8,
    AchievementDisabled = 9,
    LeaderboardDisabled = 10,
    AchievementUnprimed = 11,
    AchievementProgressUpdated = 12,
}

/// <summary>rcheevos memref kinds (<c>RC_MEMREF_TYPE_*</c>).</summary>
internal enum RcMemRefType : byte
{
    MemRef = 0,
    ModifiedMemRef = 1,
    Value = 2,
}

/// <summary>rcheevos typed value kinds (<c>RC_VALUE_TYPE_*</c>).</summary>
internal enum RcValueType : byte
{
    None = 0,
    Unsigned = 1,
    Signed = 2,
    Float = 3,
}
