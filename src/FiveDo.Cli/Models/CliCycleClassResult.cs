namespace FiveDo.Cli;

/// <summary>
/// One instruction class in the ARM cycle-class ledger reported by <c>run --json</c>.
/// </summary>
/// <param name="InstructionClass">Decoded instruction class name.</param>
/// <param name="Instructions">Instructions decoded in this class.</param>
/// <param name="Cycles">ARM clocks charged to this class.</param>
/// <param name="CyclesPerInstruction">Average cycle cost of one instruction in this class.</param>
internal sealed record CliCycleClassResult(
    string InstructionClass,
    long Instructions,
    long Cycles,
    double CyclesPerInstruction);
