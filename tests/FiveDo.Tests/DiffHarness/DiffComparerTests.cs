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
            WriteManifest(manifest),
            [match, allowed, diverged],
            provenance);

        Assert.Equal(3, report.Summary.Titles);
        Assert.Equal(1, report.Summary.Matched);
        Assert.Equal(1, report.Summary.Allowed);
        Assert.Equal(1, report.Summary.Diverged);
        Assert.False(report.Summary.Passed);
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

    private string WriteManifest(DiffManifest manifest)
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
