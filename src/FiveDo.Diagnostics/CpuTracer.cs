using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Cpu;

namespace FiveDo.Diagnostics;

/// <summary>
/// Steps the CPU and reports each instruction for debugging. Host-only; never used in the
/// deterministic core loop.
/// </summary>
public static class CpuTracer
{
    /// <summary>Traces a fixed number of instructions to a writer.</summary>
    /// <param name="cpu">CPU to step.</param>
    /// <param name="bus">Address bus for instruction fetches.</param>
    /// <param name="writer">Destination writer.</param>
    /// <param name="count">Instruction count.</param>
    public static void Trace(Arm60 cpu, IAddressBus bus, TextWriter writer, int count)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        for (var i = 0; i < count; i++)
        {
            CpuTraceEntry entry = Step(cpu, bus);
            writer.WriteLine(
                $"{entry.Pc:x8} {entry.Cpsr:x8} {entry.Instruction:x8} {entry.Disassembly}");
        }
    }

    /// <summary>Captures and executes one instruction.</summary>
    /// <param name="cpu">CPU to step.</param>
    /// <param name="bus">Address bus for instruction fetches.</param>
    /// <returns>The trace entry for the executed instruction.</returns>
    public static CpuTraceEntry Step(Arm60 cpu, IAddressBus bus)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(bus);

        var pc = cpu.ProgramCounter;
        var instruction = bus.Read32(pc);
        var entry = new CpuTraceEntry(pc, cpu.Cpsr, instruction, ArmDisassembler.Disassemble(instruction, pc));
        _ = cpu.Step();
        return entry;
    }
}

/// <summary>
/// Dumps raw emulated memory to a file for offline inspection.
/// </summary>
public static class MemoryDumper
{
    /// <summary>Writes a range of raw RAM/VRAM bytes.</summary>
    /// <param name="ram">Raw RAM accessor.</param>
    /// <param name="offset">Byte offset into the allocation.</param>
    /// <param name="length">Number of bytes.</param>
    /// <param name="path">Destination path.</param>
    public static void WriteRam(IRam ram, uint offset, int length, string path)
    {
        ArgumentNullException.ThrowIfNull(ram);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = ram.Read8(offset + (uint)i);
        }

        WriteBytes(data, path);
    }

    /// <summary>Writes an arbitrary byte buffer to a file.</summary>
    /// <param name="data">Bytes to write.</param>
    /// <param name="path">Destination path.</param>
    public static void WriteBytes(ReadOnlySpan<byte> data, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, data.ToArray());
    }
}
