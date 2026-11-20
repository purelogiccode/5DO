// Ported from rcheevos (MIT) - src/rcheevos/operand.c.
using System;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>A condition operand: a constant, a memory reference, or a derived value.</summary>
internal sealed class RcOperand
{
    public const int MaxVariableNameLength = 15;

    public RcMemRefBase? MemRef;

    public uint Num;

    public double Double;

    public RcOperandType Type = RcOperandType.None;

    public RcMemSize Size;

    public RcOperandType MemRefAccessType = RcOperandType.None;

    public bool IsCombining;

    internal RcOperand Clone() => (RcOperand)MemberwiseClone();

    internal static RcOperand Const(uint value) => new()
    {
        Type = RcOperandType.Const,
        Size = RcMemSize.Bits32,
        MemRefAccessType = RcOperandType.None,
        Num = value,
    };

    internal static RcOperand FloatConst(double value) => new()
    {
        Type = RcOperandType.Float,
        Size = RcMemSize.Float,
        MemRefAccessType = RcOperandType.None,
        Double = value,
    };

    internal static bool AreEqual(RcOperand left, RcOperand right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Type != right.Type)
        {
            return false;
        }

        switch (left.Type)
        {
            case RcOperandType.Const:
                return left.Num == right.Num;
            case RcOperandType.Float:
                return left.Double.Equals(right.Double);
            case RcOperandType.Recall:
                return ReferenceEquals(left.MemRef, right.MemRef);
            case RcOperandType.Func:
                return false;
        }

        if (left.Size != right.Size
            || left.MemRef is null
            || right.MemRef is null
            || left.MemRef.Value.MemRefType != right.MemRef.Value.MemRefType)
        {
            return false;
        }

        if (left.MemRef is RcModifiedMemRef leftModified && right.MemRef is RcModifiedMemRef rightModified)
        {
            return leftModified.ModifierType == rightModified.ModifierType
                && leftModified.Depth == rightModified.Depth
                && AreEqual(leftModified.Modifier, rightModified.Modifier)
                && AreEqual(leftModified.Parent, rightModified.Parent);
        }

        return left.MemRef.Address == right.MemRef.Address
            && left.MemRef.Value.Size == right.MemRef.Value.Size;
    }

    public bool IsMemRef => Type is not (RcOperandType.Const or RcOperandType.Float
        or RcOperandType.Func or RcOperandType.Recall);

    public bool IsRecall => Type == RcOperandType.Recall;

    public bool IsFloat
    {
        get
        {
            if (Type == RcOperandType.Float)
            {
                return true;
            }

            if (Type == RcOperandType.Recall)
            {
                return RcMemRefHelpers.IsFloat(Size);
            }

            return IsFloatMemRef;
        }
    }

    public bool IsFloatMemRef
    {
        get
        {
            if (!IsMemRef || MemRef is null)
            {
                return false;
            }

            if (MemRef is RcModifiedMemRef modified && modified.ModifierType != RcOperator.IndirectRead)
            {
                return RcMemRefHelpers.IsFloat(MemRef.Value.Size);
            }

            return RcMemRefHelpers.IsFloat(Size);
        }
    }

    internal static bool IsMemRefType(RcOperandType type) => type is not (RcOperandType.Const
        or RcOperandType.Float or RcOperandType.Func or RcOperandType.Recall);

    internal static bool IsTransform(RcOperandType type) => type is RcOperandType.Bcd or RcOperandType.Inverted;

    internal static RcOperand Parse(RcCursor cursor, RcParseState parse)
    {
        var self = new RcOperand();
        ParseInto(self, cursor, parse);
        return self;
    }

    internal static void ParseInto(RcOperand self, RcCursor cursor, RcParseState parse)
    {
        self.IsCombining = false;
        char current = cursor.Current;

        switch (current)
        {
            case 'h':
            case 'H':
                if (cursor.CharAt(2) is 'x' or 'X')
                {
                    throw new RcParseException(RcError.InvalidConstOperand);
                }

                cursor.Advance();
                if (!RcText.TryParseUInt32(cursor, 16, out uint hexValue, out int hexEnd))
                {
                    throw new RcParseException(RcError.InvalidConstOperand);
                }

                SetConst(self, hexValue);
                cursor.Position = hexEnd;
                break;

            case 'f':
            case 'F':
                if (RcText.IsAlpha(cursor.CharAt(1)))
                {
                    ParseMemory(self, cursor, parse);
                    break;
                }

                goto case 'v';

            case 'v':
            case 'V':
                cursor.Advance();
                goto case '+';

            case '+':
            case '-':
                bool negative = false;
                bool allowDecimal = current is 'f' or 'F';
                if (cursor.Current == '-')
                {
                    negative = true;
                    cursor.Advance();
                }
                else if (cursor.Current == '+')
                {
                    cursor.Advance();
                }

                if (!RcText.TryParseUInt32(cursor, 10, out uint value, out int end))
                {
                    throw new RcParseException(allowDecimal ? RcError.InvalidFpOperand : RcError.InvalidConstOperand);
                }

                if (cursor.Text.Length > end && cursor.Text[end] == '.' && allowDecimal)
                {
                    ParseDecimalFloat(self, cursor, end, value, negative);
                    break;
                }

                cursor.Position = end;
                if (value > 0x7fffffff)
                {
                    value = 0x7fffffff;
                }

                SetConst(self, negative ? unchecked((uint)(-value)) : value);
                break;

            case '{':
                cursor.Advance();
                ParseVariable(self, cursor, parse);
                break;

            case '0':
                if (cursor.CharAt(1) is 'x' or 'X')
                {
                    ParseMemory(self, cursor, parse);
                    break;
                }

                goto case '1';

            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
                if (!RcText.TryParseUInt32(cursor, 10, out uint decimalValue, out int decimalEnd))
                {
                    throw new RcParseException(RcError.InvalidConstOperand);
                }

                SetConst(self, decimalValue);
                cursor.Position = decimalEnd;
                break;

            case '@':
                ParseFunction(self, cursor);
                break;

            default:
                ParseMemory(self, cursor, parse);
                break;
        }
    }

    internal static void Evaluate(out RcTypedValue result, RcOperand self, RcEvalState? evalState)
    {
        switch (self.Type)
        {
            case RcOperandType.Const:
                result = RcTypedValue.FromUnsigned(self.Num);
                return;

            case RcOperandType.Float:
                result = RcTypedValue.FromFloat((float)self.Double);
                return;

            case RcOperandType.Func:
                // This feature was never actualized in rcheevos.
                result = RcTypedValue.FromUnsigned(0);
                return;

            case RcOperandType.Recall:
                if (!IsMemRefType(self.MemRefAccessType))
                {
                    RcOperand recall = self.Clone();
                    recall.Type = self.MemRefAccessType;
                    Evaluate(out result, recall, evalState);
                    return;
                }

                if (self.MemRef is null)
                {
                    result = RcTypedValue.FromUnsigned(0);
                    return;
                }

                RcMemRefHelpers.GetMemRefValue(out result, self.MemRef, self.MemRefAccessType);
                break;

            default:
                if (self.MemRef is null)
                {
                    result = RcTypedValue.FromUnsigned(0);
                    return;
                }

                RcMemRefHelpers.GetMemRefValue(out result, self.MemRef, self.Type);
                break;
        }

        RcMemRefHelpers.Transform(ref result, self.Size);

        if (result.Type == RcValueType.Unsigned)
        {
            result.U32 = TransformOperandValue(result.U32, self);
        }
    }

    /// <summary>Converts a condition into an operand, allocating a modified memory reference for modifiers.</summary>
    internal static void FromCondition(RcCondition condition, RcOperand operand, RcParseState parse)
    {
        if (condition.Oper == RcOperator.None)
        {
            if (!ReferenceEquals(operand, condition.Operand1))
            {
                Copy(condition.Operand1, operand);
            }

            return;
        }

        RcMemSize newSize = RcMemSize.Bits32;
        if (condition.Operand1.IsFloat || condition.Operand2.IsFloat)
        {
            newSize = RcMemSize.Float;
        }

        operand.MemRef = parse.AllocModifiedMemRef(
            newSize,
            condition.Operand1,
            condition.Oper,
            condition.Operand2);

        operand.Type = operand.MemRefAccessType = RcOperandType.Address;
        operand.Size = newSize;
    }

    internal static void Copy(RcOperand source, RcOperand destination)
    {
        destination.MemRef = source.MemRef;
        destination.Num = source.Num;
        destination.Double = source.Double;
        destination.Type = source.Type;
        destination.Size = source.Size;
        destination.MemRefAccessType = source.MemRefAccessType;
        destination.IsCombining = source.IsCombining;
    }

    /// <summary>Combines the operand with the current AddSource/SubSource accumulator.</summary>
    internal static void AddSource(RcOperand self, RcParseState parse, RcMemSize newSize)
    {
        RcModifiedMemRef modified;
        if (self.Type is RcOperandType.Delta or RcOperandType.Prior && self.Type == parse.AddSourceParent.Type)
        {
            RcOperand modifier = self.Clone();
            modifier.Type = parse.AddSourceParent.Type = RcOperandType.Address;

            modified = parse.AllocModifiedMemRef(newSize, parse.AddSourceParent, parse.AddSourceOper, modifier);
        }
        else
        {
            modified = parse.AllocModifiedMemRef(newSize, parse.AddSourceParent, parse.AddSourceOper, self);
            self.Type = self.MemRefAccessType = RcOperandType.Address;
        }

        self.MemRef = modified;

        // The result of an AddSource operation is always a 32-bit integer.
        self.Size = RcMemSize.Bits32;
    }

    private static void ParseFunction(RcOperand self, RcCursor cursor)
    {
        cursor.Advance();
        if (!RcText.IsAlpha(cursor.Current))
        {
            throw new RcParseException(RcError.InvalidFuncOperand);
        }

        while (RcText.IsAlnum(cursor.Current) || cursor.Current == '_')
        {
            cursor.Advance();
        }

        self.Type = RcOperandType.Func;
        self.Size = RcMemSize.Bits32;
        self.MemRefAccessType = RcOperandType.Address;
    }

    private static void ParseVariable(RcOperand self, RcCursor cursor, RcParseState parse)
    {
        Span<char> name = stackalloc char[MaxVariableNameLength + 1];
        int length = 0;
        while (length < MaxVariableNameLength && cursor.Current != '}')
        {
            char ch = cursor.Current;
            if (length == 0 ? !RcText.IsAlpha(ch) : !RcText.IsAlnum(ch))
            {
                throw new RcParseException(RcError.InvalidVariableName);
            }

            name[length++] = ch;
            cursor.Advance();
        }

        if (length == 0 || cursor.Current != '}')
        {
            throw new RcParseException(RcError.InvalidVariableName);
        }

        cursor.Advance();

        ReadOnlySpan<char> variable = name[..length];
        if (!variable.SequenceEqual("recall"))
        {
            throw new RcParseException(RcError.UnknownVariableName);
        }

        if (parse.Remember.Type == RcOperandType.None)
        {
            self.MemRef = null;
            self.Size = RcMemSize.Bits32;
            self.MemRefAccessType = RcOperandType.Address;
        }
        else
        {
            Copy(parse.Remember, self);
            self.IsCombining = false;
            self.MemRefAccessType = self.Type;
        }

        self.Type = RcOperandType.Recall;
    }

    private static void ParseMemory(RcOperand self, RcCursor cursor, RcParseState parse)
    {
        switch (cursor.Current)
        {
            case 'd':
            case 'D':
                self.Type = RcOperandType.Delta;
                cursor.Advance();
                break;

            case 'p':
            case 'P':
                self.Type = RcOperandType.Prior;
                cursor.Advance();
                break;

            case 'b':
            case 'B':
                self.Type = RcOperandType.Bcd;
                cursor.Advance();
                break;

            case '~':
                self.Type = RcOperandType.Inverted;
                cursor.Advance();
                break;

            default:
                self.Type = RcOperandType.Address;
                break;
        }

        self.MemRefAccessType = self.Type;

        ParseMemRef(cursor, out RcMemSize size, out uint address);
        self.Size = size;

        RcMemSize sharedSize = RcMemRefHelpers.SharedSize(size);
        if (sharedSize != size && self.Type == RcOperandType.Prior
            && RcMemRefHelpers.Mask(sharedSize) != RcMemRefHelpers.Mask(size))
        {
            sharedSize = size;
        }

        if (parse.IndirectParent.Type != RcOperandType.None)
        {
            RcOperand offset = Const(address);
            self.MemRef = parse.AllocModifiedMemRef(
                sharedSize,
                parse.IndirectParent,
                RcOperator.IndirectRead,
                offset);
        }
        else
        {
            self.MemRef = parse.AllocMemRef(address, sharedSize);
        }
    }

    private static void ParseMemRef(RcCursor cursor, out RcMemSize size, out uint address)
    {
        if (cursor.Current == '0')
        {
            if (cursor.CharAt(1) is not 'x' and not 'X')
            {
                throw new RcParseException(RcError.InvalidMemoryOperand);
            }

            cursor.Skip(2);
            char sizeChar = cursor.Advance();
            switch (sizeChar)
            {
                case 'h':
                case 'H':
                    size = RcMemSize.Bits8;
                    break;
                case ' ':
                    size = RcMemSize.Bits16;
                    break;
                case 'x':
                case 'X':
                    size = RcMemSize.Bits32;
                    break;
                case 'm':
                case 'M':
                    size = RcMemSize.Bit0;
                    break;
                case 'n':
                case 'N':
                    size = RcMemSize.Bit1;
                    break;
                case 'o':
                case 'O':
                    size = RcMemSize.Bit2;
                    break;
                case 'p':
                case 'P':
                    size = RcMemSize.Bit3;
                    break;
                case 'q':
                case 'Q':
                    size = RcMemSize.Bit4;
                    break;
                case 'r':
                case 'R':
                    size = RcMemSize.Bit5;
                    break;
                case 's':
                case 'S':
                    size = RcMemSize.Bit6;
                    break;
                case 't':
                case 'T':
                    size = RcMemSize.Bit7;
                    break;
                case 'l':
                case 'L':
                    size = RcMemSize.Low;
                    break;
                case 'u':
                case 'U':
                    size = RcMemSize.High;
                    break;
                case 'k':
                case 'K':
                    size = RcMemSize.Bitcount;
                    break;
                case 'w':
                case 'W':
                    size = RcMemSize.Bits24;
                    break;
                case 'g':
                case 'G':
                    size = RcMemSize.Bits32Be;
                    break;
                case 'i':
                case 'I':
                    size = RcMemSize.Bits16Be;
                    break;
                case 'j':
                case 'J':
                    size = RcMemSize.Bits24Be;
                    break;
                case '0':
                    if (cursor.Current == 'x')
                    {
                        throw new RcParseException(RcError.InvalidMemoryOperand);
                    }

                    goto default;
                default:
                    // Legacy addresses without a size prefix are 16-bit.
                    cursor.Position--;
                    size = RcMemSize.Bits16;
                    break;
            }
        }
        else if (cursor.Current is 'f' or 'F')
        {
            cursor.Advance();
            char sizeChar = cursor.Advance();
            switch (sizeChar)
            {
                case 'f':
                case 'F':
                    size = RcMemSize.Float;
                    break;
                case 'b':
                case 'B':
                    size = RcMemSize.FloatBe;
                    break;
                case 'h':
                case 'H':
                    size = RcMemSize.Double32;
                    break;
                case 'i':
                case 'I':
                    size = RcMemSize.Double32Be;
                    break;
                case 'm':
                case 'M':
                    size = RcMemSize.Mbf32;
                    break;
                case 'l':
                case 'L':
                    size = RcMemSize.Mbf32Le;
                    break;
                default:
                    throw new RcParseException(RcError.InvalidFpOperand);
            }
        }
        else
        {
            throw new RcParseException(RcError.InvalidMemoryOperand);
        }

        if (!RcText.TryParseUInt32(cursor, 16, out address, out int end))
        {
            throw new RcParseException(RcError.InvalidMemoryOperand);
        }

        cursor.Position = end;
    }

    private static void ParseDecimalFloat(
        RcOperand self,
        RcCursor cursor,
        int dotIndex,
        uint integerValue,
        bool negative)
    {
        int index = dotIndex + 1;
        if (index >= cursor.Text.Length || !RcText.IsDigit(cursor.Text[index]))
        {
            throw new RcParseException(RcError.InvalidFpOperand);
        }

        // Only keep as many fraction digits as fit in a 32-bit value. Float has about
        // seven significant digits of precision anyway (rcheevos behavior).
        ulong shift = 1;
        ulong fraction = 0;
        while (index < cursor.Text.Length && RcText.IsDigit(cursor.Text[index]))
        {
            if (shift < 1_000_000_000)
            {
                fraction *= 10;
                fraction += (uint)(cursor.Text[index] - '0');
                shift *= 10;
            }

            ++index;
        }

        double value;
        if (fraction != 0)
        {
            double fractional = (double)fraction / shift;
            value = negative
                ? -integerValue - fractional
                : integerValue + fractional;
        }
        else
        {
            value = negative ? -integerValue : integerValue;
        }

        self.Type = RcOperandType.Float;
        self.Size = RcMemSize.Float;
        self.MemRefAccessType = RcOperandType.None;
        self.Double = value;

        cursor.Position = index;
    }

    private static void SetConst(RcOperand self, uint value)
    {
        self.Size = RcMemSize.Bits32;
        self.Type = RcOperandType.Const;
        self.MemRefAccessType = RcOperandType.None;
        self.Num = value;
    }

    private static uint TransformOperandValue(uint value, RcOperand self)
    {
        switch (self.Type)
        {
            case RcOperandType.Bcd:
                switch (self.Size)
                {
                    case RcMemSize.Bits8:
                        value = ((value >> 4) & 0x0f) * 10 + (value & 0x0f);
                        break;

                    case RcMemSize.Bits16:
                    case RcMemSize.Bits16Be:
                        value = ((value >> 12) & 0x0f) * 1000
                            + ((value >> 8) & 0x0f) * 100
                            + ((value >> 4) & 0x0f) * 10
                            + (value & 0x0f);
                        break;

                    case RcMemSize.Bits24:
                    case RcMemSize.Bits24Be:
                        value = ((value >> 20) & 0x0f) * 100000
                            + ((value >> 16) & 0x0f) * 10000
                            + ((value >> 12) & 0x0f) * 1000
                            + ((value >> 8) & 0x0f) * 100
                            + ((value >> 4) & 0x0f) * 10
                            + (value & 0x0f);
                        break;

                    case RcMemSize.Bits32:
                    case RcMemSize.Bits32Be:
                    case RcMemSize.Variable:
                        value = ((value >> 28) & 0x0f) * 10000000
                            + ((value >> 24) & 0x0f) * 1000000
                            + ((value >> 20) & 0x0f) * 100000
                            + ((value >> 16) & 0x0f) * 10000
                            + ((value >> 12) & 0x0f) * 1000
                            + ((value >> 8) & 0x0f) * 100
                            + ((value >> 4) & 0x0f) * 10
                            + (value & 0x0f);
                        break;
                }

                break;

            case RcOperandType.Inverted:
                switch (self.Size)
                {
                    case RcMemSize.Low:
                    case RcMemSize.High:
                        value ^= 0x0f;
                        break;
                    case RcMemSize.Bits8:
                        value ^= 0xff;
                        break;
                    case RcMemSize.Bits16:
                    case RcMemSize.Bits16Be:
                        value ^= 0xffff;
                        break;
                    case RcMemSize.Bits24:
                    case RcMemSize.Bits24Be:
                        value ^= 0xffffff;
                        break;
                    case RcMemSize.Bits32:
                    case RcMemSize.Bits32Be:
                    case RcMemSize.Variable:
                        value ^= 0xffffffff;
                        break;
                    default:
                        value ^= 0x01;
                        break;
                }

                break;
        }

        return value;
    }
}
