// Ported from rcheevos (MIT) - include/rc_error.h and src/rcheevos/*.c.
// The original copyright and MIT permission notice are preserved in
// THIRD-PARTY-NOTICES.md.

using System;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>rcheevos error codes, kept in the same order as <c>rc_error.h</c>.</summary>
internal enum RcError
{
    Ok = 0,
    InvalidFuncOperand = -1,
    InvalidMemoryOperand = -2,
    InvalidConstOperand = -3,
    InvalidFpOperand = -4,
    InvalidConditionType = -5,
    InvalidOperator = -6,
    InvalidRequiredHits = -7,
    DuplicatedStart = -8,
    DuplicatedCancel = -9,
    DuplicatedSubmit = -10,
    DuplicatedValue = -11,
    DuplicatedProgress = -12,
    MissingStart = -13,
    MissingCancel = -14,
    MissingSubmit = -15,
    MissingValue = -16,
    InvalidLboardField = -17,
    MissingDisplayString = -18,
    OutOfMemory = -19,
    InvalidValueFlag = -20,
    MissingValueMeasured = -21,
    MultipleMeasured = -22,
    InvalidMeasuredTarget = -23,
    InvalidComparison = -24,
    InvalidState = -25,
    InvalidJson = -26,
    ApiFailure = -27,
    LoginRequired = -28,
    NoGameLoaded = -29,
    HardcoreDisabled = -30,
    Aborted = -31,
    NoResponse = -32,
    AccessDenied = -33,
    InvalidCredentials = -34,
    ExpiredToken = -35,
    InsufficientBuffer = -36,
    InvalidVariableName = -37,
    UnknownVariableName = -38,
    NotFound = -39,
    InvalidValue = -40,
}

/// <summary>Thrown when a RetroAchievements definition cannot be parsed.</summary>
internal sealed class RcParseException : Exception
{
    public RcParseException()
    {
    }

    public RcParseException(string message)
        : base(message)
    {
    }

    public RcParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public RcParseException(RcError error, string? detail = null, int line = 0)
        : base(BuildMessage(error, detail, line))
    {
        Error = error;
    }

    public RcError Error { get; }

    private static string BuildMessage(RcError error, string? detail, int line)
    {
        string message = error switch
        {
            RcError.InvalidFuncOperand => "Invalid function operand.",
            RcError.InvalidMemoryOperand => "Invalid memory operand.",
            RcError.InvalidConstOperand => "Invalid constant operand.",
            RcError.InvalidFpOperand => "Invalid floating-point operand.",
            RcError.InvalidConditionType => "Invalid condition type.",
            RcError.InvalidOperator => "Invalid operator.",
            RcError.InvalidRequiredHits => "Invalid required-hit count.",
            RcError.DuplicatedStart => "Duplicate Start condition.",
            RcError.DuplicatedCancel => "Duplicate Cancel condition.",
            RcError.DuplicatedSubmit => "Duplicate Submit condition.",
            RcError.DuplicatedValue => "Duplicate Value condition.",
            RcError.DuplicatedProgress => "Duplicate Progress condition.",
            RcError.MissingStart => "Missing Start condition.",
            RcError.MissingCancel => "Missing Cancel condition.",
            RcError.MissingSubmit => "Missing Submit condition.",
            RcError.MissingValue => "Missing Value condition.",
            RcError.InvalidLboardField => "Invalid leaderboard field.",
            RcError.MissingDisplayString => "Missing rich-presence display string.",
            RcError.InvalidValueFlag => "Invalid flag in a value expression.",
            RcError.MissingValueMeasured => "Missing Measured condition in a value.",
            RcError.MultipleMeasured => "Multiple Measured conditions have different targets.",
            RcError.InvalidMeasuredTarget => "Invalid Measured target.",
            RcError.InvalidVariableName => "Invalid variable name.",
            RcError.UnknownVariableName => "Unknown variable name.",
            RcError.InvalidValue => "Invalid value expression.",
            _ => "Invalid RetroAchievements definition.",
        };

        if (!string.IsNullOrEmpty(detail))
        {
            message += " " + detail;
        }

        if (line > 0)
        {
            message += $" (line {line})";
        }

        return message;
    }
}
