using System;
using System.IO;
using FiveDo.DiffHarness;

namespace FiveDo.Tests.DiffHarness;

public sealed class DiffReportWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "fivedo-diff-report-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void BuildMarkdown_IncludesStatusAndDivergence()
    {
        DiffReport report = Report();

        var markdown = DiffReportWriter.BuildMarkdown(report);

        Assert.Contains("5DO Differential Report - unit", markdown, StringComparison.Ordinal);
        Assert.Contains("Diverged", markdown, StringComparison.Ordinal);
        Assert.Contains("0x00000002", markdown, StringComparison.Ordinal);
        Assert.Contains("test reason", markdown, StringComparison.Ordinal);
        Assert.Contains("ARM override: `arm60`", markdown, StringComparison.Ordinal);
        Assert.Contains("CLIO override: `freedo`", markdown, StringComparison.Ordinal);
        Assert.Contains("CEL override: `opera`", markdown, StringComparison.Ordinal);
        Assert.Contains("CD override: `opera`", markdown, StringComparison.Ordinal);
        Assert.Contains("Profile override: `accuracy`", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMarkdown_ReportsAudioAlignmentSummary()
    {
        DiffReport report = Report();
        report = new DiffReport
        {
            ManifestName = report.ManifestName,
            ManifestPath = report.ManifestPath,
            ManifestHash = report.ManifestHash,
            GeneratedUtc = report.GeneratedUtc,
            Provenance = report.Provenance,
            Titles = report.Titles,
            Summary = new DiffSummary
            {
                Titles = 1,
                Checkpoints = 1,
                Fields = 1,
                AudioFields = 3,
                AudioComparedTitles = 1,
                AudioAlignedTitles = 1,
            },
        };

        var markdown = DiffReportWriter.BuildMarkdown(report);
        var html = DiffReportWriter.BuildHtml(report);

        Assert.Contains(
            "| Audio hashes aligned (all checkpoints) | 1 / 1 |",
            markdown,
            StringComparison.Ordinal);
        Assert.Contains("| Audio lag (sample count) | 0 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Audio content diverged | 0 |", markdown, StringComparison.Ordinal);
        Assert.Contains("Audio aligned", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMarkdown_OmitsAudioRowsWhenAudioIsNotCompared()
    {
        var markdown = DiffReportWriter.BuildMarkdown(Report());

        Assert.DoesNotContain("Audio hashes aligned", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtml_EscapesNames()
    {
        DiffReport report = Report("<script>alert(1)</script>");

        var html = DiffReportWriter.BuildHtml(report);

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteAll_CreatesEveryFormat()
    {
        DiffReport report = Report();
        Directory.CreateDirectory(_directory);

        var json = DiffReportWriter.WriteAll(_directory, report, "report");

        Assert.True(File.Exists(json));
        Assert.True(File.Exists(Path.Combine(_directory, "report.md")));
        Assert.True(File.Exists(Path.Combine(_directory, "report.html")));
    }

    private static DiffReport Report(string titleName = "Title")
    {
        return new DiffReport
        {
            ManifestName = "unit",
            ManifestPath = "manifest.json",
            ManifestHash = "hash",
            GeneratedUtc = "2026-10-31T00:00:00Z",
            Provenance = new DiffProvenance
            {
                GitCommit = "abc",
                CliPath = "cli",
                OperaCorePath = "core",
                OperaCoreMd5 = "md5",
                ArmTimingOverride = "arm60",
                ClioTimersOverride = "freedo",
                CelTimingOverride = "opera",
                CdTimingOverride = "opera",
                ProfileOverride = "accuracy",
                Host = "test",
                Runtime = "test",
            },
            Titles =
            [
                new DiffTitleResult
                {
                    Id = "one",
                    Name = titleName,
                    Disc = "disc",
                    Status = "Diverged",
                    Frames = 100,
                    DivergedCount = 1,
                    Checkpoints =
                    [
                        new DiffCheckpointResult
                        {
                            Frame = 100,
                            Match = false,
                            Fields =
                            [
                                new DiffFieldResult
                                {
                                    Field = "frameHash",
                                    Match = false,
                                    FiveDo = "0x00000001",
                                    Opera = "0x00000002",
                                    AllowedReason = "test reason",
                                    Issue = "CORE-01",
                                },
                            ],
                        },
                    ],
                },
            ],
            Summary = new DiffSummary
            {
                Titles = 1,
                Diverged = 1,
                Checkpoints = 1,
                Fields = 1,
                DivergedFields = 1,
            },
        };
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
