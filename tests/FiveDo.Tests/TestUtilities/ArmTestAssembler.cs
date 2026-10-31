using System;

namespace FiveDo.Tests.TestUtilities;

/// <summary>
/// Small ARM instruction encoder used by the CPU tests.
/// </summary>
internal static class ArmTestAssembler
{
    public const uint Al = 0xe;

    public static uint Condition(uint condition, uint instruction)
    {
        return (condition << 28) | (instruction & 0x0fffffffu);
    }

    public static uint EncodeImmediate(uint value)
    {
        for (var rotate = 0; rotate < 16; rotate++)
        {
            var candidate = RotateLeft(value, rotate * 2);
            if ((candidate & ~0xffu) == 0)
            {
                return ((uint)rotate << 8) | candidate;
            }
        }

        throw new ArgumentException($"The value 0x{value:x8} cannot be encoded as an ARM immediate.", nameof(value));
    }

    public static uint DpImmediate(
        int opcode,
        int rd,
        int rn,
        uint immediate,
        bool setFlags = false,
        uint condition = Al)
    {
        var instruction = 0x02000000u | ((uint)opcode << 21) | ((uint)rn << 16) | ((uint)rd << 12) | EncodeImmediate(immediate);
        if (setFlags)
        {
            instruction |= 1u << 20;
        }

        return Condition(condition, instruction);
    }

    public static uint DpRegister(
        int opcode,
        int rd,
        int rn,
        int rm,
        int shiftType = 0,
        int shift = 0,
        bool setFlags = false,
        uint condition = Al)
    {
        var instruction = ((uint)opcode << 21) |
            ((uint)rn << 16) |
            ((uint)rd << 12) |
            ((uint)shift << 7) |
            ((uint)shiftType << 5) |
            (uint)rm;
        if (setFlags)
        {
            instruction |= 1u << 20;
        }

        return Condition(condition, instruction);
    }

    public static uint DpRegisterShiftedByRegister(
        int opcode,
        int rd,
        int rn,
        int rm,
        int rs,
        int shiftType = 0,
        bool setFlags = false,
        uint condition = Al)
    {
        var instruction = ((uint)opcode << 21) |
            ((uint)rn << 16) |
            ((uint)rd << 12) |
            ((uint)rs << 8) |
            ((uint)shiftType << 5) |
            (1u << 4) |
            (uint)rm;
        if (setFlags)
        {
            instruction |= 1u << 20;
        }

        return Condition(condition, instruction);
    }

    public static uint Mov(int rd, uint value, uint condition = Al)
    {
        return DpImmediate(13, rd, 0, value, false, condition);
    }

    public static uint Mvn(int rd, uint value, uint condition = Al)
    {
        return DpImmediate(15, rd, 0, value, false, condition);
    }

    public static uint MovRegister(int rd, int rm, uint condition = Al)
    {
        return DpRegister(13, rd, 0, rm, condition: condition);
    }

    public static uint AddImmediate(int rd, int rn, uint value, bool setFlags = false, uint condition = Al)
    {
        return DpImmediate(4, rd, rn, value, setFlags, condition);
    }

    public static uint SubImmediate(int rd, int rn, uint value, bool setFlags = false, uint condition = Al)
    {
        return DpImmediate(2, rd, rn, value, setFlags, condition);
    }

    public static uint CmpImmediate(int rn, uint value, uint condition = Al)
    {
        return DpImmediate(10, 0, rn, value, true, condition);
    }

    public static uint CmpRegister(int rn, int rm, uint condition = Al)
    {
        return DpRegister(10, 0, rn, rm, setFlags: true, condition: condition);
    }

    public static uint TstRegister(int rn, int rm, uint condition = Al)
    {
        return DpRegister(8, 0, rn, rm, setFlags: true, condition: condition);
    }

    public static uint TeqRegister(int rn, int rm, uint condition = Al)
    {
        return DpRegister(9, 0, rn, rm, setFlags: true, condition: condition);
    }

    public static uint Mul(int rd, int rm, int rs, bool setFlags = false, uint condition = Al)
    {
        return Condition(condition, 0x00000090u | ((uint)rd << 16) | ((uint)rs << 8) | (uint)rm | (setFlags ? 1u << 20 : 0));
    }

    public static uint Mla(int rd, int rm, int rs, int rn, bool setFlags = false, uint condition = Al)
    {
        return Condition(condition, 0x00200090u | ((uint)rd << 16) | ((uint)rn << 12) | ((uint)rs << 8) | (uint)rm | (setFlags ? 1u << 20 : 0));
    }

    public static uint Swp(int rd, int rm, int rn, bool byteAccess = false, uint condition = Al)
    {
        return Condition(condition, 0x01000090u | (byteAccess ? 1u << 22 : 0) | ((uint)rn << 16) | ((uint)rd << 12) | (uint)rm);
    }

    public static uint LdrImmediate(
        int rd,
        int rn,
        int offset = 0,
        bool preIndex = true,
        bool add = true,
        bool writeback = false,
        bool byteAccess = false,
        uint condition = Al)
    {
        var instruction = 0x04000000u |
            (preIndex ? 1u << 24 : 0) |
            (add ? 1u << 23 : 0) |
            (byteAccess ? 1u << 22 : 0) |
            (writeback ? 1u << 21 : 0) |
            (1u << 20) |
            ((uint)rn << 16) |
            ((uint)rd << 12) |
            ((uint)offset & 0xfff);
        return Condition(condition, instruction);
    }

    public static uint StrImmediate(
        int rd,
        int rn,
        int offset = 0,
        bool preIndex = true,
        bool add = true,
        bool writeback = false,
        bool byteAccess = false,
        uint condition = Al)
    {
        var instruction = 0x04000000u |
            (preIndex ? 1u << 24 : 0) |
            (add ? 1u << 23 : 0) |
            (byteAccess ? 1u << 22 : 0) |
            (writeback ? 1u << 21 : 0) |
            ((uint)rn << 16) |
            ((uint)rd << 12) |
            ((uint)offset & 0xfff);
        return Condition(condition, instruction);
    }

    public static uint LdrRegister(
        int rd,
        int rn,
        int rm,
        int shiftType = 0,
        int shift = 0,
        bool preIndex = true,
        bool add = true,
        bool writeback = false,
        bool byteAccess = false,
        uint condition = Al)
    {
        var instruction = 0x04000000u |
            (1u << 25) |
            (preIndex ? 1u << 24 : 0) |
            (add ? 1u << 23 : 0) |
            (byteAccess ? 1u << 22 : 0) |
            (writeback ? 1u << 21 : 0) |
            (1u << 20) |
            ((uint)rn << 16) |
            ((uint)rd << 12) |
            ((uint)shift << 7) |
            ((uint)shiftType << 5) |
            (uint)rm;
        return Condition(condition, instruction);
    }

    public static uint LdrRegisterShiftedByRegister(
        int rd,
        int rn,
        int rm,
        int rs,
        int shiftType = 0,
        uint condition = Al)
    {
        var instruction = 0x04000000u |
            (1u << 25) |
            (1u << 24) |
            (1u << 23) |
            (1u << 20) |
            ((uint)rn << 16) |
            ((uint)rd << 12) |
            ((uint)rs << 8) |
            ((uint)shiftType << 5) |
            (1u << 4) |
            (uint)rm;
        return Condition(condition, instruction);
    }

    public static uint Block(
        bool load,
        int rn,
        uint list,
        bool preIndex,
        bool add,
        bool writeback = false,
        bool userMode = false,
        uint condition = Al)
    {
        var instruction = 0x08000000u |
            (preIndex ? 1u << 24 : 0) |
            (add ? 1u << 23 : 0) |
            (userMode ? 1u << 22 : 0) |
            (writeback ? 1u << 21 : 0) |
            (load ? 1u << 20 : 0) |
            ((uint)rn << 16) |
            (list & 0xffff);
        return Condition(condition, instruction);
    }

    public static uint Branch(uint target, uint pc, bool link = false, uint condition = Al)
    {
        var offset = unchecked((target - (pc + 8)) >> 2) & 0xffffff;
        var instruction = 0x0a000000u | offset | (link ? 1u << 24 : 0);
        return Condition(condition, instruction);
    }

    public static uint Swi(uint immediate, uint condition = Al)
    {
        return Condition(condition, 0x0f000000u | (immediate & 0xffffff));
    }

    public static uint Mrs(int rd, bool spsr = false, uint condition = Al)
    {
        return Condition(condition, 0x010f0000u | (spsr ? 1u << 22 : 0) | ((uint)rd << 12));
    }

    public static uint Msr(int rm, bool spsr = false, uint mask = 0x9, uint condition = Al)
    {
        return Condition(condition, 0x0129f000u | (spsr ? 1u << 22 : 0) | ((mask & 0xf) << 16) | (uint)rm);
    }

    public static uint Nop(uint condition = Al)
    {
        return MovRegister(0, 0, condition);
    }

    private static uint RotateLeft(uint value, int shift)
    {
        shift &= 31;
        return shift == 0 ? value : (value << shift) | (value >> (32 - shift));
    }
}
