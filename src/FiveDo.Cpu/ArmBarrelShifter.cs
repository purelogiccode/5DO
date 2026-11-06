namespace FiveDo.Cpu;

/// <summary>
/// ARM barrel shifter, including the two FreeDOCore variants that differ in whether they
/// update the carry flag directly.
/// </summary>
/// <remarks>
/// FreeDO: <c>ARM_SHIFT_NSC</c> (returns the carry in a global <c>carry_out</c>) and
/// <c>ARM_SHIFT_SC</c> (updates CPSR.C). Shift types are 0 = LSL, 1 = LSR,
/// 2 = ASR, 3 = ROR, and 4 = RRX. The 3DO ARM60 has no Thumb or halfword-shift forms.
/// </remarks>
public static class ArmBarrelShifter
{
    /// <summary>Shifts a value without directly updating CPSR.C. FreeDO: <c>ARM_SHIFT_NSC</c>.</summary>
    /// <param name="state">CPU state (supplies the old carry for shift count zero and RRX).</param>
    /// <param name="value">Value to shift.</param>
    /// <param name="shift">Shift count, 0-255.</param>
    /// <param name="type">Shift type, 0-4.</param>
    /// <param name="carryOut">Carry produced by the shift.</param>
    /// <returns>The shifted value.</returns>
    public static uint ShiftNoCarry(Arm60State state, uint value, byte shift, byte type, out uint carryOut)
    {
        return ShiftNoCarry(value, shift, type, ArmAlu.GetCarry(state), out carryOut);
    }

    /// <summary>
    /// Shifts a value without touching CPU state, using an explicit carry input.
    /// </summary>
    /// <param name="value">Value to shift.</param>
    /// <param name="shift">Shift count, 0-255.</param>
    /// <param name="type">Shift type, 0-4.</param>
    /// <param name="carryIn">Carry input for shift count zero and RRX.</param>
    /// <param name="carryOut">Carry produced by the shift.</param>
    /// <returns>The shifted value.</returns>
    /// <remarks>
    /// The JIT keeps CPSR in a block-local while it runs, so it cannot use the
    /// <see cref="Arm60State"/> overload without flushing first. This overload is the
    /// exact same arithmetic with the carry supplied directly.
    /// </remarks>
    public static uint ShiftNoCarry(uint value, byte shift, byte type, uint carryIn, out uint carryOut)
    {
        switch (type)
        {
            case 0:
                if (shift != 0)
                {
                    carryOut = shift > 32 ? 0u : (value << (shift - 1)) >> 31;
                }
                else
                {
                    carryOut = carryIn;
                }

                if (shift == 0)
                {
                    return value;
                }

                return shift > 31 ? 0u : value << shift;

            case 1:
                if (shift != 0)
                {
                    carryOut = shift > 32 ? 0u : (value >> (shift - 1)) & 1u;
                }
                else
                {
                    carryOut = carryIn;
                }

                if (shift == 0)
                {
                    return value;
                }

                return shift > 31 ? 0u : value >> shift;

            case 2:
                if (shift != 0)
                {
                    carryOut = shift > 32
                        ? (uint)((int)value >> 31) & 1u
                        : (uint)((int)value >> (shift - 1)) & 1u;
                }
                else
                {
                    carryOut = carryIn;
                }

                if (shift == 0)
                {
                    return value;
                }

                return shift > 31
                    ? (uint)((int)value >> 31)
                    : (uint)((int)value >> shift);

            case 3:
                if (shift != 0)
                {
                    carryOut = (shift & 31) != 0
                        ? (value >> (shift - 1)) & 1u
                        : (value >> 31) & 1u;
                }
                else
                {
                    carryOut = carryIn;
                }

                shift &= 31;
                return shift == 0 ? value : RotateRight(value, shift);

            case 4:
                carryOut = value & 1u;
                return (value >> 1) | (carryIn << 31);

            default:
                carryOut = 0;
                return 0;
        }
    }

    /// <summary>Shifts a value and updates CPSR.C directly. FreeDO: <c>ARM_SHIFT_SC</c>.</summary>
    /// <param name="state">CPU state to update.</param>
    /// <param name="value">Value to shift.</param>
    /// <param name="shift">Shift count, 0-255.</param>
    /// <param name="type">Shift type, 0-4.</param>
    /// <returns>The shifted value.</returns>
    public static uint ShiftSetCarry(Arm60State state, uint value, byte shift, byte type)
    {
        switch (type)
        {
            case 0:
                if (shift != 0)
                {
                    state.SetC(shift > 32 ? 0u : (value << (shift - 1)) >> 31);
                }
                else
                {
                    return value;
                }

                return shift > 31 ? 0u : value << shift;

            case 1:
                if (shift != 0)
                {
                    state.SetC(shift > 32 ? 0u : (value >> (shift - 1)) & 1u);
                }
                else
                {
                    return value;
                }

                return shift > 31 ? 0u : value >> shift;

            case 2:
                if (shift != 0)
                {
                    state.SetC(shift > 32
                        ? (uint)((int)value >> 31) & 1u
                        : (uint)((int)value >> (shift - 1)) & 1u);
                }
                else
                {
                    return value;
                }

                return shift > 31
                    ? (uint)((int)value >> 31)
                    : (uint)((int)value >> shift);

            case 3:
                if (shift != 0)
                {
                    shift &= 31;
                    state.SetC(shift != 0
                        ? (value >> (shift - 1)) & 1u
                        : (value >> 31) & 1u);
                }
                else
                {
                    return value;
                }

                return shift == 0 ? value : RotateRight(value, shift);

            case 4:
                var carry = ArmAlu.GetCarry(state) << 31;
                state.SetC(value & 1u);
                return (value >> 1) | carry;

            default:
                return 0;
        }
    }

    /// <summary>Rotates a value right by 1-31 bits.</summary>
    /// <param name="value">Value to rotate.</param>
    /// <param name="shift">Rotate count; masked to five bits.</param>
    /// <returns>The rotated value.</returns>
    internal static uint RotateRight(uint value, int shift)
    {
        shift &= 31;
        if (shift == 0)
        {
            return value;
        }

        return (value >> shift) | (value << (32 - shift));
    }
}
