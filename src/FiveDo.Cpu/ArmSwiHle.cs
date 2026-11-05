using System;
using FiveDo.Core;

namespace FiveDo.Cpu;

/// <summary>
/// Host implementations of the Portfolio OS fixed-point math SWIs.
/// </summary>
/// <remarks>
/// <para>
/// This is a direct port of Opera's <c>opera_swi_hle_0x5XXXX.h</c> and
/// <c>opera_fixedpoint_math.c</c>. The routines operate on the same 16.16 fixed-point
/// values as the guest and use <see cref="IRam"/> so unaligned guest pointers behave like
/// the reference's byte-addressed DRAM access.
/// </para>
/// <para>
/// Only the SWIs Opera actually intercepts are handled here. Opera leaves
/// <c>0x50003</c>, <c>0x50004</c>, <c>0x5000A</c>, <c>0x5000B</c>, and <c>0x5000D</c> to
/// the guest OS, so <see cref="TryExecute"/> returns <see langword="false"/> for them and
/// the CPU traps normally.
/// </para>
/// </remarks>
internal static class ArmSwiHle
{
    private const uint CommentMask = 0x000FFFFF;

    /// <summary>Tries to execute one Portfolio OS math SWI.</summary>
    /// <param name="instruction">The SWI instruction, including its 24-bit comment.</param>
    /// <param name="registers">The ARM user register file (r0-r15).</param>
    /// <param name="ram">Raw guest RAM accessor.</param>
    /// <returns><see langword="true"/> when the SWI was handled on the host.</returns>
    public static bool TryExecute(uint instruction, uint[] registers, IRam ram)
    {
        ArgumentNullException.ThrowIfNull(registers);
        ArgumentNullException.ThrowIfNull(ram);
        switch (instruction & CommentMask)
        {
            case 0x50000:
                MulVec3Mat33(ram, registers[0], registers[1], registers[2]);
                return true;
            case 0x50001:
                MulMat33Mat33(ram, registers[0], registers[1], registers[2]);
                return true;
            case 0x50002:
                MulManyVec3Mat33(ram, registers[0], registers[1], registers[2], (int)registers[3]);
                return true;
            case 0x50005:
                MulManyF16(ram, registers[0], registers[1], registers[2], (int)registers[3]);
                return true;
            case 0x50006:
                MulScalerF16(ram, registers[0], registers[1], (int)registers[2], (int)registers[3]);
                return true;
            case 0x50007:
                MulVec4Mat44(ram, registers[0], registers[1], registers[2]);
                return true;
            case 0x50008:
                MulMat44Mat44(ram, registers[0], registers[1], registers[2]);
                return true;
            case 0x50009:
                MulManyVec4Mat44(ram, registers[0], registers[1], registers[2], (int)registers[3]);
                return true;
            case 0x5000C:
                registers[0] = unchecked((uint)Dot3(ram, registers[0], registers[1]));
                return true;
            case 0x5000E:
                Cross3(ram, registers[0], registers[1], registers[2]);
                return true;
            case 0x5000F:
                registers[0] = unchecked((uint)AbsVec3(ram, registers[0]));
                return true;
            case 0x50010:
                registers[0] = unchecked((uint)AbsVec4(ram, registers[0]));
                return true;
            case 0x50011:
                MulVec3Mat33DivZ(ram, registers[0], registers[1], registers[2], (int)registers[3]);
                return true;
            case 0x50012:
                MulManyVec3Mat33DivZStruct(ram, registers[0]);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Reads one 16.16 fixed-point value.</summary>
    private static int ReadS32(IRam ram, uint address)
    {
        return unchecked((int)ram.Read32(address));
    }

    /// <summary>Writes one 16.16 fixed-point value.</summary>
    private static void WriteS32(IRam ram, uint address, int value)
    {
        ram.Write32(address, unchecked((uint)value));
    }

    private static void ReadMat33(IRam ram, uint address, Span<int> destination)
    {
        for (var i = 0; i < 9; i++)
        {
            destination[i] = ReadS32(ram, address + ((uint)i * 4));
        }
    }

    private static void WriteMat33(IRam ram, uint address, ReadOnlySpan<int> source)
    {
        for (var i = 0; i < 9; i++)
        {
            WriteS32(ram, address + ((uint)i * 4), source[i]);
        }
    }

    private static void ReadMat44(IRam ram, uint address, Span<int> destination)
    {
        for (var i = 0; i < 16; i++)
        {
            destination[i] = ReadS32(ram, address + ((uint)i * 4));
        }
    }

    private static void WriteMat44(IRam ram, uint address, ReadOnlySpan<int> source)
    {
        for (var i = 0; i < 16; i++)
        {
            WriteS32(ram, address + ((uint)i * 4), source[i]);
        }
    }

    /// <summary>Opera: <c>MulVec3Mat33_F16</c> (SWI 0x50000).</summary>
    private static void MulVec3Mat33(IRam ram, uint destAddress, uint vectorAddress, uint matrixAddress)
    {
        Span<int> matrix = stackalloc int[9];
        ReadMat33(ram, matrixAddress, matrix);
        MulVec3WithMatrix(ram, destAddress, vectorAddress, matrix);
    }

    private static void MulVec3WithMatrix(
        IRam ram,
        uint destAddress,
        uint vectorAddress,
        ReadOnlySpan<int> matrix)
    {
        int x = ReadS32(ram, vectorAddress);
        int y = ReadS32(ram, vectorAddress + 4);
        int z = ReadS32(ram, vectorAddress + 8);

        int rx = unchecked((int)((((long)x * matrix[0]) + ((long)y * matrix[3]) + ((long)z * matrix[6])) >> 16));
        int ry = unchecked((int)((((long)x * matrix[1]) + ((long)y * matrix[4]) + ((long)z * matrix[7])) >> 16));
        int rz = unchecked((int)((((long)x * matrix[2]) + ((long)y * matrix[5]) + ((long)z * matrix[8])) >> 16));

        WriteS32(ram, destAddress, rx);
        WriteS32(ram, destAddress + 4, ry);
        WriteS32(ram, destAddress + 8, rz);
    }

    /// <summary>Opera: <c>MulMat33Mat33_F16</c> (SWI 0x50001).</summary>
    private static void MulMat33Mat33(IRam ram, uint destAddress, uint source1Address, uint source2Address)
    {
        Span<int> source1 = stackalloc int[9];
        Span<int> source2 = stackalloc int[9];
        Span<int> result = stackalloc int[9];
        ReadMat33(ram, source1Address, source1);
        ReadMat33(ram, source2Address, source2);

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                result[(row * 3) + column] = unchecked((int)(
                    (((long)source1[row * 3] * source2[column])
                        + ((long)source1[(row * 3) + 1] * source2[3 + column])
                        + ((long)source1[(row * 3) + 2] * source2[6 + column])) >> 16));
            }
        }

        WriteMat33(ram, destAddress, result);
    }

    /// <summary>Opera: <c>MulManyVec3Mat33_F16</c> (SWI 0x50002).</summary>
    private static void MulManyVec3Mat33(
        IRam ram,
        uint destAddress,
        uint sourceAddress,
        uint matrixAddress,
        int count)
    {
        Span<int> matrix = stackalloc int[9];
        ReadMat33(ram, matrixAddress, matrix);
        for (var i = 0; i < count; i++)
        {
            MulVec3WithMatrix(
                ram,
                destAddress + ((uint)i * 12),
                sourceAddress + ((uint)i * 12),
                matrix);
        }
    }

    /// <summary>Opera: <c>MulManyF16</c> (SWI 0x50005).</summary>
    private static void MulManyF16(
        IRam ram,
        uint destAddress,
        uint source1Address,
        uint source2Address,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            uint offset = (uint)i * 4;
            int source1 = ReadS32(ram, source1Address + offset);
            int source2 = ReadS32(ram, source2Address + offset);
            WriteS32(ram, destAddress + offset, unchecked((int)(((long)source1 * source2) >> 16)));
        }
    }

    /// <summary>Opera: <c>MulScalerF16</c> (SWI 0x50006).</summary>
    private static void MulScalerF16(
        IRam ram,
        uint destAddress,
        uint sourceAddress,
        int scaler,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            uint offset = (uint)i * 4;
            int source = ReadS32(ram, sourceAddress + offset);
            WriteS32(ram, destAddress + offset, unchecked((int)(((long)source * scaler) >> 16)));
        }
    }

    /// <summary>Opera: <c>MulVec4Mat44_F16</c> (SWI 0x50007).</summary>
    private static void MulVec4Mat44(IRam ram, uint destAddress, uint vectorAddress, uint matrixAddress)
    {
        Span<int> matrix = stackalloc int[16];
        ReadMat44(ram, matrixAddress, matrix);
        MulVec4WithMatrix(ram, destAddress, vectorAddress, matrix);
    }

    private static void MulVec4WithMatrix(
        IRam ram,
        uint destAddress,
        uint vectorAddress,
        ReadOnlySpan<int> matrix)
    {
        int x = ReadS32(ram, vectorAddress);
        int y = ReadS32(ram, vectorAddress + 4);
        int z = ReadS32(ram, vectorAddress + 8);
        int w = ReadS32(ram, vectorAddress + 12);

        for (var column = 0; column < 4; column++)
        {
            int value = unchecked((int)(
                (((long)x * matrix[column])
                    + ((long)y * matrix[4 + column])
                    + ((long)z * matrix[8 + column])
                    + ((long)w * matrix[12 + column])) >> 16));
            WriteS32(ram, destAddress + ((uint)column * 4), value);
        }
    }

    /// <summary>Opera: <c>MulMat44Mat44_F16</c> (SWI 0x50008).</summary>
    private static void MulMat44Mat44(IRam ram, uint destAddress, uint source1Address, uint source2Address)
    {
        Span<int> source1 = stackalloc int[16];
        Span<int> source2 = stackalloc int[16];
        Span<int> result = stackalloc int[16];
        ReadMat44(ram, source1Address, source1);
        ReadMat44(ram, source2Address, source2);

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                result[(row * 4) + column] = unchecked((int)(
                    (((long)source1[row * 4] * source2[column])
                        + ((long)source1[(row * 4) + 1] * source2[4 + column])
                        + ((long)source1[(row * 4) + 2] * source2[8 + column])
                        + ((long)source1[(row * 4) + 3] * source2[12 + column])) >> 16));
            }
        }

        WriteMat44(ram, destAddress, result);
    }

    /// <summary>Opera: <c>MulManyVec4Mat44_F16</c> (SWI 0x50009).</summary>
    private static void MulManyVec4Mat44(
        IRam ram,
        uint destAddress,
        uint sourceAddress,
        uint matrixAddress,
        int count)
    {
        Span<int> matrix = stackalloc int[16];
        ReadMat44(ram, matrixAddress, matrix);
        for (var i = 0; i < count; i++)
        {
            MulVec4WithMatrix(
                ram,
                destAddress + ((uint)i * 16),
                sourceAddress + ((uint)i * 16),
                matrix);
        }
    }

    /// <summary>Opera: <c>Dot3_F16</c> (SWI 0x5000C).</summary>
    private static int Dot3(IRam ram, uint vector1Address, uint vector2Address)
    {
        return unchecked((int)((
            ((long)ReadS32(ram, vector1Address) * ReadS32(ram, vector2Address))
            + ((long)ReadS32(ram, vector1Address + 4) * ReadS32(ram, vector2Address + 4))
            + ((long)ReadS32(ram, vector1Address + 8) * ReadS32(ram, vector2Address + 8))) >> 16));
    }

    /// <summary>Opera: <c>Cross3_F16</c> (SWI 0x5000E).</summary>
    private static void Cross3(IRam ram, uint destAddress, uint vector1Address, uint vector2Address)
    {
        int x1 = ReadS32(ram, vector1Address);
        int y1 = ReadS32(ram, vector1Address + 4);
        int z1 = ReadS32(ram, vector1Address + 8);
        int x2 = ReadS32(ram, vector2Address);
        int y2 = ReadS32(ram, vector2Address + 4);
        int z2 = ReadS32(ram, vector2Address + 8);

        int x = unchecked((int)((((long)y1 * z2) - ((long)z1 * y2)) >> 16));
        int y = unchecked((int)((((long)z1 * x2) - ((long)x1 * z2)) >> 16));
        int z = unchecked((int)((((long)x1 * y2) - ((long)y1 * x2)) >> 16));

        WriteS32(ram, destAddress, x);
        WriteS32(ram, destAddress + 4, y);
        WriteS32(ram, destAddress + 8, z);
    }

    /// <summary>Opera: <c>AbsVec3_F16</c> (SWI 0x5000F).</summary>
    private static int AbsVec3(IRam ram, uint vectorAddress)
    {
        int x = ReadS32(ram, vectorAddress);
        int y = ReadS32(ram, vectorAddress + 4);
        int z = ReadS32(ram, vectorAddress + 8);
        int sum = unchecked((int)((
            ((long)x * x) + ((long)y * y) + ((long)z * z)) >> 16));
        return SqrtFrac16(sum);
    }

    /// <summary>Opera: <c>AbsVec4_F16</c> (SWI 0x50010).</summary>
    private static int AbsVec4(IRam ram, uint vectorAddress)
    {
        int x = ReadS32(ram, vectorAddress);
        int y = ReadS32(ram, vectorAddress + 4);
        int z = ReadS32(ram, vectorAddress + 8);
        int w = ReadS32(ram, vectorAddress + 12);
        int sum = unchecked((int)((
            ((long)x * x) + ((long)y * y) + ((long)z * z) + ((long)w * w)) >> 16));
        return SqrtFrac16(sum);
    }

    /// <summary>Opera: <c>MulVec3Mat33DivZ_F16</c> (SWI 0x50011).</summary>
    private static void MulVec3Mat33DivZ(
        IRam ram,
        uint destAddress,
        uint vectorAddress,
        uint matrixAddress,
        int n)
    {
        Span<int> matrix = stackalloc int[9];
        ReadMat33(ram, matrixAddress, matrix);
        MulVec3WithMatrix(ram, destAddress, vectorAddress, matrix);
        DivideZ(ram, destAddress, n);
    }

    /// <summary>Opera: <c>MulManyVec3Mat33DivZ_F16</c> (SWI 0x50012).</summary>
    private static void MulManyVec3Mat33DivZStruct(IRam ram, uint structAddress)
    {
        uint destAddress = ram.Read32(structAddress);
        uint sourceAddress = ram.Read32(structAddress + 4);
        uint matrixAddress = ram.Read32(structAddress + 8);
        int n = ReadS32(ram, structAddress + 12);
        uint count = ram.Read32(structAddress + 16);

        Span<int> matrix = stackalloc int[9];
        ReadMat33(ram, matrixAddress, matrix);
        for (uint i = 0; i < count; i++)
        {
            uint offset = i * 12;
            uint dest = destAddress + offset;
            MulVec3WithMatrix(ram, dest, sourceAddress + offset, matrix);
            DivideZ(ram, dest, n);
        }
    }

    private static void DivideZ(IRam ram, uint destAddress, int n)
    {
        int z = ReadS32(ram, destAddress + 8);
        if (z == 0)
        {
            return;
        }

        long multiplier = ((long)n << 16) / z;
        int x = ReadS32(ram, destAddress);
        int y = ReadS32(ram, destAddress + 4);
        WriteS32(ram, destAddress, unchecked((int)((x * multiplier) >> 16)));
        WriteS32(ram, destAddress + 4, unchecked((int)((y * multiplier) >> 16)));
    }

    /// <summary>Opera's <c>sqrt_frac16</c> restoring loop.</summary>
    /// <remarks>
    /// This intentionally mirrors the reference implementation, including its 17-iteration
    /// <c>do/while</c> and signed 32-bit shifts. It is not a general-purpose square root.
    /// </remarks>
    private static int SqrtFrac16(int value)
    {
        var root = 0;
        var remainderHigh = 0;
        var remainderLow = value;
        var count = 16;
        do
        {
            remainderHigh = unchecked((remainderHigh << 16) | (remainderLow >> 16));
            remainderLow = unchecked(remainderLow << 16);
            var testDivisor = unchecked((root << 1) + 1);
            if (remainderHigh >= testDivisor)
            {
                remainderHigh = unchecked(remainderHigh - testDivisor);
                root = unchecked(root + 1);
            }
        }
        while (count-- != 0);

        return root;
    }
}
