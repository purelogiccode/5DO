// Ported from rcheevos (MIT) - src/rcheevos/value.c (rc_typed_value_*).
using System;
using System.Runtime.InteropServices;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>
/// A 32-bit value tagged as unsigned, signed, or float. The three views share
/// storage to mirror the C <c>union</c> in <c>rc_typed_value_t</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RcTypedValue
{
    private const float FloatEpsilon = 1.1920929E-07f;

    private uint _bits;

    public RcValueType Type;

    public uint U32
    {
        readonly get => _bits;
        set => _bits = value;
    }

    public int I32
    {
        readonly get => unchecked((int)_bits);
        set => _bits = unchecked((uint)value);
    }

    public float F32
    {
        readonly get => BitConverter.Int32BitsToSingle(unchecked((int)_bits));
        set => _bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
    }

    public static RcTypedValue FromUnsigned(uint value) => new() { Type = RcValueType.Unsigned, U32 = value };

    public static RcTypedValue FromSigned(int value) => new() { Type = RcValueType.Signed, I32 = value };

    public static RcTypedValue FromFloat(float value) => new() { Type = RcValueType.Float, F32 = value };

    public static void Convert(ref RcTypedValue value, RcValueType newType)
    {
        switch (newType)
        {
            case RcValueType.Unsigned:
                switch (value.Type)
                {
                    case RcValueType.Unsigned:
                        return;
                    case RcValueType.Signed:
                        value.U32 = unchecked((uint)value.I32);
                        break;
                    case RcValueType.Float:
                        value.U32 = FloatToUnsigned(value.F32);
                        break;
                    default:
                        value.U32 = 0;
                        break;
                }

                break;

            case RcValueType.Signed:
                switch (value.Type)
                {
                    case RcValueType.Signed:
                        return;
                    case RcValueType.Unsigned:
                        value.I32 = unchecked((int)value.U32);
                        break;
                    case RcValueType.Float:
                        value.I32 = FloatToInt32(value.F32);
                        break;
                    default:
                        value.I32 = 0;
                        break;
                }

                break;

            case RcValueType.Float:
                switch (value.Type)
                {
                    case RcValueType.Float:
                        return;
                    case RcValueType.Unsigned:
                        value.F32 = value.U32;
                        break;
                    case RcValueType.Signed:
                        value.F32 = value.I32;
                        break;
                    default:
                        value.F32 = 0.0f;
                        break;
                }

                break;
        }

        value.Type = newType;
    }

    public static RcTypedValue Converted(RcTypedValue source, RcValueType newType)
    {
        RcTypedValue copy = source;
        Convert(ref copy, newType);
        return copy;
    }

    public static void Negate(ref RcTypedValue value)
    {
        switch (value.Type)
        {
            case RcValueType.Unsigned:
                Convert(ref value, RcValueType.Signed);
                goto case RcValueType.Signed;

            case RcValueType.Signed:
                value.I32 = unchecked(-value.I32);
                break;

            case RcValueType.Float:
                value.F32 = -value.F32;
                break;
        }
    }

    public static void Add(ref RcTypedValue value, RcTypedValue amount)
    {
        RcTypedValue amountCopy = amount;

        if (amountCopy.Type != value.Type && value.Type != RcValueType.None)
        {
            if (amountCopy.Type == RcValueType.Float)
            {
                Convert(ref value, RcValueType.Float);
            }
            else
            {
                amountCopy = Converted(amountCopy, value.Type);
            }
        }

        switch (value.Type)
        {
            case RcValueType.Unsigned:
                value.U32 = unchecked(value.U32 + amountCopy.U32);
                break;

            case RcValueType.Signed:
                value.I32 = unchecked(value.I32 + amountCopy.I32);
                break;

            case RcValueType.Float:
                value.F32 += amountCopy.F32;
                break;

            case RcValueType.None:
                value = amountCopy;
                break;
        }
    }

    public static void Multiply(ref RcTypedValue value, RcTypedValue amount)
    {
        RcTypedValue converted;

        switch (value.Type)
        {
            case RcValueType.Unsigned:
                switch (amount.Type)
                {
                    case RcValueType.Unsigned:
                        value.U32 = unchecked(value.U32 * amount.U32);
                        break;

                    case RcValueType.Signed:
                        value.U32 = unchecked(value.U32 * (uint)amount.I32);
                        break;

                    case RcValueType.Float:
                        Convert(ref value, RcValueType.Float);
                        value.F32 *= amount.F32;
                        break;

                    default:
                        value.Type = RcValueType.None;
                        break;
                }

                break;

            case RcValueType.Signed:
                switch (amount.Type)
                {
                    case RcValueType.Signed:
                        value.I32 = unchecked(value.I32 * amount.I32);
                        break;

                    case RcValueType.Unsigned:
                        value.I32 = unchecked(value.I32 * (int)amount.U32);
                        break;

                    case RcValueType.Float:
                        Convert(ref value, RcValueType.Float);
                        value.F32 *= amount.F32;
                        break;

                    default:
                        value.Type = RcValueType.None;
                        break;
                }

                break;

            case RcValueType.Float:
                if (amount.Type == RcValueType.None)
                {
                    value.Type = RcValueType.None;
                }
                else
                {
                    converted = Converted(amount, RcValueType.Float);
                    value.F32 *= converted.F32;
                }

                break;

            default:
                value.Type = RcValueType.None;
                break;
        }
    }

    public static void Divide(ref RcTypedValue value, RcTypedValue amount)
    {
        RcTypedValue converted;

        switch (amount.Type)
        {
            case RcValueType.Unsigned:
                if (amount.U32 == 0)
                {
                    value.Type = RcValueType.None;
                    return;
                }

                switch (value.Type)
                {
                    case RcValueType.Unsigned:
                        value.U32 /= amount.U32;
                        return;
                    case RcValueType.Signed:
                        value.I32 = DivideInt32(value.I32, unchecked((int)amount.U32));
                        return;
                    case RcValueType.Float:
                        converted = Converted(amount, RcValueType.Float);
                        break;
                    default:
                        value.Type = RcValueType.None;
                        return;
                }

                break;

            case RcValueType.Signed:
                if (amount.I32 == 0)
                {
                    value.Type = RcValueType.None;
                    return;
                }

                switch (value.Type)
                {
                    case RcValueType.Signed:
                        value.I32 = DivideInt32(value.I32, amount.I32);
                        return;
                    case RcValueType.Unsigned:
                        value.U32 /= unchecked((uint)amount.I32);
                        return;
                    case RcValueType.Float:
                        converted = Converted(amount, RcValueType.Float);
                        break;
                    default:
                        value.Type = RcValueType.None;
                        return;
                }

                break;

            case RcValueType.Float:
                converted = amount;
                break;

            default:
                value.Type = RcValueType.None;
                return;
        }

        if (converted.F32 == 0.0f)
        {
            value.Type = RcValueType.None;
            return;
        }

        Convert(ref value, RcValueType.Float);
        value.F32 /= converted.F32;
    }

    public static void Modulus(ref RcTypedValue value, RcTypedValue amount)
    {
        RcTypedValue converted;

        switch (amount.Type)
        {
            case RcValueType.Unsigned:
                if (amount.U32 == 0)
                {
                    value.Type = RcValueType.None;
                    return;
                }

                switch (value.Type)
                {
                    case RcValueType.Unsigned:
                        value.U32 %= amount.U32;
                        return;
                    case RcValueType.Signed:
                        value.I32 = ModulusInt32(value.I32, unchecked((int)amount.U32));
                        return;
                    case RcValueType.Float:
                        converted = Converted(amount, RcValueType.Float);
                        break;
                    default:
                        value.Type = RcValueType.None;
                        return;
                }

                break;

            case RcValueType.Signed:
                if (amount.I32 == 0)
                {
                    value.Type = RcValueType.None;
                    return;
                }

                switch (value.Type)
                {
                    case RcValueType.Signed:
                        value.I32 = ModulusInt32(value.I32, amount.I32);
                        return;
                    case RcValueType.Unsigned:
                        value.U32 %= unchecked((uint)amount.I32);
                        return;
                    case RcValueType.Float:
                        converted = Converted(amount, RcValueType.Float);
                        break;
                    default:
                        value.Type = RcValueType.None;
                        return;
                }

                break;

            case RcValueType.Float:
                converted = amount;
                break;

            default:
                value.Type = RcValueType.None;
                return;
        }

        if (converted.F32 == 0.0f)
        {
            value.Type = RcValueType.None;
            return;
        }

        Convert(ref value, RcValueType.Float);
        value.F32 = value.F32 % converted.F32;
    }

    public static void Combine(ref RcTypedValue value, ref RcTypedValue amount, RcOperator oper)
    {
        switch (oper)
        {
            case RcOperator.Mult:
                Multiply(ref value, amount);
                break;

            case RcOperator.Div:
                Divide(ref value, amount);
                break;

            case RcOperator.And:
                Convert(ref value, RcValueType.Unsigned);
                Convert(ref amount, RcValueType.Unsigned);
                value.U32 &= amount.U32;
                break;

            case RcOperator.Xor:
                Convert(ref value, RcValueType.Unsigned);
                Convert(ref amount, RcValueType.Unsigned);
                value.U32 ^= amount.U32;
                break;

            case RcOperator.Mod:
                Modulus(ref value, amount);
                break;

            case RcOperator.Add:
                Add(ref value, amount);
                break;

            case RcOperator.Sub:
                Negate(ref amount);
                Add(ref value, amount);
                break;
        }
    }

    public static bool Compare(RcTypedValue left, RcTypedValue right, RcOperator oper)
    {
        RcTypedValue value1 = left;
        RcTypedValue value2 = right;

        if (value2.Type != value1.Type)
        {
            if (value2.Type == RcValueType.Float)
            {
                value1 = Converted(value1, value2.Type);
            }
            else
            {
                value2 = Converted(value2, value1.Type);
            }
        }

        switch (value1.Type)
        {
            case RcValueType.Unsigned:
                return oper switch
                {
                    RcOperator.Eq => value1.U32 == value2.U32,
                    RcOperator.Ne => value1.U32 != value2.U32,
                    RcOperator.Lt => value1.U32 < value2.U32,
                    RcOperator.Le => value1.U32 <= value2.U32,
                    RcOperator.Gt => value1.U32 > value2.U32,
                    RcOperator.Ge => value1.U32 >= value2.U32,
                    _ => true,
                };

            case RcValueType.Signed:
                return oper switch
                {
                    RcOperator.Eq => value1.I32 == value2.I32,
                    RcOperator.Ne => value1.I32 != value2.I32,
                    RcOperator.Lt => value1.I32 < value2.I32,
                    RcOperator.Le => value1.I32 <= value2.I32,
                    RcOperator.Gt => value1.I32 > value2.I32,
                    RcOperator.Ge => value1.I32 >= value2.I32,
                    _ => true,
                };

            case RcValueType.Float:
                return CompareFloats(value1.F32, value2.F32, oper);

            default:
                return true;
        }
    }

    private static bool CompareFloats(float f1, float f2, RcOperator oper)
    {
        if (!f1.Equals(f2))
        {
            float abs1 = f1 < 0 ? -f1 : f1;
            float abs2 = f2 < 0 ? -f2 : f2;
            float threshold = (abs1 < abs2 ? abs1 : abs2) * FloatEpsilon;
            float diff = f1 - f2;
            float absDiff = diff < 0 ? -diff : diff;

            if (absDiff > threshold)
            {
                if (diff > threshold)
                {
                    return oper is RcOperator.Ne or RcOperator.Gt or RcOperator.Ge;
                }

                return oper is RcOperator.Ne or RcOperator.Lt or RcOperator.Le;
            }
        }

        return oper is RcOperator.Eq or RcOperator.Ge or RcOperator.Le;
    }

    private static uint FloatToUnsigned(float value)
    {
        if (!float.IsFinite(value) || value <= 0)
        {
            return 0;
        }

        return value >= uint.MaxValue ? uint.MaxValue : unchecked((uint)value);
    }

    private static int FloatToInt32(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0;
        }

        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        return value >= int.MaxValue ? int.MaxValue : unchecked((int)value);
    }

    private static int DivideInt32(int value, int divisor)
    {
        if (value == int.MinValue && divisor == -1)
        {
            return int.MinValue;
        }

        return value / divisor;
    }

    private static int ModulusInt32(int value, int divisor)
    {
        if (value == int.MinValue && divisor == -1)
        {
            return 0;
        }

        return value % divisor;
    }
}

