using System.Runtime.InteropServices;

namespace FiveDo.Clio;

/// <summary>
/// One row of the CLIO timer event ledger.
/// </summary>
/// <param name="Timer">Timer index, 0-15.</param>
/// <param name="Executions">Timer decrements performed since the ledger was reset.</param>
/// <param name="Underflows">Counter wraps (zero to reload or stop) observed for this timer.</param>
/// <param name="Reloads">Underflows that reloaded from the <c>+4</c> reload register.</param>
/// <param name="Stops">Underflows that cleared <c>DECREMENT</c> and left the counter at <c>0xFFFF</c>.</param>
/// <param name="Fiqs">Odd-timer FIQs raised by underflows.</param>
/// <remarks>
/// The ledger is diagnostic telemetry used to compare the FreeDO all-timer scan with
/// Opera's per-slot scheduler. It is not serialized and is only collected when
/// <see cref="Clio.TimerTelemetryEnabled"/> is set.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ClioTimerUsage(
    int Timer,
    long Executions,
    long Underflows,
    long Reloads,
    long Stops,
    long Fiqs);
