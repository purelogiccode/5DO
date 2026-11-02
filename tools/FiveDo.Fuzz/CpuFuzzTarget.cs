using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Cpu;
using FiveDo.Memory;

namespace FiveDo.Fuzz;

/// <summary>Executes random ARM60 instruction streams and checks bounded execution.</summary>
internal sealed class CpuFuzzTarget : IFuzzTarget
{
    private const uint ProgramAddress = 0x1000;

    /// <inheritdoc />
    public string Name => "cpu";

    /// <inheritdoc />
    public FuzzCase CreateCase(FuzzContext context, int iteration)
    {
        ArgumentNullException.ThrowIfNull(context);
        int instructionCount = Mutation.RandomLength(context.Random, 1, 64);
        var payload = new byte[8 + (instructionCount * 4)];
        context.Random.NextBytes(payload);
        payload[4] = (byte)instructionCount;
        return new FuzzCase($"random ARM60 stream ({instructionCount} instructions)", payload);
    }

    /// <inheritdoc />
    public void Execute(FuzzCase fuzzCase, FuzzContext context)
    {
        ArgumentNullException.ThrowIfNull(fuzzCase);
        ArgumentNullException.ThrowIfNull(context);
        if (fuzzCase.Payload.Length < 12)
        {
            throw new InvalidDataException("The CPU target needs a CPSR word and at least one instruction.");
        }

        var memory = new FiveDoMemory(new EmulatorTuning
        {
            ArmTiming = (fuzzCase.Payload[0] & 1) != 0
                ? ArmTimingMode.Arm60Datasheet
                : ArmTimingMode.FreeDoCompatible,
        });
        int instructionCount = Math.Min(fuzzCase.Payload[4], (fuzzCase.Payload.Length - 8) / 4);
        for (var index = 0; index < instructionCount; index++)
        {
            uint instruction = (uint)(
                fuzzCase.Payload[8 + (index * 4)]
                | (fuzzCase.Payload[9 + (index * 4)] << 8)
                | (fuzzCase.Payload[10 + (index * 4)] << 16)
                | (fuzzCase.Payload[11 + (index * 4)] << 24));
            memory.Ram.Write32(ProgramAddress + ((uint)index * 4), instruction);
        }

        var cpu = new Arm60(memory.Bus);
        cpu.ProgramCounter = ProgramAddress;
        cpu.Cpsr = (uint)(
            fuzzCase.Payload[1]
            | (fuzzCase.Payload[2] << 8)
            | (fuzzCase.Payload[3] << 16));
        for (var step = 0; step < instructionCount; step++)
        {
            _ = cpu.Step();
        }

        if (cpu.InstructionsExecuted != instructionCount)
        {
            throw new InvalidOperationException(
                $"The ARM60 executed {cpu.InstructionsExecuted} instructions instead of {instructionCount}.");
        }
    }
}
