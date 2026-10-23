namespace ThreeDo.Cpu;

/// <summary>
/// ARM data-processing ALU operations, condition-code helpers, and MRS/MSR handling.
/// </summary>
/// <remarks>
/// FreeDO: <c>ARM_ALU_Exec</c>, <c>ARM_SET_ZN</c>, <c>ARM_SET_CV</c>, and
/// <c>ARM_SET_CV_sub</c> in <c>arm.cpp</c>. The opcode is the five-bit field at bit 20,
/// so even opcodes are the flag-preserving forms and odd opcodes are the S-bit forms.
/// </remarks>
public static class ArmAlu
{
    /// <summary>Executes one data-processing operation.</summary>
    /// <param name="state">CPU state to read and update.</param>
    /// <param name="instruction">The full instruction word.</param>
    /// <param name="opcode">Five-bit ALU opcode from bits 20-24.</param>
    /// <param name="op1">First operand (Rn).</param>
    /// <param name="op2">Second operand (shifted immediate or register).</param>
    /// <returns>
    /// <see langword="true"/> when the operation handled the instruction without writing Rd
    /// (MRS, MSR, TST, TEQ, CMP, or CMN); otherwise <see langword="false"/>.
    /// </returns>
    public static bool Execute(Arm60State state, uint instruction, uint opcode, uint op1, uint op2)
    {
        var destination = (int)((instruction >> 12) & 0xf);
        switch (opcode)
        {
            case 0:
                state.User[destination] = op1 & op2;
                break;
            case 2:
                state.User[destination] = op1 ^ op2;
                break;
            case 4:
                state.User[destination] = unchecked(op1 - op2);
                break;
            case 6:
                state.User[destination] = unchecked(op2 - op1);
                break;
            case 8:
                state.User[destination] = unchecked(op1 + op2);
                break;
            case 10:
                state.User[destination] = unchecked(op1 + op2 + GetCarry(state));
                break;
            case 12:
                state.User[destination] = unchecked(op1 - op2 - (GetCarry(state) ^ 1u));
                break;
            case 14:
                state.User[destination] = unchecked(op2 - op1 - (GetCarry(state) ^ 1u));
                break;
            case 16:
            case 20:
                state.User[destination] = ((instruction >> 22) & 1) != 0
                    ? state.CurrentSpsr
                    : state.Cpsr;
                return true;
            case 18:
            case 22:
                SetStatusRegister(state, instruction, op2);
                return true;
            case 24:
                state.User[destination] = op1 | op2;
                break;
            case 26:
                state.User[destination] = op2;
                break;
            case 28:
                state.User[destination] = op1 & ~op2;
                break;
            case 30:
                state.User[destination] = ~op2;
                break;
            case 1:
                state.User[destination] = op1 & op2;
                SetZn(state, state.User[destination]);
                break;
            case 3:
                state.User[destination] = op1 ^ op2;
                SetZn(state, state.User[destination]);
                break;
            case 5:
                state.User[destination] = unchecked(op1 - op2);
                SetZn(state, state.User[destination]);
                SetCvSub(state, state.User[destination], op1, op2);
                break;
            case 7:
                state.User[destination] = unchecked(op2 - op1);
                SetZn(state, state.User[destination]);
                SetCvSub(state, state.User[destination], op2, op1);
                break;
            case 9:
                state.User[destination] = unchecked(op1 + op2);
                SetZn(state, state.User[destination]);
                SetCv(state, state.User[destination], op1, op2);
                break;
            case 11:
                state.User[destination] = unchecked(op1 + op2 + GetCarry(state));
                SetZn(state, state.User[destination]);
                SetCv(state, state.User[destination], op1, op2);
                break;
            case 13:
                state.User[destination] = unchecked(op1 - op2 - (GetCarry(state) ^ 1u));
                SetZn(state, state.User[destination]);
                SetCvSub(state, state.User[destination], op1, op2);
                break;
            case 15:
                state.User[destination] = unchecked(op2 - op1 - (GetCarry(state) ^ 1u));
                SetZn(state, state.User[destination]);
                SetCvSub(state, state.User[destination], op2, op1);
                break;
            case 17:
                SetZn(state, op1 & op2);
                return true;
            case 19:
                SetZn(state, op1 ^ op2);
                return true;
            case 21:
                SetCvSub(state, unchecked(op1 - op2), op1, op2);
                SetZn(state, unchecked(op1 - op2));
                return true;
            case 23:
                SetCv(state, unchecked(op1 + op2), op1, op2);
                SetZn(state, unchecked(op1 + op2));
                return true;
            case 25:
                state.User[destination] = op1 | op2;
                SetZn(state, state.User[destination]);
                break;
            case 27:
                state.User[destination] = op2;
                SetZn(state, state.User[destination]);
                break;
            case 29:
                state.User[destination] = op1 & ~op2;
                SetZn(state, state.User[destination]);
                break;
            case 31:
                state.User[destination] = ~op2;
                SetZn(state, state.User[destination]);
                break;
        }

        return false;
    }

    /// <summary>Gets the current carry flag as zero or one. FreeDO: <c>ARM_GET_C</c>.</summary>
    /// <param name="state">CPU state.</param>
    /// <returns>Zero or one.</returns>
    internal static uint GetCarry(Arm60State state) => (state.Cpsr >> 29) & 1;

    /// <summary>Sets the N and Z flags from a result. FreeDO: <c>ARM_SET_ZN</c>.</summary>
    /// <param name="state">CPU state.</param>
    /// <param name="value">Result value.</param>
    internal static void SetZn(Arm60State state, uint value)
    {
        if (value != 0)
        {
            state.Cpsr = (state.Cpsr & 0x3fffffffu) | (value & 0x80000000u);
        }
        else
        {
            state.Cpsr = (state.Cpsr & 0x3fffffffu) | 0x40000000u;
        }
    }

    /// <summary>Sets C and V for an addition. FreeDO: <c>ARM_SET_CV</c>.</summary>
    /// <param name="state">CPU state.</param>
    /// <param name="result">Result value.</param>
    /// <param name="op1">First operand.</param>
    /// <param name="op2">Second operand.</param>
    internal static void SetCv(Arm60State state, uint result, uint op1, uint op2)
    {
        state.Cpsr = (state.Cpsr & 0xcfffffffu) |
            ((((op1 & op2) | (~result & (op1 | op2))) & 0x80000000u) >> 2) |
            ((((op1 & (op2 & ~result)) | (~op1 & ~op2 & result)) & 0x80000000u) >> 3);
    }

    /// <summary>Sets C and V for a subtraction. FreeDO: <c>ARM_SET_CV_sub</c>.</summary>
    /// <param name="state">CPU state.</param>
    /// <param name="result">Result value.</param>
    /// <param name="op1">First operand.</param>
    /// <param name="op2">Second operand.</param>
    internal static void SetCvSub(Arm60State state, uint result, uint op1, uint op2)
    {
        state.Cpsr = (state.Cpsr & 0xcfffffffu) |
            ((((op1 & ~op2) | (~result & (op1 | ~op2))) & 0x80000000u) >> 2) |
            ((((op1 & (~op2 & ~result)) | (~op1 & op2 & result)) & 0x80000000u) >> 3);
    }

    private static void SetStatusRegister(Arm60State state, uint instruction, uint value)
    {
        var mode = ArmModes.Table[state.Cpsr & 0x1f];
        if (mode >= state.Spsr.Length)
        {
            return;
        }

        var isSpsr = ((instruction >> 22) & 1) != 0;
        if (((instruction >> 16) & 1) == 0 || mode == ArmModes.User)
        {
            if (isSpsr)
            {
                state.Spsr[mode] = (state.Spsr[mode] & 0x0fffffffu) | (value & 0xf0000000u);
            }
            else
            {
                state.Cpsr = (state.Cpsr & 0x0fffffffu) | (value & 0xf0000000u);
            }
        }
        else
        {
            if (isSpsr)
            {
                state.Spsr[mode] = value & 0xf00000dfu;
            }
            else
            {
                state.SetCpsr(value);
            }
        }
    }
}
