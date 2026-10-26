using System;
using System.Collections.Generic;

namespace FiveDo.Diagnostics;

/// <summary>
/// The continuous performance gate used by the benchmark and CI. The budgets are
/// deliberately CPU-side and independent of output resolution: a 60 Hz frame has
/// 16.667 ms, and the emulator core must stay well below it.
/// </summary>
public static class BenchmarkGate
{
    /// <summary>Maximum allowed core p99 frame time in milliseconds.</summary>
    public const double CoreP99BudgetMs = 8.0;

    /// <summary>Maximum allowed mean total frame time in milliseconds.</summary>
    public const double TotalMeanBudgetMs = 8.0;

    /// <summary>Maximum allowed total p99 frame time in milliseconds.</summary>
    public const double TotalP99BudgetMs = 16.667;

    /// <summary>Evaluates one benchmark result against the performance budgets.</summary>
    /// <param name="bytesPerFrame">Managed bytes allocated per steady-state frame.</param>
    /// <param name="coreP99Ms">Core p99 frame time in milliseconds.</param>
    /// <param name="totalMeanMs">Mean total frame time in milliseconds.</param>
    /// <param name="totalP99Ms">Total p99 frame time in milliseconds.</param>
    /// <returns>An empty list when the result passes; otherwise one message per failure.</returns>
    public static IReadOnlyList<string> Evaluate(
        double bytesPerFrame,
        double coreP99Ms,
        double totalMeanMs,
        double totalP99Ms)
    {
        var failures = new List<string>();
        if (bytesPerFrame != 0)
        {
            failures.Add(FormattableString.Invariant(
                $"allocations: {bytesPerFrame} bytes/frame (expected 0)"));
        }

        if (coreP99Ms > CoreP99BudgetMs)
        {
            failures.Add(FormattableString.Invariant(
                $"core p99: {coreP99Ms:F3} ms (budget {CoreP99BudgetMs:F3} ms)"));
        }

        if (totalMeanMs > TotalMeanBudgetMs)
        {
            failures.Add(FormattableString.Invariant(
                $"total mean: {totalMeanMs:F3} ms/frame (budget {TotalMeanBudgetMs:F3} ms)"));
        }

        if (totalP99Ms > TotalP99BudgetMs)
        {
            failures.Add(FormattableString.Invariant(
                $"total p99: {totalP99Ms:F3} ms (budget {TotalP99BudgetMs:F3} ms)"));
        }

        return failures;
    }

    /// <summary>Gets a human-readable description of the budgets.</summary>
    public static string Description => FormattableString.Invariant(
        $"0 bytes/frame, core p99 <= {CoreP99BudgetMs:F0} ms, total mean <= {TotalMeanBudgetMs:F0} ms, total p99 <= {TotalP99BudgetMs:F3} ms");
}
