namespace FiveDo.Cpu;

/// <summary>
/// Instruction masks, condition evaluation, and coarse classification for the ARM60.
/// </summary>
/// <remarks>
/// The masks are copied from <c>arm.cpp</c>. Classification mirrors the reference's
/// switch fallthrough so diagnostics and the disassembler agree with execution.
/// </remarks>
public static class ArmDecoder
{
    /// <summary>Multiply/MLA mask. FreeDO: <c>ARM_MUL_MASK</c>.</summary>
    public const uint MultiplyMask = 0x0fc000f0;

    /// <summary>Multiply/MLA signature. FreeDO: <c>ARM_MUL_SIGN</c>.</summary>
    public const uint MultiplySignature = 0x00000090;

    /// <summary>Swap mask. FreeDO: <c>ARM_SDS_MASK</c>.</summary>
    public const uint SwapMask = 0x0fb00ff0;

    /// <summary>Swap signature. FreeDO: <c>ARM_SDS_SIGN</c>.</summary>
    public const uint SwapSignature = 0x01000090;

    /// <summary>Undefined-instruction mask. FreeDO: <c>ARM_UND_MASK</c>.</summary>
    public const uint UndefinedMask = 0x0e000010;

    /// <summary>Undefined-instruction signature. FreeDO: <c>ARM_UND_SIGN</c>.</summary>
    public const uint UndefinedSignature = 0x06000010;

    /// <summary>MRS mask. FreeDO: <c>ARM_MRS_MASK</c>.</summary>
    public const uint MrsMask = 0x0fbf0fff;

    /// <summary>MRS signature. FreeDO: <c>ARM_MRS_SIGN</c>.</summary>
    public const uint MrsSignature = 0x010f0000;

    /// <summary>MSR register mask. FreeDO: <c>ARM_MSR_MASK</c>.</summary>
    public const uint MsrMask = 0x0fbffff0;

    /// <summary>MSR register signature. FreeDO: <c>ARM_MSR_SIGN</c>.</summary>
    public const uint MsrSignature = 0x0129f000;

    /// <summary>Data-processing discriminator. FreeDO uses <c>0x2000090 != 0x90</c>.</summary>
    public const uint DataProcessingMask = 0x02000090;

    /// <summary>Single-data-transfer discriminator. FreeDO uses <c>0x2000090 != 0x2000090</c>.</summary>
    public const uint SingleDataTransferMask = 0x02000090;

    private static readonly ushort[] ConditionFlags =
    {
        0xf0f0, // EQ - Z set (equal)
        0x0f0f, // NE - Z clear (not equal)
        0xcccc, // CS - C set (unsigned higher or same)
        0x3333, // CC - C clear (unsigned lower)
        0xff00, // MI - N set (negative)
        0x00ff, // PL - N clear (positive or zero)
        0xaaaa, // VS - V set (overflow)
        0x5555, // VC - V clear (no overflow)
        0x0c0c, // HI - C set and Z clear (unsigned higher)
        0xf3f3, // LS - C clear or Z set (unsigned lower or same)
        0xaa55, // GE - N set and V set, or N clear and V clear
        0x55aa, // LT - N set and V clear, or N clear and V set
        0x0a05, // GT - Z clear, and either N set and V set, or N clear and V clear
        0xf5fa, // LE - Z set, or N set and V clear, or N clear and V set
        0xffff, // AL - always
        0x0000, // NV - never
    };

    /// <summary>Evaluates one ARM condition code. FreeDO: <c>cond_flags_cross</c>.</summary>
    /// <param name="cpsr">Current CPSR.</param>
    /// <param name="condition">Condition code from bits 28-31.</param>
    /// <returns><see langword="true"/> when the condition passes.</returns>
    public static bool ConditionPassed(uint cpsr, uint condition)
    {
        return ((ConditionFlags[condition & 0xf] >> (int)((cpsr >> 28) & 0xf)) & 1) != 0;
    }

    /// <summary>Classifies an instruction using FreeDOCore's decode order.</summary>
    /// <param name="instruction">Instruction word.</param>
    /// <returns>The instruction class.</returns>
    public static ArmInstructionClass Classify(uint instruction)
    {
        var high = (instruction >> 24) & 0xf;
        if (high == 0 && (instruction & MultiplyMask) == MultiplySignature)
        {
            return ArmInstructionClass.Multiply;
        }

        if ((high == 0 || high == 1) && (instruction & SwapMask) == SwapSignature)
        {
            return ArmInstructionClass.Swap;
        }

        if (high <= 3)
        {
            if ((instruction & DataProcessingMask) != 0x90)
            {
                return ArmInstructionClass.DataProcessing;
            }

            if ((instruction & UndefinedMask) == UndefinedSignature)
            {
                return ArmInstructionClass.Undefined;
            }

            return (instruction & SingleDataTransferMask) != SingleDataTransferMask
                ? ArmInstructionClass.SingleDataTransfer
                : ArmInstructionClass.Undefined;
        }

        if (high == 6 || high == 7)
        {
            if ((instruction & UndefinedMask) == UndefinedSignature)
            {
                return ArmInstructionClass.Undefined;
            }

            return (instruction & SingleDataTransferMask) != SingleDataTransferMask
                ? ArmInstructionClass.SingleDataTransfer
                : ArmInstructionClass.Undefined;
        }

        if (high == 4 || high == 5)
        {
            return (instruction & SingleDataTransferMask) != SingleDataTransferMask
                ? ArmInstructionClass.SingleDataTransfer
                : ArmInstructionClass.Undefined;
        }

        if (high == 8 || high == 9)
        {
            return ArmInstructionClass.BlockTransfer;
        }

        if (high == 0xa || high == 0xb)
        {
            return ArmInstructionClass.Branch;
        }

        if (high == 0xf)
        {
            return ArmInstructionClass.SoftwareInterrupt;
        }

        return ArmInstructionClass.Coprocessor;
    }
}
