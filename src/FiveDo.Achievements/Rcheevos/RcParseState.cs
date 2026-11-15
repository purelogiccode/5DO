// Ported from rcheevos (MIT) - src/rcheevos/rc_internal.h (parse state) and
// src/rcheevos/operand.c (parsing helpers).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>A mutable cursor over a RetroAchievements definition string.</summary>
internal sealed class RcCursor(string text)
{
    public string Text { get; } = text;

    public int Position { get; set; }

    public char Current => Position < Text.Length ? Text[Position] : '\0';

    public bool End => Position >= Text.Length;

    public char CharAt(int offset)
    {
        int index = Position + offset;
        return (uint)index < (uint)Text.Length ? Text[index] : '\0';
    }

    public char Advance() => Position < Text.Length ? Text[Position++] : '\0';

    public void Skip(int count) => Position += count;

    public override string ToString() => Position < Text.Length ? Text[Position..] : string.Empty;
}

/// <summary>State shared by the condition/value/trigger parsers.</summary>
internal sealed class RcParseState
{
    public bool IsValue { get; set; }

    public bool IgnoreNonParseErrors => false;

    public uint MeasuredTarget { get; set; }

    public bool MeasuredAsPercent { get; set; }

    public bool HasRequiredHits { get; set; }

    public RcOperand AddSourceParent { get; set; } = new();

    public RcOperator AddSourceOper { get; set; } = RcOperator.None;

    public RcOperand IndirectParent { get; set; } = new();

    public RcOperand Remember { get; set; } = new();

    public List<RcMemRefBase> MemRefs { get; } = [];

    public List<RcValue> Variables { get; } = [];

    public RcMemRef AllocMemRef(uint address, RcMemSize size)
    {
        foreach (RcMemRefBase memref in MemRefs)
        {
            if (memref is RcMemRef plain && plain.Address == address && plain.Value.Size == size)
            {
                return plain;
            }
        }

        var created = new RcMemRef(address, size);
        MemRefs.Add(created);
        return created;
    }

    public RcModifiedMemRef AllocModifiedMemRef(
        RcMemSize size,
        RcOperand parent,
        RcOperator modifierType,
        RcOperand modifier)
    {
        foreach (RcMemRefBase memref in MemRefs)
        {
            if (memref is RcModifiedMemRef modified
                && modified.Value.Size == size
                && modified.ModifierType == modifierType
                && RcOperand.AreEqual(modified.Parent, parent)
                && RcOperand.AreEqual(modified.Modifier, modifier))
            {
                return modified;
            }
        }

        uint address = modifier.Type == RcOperandType.Const ? modifier.Num : modifier.MemRef?.Address ?? 0;
        int depth = parent.MemRef is RcModifiedMemRef parentModified ? parentModified.Depth + 1 : 0;

        var created = new RcModifiedMemRef
        {
            Parent = parent.Clone(),
            Modifier = modifier.Clone(),
            ModifierType = modifierType,
            Depth = depth,
        };
        created.Address = address;
        created.Value.MemRefType = RcMemRefType.ModifiedMemRef;
        created.Value.Size = size;
        created.Value.Type = RcMemRefHelpers.IsFloat(size) ? RcValueType.Float : RcValueType.Unsigned;

        MemRefs.Add(created);
        return created;
    }
}

/// <summary>Small ASCII parsing helpers matching the C locale used by rcheevos.</summary>
internal static class RcText
{
    public static bool IsAlpha(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    public static bool IsDigit(char value) => value is >= '0' and <= '9';

    public static bool IsAlnum(char value) => IsAlpha(value) || IsDigit(value);

    public static bool IsSpace(char value) => value is ' ' or '\t' or '\r' or '\n' or '\f' or '\v';

    public static bool TryParseUnsigned(RcCursor cursor, int radix, out uint value, out int end)
    {
        int index = cursor.Position;
        string text = cursor.Text;

        while (index < text.Length && IsSpace(text[index]))
        {
            ++index;
        }

        bool negative = false;
        if (index < text.Length && (text[index] == '+' || text[index] == '-'))
        {
            negative = text[index] == '-';
            ++index;
        }

        int start = index;
        ulong result = 0;
        while (index < text.Length)
        {
            int digit = DigitValue(text[index]);
            if (digit < 0 || digit >= radix)
            {
                break;
            }

            result = (result * (uint)radix) + (uint)digit;
            if (result > uint.MaxValue)
            {
                result = uint.MaxValue;
            }

            ++index;
        }

        end = index;
        if (index == start)
        {
            value = 0;
            return false;
        }

        value = negative ? unchecked((uint)(-(long)result)) : (uint)result;
        return true;
    }

    public static bool TryParseUInt32(RcCursor cursor, int radix, out uint value, out int end)
    {
        if (!TryParseUnsigned(cursor, radix, out value, out end))
        {
            return false;
        }

        return true;
    }

    public static string FormatInvariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private static int DigitValue(char value)
    {
        if (value is >= '0' and <= '9')
        {
            return value - '0';
        }

        if (value is >= 'a' and <= 'f')
        {
            return value - 'a' + 10;
        }

        if (value is >= 'A' and <= 'F')
        {
            return value - 'A' + 10;
        }

        return -1;
    }
}
