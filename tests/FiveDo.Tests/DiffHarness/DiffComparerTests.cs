using System;
using System.IO;
using System.Linq;
using FiveDo.DiffHarness;

namespace FiveDo.Tests.DiffHarness;

public sealed class DiffComparerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "fivedo-diff-compare-" + Guid.NewGuid().ToString("N"));

    public DiffComparerTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Compare_EqualFrameHashes_ReturnsMatch()
    {
        DiffManifest manifest = Manifest();
        DiffTitle title = Title();
        DiffTitleResult result = DiffComparer.Compare(
            manifest, title, Capture(0x12345678), Capture(0x12345678), 100);

        Assert.Equal("Match", result.Status);
        Assert.Equal(0, result.DivergedCount);
        Assert.True(result.Checkpoints[0].Match);
    }

    [Fact]
    public void Compare_DifferentFrameHashes_ReturnsDiverged()
    {
        DiffManifest manifest = Manifest();
        DiffTitle title = Title();
        DiffTitleResult result = DiffComparer.Compare(
            manifest, title, Capture(0x12345678), Capture(0x87654321), 100);

        Assert.Equal("Diverged", result.Status);
        Assert.Equal(1, result.DivergedCount);
        DiffFieldResult field = Assert.Single(result.Checkpoints[0].Fields);
        Assert.False(field.Match);
        Assert.Null(field.AllowedReason);
    }

    [Fact]
    public void Compare_ExactAllowance_ReturnsAllowed()
    {
        DiffManifest manifest = Manifest();
        DiffTitle title = Title("allowlist", new DiffAllowance
        {
            Field = "frameHash",
            Checkpoint = 100,
            Reason = "known boot timing",
            Issue = "CORE-04",
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest, title, Capture(0x12345678), Capture(0x87654321), 100);

        Assert.Equal("Allowed", result.Status);
        Assert.Equal(1, result.AllowedCount);
        DiffFieldResult field = Assert.Single(result.Checkpoints[0].Fields);
        Assert.Equal("known boot timing", field.AllowedReason);
        Assert.Equal("CORE-04", field.Issue);
    }

    [Fact]
    public void Compare_RangeAllowance_ReturnsAllowed()
    {
        DiffManifest manifest = Manifest();
        DiffTitle title = Title("allowlist", new DiffAllowance
        {
            Field = "frameHash",
            From = 50,
            To = 150,
            Reason = "known boot timing",
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest, title, Capture(0x12345678), Capture(0x87654321), 100);

        Assert.Equal("Allowed", result.Status);
    }

    [Fact]
    public void Compare_MissingSample_IsDivergedAndMarkedMissing()
    {
        DiffManifest manifest = Manifest();
        DiffTitle title = Title();
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            title,
            Capture(0x12345678),
            new CaptureResult
            {
                Engine = "opera",
                Ok = true,
                Checkpoints = [],
            },
            100);

        Assert.Equal("Diverged", result.Status);
        DiffFieldResult field = Assert.Single(result.Checkpoints[0].Fields);
        Assert.False(field.Match);
        Assert.Equal("missing sample", field.Issue);
    }

    [Fact]
    public void Compare_OffsetHash_IsReportedAsOffset()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            FrameWindow = 1,
        });
        DiffTitle title = Title();
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            title,
            CaptureMany((99, 6), (100, 7)),
            CaptureMany((99, 7), (100, 9)),
            100);

        Assert.Equal("Offset", result.Status);
        Assert.Equal(1, result.OffsetCount);
        DiffFieldResult field = Assert.Single(result.Checkpoints[0].Fields);
        Assert.Contains("matches Opera frame 99", field.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_PcMismatch_IsGatedWhenEnabled()
    {
        DiffManifest manifest = Manifest();
        DiffTitle title = Title();
        CaptureResult fiveDo = CaptureWithPc(0x1000);
        CaptureResult opera = CaptureWithPc(0x1004);

        DiffTitleResult strict = DiffComparer.Compare(
            manifest, title, fiveDo, opera, 100, comparePc: true);
        DiffTitleResult tolerant = DiffComparer.Compare(
            manifest, title, fiveDo, opera, 100, comparePc: true, pcTolerance: 4);

        Assert.Equal("Diverged", strict.Status);
        Assert.Equal(4L, strict.MaxPcDelta);
        Assert.Equal("Match", tolerant.Status);
    }

    [Fact]
    public void Compare_PcMismatchWithMatchingFrame_ReportsNoBehavioralDivergence()
    {
        DiffManifest manifest = Manifest();
        DiffTitle title = Title();
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            title,
            CaptureWithPc(0x1000),
            CaptureWithPc(0x1004),
            100,
            comparePc: true);

        Assert.Equal("Diverged", result.Status);
        Assert.Equal("pc", result.FirstDivergence?.Field);
        Assert.Null(result.FirstBehavioralDivergence);
    }

    [Fact]
    public void Compare_PcMismatchWithInstructionAlignedNeighbor_ReportsPhaseDiagnostic()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            ComparePc = true,
            FrameWindow = 1,
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            Title(),
            CaptureCpuSeries((99, 0x1000, 900_000), (100, 0x1000, 1_000_000)),
            CaptureCpuSeries((99, 0x1000, 1_000_000), (100, 0x2000, 1_090_000)),
            100);

        DiffFieldResult pc = result.Checkpoints[0].Fields
            .Single(field => string.Equals(field.Field, "pc", StringComparison.Ordinal));
        Assert.False(pc.Match);
        Assert.Contains("instruction phase", pc.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("Opera frame 99", pc.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("PC matches", pc.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(99, result.PcPhaseFrame);
        Assert.Equal(0, result.PcPhaseInstructionDelta);
    }

    [Fact]
    public void Compare_InstructionPhasePrefersTheSmallestAbsoluteDelta()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            ComparePc = true,
            FrameWindow = 1,
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            Title(),
            CaptureCpuSeries((99, 0x1000, 900_000), (100, 0x1000, 1_000_000)),
            CaptureCpuSeries(
                (99, 0x2000, 1_001_000),
                (100, 0x2000, 1_090_000),
                (101, 0x1000, 999_900)),
            100);

        Assert.Equal(101, result.PcPhaseFrame);
        Assert.Equal(-100, result.PcPhaseInstructionDelta);
    }

    [Fact]
    public void Compare_PcMismatchWithoutInstructionAlignment_HasNoPhaseDiagnostic()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            ComparePc = true,
            FrameWindow = 1,
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            Title(),
            CaptureCpuSeries((99, 0x1000, 900_000), (100, 0x1000, 1_000_000)),
            CaptureCpuSeries((99, 0x1000, 950_000), (100, 0x2000, 1_010_000)),
            100);

        DiffFieldResult pc = result.Checkpoints[0].Fields
            .Single(field => string.Equals(field.Field, "pc", StringComparison.Ordinal));
        Assert.False(pc.Match);
        Assert.Null(pc.Diagnostic);
        Assert.Null(result.PcPhaseFrame);
    }

    [Fact]
    public void Compare_CpsrMismatch_IsGatedWhenEnabled()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            CompareCpsr = true,
        });
        DiffTitle title = Title();
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            title,
            CaptureWithCpu(0x1000, 0x10),
            CaptureWithCpu(0x1000, 0x20),
            100);

        Assert.Equal("Diverged", result.Status);
        Assert.NotNull(result.FirstDivergence);
        Assert.Equal("cpsr", result.FirstDivergence!.Field);
        Assert.Equal(100, result.FirstDivergence.Frame);
    }

    [Fact]
    public void Compare_CdCommandMismatch_IsGatedWhenEnabled()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            CompareCd = true,
        });
        DiffTitle title = Title();
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            title,
            CaptureWithCd(4, 0x11111111),
            CaptureWithCd(4, 0x22222222),
            100);

        Assert.Equal("Diverged", result.Status);
        Assert.NotNull(result.FirstDivergence);
        Assert.Equal("cdCommandHash", result.FirstDivergence!.Field);
    }

    [Fact]
    public void Compare_FirstDivergence_ReportsEarliestUnexplainedCheckpoint()
    {
        DiffManifest manifest = new()
        {
            Name = "unit",
            Defaults = new DiffDefaults
            {
                Frames = 200,
                Checkpoints = [100, 200],
                Compare = new DiffCompareOptions { CompareSamples = false },
            },
            Titles = [Title()],
        };
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            Title(),
            CaptureMany((100, 1), (200, 2)),
            CaptureMany((100, 1), (200, 3)),
            200);

        Assert.Equal("Diverged", result.Status);
        Assert.NotNull(result.FirstDivergence);
        Assert.Equal(200, result.FirstDivergence!.Frame);
        Assert.Equal("frameHash", result.FirstDivergence.Field);
        Assert.Equal("0x00000002", result.FirstDivergence.FiveDo);
        Assert.Equal("0x00000003", result.FirstDivergence.Opera);
    }

    [Fact]
    public void Compare_SampleCountWithinTolerance_Matches()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = true,
            SampleTolerance = 64,
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            Title(),
            CaptureWithSamples(10_000),
            CaptureWithSamples(10_032),
            100);

        Assert.Equal("Match", result.Status);
        Assert.Null(result.FirstDivergence);
    }

    [Fact]
    public void Compare_SampleCountOutsideTolerance_DivergesWithDelta()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = true,
            SampleTolerance = 16,
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            Title(),
            CaptureWithSamples(10_000),
            CaptureWithSamples(10_032),
            100);

        Assert.Equal("Diverged", result.Status);
        Assert.NotNull(result.FirstDivergence);
        Assert.Equal("samples", result.FirstDivergence!.Field);
        Assert.Contains("sample delta 32", result.FirstDivergence.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_CdCommandSequence_ReportsFirstDivergentCommand()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            CompareCd = true,
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            Title(),
            CaptureWithCdCommands([0x83, 0x85], 2, 0x11111111),
            CaptureWithCdCommands([0x83, 0x86], 2, 0x11111111),
            100);

        Assert.Equal("Diverged", result.Status);
        Assert.NotNull(result.FirstDivergence);
        Assert.Equal("cdCommands", result.FirstDivergence!.Field);
        Assert.Contains("command 1", result.FirstDivergence.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("0x85", result.FirstDivergence.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("0x86", result.FirstDivergence.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_CdCommandSequence_ReportsPrefixDifference()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            CompareCd = true,
        });
        DiffTitleResult result = DiffComparer.Compare(
            manifest,
            Title(),
            CaptureWithCdCommands([0x83], 1, 0x11111111),
            CaptureWithCdCommands([0x83, 0x85], 2, 0x11111111),
            100);

        DiffFieldResult field = result.Checkpoints[0].Fields
            .Single(item => string.Equals(item.Field, "cdCommands", StringComparison.Ordinal));
        Assert.False(field.Match);
        Assert.Contains("prefixes match", field.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("5DO has 1", field.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("Opera has 2", field.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildReport_AggregatesStatuses()
    {
        DiffManifest manifest = Manifest();
        DiffProvenance provenance = new() { Host = "test", Runtime = "test" };
        DiffTitleResult match = DiffComparer.Compare(
            manifest, Title("a"), Capture(1), Capture(1), 100);
        DiffTitleResult allowed = DiffComparer.Compare(
            manifest,
            Title("b", new DiffAllowance { Field = "frameHash", Checkpoint = 100, Reason = "known" }),
            Capture(1),
            Capture(2),
            100);
        DiffTitleResult diverged = DiffComparer.Compare(
            manifest, Title("c"), Capture(1), Capture(2), 100);

        DiffReport report = DiffComparer.BuildReport(
            manifest,
            WriteManifest(),
            [match, allowed, diverged],
            provenance);

        Assert.Equal(3, report.Summary.Titles);
        Assert.Equal(1, report.Summary.Matched);
        Assert.Equal(1, report.Summary.Allowed);
        Assert.Equal(1, report.Summary.Diverged);
        Assert.False(report.Summary.Passed);
    }

    [Fact]
    public void BuildReport_CountsInstructionPhaseTitles()
    {
        DiffManifest manifest = Manifest(new DiffCompareOptions
        {
            CompareSamples = false,
            ComparePc = true,
            FrameWindow = 1,
        });
        DiffTitleResult phased = DiffComparer.Compare(
            manifest,
            Title("phased"),
            CaptureCpuSeries((99, 0x1000, 900_000), (100, 0x1000, 1_000_000)),
            CaptureCpuSeries((99, 0x1000, 1_000_000), (100, 0x2000, 1_090_000)),
            100);
        DiffTitleResult unaligned = DiffComparer.Compare(
            manifest,
            Title("unaligned"),
            CaptureCpuSeries((99, 0x1000, 900_000), (100, 0x1000, 1_000_000)),
            CaptureCpuSeries((99, 0x1000, 950_000), (100, 0x2000, 1_010_000)),
            100);

        DiffReport report = DiffComparer.BuildReport(
            manifest,
            WriteManifest(),
            [phased, unaligned],
            new DiffProvenance { Host = "test", Runtime = "test" });

        Assert.Equal(1, report.Summary.PcPhaseTitles);
        Assert.Equal(2, report.Summary.Diverged);
    }

    private static DiffManifest Manifest(DiffCompareOptions? compare = null)
    {
        return new DiffManifest
        {
            Name = "unit",
            Defaults = new DiffDefaults
            {
                Frames = 100,
                Compare = compare ?? new DiffCompareOptions { CompareSamples = false },
            },
            Titles = [Title()],
        };
    }

    private static DiffTitle Title(string id = "one", params DiffAllowance[] allowances)
    {
        return new DiffTitle
        {
            Id = id,
            Name = id,
            Disc = "disc.chd",
            Allow = allowances,
        };
    }

    private static CaptureResult Capture(uint hash)
    {
        return new CaptureResult
        {
            Engine = "capture",
            Ok = true,
            Checkpoints =
            [
                new CaptureSample { Frame = 100, FrameHash = hash, Samples = 100 },
            ],
        };
    }

    private static CaptureResult CaptureMany(params (long Frame, uint Hash)[] samples)
    {
        return new CaptureResult
        {
            Engine = "capture",
            Ok = true,
            Checkpoints = samples
                .Select(sample => new CaptureSample
                {
                    Frame = sample.Frame,
                    FrameHash = sample.Hash,
                    Samples = sample.Frame * 100,
                })
                .ToArray(),
        };
    }

    private static CaptureResult CaptureWithPc(uint pc)
    {
        return new CaptureResult
        {
            Engine = "capture",
            Ok = true,
            Checkpoints =
            [
                new CaptureSample { Frame = 100, FrameHash = 1, Samples = 100, Pc = pc },
            ],
        };
    }

    private static CaptureResult CaptureWithCpu(uint pc, uint cpsr)
    {
        return new CaptureResult
        {
            Engine = "capture",
            Ok = true,
            Checkpoints =
            [
                new CaptureSample
                {
                    Frame = 100,
                    FrameHash = 1,
                    Samples = 100,
                    Pc = pc,
                    Cpsr = cpsr,
                },
            ],
        };
    }

    private static CaptureResult CaptureCpuSeries(
        params (long Frame, uint Pc, long Instructions)[] samples)
    {
        return new CaptureResult
        {
            Engine = "capture",
            Ok = true,
            Checkpoints = samples
                .Select(sample => new CaptureSample
                {
                    Frame = sample.Frame,
                    FrameHash = 1,
                    Samples = sample.Frame * 100,
                    Pc = sample.Pc,
                    Instructions = sample.Instructions,
                })
                .ToArray(),
        };
    }

    private static CaptureResult CaptureWithCdCommands(byte[] commands, int count, uint hash)
    {
        return new CaptureResult
        {
            Engine = "capture",
            Ok = true,
            CdCommands = commands,
            Checkpoints =
            [
                new CaptureSample
                {
                    Frame = 100,
                    FrameHash = 1,
                    Samples = 100,
                    CdCommandCount = count,
                    CdCommandHash = hash,
                },
            ],
        };
    }

    private static CaptureResult CaptureWithSamples(long samples)
    {
        return new CaptureResult
        {
            Engine = "capture",
            Ok = true,
            Checkpoints =
            [
                new CaptureSample { Frame = 100, FrameHash = 1, Samples = samples },
            ],
        };
    }

    private static CaptureResult CaptureWithCd(int count, uint hash)
    {
        return new CaptureResult
        {
            Engine = "capture",
            Ok = true,
            Checkpoints =
            [
                new CaptureSample
                {
                    Frame = 100,
                    FrameHash = 1,
                    Samples = 100,
                    CdCommandCount = count,
                    CdCommandHash = hash,
                },
            ],
        };
    }

    private string WriteManifest()
    {
        var path = Path.Combine(_directory, "manifest.json");
        File.WriteAllText(path, "{}");
        return path;
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
