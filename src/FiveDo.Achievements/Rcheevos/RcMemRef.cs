// Ported from rcheevos (MIT) - src/rcheevos/memref.c.
using System.Collections.Generic;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>Reads <paramref name="numBytes"/> bytes from <paramref name="address"/> in little-endian order.</summary>
internal delegate uint RcPeek(uint address, uint numBytes, object? userData);

/// <summary>The mutable value of a memory reference.</summary>
internal struct RcMemRefValue
{
    public uint Value;
    public uint Prior;
    public RcMemSize Size;
    public bool Changed;
    public RcValueType Type;
    public RcMemRefType MemRefType;
}

/// <summary>Common storage for plain, modified, and named-value memory references.</summary>
internal abstract class RcMemRefBase
{
    public RcMemRefValue Value;
    public uint Address;
}

/// <summary>A single address/size pair read from the emulated memory.</summary>
internal sealed class RcMemRef : RcMemRefBase
{
    public RcMemRef(uint address, RcMemSize size)
    {
        Address = address;
        Value.MemRefType = RcMemRefType.MemRef;
        Value.Type = RcValueType.Unsigned;
        Value.Size = size;
    }
}

/// <summary>A memory reference derived from another operand (AddAddress/AddSource/SubSource).</summary>
internal sealed class RcModifiedMemRef : RcMemRefBase
{
    public required RcOperand Parent { get; init; }

    public required RcOperand Modifier { get; init; }

    public RcOperator ModifierType { get; init; }

    public int Depth { get; init; }
}

/// <summary>Memory-size helpers and the rcheevos value transformations.</summary>
internal static class RcMemRefHelpers
{
    private static readonly byte[] BitsSet = [0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 4];

    private static readonly uint[] Masks =
    [
        0x000000ff, // Bits8
        0x0000ffff, // Bits16
        0x00ffffff, // Bits24
        0xffffffff, // Bits32
        0x0000000f, // Low
        0x000000f0, // High
        0x00000001, // Bit0
        0x00000002, // Bit1
        0x00000004, // Bit2
        0x00000008, // Bit3
        0x00000010, // Bit4
        0x00000020, // Bit5
        0x00000040, // Bit6
        0x00000080, // Bit7
        0x000000ff, // Bitcount
        0x0000ffff, // Bits16Be
        0x00ffffff, // Bits24Be
        0xffffffff, // Bits32Be
        0xffffffff, // Float
        0xffffffff, // Mbf32
        0xffffffff, // Mbf32Le
        0xffffffff, // FloatBe
        0xffffffff, // Double32
        0xffffffff, // Double32Be
        0xffffffff, // Variable
    ];

    private static readonly RcMemSize[] SharedSizes =
    [
        RcMemSize.Bits8,    // Bits8
        RcMemSize.Bits16,   // Bits16
        RcMemSize.Bits32,   // Bits24
        RcMemSize.Bits32,   // Bits32
        RcMemSize.Bits8,    // Low
        RcMemSize.Bits8,    // High
        RcMemSize.Bits8,    // Bit0
        RcMemSize.Bits8,    // Bit1
        RcMemSize.Bits8,    // Bit2
        RcMemSize.Bits8,    // Bit3
        RcMemSize.Bits8,    // Bit4
        RcMemSize.Bits8,    // Bit5
        RcMemSize.Bits8,    // Bit6
        RcMemSize.Bits8,    // Bit7
        RcMemSize.Bits8,    // Bitcount
        RcMemSize.Bits16,   // Bits16Be
        RcMemSize.Bits32,   // Bits24Be
        RcMemSize.Bits32,   // Bits32Be
        RcMemSize.Bits32,   // Float
        RcMemSize.Bits32,   // Mbf32
        RcMemSize.Bits32,   // Mbf32Le
        RcMemSize.Bits32,   // FloatBe
        RcMemSize.Bits32,   // Double32
        RcMemSize.Bits32,   // Double32Be
        RcMemSize.Bits32,   // Variable
    ];

    public static uint Mask(RcMemSize size)
    {
        int index = (int)size;
        return (uint)index < (uint)Masks.Length ? Masks[index] : 0xffffffff;
    }

    public static RcMemSize SharedSize(RcMemSize size)
    {
        int index = (int)size;
        return (uint)index < (uint)SharedSizes.Length ? SharedSizes[index] : size;
    }

    public static uint PeekValue(uint address, RcMemSize size, RcPeek? peek, object? userData)
    {
        if (peek is null)
        {
            return 0;
        }

        switch (size)
        {
            case RcMemSize.Bits8:
                return peek(address, 1, userData);
            case RcMemSize.Bits16:
                return peek(address, 2, userData);
            case RcMemSize.Bits32:
                return peek(address, 4, userData);
            default:
                {
                    int index = (int)size;
                    if ((uint)index >= (uint)SharedSizes.Length)
                    {
                        return 0;
                    }

                    return PeekValue(address, SharedSizes[index], peek, userData) & Masks[index];
                }
        }
    }

    public static void UpdateValue(ref RcMemRefValue memref, uint newValue)
    {
        if (memref.Value == newValue)
        {
            memref.Changed = false;
        }
        else
        {
            memref.Prior = memref.Value;
            memref.Value = newValue;
            memref.Changed = true;
        }
    }

    public static uint GetValue(RcMemRefValue memref, RcOperandType operandType)
    {
        switch (operandType)
        {
            case RcOperandType.Address:
                return memref.Value;

            case RcOperandType.Delta:
                return memref.Changed ? memref.Prior : memref.Value;

            case RcOperandType.Prior:
                return memref.Prior;

            default:
                return memref.Value;
        }
    }

    public static void GetMemRefValue(out RcTypedValue value, RcMemRefBase memref, RcOperandType operandType)
    {
        value = default;
        value.Type = memref.Value.Type;
        value.U32 = GetValue(memref.Value, operandType);
    }

    public static void Transform(ref RcTypedValue value, RcMemSize size)
    {
        // ASSERT: value.Type == RcValueType.Unsigned.
        switch (size)
        {
            case RcMemSize.Bits8:
                value.U32 &= 0x000000ff;
                break;
            case RcMemSize.Bits16:
                value.U32 &= 0x0000ffff;
                break;
            case RcMemSize.Bits24:
                value.U32 &= 0x00ffffff;
                break;
            case RcMemSize.Bits32:
                break;
            case RcMemSize.Bit0:
                value.U32 = (value.U32 >> 0) & 1;
                break;
            case RcMemSize.Bit1:
                value.U32 = (value.U32 >> 1) & 1;
                break;
            case RcMemSize.Bit2:
                value.U32 = (value.U32 >> 2) & 1;
                break;
            case RcMemSize.Bit3:
                value.U32 = (value.U32 >> 3) & 1;
                break;
            case RcMemSize.Bit4:
                value.U32 = (value.U32 >> 4) & 1;
                break;
            case RcMemSize.Bit5:
                value.U32 = (value.U32 >> 5) & 1;
                break;
            case RcMemSize.Bit6:
                value.U32 = (value.U32 >> 6) & 1;
                break;
            case RcMemSize.Bit7:
                value.U32 = (value.U32 >> 7) & 1;
                break;
            case RcMemSize.Low:
                value.U32 &= 0x0f;
                break;
            case RcMemSize.High:
                value.U32 = (value.U32 >> 4) & 0x0f;
                break;
            case RcMemSize.Bitcount:
                value.U32 = (uint)(BitsSet[value.U32 & 0x0F] + BitsSet[(value.U32 >> 4) & 0x0F]);
                break;
            case RcMemSize.Bits16Be:
                value.U32 = ((value.U32 & 0xFF00) >> 8) | ((value.U32 & 0x00FF) << 8);
                break;
            case RcMemSize.Bits24Be:
                value.U32 = ((value.U32 & 0xFF0000) >> 16)
                    | (value.U32 & 0x00FF00)
                    | ((value.U32 & 0x0000FF) << 16);
                break;
            case RcMemSize.Bits32Be:
                value.U32 = ((value.U32 & 0xFF000000) >> 24)
                    | ((value.U32 & 0x00FF0000) >> 8)
                    | ((value.U32 & 0x0000FF00) << 8)
                    | ((value.U32 & 0x000000FF) << 24);
                break;
            case RcMemSize.Float:
                TransformFloat(ref value);
                break;
            case RcMemSize.FloatBe:
                TransformFloatBe(ref value);
                break;
            case RcMemSize.Double32:
                TransformDouble32(ref value);
                break;
            case RcMemSize.Double32Be:
                TransformDouble32Be(ref value);
                break;
            case RcMemSize.Mbf32:
                TransformMbf32(ref value);
                break;
            case RcMemSize.Mbf32Le:
                TransformMbf32Le(ref value);
                break;
        }
    }

    public static bool IsFloat(RcMemSize size) => size is RcMemSize.Float or RcMemSize.FloatBe
        or RcMemSize.Double32 or RcMemSize.Double32Be or RcMemSize.Mbf32 or RcMemSize.Mbf32Le;

    private static void TransformFloat(ref RcTypedValue value)
    {
        uint mantissa = value.U32 & 0x7FFFFF;
        int exponent = (int)((value.U32 >> 23) & 0xFF) - 127;
        int sign = (int)(value.U32 & 0x80000000);
        value.F32 = BuildFloat(mantissa, exponent, sign);
        value.Type = RcValueType.Float;
    }

    private static void TransformFloatBe(ref RcTypedValue value)
    {
        uint mantissa = ((value.U32 & 0xFF000000) >> 24)
            | ((value.U32 & 0x00FF0000) >> 8)
            | ((value.U32 & 0x00007F00) << 8);
        int exponent = (int)(((value.U32 & 0x0000007F) << 1) | ((value.U32 & 0x00008000) >> 15)) - 127;
        int sign = (int)(value.U32 & 0x00000080);
        value.F32 = BuildFloat(mantissa, exponent, sign);
        value.Type = RcValueType.Float;
    }

    private static void TransformDouble32(ref RcTypedValue value)
    {
        uint mantissa = (value.U32 & 0x000FFFFF) << 3;
        int exponent = (int)((value.U32 >> 20) & 0x7FF) - 1023;
        int sign = (int)(value.U32 & 0x80000000);
        value.F32 = BuildFloat(mantissa, exponent, sign);
        value.Type = RcValueType.Float;
    }

    private static void TransformDouble32Be(ref RcTypedValue value)
    {
        uint mantissa = (((value.U32 & 0xFF000000) >> 24)
            | ((value.U32 & 0x00FF0000) >> 8)
            | ((value.U32 & 0x00000F00) << 8)) << 3;
        int exponent = (int)(((value.U32 & 0x0000007F) << 4) | ((value.U32 & 0x0000F000) >> 12)) - 1023;
        int sign = (int)(value.U32 & 0x00000080);
        value.F32 = BuildFloat(mantissa, exponent, sign);
        value.Type = RcValueType.Float;
    }

    private static void TransformMbf32(ref RcTypedValue value)
    {
        uint mantissa = ((value.U32 & 0xFF000000) >> 24)
            | ((value.U32 & 0x00FF0000) >> 8)
            | ((value.U32 & 0x00007F00) << 8);
        int exponent = (int)(value.U32 & 0xFF) - 129;
        int sign = (int)(value.U32 & 0x00008000);

        value.F32 = mantissa == 0 && exponent == -129
            ? (sign != 0 ? -0.0f : 0.0f)
            : BuildFloat(mantissa, exponent, sign);
        value.Type = RcValueType.Float;
    }

    private static void TransformMbf32Le(ref RcTypedValue value)
    {
        uint mantissa = value.U32 & 0x007FFFFF;
        int exponent = (int)(value.U32 >> 24) - 129;
        int sign = (int)(value.U32 & 0x00800000);

        value.F32 = mantissa == 0 && exponent == -129
            ? (sign != 0 ? -0.0f : 0.0f)
            : BuildFloat(mantissa, exponent, sign);
        value.Type = RcValueType.Float;
    }

    private static float BuildFloat(uint mantissaBits, int exponent, int sign)
    {
        const uint ImpliedBit = 1u << 23;
        uint mantissa = mantissaBits | ImpliedBit;
        double dbl = (double)mantissa / ImpliedBit;

        if (exponent > 127)
        {
            dbl = mantissaBits == 0 ? double.PositiveInfinity : double.NaN;
        }
        else if (exponent > 0)
        {
            while (exponent > 30)
            {
                dbl *= 1 << 30;
                exponent -= 30;
            }

            dbl *= (long)1 << exponent;
        }
        else if (exponent < 0)
        {
            if (exponent == -127)
            {
                dbl = (double)mantissaBits / ImpliedBit;
                exponent = 126;
            }
            else
            {
                exponent = -exponent;
            }

            while (exponent > 30)
            {
                dbl /= 1 << 30;
                exponent -= 30;
            }

            dbl /= (long)1 << exponent;
        }

        return sign != 0 ? (float)-dbl : (float)dbl;
    }

    /// <summary>Recomputes the value of a modified memory reference.</summary>
    public static uint GetModifiedValue(RcModifiedMemRef memref, RcPeek? peek, object? userData)
    {
        RcOperand.Evaluate(out RcTypedValue value, memref.Parent, null);
        RcOperand.Evaluate(out RcTypedValue modifier, memref.Modifier, null);

        switch (memref.ModifierType)
        {
            case RcOperator.IndirectRead:
                RcTypedValue.Add(ref value, modifier);
                RcTypedValue.Convert(ref value, RcValueType.Unsigned);
                value.U32 = PeekValue(value.U32, memref.Value.Size, peek, userData);
                value.Type = memref.Value.Type;
                break;

            case RcOperator.SubParent:
                RcTypedValue.Negate(ref value);
                RcTypedValue.Add(ref value, modifier);
                RcTypedValue.Convert(ref value, memref.Value.Type);
                break;

            case RcOperator.SubAccumulator:
                RcTypedValue.Negate(ref modifier);
                goto case RcOperator.AddAccumulator;

            case RcOperator.AddAccumulator:
                RcTypedValue.Convert(ref modifier, value.Type);
                RcTypedValue.Add(ref value, modifier);
                RcTypedValue.Convert(ref value, memref.Value.Type);
                break;

            default:
                RcTypedValue.Combine(ref value, ref modifier, memref.ModifierType);
                RcTypedValue.Convert(ref value, memref.Value.Type);
                break;
        }

        return value.U32;
    }

    /// <summary>Refreshes every memory reference in a set, including dependent modified references.</summary>
    public static void UpdateMemRefs(IEnumerable<RcMemRefBase> memrefs, RcPeek? peek, object? userData)
    {
        var modified = new List<RcModifiedMemRef>();

        foreach (RcMemRefBase memref in memrefs)
        {
            if (memref is RcModifiedMemRef modifiedMemRef)
            {
                modified.Add(modifiedMemRef);
            }
            else if (memref.Value.Type != RcValueType.None)
            {
                UpdateValue(ref memref.Value, PeekValue(memref.Address, memref.Value.Size, peek, userData));
            }
        }

        // Modified memrefs are created bottom-up, but an indirect parent can also be a
        // modified memref. Resolve dependencies recursively so every parent is current.
        var updated = new HashSet<RcMemRefBase>(ReferenceEqualityComparer.Instance);
        foreach (RcModifiedMemRef memref in modified)
        {
            UpdateModified(memref, peek, userData, updated);
        }
    }

    private static void UpdateModified(
        RcModifiedMemRef memref,
        RcPeek? peek,
        object? userData,
        HashSet<RcMemRefBase> updated)
    {
        if (!updated.Add(memref))
        {
            return;
        }

        if (memref.Parent.MemRef is RcModifiedMemRef parentModified)
        {
            UpdateModified(parentModified, peek, userData, updated);
        }

        if (memref.Modifier.MemRef is RcModifiedMemRef modifierModified)
        {
            UpdateModified(modifierModified, peek, userData, updated);
        }

        UpdateValue(ref memref.Value, GetModifiedValue(memref, peek, userData));
    }
}

