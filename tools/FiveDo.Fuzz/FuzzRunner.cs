using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FiveDo.Fuzz;

/// <summary>Runs the selected fuzz targets and aggregates their results.</summary>
internal static class FuzzRunner
{
    /// <summary>Runs a fuzz session.</summary>
    /// <param name="options">Run options.</param>
    /// <returns>The aggregated report.</returns>
    internal static FuzzReport Run(FuzzOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var targetReports = new List<FuzzTargetReport>();
        var targetNames = new List<string>();
        var totalIterations = 0;
        var totalTyped = 0;
        var totalUnexpected = 0;

        foreach (FuzzTargetKind kind in FuzzOptions.EnumerateTargets(options.Targets))
        {
            int seed = unchecked((options.Seed * 397) ^ (int)kind);
            targetNames.Add(kind.ToString());
            FuzzTargetReport report = RunTarget(kind, options, seed);
            targetReports.Add(report);
            totalIterations += report.Iterations;
            totalTyped += report.TypedFailures;
            totalUnexpected += report.UnexpectedFailures;
        }

        stopwatch.Stop();
        return new FuzzReport(
            started,
            stopwatch.Elapsed.TotalSeconds,
            options.Seed,
            targetNames,
            totalIterations,
            totalTyped,
            totalUnexpected,
            targetReports);
    }

    private static FuzzTargetReport RunTarget(FuzzTargetKind kind, FuzzOptions options, int seed)
    {
        IFuzzTarget target = FuzzTargetFactory.Create(kind);
        var failures = new List<FuzzFailure>();
        var random = new Random(seed);
        var iterations = 0;
        var typed = 0;
        var unexpected = 0;
        var deadline = options.Seconds > 0
            ? DateTimeOffset.UtcNow + TimeSpan.FromSeconds(options.Seconds)
            : DateTimeOffset.MaxValue;

        using var context = new FuzzContext(random);
        while (options.Iterations == 0 || iterations < options.Iterations)
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                break;
            }

            FuzzCase fuzzCase;
            try
            {
                fuzzCase = target.CreateCase(context, iterations);
            }
            catch (Exception exception)
            {
                // Case generation is our own code. A failure here is always a harness defect,
                // but report it instead of aborting the remaining targets.
                unexpected++;
                failures.Add(new FuzzFailure(
                    target.Name,
                    iterations,
                    exception.GetType().FullName ?? exception.GetType().Name,
                    exception.Message,
                    string.Empty));
                iterations++;
                continue;
            }

            try
            {
                target.Execute(fuzzCase, context);
            }
            catch (Exception exception) when (FuzzFailureClassifier.IsTypedFailure(exception))
            {
                typed++;
            }
            catch (Exception exception)
            {
                unexpected++;
                var reproduction = ReproWriter.Write(
                    options.OutputDirectory,
                    target.Name,
                    seed,
                    iterations,
                    fuzzCase,
                    exception);
                failures.Add(new FuzzFailure(
                    target.Name,
                    iterations,
                    exception.GetType().FullName ?? exception.GetType().Name,
                    exception.Message,
                    reproduction));
            }

            iterations++;
        }

        return new FuzzTargetReport(target.Name, iterations, typed, unexpected, failures);
    }
}
