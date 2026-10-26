using System;
using ThreeDo.Diagnostics;

namespace ThreeDo.Tests.Diagnostics;

public sealed class BenchmarkComparisonTests
{
    [Fact]
    public void Evaluate_AcceptsAResultInsideTheThreshold()
    {
        var failures = BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 1.5,
            currentTotalMeanMs: 1.8,
            currentTotalP99Ms: 4.0,
            baselineCoreP99Ms: 1.4,
            baselineTotalMeanMs: 1.7,
            baselineTotalP99Ms: 3.8);

        Assert.Empty(failures);
    }

    [Fact]
    public void Evaluate_AcceptsAnImprovement()
    {
        var failures = BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 1.0,
            currentTotalMeanMs: 1.1,
            currentTotalP99Ms: 2.0,
            baselineCoreP99Ms: 2.0,
            baselineTotalMeanMs: 2.0,
            baselineTotalP99Ms: 4.0);

        Assert.Empty(failures);
    }

    [Fact]
    public void Evaluate_RejectsACoreRegression()
    {
        var failures = BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 1.7,
            currentTotalMeanMs: 1.8,
            currentTotalP99Ms: 4.0,
            baselineCoreP99Ms: 1.5,
            baselineTotalMeanMs: 1.8,
            baselineTotalP99Ms: 4.0);

        Assert.Single(failures);
        Assert.Contains("core p99", failures[0], StringComparison.Ordinal);
        Assert.Contains("baseline 1.500", failures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_RejectsATotalMeanRegression()
    {
        var failures = BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 1.5,
            currentTotalMeanMs: 2.1,
            currentTotalP99Ms: 4.0,
            baselineCoreP99Ms: 1.5,
            baselineTotalMeanMs: 1.8,
            baselineTotalP99Ms: 4.0);

        Assert.Single(failures);
        Assert.Contains("total mean", failures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_RejectsATotalP99Regression()
    {
        var failures = BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 1.5,
            currentTotalMeanMs: 1.8,
            currentTotalP99Ms: 5.0,
            baselineCoreP99Ms: 1.5,
            baselineTotalMeanMs: 1.8,
            baselineTotalP99Ms: 4.0);

        Assert.Single(failures);
        Assert.Contains("total p99", failures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_ReportsEveryRegressionAtOnce()
    {
        var failures = BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 3.0,
            currentTotalMeanMs: 3.0,
            currentTotalP99Ms: 6.0,
            baselineCoreP99Ms: 1.0,
            baselineTotalMeanMs: 1.0,
            baselineTotalP99Ms: 2.0);

        Assert.Equal(3, failures.Count);
    }

    [Fact]
    public void Evaluate_IgnoresMissingBaselineMetrics()
    {
        var failures = BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 3.0,
            currentTotalMeanMs: 3.0,
            currentTotalP99Ms: 6.0,
            baselineCoreP99Ms: 0,
            baselineTotalMeanMs: double.NaN,
            baselineTotalP99Ms: -1);

        Assert.Empty(failures);
    }

    [Fact]
    public void Evaluate_HonorsACustomThreshold()
    {
        var failures = BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 2.15,
            currentTotalMeanMs: 1.0,
            currentTotalP99Ms: 1.0,
            baselineCoreP99Ms: 2.0,
            baselineTotalMeanMs: 1.0,
            baselineTotalP99Ms: 1.0,
            threshold: 0.05);

        Assert.Single(failures);
        Assert.Contains("core p99", failures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_RejectsANonPositiveThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BenchmarkComparison.Evaluate(
            currentCoreP99Ms: 1.0,
            currentTotalMeanMs: 1.0,
            currentTotalP99Ms: 1.0,
            baselineCoreP99Ms: 1.0,
            baselineTotalMeanMs: 1.0,
            baselineTotalP99Ms: 1.0,
            threshold: 0));
    }

    [Fact]
    public void Description_NamesTheRelativeComparison()
    {
        Assert.Contains("baseline", BenchmarkComparison.Description, StringComparison.Ordinal);
        Assert.Contains("10", BenchmarkComparison.Description, StringComparison.Ordinal);
    }
}
