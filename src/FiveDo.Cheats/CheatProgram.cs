using FiveDo.Input;

namespace FiveDo.Cheats;

/// <summary>A parsed cheat code ready for per-frame execution.</summary>
internal sealed class CheatProgram
{
    public CheatProgram(CheatStatement[] statements)
    {
        Statements = statements;
    }

    public CheatStatement[] Statements { get; }
}

/// <summary>One statement in a parsed cheat code.</summary>
internal abstract class CheatStatement;

/// <summary>Writes a fixed byte sequence to guest RAM.</summary>
internal sealed class CheatWriteStatement : CheatStatement
{
    public CheatWriteStatement(uint address, byte[] bytes)
    {
        Address = address;
        Bytes = bytes;
    }

    public uint Address { get; }

    public byte[] Bytes { get; }
}

/// <summary>Runs an action when a condition is true.</summary>
internal sealed class CheatConditionalStatement : CheatStatement
{
    public CheatConditionalStatement(CheatCondition condition, CheatStatement action)
    {
        Condition = condition;
        Action = action;
    }

    public CheatCondition Condition { get; }

    public CheatStatement Action { get; }
}

/// <summary>A boolean condition evaluated against the running machine.</summary>
internal abstract class CheatCondition;

/// <summary>Compares a guest memory value.</summary>
internal sealed class CheatMemoryCondition : CheatCondition
{
    public CheatMemoryCondition(uint address, int size, CheatComparison comparison, uint value)
    {
        Address = address;
        Size = size;
        Comparison = comparison;
        Value = value;
    }

    public uint Address { get; }

    public int Size { get; }

    public CheatComparison Comparison { get; }

    public uint Value { get; }
}

/// <summary>Tests the current state of a player-1 button.</summary>
internal sealed class CheatButtonCondition : CheatCondition
{
    public CheatButtonCondition(ControllerButton button)
    {
        Button = button;
    }

    public ControllerButton Button { get; }
}

/// <summary>Combines two conditions with a logical operator.</summary>
internal sealed class CheatLogicalCondition : CheatCondition
{
    public CheatLogicalCondition(CheatCondition left, CheatCondition right, bool isAnd)
    {
        Left = left;
        Right = right;
        IsAnd = isAnd;
    }

    public CheatCondition Left { get; }

    public CheatCondition Right { get; }

    public bool IsAnd { get; }
}

/// <summary>Negates a condition.</summary>
internal sealed class CheatNotCondition : CheatCondition
{
    public CheatNotCondition(CheatCondition inner)
    {
        Inner = inner;
    }

    public CheatCondition Inner { get; }
}

/// <summary>Memory comparison operators.</summary>
internal enum CheatComparison
{
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}
