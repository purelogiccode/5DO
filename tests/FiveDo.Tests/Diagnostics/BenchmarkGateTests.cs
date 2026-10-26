using System;
using FiveDo.Diagnostics;

namespace FiveDo.Tests.Diagnostics;

public sealed class BenchmarkGateTests
{
    [Fact]
    public void Evaluate_AcceptsAFrameInsideEveryBudget()
    {
        var failures = BenchmarkGate.Evaluate(0, 1.5, 1.8, 4.0);

        Assert.Empty(failures);
    }

    [Fact]
    public void Evaluate_RejectsAllocations()
    {
        var failures = BenchmarkGate.Evaluate(128, 1.5, 1.8, 4.0);

        Assert.Single(failures);
        Assert.Contains("bytes/frame", failures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_RejectsACoreP99OverBudget()
    {
        var failures = BenchmarkGate.Evaluate(0, 8.1, 1.8, 4.0);

        Assert.Single(failures);
        Assert.Contains("core p99", failures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_RejectsATotalMeanOverBudget()
    {
        var failures = BenchmarkGate.Evaluate(0, 1.5, 8.1, 12.0);

        Assert.Single(failures);
        Assert.Contains("total mean", failures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_RejectsATotalP99OverBudget()
    {
        var failures = BenchmarkGate.Evaluate(0, 1.5, 1.8, 16.7);

        Assert.Single(failures);
        Assert.Contains("total p99", failures[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_ReportsEveryFailureAtOnce()
    {
        var failures = BenchmarkGate.Evaluate(64, 9.0, 9.0, 20.0);

        Assert.Equal(4, failures.Count);
    }

    [Fact]
    public void Description_NamesTheBudgets()
    {
        Assert.Contains("0 bytes/frame", BenchmarkGate.Description, StringComparison.Ordinal);
        Assert.Contains("core p99", BenchmarkGate.Description, StringComparison.Ordinal);
        Assert.Contains("16.667", BenchmarkGate.Description, StringComparison.Ordinal);
    }
}
