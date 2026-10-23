using System;
using System.Globalization;
using System.Text;

namespace ThreeDo.Cpu;

/// <summary>
/// A readable disassembler for the ARM60 instruction subset implemented by
/// <see cref="Arm60"/>.
/// </summary>
/// <remarks>
/// The disassembler is intended for CLI tracing and debug logs. It follows the same decode
/// order as the interpreter so that an instruction is never shown as a different class than
/// the one that executes.
/// </remarks>
public static class ArmDisassembler
{
    private static readonly string[] Conditions =
    {
        "eq", "ne", "cs", "cc", "mi", "pl", "vs", "vc",
        "hi", "ls", "ge", "lt", "gt", "le", "", "nv",
    };

    private static readonly string[] DataProcessingNames =
    {
        "and", "eor", "sub", "rsb", "add", "adc", "sbc", "rsc",
        "tst", "teq", "cmp", "cmn", "orr", "mov", "bic", "mvn",
    };

    private static readonly string[] ShiftNames = { "lsl", "lsr", "asr", "ror" };

    private static readonly string[] BlockModes = { "da", "ia", "db", "ib" };

    /// <summary>Disassembles one instruction.</summary>
    /// <param name="instruction">Instruction word.</param>
    /// <param name="pc">Address of the instruction, used for branch targets.</param>
    /// <returns>A lower-case ARM assembly string.</returns>
    public static string Disassemble(uint instruction, uint pc = 0)
    {
        var condition = Conditions[(instruction >> 28) & 0xf];
        if ((instruction & ArmDecoder.MultiplyMask) == ArmDecoder.MultiplySignature)
        {
            return FormatMultiply(instruction, condition);
        }

        if ((instruction & ArmDecoder.SwapMask) == ArmDecoder.SwapSignature)
        {
            return FormatSwap(instruction, condition);
        }

        if ((instruction & ArmDecoder.MrsMask) == ArmDecoder.MrsSignature)
        {
            return $"mrs{condition} {Register((int)((instruction >> 12) & 0xf))}, {(((instruction >> 22) & 1) != 0 ? "spsr" : "cpsr")}";
        }

        if ((instruction & ArmDecoder.MsrMask) == ArmDecoder.MsrSignature)
        {
            var status = ((instruction >> 22) & 1) != 0 ? "spsr" : "cpsr";
            return $"msr{condition} {status}, {Register((int)(instruction & 0xf))}";
        }

        var high = (instruction >> 24) & 0xf;
        if ((high <= 3) && (instruction & ArmDecoder.DataProcessingMask) != 0x90)
        {
            return FormatDataProcessing(instruction, condition);
        }

        if ((instruction & ArmDecoder.UndefinedMask) == ArmDecoder.UndefinedSignature)
        {
            return $".word 0x{instruction.ToString("x8", CultureInfo.InvariantCulture)}";
        }

        if (high >= 4 && high <= 7 && (instruction & ArmDecoder.SingleDataTransferMask) != ArmDecoder.SingleDataTransferMask)
        {
            return FormatSingleDataTransfer(instruction, condition);
        }

        if (high == 8 || high == 9)
        {
            return FormatBlockTransfer(instruction, condition);
        }

        if (high == 0xa || high == 0xb)
        {
            return FormatBranch(instruction, condition, pc);
        }

        if (high == 0xf)
        {
            return $"swi{condition} #0x{(instruction & 0xffffff).ToString("x", CultureInfo.InvariantCulture)}";
        }

        return $".word 0x{instruction.ToString("x8", CultureInfo.InvariantCulture)}";
    }

    private static string FormatMultiply(uint instruction, string condition)
    {
        var accumulate = (instruction & (1u << 21)) != 0;
        var setFlags = (instruction & (1u << 20)) != 0;
        var destination = Register((int)((instruction >> 16) & 0xf));
        var accumulator = Register((int)((instruction >> 12) & 0xf));
        var source = Register((int)((instruction >> 8) & 0xf));
        var multiplier = Register((int)(instruction & 0xf));
        var name = accumulate ? "mla" : "mul";
        var flags = setFlags ? "s" : string.Empty;
        return accumulate
            ? $"{name}{condition}{flags} {destination}, {multiplier}, {source}, {accumulator}"
            : $"{name}{condition}{flags} {destination}, {multiplier}, {source}";
    }

    private static string FormatSwap(uint instruction, string condition)
    {
        var byteAccess = (instruction & (1u << 22)) != 0;
        var name = byteAccess ? "swpb" : "swp";
        var destination = Register((int)((instruction >> 12) & 0xf));
        var source = Register((int)(instruction & 0xf));
        var baseRegister = Register((int)((instruction >> 16) & 0xf));
        return $"{name}{condition} {destination}, {source}, [{baseRegister}]";
    }

    private static string FormatDataProcessing(uint instruction, string condition)
    {
        var opcode = (int)((instruction >> 21) & 0xf);
        var name = DataProcessingNames[opcode];
        var setFlags = (instruction & (1u << 20)) != 0;
        var test = opcode >= 8 && opcode <= 11;
        var destination = Register((int)((instruction >> 12) & 0xf));
        var first = Register((int)((instruction >> 16) & 0xf));
        var operand = FormatOperand2(instruction);
        var flags = setFlags && !test ? "s" : string.Empty;

        if (opcode is 13 or 15)
        {
            return $"{name}{condition}{flags} {destination}, {operand}";
        }

        if (test)
        {
            return $"{name}{condition} {first}, {operand}";
        }

        return $"{name}{condition}{flags} {destination}, {first}, {operand}";
    }

    private static string FormatOperand2(uint instruction)
    {
        if ((instruction & (1u << 25)) != 0)
        {
            var immediate = instruction & 0xff;
            var rotate = (int)((instruction >> 7) & 0x1e);
            if (rotate != 0)
            {
                immediate = ArmBarrelShifter.RotateRight(immediate, rotate);
            }

            return $"#0x{immediate.ToString("x", CultureInfo.InvariantCulture)}";
        }

        var register = Register((int)(instruction & 0xf));
        var shiftType = (int)((instruction >> 5) & 3);
        if ((instruction & (1u << 4)) != 0)
        {
            return $"{register}, {ShiftNames[shiftType]} {Register((int)((instruction >> 8) & 0xf))}";
        }

        var shift = (int)((instruction >> 7) & 0x1f);
        if (shift == 0)
        {
            if (shiftType == 0)
            {
                return register;
            }

            if (shiftType == 3)
            {
                return $"{register}, rrx";
            }

            shift = 32;
        }

        return $"{register}, {ShiftNames[shiftType]} #{shift.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatSingleDataTransfer(uint instruction, string condition)
    {
        var load = (instruction & (1u << 20)) != 0;
        var byteAccess = (instruction & (1u << 22)) != 0;
        var preIndex = (instruction & (1u << 24)) != 0;
        var add = (instruction & (1u << 23)) != 0;
        var writeback = (instruction & (1u << 21)) != 0;
        var name = load ? "ldr" : "str";
        var suffix = byteAccess ? "b" : string.Empty;
        var destination = Register((int)((instruction >> 12) & 0xf));
        var baseRegister = Register((int)((instruction >> 16) & 0xf));
        var offset = FormatAddressOffset(instruction, add);
        var builder = new StringBuilder();
        builder.Append(name).Append(condition).Append(suffix).Append(' ').Append(destination).Append(", [").Append(baseRegister);
        if (preIndex)
        {
            if (offset.Length != 0)
            {
                builder.Append(", ").Append(offset);
            }

            builder.Append(']');
            if (writeback)
            {
                builder.Append('!');
            }
        }
        else
        {
            builder.Append(']');
            if (offset.Length != 0)
            {
                builder.Append(", ").Append(offset);
            }
        }

        return builder.ToString();
    }

    private static string FormatAddressOffset(uint instruction, bool add)
    {
        var sign = add ? string.Empty : "-";
        if ((instruction & (1u << 25)) != 0)
        {
            var register = Register((int)(instruction & 0xf));
            var shiftType = (int)((instruction >> 5) & 3);
            if ((instruction & (1u << 4)) != 0)
            {
                return $"{sign}{register}, {ShiftNames[shiftType]} {Register((int)((instruction >> 8) & 0xf))}";
            }

            var shift = (int)((instruction >> 7) & 0x1f);
            if (shift == 0 && shiftType == 0)
            {
                return $"{sign}{register}";
            }

            if (shift == 0 && shiftType == 3)
            {
                return $"{sign}{register}, rrx";
            }

            return $"{sign}{register}, {ShiftNames[shiftType]} #{(shift == 0 ? 32 : shift).ToString(CultureInfo.InvariantCulture)}";
        }

        var immediate = (int)(instruction & 0xfff);
        return immediate == 0 ? string.Empty : $"#{sign}0x{immediate.ToString("x", CultureInfo.InvariantCulture)}";
    }

    private static string FormatBlockTransfer(uint instruction, string condition)
    {
        var load = (instruction & (1u << 20)) != 0;
        var mode = BlockModes[(int)((instruction >> 23) & 3)];
        var writeback = (instruction & (1u << 21)) != 0;
        var user = (instruction & (1u << 22)) != 0;
        var baseRegister = Register((int)((instruction >> 16) & 0xf));
        var list = instruction & 0xffff;
        var builder = new StringBuilder();
        builder.Append(load ? "ldm" : "stm").Append(condition).Append(mode).Append(' ').Append(baseRegister);
        if (writeback)
        {
            builder.Append('!');
        }

        builder.Append(", {");
        var first = true;
        for (var i = 0; i < 16; i++)
        {
            if ((list & (1u << i)) == 0)
            {
                continue;
            }

            if (!first)
            {
                builder.Append(", ");
            }

            builder.Append(Register(i));
            first = false;
        }

        builder.Append('}');
        if (user)
        {
            builder.Append('^');
        }

        return builder.ToString();
    }

    private static string FormatBranch(uint instruction, string condition, uint pc)
    {
        var link = (instruction & (1u << 24)) != 0;
        var offset = (instruction & 0xffffff) | ((instruction & 0x800000) != 0 ? 0xff000000u : 0u);
        var target = unchecked(pc + 8 + (offset << 2));
        return $"{(link ? "bl" : "b")}{condition} 0x{target.ToString("x8", CultureInfo.InvariantCulture)}";
    }

    private static string Register(int index) => $"r{index.ToString(CultureInfo.InvariantCulture)}";
}
