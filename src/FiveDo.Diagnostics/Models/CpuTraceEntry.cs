using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Cpu;

namespace FiveDo.Diagnostics;


/// <summary>One captured CPU instruction.</summary>
/// <param name="Pc">Program counter before the instruction.</param>
/// <param name="Cpsr">Current program status register.</param>
/// <param name="Instruction">Raw instruction word.</param>
/// <param name="Disassembly">Readable disassembly.</param>
public readonly record struct CpuTraceEntry(uint Pc, uint Cpsr, uint Instruction, string Disassembly);
