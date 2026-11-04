using System.Runtime.InteropServices;

namespace FiveDo.Cpu;

/// <summary>
/// One row of the ARM instruction-class cycle ledger.
/// </summary>
/// <param name="InstructionClass">Decoded instruction class.</param>
/// <param name="Instructions">Instructions decoded in this class since the ledger was last cleared.</param>
/// <param name="Cycles">ARM clocks charged to this class since the ledger was last cleared.</param>
/// <remarks>
/// The ledger is diagnostic telemetry. It is not serialized, is not part of the
/// architectural state, and is only collected when
/// <see cref="Arm60.CycleClassTelemetryEnabled"/> is set.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ArmCycleClassUsage(
    ArmInstructionClass InstructionClass,
    long Instructions,
    long Cycles)
{
    /// <summary>Gets the average cycle cost of one instruction in this class.</summary>
    /// <remarks>Returns zero when no instruction of the class has been decoded.</remarks>
    public double CyclesPerInstruction => Instructions == 0 ? 0 : (double)Cycles / Instructions;
}
