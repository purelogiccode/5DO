using System;
using System.IO;
using System.Linq;
using FiveDo.Fuzz;

namespace FiveDo.Tests.Storage;

public sealed class FuzzHarnessTests
{
    [Fact]
    public void AllTargets_RunCleanWithBoundedIterations()
    {
        var output = Path.Combine(Path.GetTempPath(), "fivedo-fuzz-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            FuzzOptions options = FuzzOptions.CreateForTesting(FuzzTargetKind.All, 20, 7, output);
            FuzzReport report = FuzzRunner.Run(options);

            Assert.Equal(0, report.UnexpectedFailures);
            Assert.Equal(6, report.TargetReports.Count);
            Assert.All(report.TargetReports, target => Assert.Equal(20, target.Iterations));
            Assert.Equal(120, report.TotalIterations);
            Assert.Equal(
                "State,Disc,Cue,OperaFileSystem,CdCommand,Cpu",
                string.Join(',', report.Targets));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public void Parse_ReadsTargetsBoundsAndSeed()
    {
        FuzzOptions options = FuzzOptions.Parse(
            ["--target", "state,cue,cd", "--iterations", "12", "--seed", "-7", "--json"]);

        Assert.Equal(FuzzTargetKind.State | FuzzTargetKind.Cue | FuzzTargetKind.CdCommand, options.Targets);
        Assert.Equal(12, options.Iterations);
        Assert.Equal(-7, options.Seed);
        Assert.True(options.Json);
        Assert.Equal(0, options.Seconds);
    }

    [Fact]
    public void Parse_DefaultsToAllTargetsAndThirtySeconds()
    {
        FuzzOptions options = FuzzOptions.Parse([]);

        Assert.Equal(FuzzTargetKind.All, options.Targets);
        Assert.Equal(0, options.Iterations);
        Assert.Equal(30, options.Seconds);
    }

    [Fact]
    public void Parse_RejectsUnknownTarget()
    {
        Assert.Throws<ArgumentException>(() => FuzzOptions.Parse(["--target", "nonsense"]));
    }

    [Fact]
    public void EnumerateTargets_UsesStableOrder()
    {
        Assert.Equal(
            [
                FuzzTargetKind.State,
                FuzzTargetKind.Disc,
                FuzzTargetKind.Cue,
                FuzzTargetKind.OperaFileSystem,
                FuzzTargetKind.CdCommand,
                FuzzTargetKind.Cpu,
            ],
            FuzzOptions.EnumerateTargets(FuzzTargetKind.All).ToArray());
    }
}
