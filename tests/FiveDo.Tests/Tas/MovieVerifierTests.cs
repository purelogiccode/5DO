using System.Collections.Generic;
using FiveDo.Tas;

namespace FiveDo.Tests.Tas;

public sealed class MovieVerifierTests
{
    [Fact]
    public void Verify_PassesWhenEveryCheckpointMatches()
    {
        var header = new MovieHeader
        {
            FrameCount = 60,
            FinalFrameHash = 0x12345678,
            FinalProgramCounter = 0x00200000,
            FinalStateHash = 0x9ABCDEF0,
        };

        MovieVerification result = MovieVerifier.Verify(header, 60, 0x12345678, 0x00200000, 0x9ABCDEF0);

        Assert.True(result.Ok);
        Assert.Empty(result.Failures);
        Assert.Equal("movie verified", result.Summary);
    }

    [Fact]
    public void Verify_ReportsEveryMismatch()
    {
        var header = new MovieHeader
        {
            FrameCount = 60,
            FinalFrameHash = 0x12345678,
            FinalProgramCounter = 0x00200000,
            FinalStateHash = 0x9ABCDEF0,
        };

        MovieVerification result = MovieVerifier.Verify(header, 59, 0x11111111, 0x00200004, 0x22222222);

        Assert.False(result.Ok);
        Assert.Equal(4, result.Failures.Count);
        Assert.Contains(result.Failures, failure => failure.Contains("frames", System.StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("frame", System.StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("pc", System.StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("state", System.StringComparison.Ordinal));
        Assert.StartsWith("movie mismatch:", result.Summary, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithoutRecordedStateHashDoesNotRequireOne()
    {
        var header = new MovieHeader
        {
            FrameCount = 1,
            FinalFrameHash = 1,
            FinalProgramCounter = 2,
        };

        Assert.True(MovieVerifier.Verify(header, 1, 1, 2).Ok);
    }

    [Fact]
    public void Verify_WithRecordedStateHashRequiresAComputedHash()
    {
        var header = new MovieHeader
        {
            FrameCount = 1,
            FinalStateHash = 5,
        };

        MovieVerification result = MovieVerifier.Verify(header, 1, 0, 0);

        Assert.False(result.Ok);
        Assert.Contains(result.Failures, failure => failure.Contains("not computed", System.StringComparison.Ordinal));
    }

    [Fact]
    public void CompareOptions_ReportsMissingAndChangedValues()
    {
        var header = new MovieHeader
        {
            Options = new Dictionary<string, string>(System.StringComparer.Ordinal)
            {
                ["armTiming"] = "Arm60Datasheet",
                ["cdSpeed"] = "X8",
                ["celTiming"] = "OperaBusy",
            },
        };
        var current = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["armTiming"] = "Arm60Datasheet",
            ["cdSpeed"] = "X2",
        };

        IReadOnlyList<string> differences = MovieVerifier.CompareOptions(header, current);

        Assert.Equal(2, differences.Count);
        Assert.Contains(differences, difference => difference.Contains("cdSpeed", System.StringComparison.Ordinal));
        Assert.Contains(differences, difference => difference.Contains("celTiming", System.StringComparison.Ordinal));
    }

    [Fact]
    public void CompareOptions_WithoutRecordedOptionsIsEmpty()
    {
        Assert.Empty(MovieVerifier.CompareOptions(
            new MovieHeader(),
            new Dictionary<string, string>(System.StringComparer.Ordinal)));
    }
}
