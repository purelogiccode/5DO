namespace FiveDo.Cli;

/// <summary>
/// One row of the CLIO timer ledger reported by <c>run --json</c>.
/// </summary>
/// <param name="Timer">Timer index, 0-15.</param>
/// <param name="Executions">Timer decrements performed during the run.</param>
/// <param name="Underflows">Counter wraps observed for this timer.</param>
/// <param name="Reloads">Underflows that reloaded from the <c>+4</c> reload register.</param>
/// <param name="Stops">Underflows that cleared <c>DECREMENT</c> and left <c>0xFFFF</c>.</param>
/// <param name="Fiqs">Odd-timer FIQs raised by underflows.</param>
internal sealed record CliTimerStatResult(
    int Timer,
    long Executions,
    long Underflows,
    long Reloads,
    long Stops,
    long Fiqs);
