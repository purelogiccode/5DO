namespace FiveDo.Cli;

/// <summary>
/// DSP sample-loop timing counters reported by <c>run --json</c>.
/// </summary>
internal sealed record CliDspResult(
    long Instructions,
    long LoopCalls,
    long RunningLoops,
    long SleepingLoops,
    long MaxInstructionsPerLoop,
    double AverageInstructionsPerLoop);
