using System;
using System.Collections.Generic;

namespace FiveDo.Diagnostics;

/// <summary>
/// Compares a benchmark result with a previously recorded baseline on the same
/// machine. The default tolerance is 10%; improvements never fail the comparison.
/// </summary>
public static class BenchmarkComparison
{
    /// <summary>Default allowed relative regression (10%).</summary>
    public const double DefaultThreshold = 0.10;

    /// <summary>Compares the main frame-time metrics against a baseline.</summary>
    /// <param name="currentCoreP99Ms">Current core p99 frame time in milliseconds.</param>
    /// <param name="currentTotalMeanMs">Current mean total frame time in milliseconds.</param>
    /// <param name="currentTotalP99Ms">Current total p99 frame time in milliseconds.</param>
    /// <param name="baselineCoreP99Ms">Baseline core p99 frame time in milliseconds; non-positive values are ignored.</param>
    /// <param name="baselineTotalMeanMs">Baseline mean total frame time in milliseconds; non-positive values are ignored.</param>
    /// <param name="baselineTotalP99Ms">Baseline total p99 frame time in milliseconds; non-positive values are ignored.</param>
    /// <param name="threshold">Allowed relative regression, for example 0.10 for 10%.</param>
    /// <returns>An empty list when the result is within tolerance; otherwise one message per regression.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="threshold"/> is not positive.</exception>
    public static IReadOnlyList<string> Evaluate(
        double currentCoreP99Ms,
        double currentTotalMeanMs,
        double currentTotalP99Ms,
        double baselineCoreP99Ms,
        double baselineTotalMeanMs,
        double baselineTotalP99Ms,
        double threshold = DefaultThreshold)
    {
        if (threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                threshold,
                "The regression threshold must be positive.");
        }

        var failures = new List<string>();
        Compare("core p99", currentCoreP99Ms, baselineCoreP99Ms, threshold, failures);
        Compare("total mean", currentTotalMeanMs, baselineTotalMeanMs, threshold, failures);
        Compare("total p99", currentTotalP99Ms, baselineTotalP99Ms, threshold, failures);
        return failures;
    }

    /// <summary>Gets a human-readable description of the default comparison.</summary>
    public static string Description => FormattableString.Invariant(
        $"same-machine baseline, relative regression > {DefaultThreshold:P0}");

    private static void Compare(
        string name,
        double currentMs,
        double baselineMs,
        double threshold,
        List<string> failures)
    {
        if (baselineMs <= 0 || double.IsNaN(baselineMs) || double.IsInfinity(baselineMs))
        {
            return;
        }

        var limit = baselineMs * (1 + threshold);
        if (currentMs > limit)
        {
            var regression = (currentMs / baselineMs) - 1;
            failures.Add(FormattableString.Invariant(
                $"{name}: {currentMs:F3} ms vs baseline {baselineMs:F3} ms (+{regression:P1}, limit +{threshold:P0})"));
        }
    }
}
