using System;
using FiveDo.Core;

namespace FiveDo.Madam;

/// <summary>
/// VRAM access with CEL cycle accounting. FreeDO: <c>Madam.cpp: mread</c>, <c>mwrite</c>,
/// <c>mreadh</c>, <c>mwriteh</c>, and <c>readPLUTDATA</c>.
/// </summary>
/// <remarks>
/// CEL cycles are accumulated as the reference does: one cycle for a halfword or word
/// read, two for a halfword or word write, and four for a PLUT read. <see cref="TakeCycles"/>
/// returns and clears the accumulator, matching <c>_madam_GetCELCycles</c>.
/// </remarks>
public sealed class CelMemory
{
    private readonly IRam _ram;

    /// <summary>Initializes a new instance of the <see cref="CelMemory"/> class.</summary>
    /// <param name="ram">Raw RAM/VRAM accessor.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ram"/> is null.</exception>
    public CelMemory(IRam ram)
    {
        ArgumentNullException.ThrowIfNull(ram);
        _ram = ram;
    }

    /// <summary>Gets the accumulated CEL cycles. FreeDO: <c>CELCYCLES</c>.</summary>
    public int Cycles { get; private set; }

    /// <summary>Reads a word and adds one CEL cycle. FreeDO: <c>mread</c>.</summary>
    /// <param name="address">Raw VRAM byte address.</param>
    /// <returns>The word value.</returns>
    public uint ReadWord(uint address)
    {
        Cycles++;
        return _ram.Read32(address);
    }

    /// <summary>Writes a word and adds two CEL cycles. FreeDO: <c>mwrite</c>.</summary>
    /// <param name="address">Raw VRAM byte address.</param>
    /// <param name="value">Word value.</param>
    public void WriteWord(uint address, uint value)
    {
        Cycles += 2;
        _ram.Write32(address, value);
    }

    /// <summary>Reads a halfword and adds one CEL cycle. FreeDO: <c>mreadh</c>.</summary>
    /// <param name="address">Raw VRAM byte address.</param>
    /// <returns>The halfword value.</returns>
    public ushort ReadHalfword(uint address)
    {
        Cycles++;
        return _ram.Read16(address ^ 2);
    }

    /// <summary>Writes a halfword and adds two CEL cycles. FreeDO: <c>mwriteh</c>.</summary>
    /// <param name="address">Raw VRAM byte address.</param>
    /// <param name="value">Halfword value.</param>
    public void WriteHalfword(uint address, ushort value)
    {
        Cycles += 2;
        _ram.Write16(address ^ 2, value);
    }

    /// <summary>Reads a PLUT halfword and adds four CEL cycles. FreeDO: <c>readPLUTDATA</c>.</summary>
    /// <param name="plutBase">PLUT base byte address.</param>
    /// <param name="offset">Byte offset within the PLUT.</param>
    /// <returns>The halfword value, or zero when the PLUT base is zero.</returns>
    public ushort ReadPlut(uint plutBase, uint offset)
    {
        Cycles += 4;
        return plutBase == 0 ? (ushort)0 : _ram.Read16(plutBase + (offset ^ 2));
    }

    /// <summary>Returns and clears the accumulated CEL cycles. FreeDO: <c>_madam_GetCELCycles</c>.</summary>
    /// <returns>The accumulated cycle count.</returns>
    public int TakeCycles()
    {
        var cycles = Cycles;
        Cycles = 0;
        return cycles;
    }
}
