using System;
using System.Collections.Generic;

namespace FiveDo.Fuzz;

/// <summary>A single unexpected failure with a reproducible input path.</summary>
/// <param name="Target">Target name.</param>
/// <param name="Iteration">Zero-based iteration within the target.</param>
/// <param name="ExceptionType">CLR exception type.</param>
/// <param name="Message">Exception message.</param>
/// <param name="ReproductionPath">Directory or file containing the input and failure text.</param>
internal sealed record FuzzFailure(
    string Target,
    int Iteration,
    string ExceptionType,
    string Message,
    string ReproductionPath);

/// <summary>Per-target fuzz counters and failures.</summary>
/// <param name="Target">Target name.</param>
/// <param name="Iterations">Iterations executed.</param>
/// <param name="TypedFailures">Malformed inputs rejected with a documented exception.</param>
/// <param name="UnexpectedFailures">Inputs that raised an undocumented exception.</param>
/// <param name="Failures">Unexpected failure details.</param>
internal sealed record FuzzTargetReport(
    string Target,
    int Iterations,
    int TypedFailures,
    int UnexpectedFailures,
    IReadOnlyList<FuzzFailure> Failures);

/// <summary>Result of one fuzz run.</summary>
/// <param name="Started">UTC start time.</param>
/// <param name="DurationSeconds">Elapsed seconds.</param>
/// <param name="Seed">Base seed.</param>
/// <param name="Targets">Selected target names.</param>
/// <param name="TotalIterations">Iterations across all targets.</param>
/// <param name="TypedFailures">Malformed inputs rejected with a documented exception.</param>
/// <param name="UnexpectedFailures">Inputs that raised an undocumented exception.</param>
/// <param name="TargetReports">Per-target results.</param>
internal sealed record FuzzReport(
    DateTimeOffset Started,
    double DurationSeconds,
    int Seed,
    IReadOnlyList<string> Targets,
    int TotalIterations,
    int TypedFailures,
    int UnexpectedFailures,
    IReadOnlyList<FuzzTargetReport> TargetReports);
