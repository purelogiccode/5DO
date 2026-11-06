using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FiveDo.DiffHarness;

/// <summary>Compares 5DO and Opera captures and applies the manifest allowlist.</summary>
internal static class DiffComparer
{
    /// <summary>Compares one title.</summary>
    public static DiffTitleResult Compare(
        DiffManifest manifest,
        DiffTitle title,
        CaptureResult? fiveDo,
        CaptureResult? opera,
        int frames,
        bool comparePc = false,
        long? pcTolerance = null,
        bool compareCd = false,
        bool compareAudio = false,
        string? error = null)
    {
        if (error is not null || fiveDo is null || opera is null || !fiveDo.Ok || !opera.Ok)
        {
            var detail = error
                ?? fiveDo?.Error
                ?? opera?.Error
                ?? "capture missing";
            return new DiffTitleResult
            {
                Id = title.Id,
                Name = title.Name ?? title.Id,
                Disc = fiveDo?.Disc ?? title.Disc,
                Status = "Error",
                Frames = frames,
                Error = detail,
                FiveDo = fiveDo,
                Opera = opera,
            };
        }

        var compare = manifest.Defaults?.Compare ?? new DiffCompareOptions();
        var fiveDoSamples = fiveDo.Checkpoints.ToDictionary(sample => sample.Frame);
        var operaSamples = opera.Checkpoints.ToDictionary(sample => sample.Frame);
        var framesToCompare = DiffManifestLoader.ResolveCheckpoints(manifest, title);
        var checkpoints = new List<DiffCheckpointResult>(framesToCompare.Length);
        var allowedCount = 0;
        var divergedCount = 0;
        var offsetCount = 0;
        var nonOffsetDivergedCount = 0;

        foreach (var frame in framesToCompare)
        {
            fiveDoSamples.TryGetValue(frame, out var fiveDoSample);
            operaSamples.TryGetValue(frame, out var operaSample);
            var fields = new List<DiffFieldResult>();
            var frameHashMatches = fiveDoSample is not null
                && operaSample is not null
                && fiveDoSample.FrameHash == operaSample.FrameHash;
            var offset = frameHashMatches || fiveDoSample is null || operaSample is null
                ? null
                : FindOffset(fiveDoSamples, operaSamples, frame, compare.FrameWindow);
            var frameHashDiverged = AddField(
                fields,
                "frameHash",
                fiveDoSample is null ? null : HarnessCommon.Hex(fiveDoSample.FrameHash),
                operaSample is null ? null : HarnessCommon.Hex(operaSample.FrameHash),
                frameHashMatches,
                title, frame, ref allowedCount, ref divergedCount,
                fiveDoSample is null || operaSample is null,
                diagnostic: offset is null
                    ? null
                    : $"5DO frame {frame} matches Opera frame {offset.Value.Frame}");
            if (frameHashDiverged)
            {
                if (offset is not null)
                {
                    offsetCount++;
                }
                else
                {
                    nonOffsetDivergedCount++;
                }
            }

            if (compare.CompareSamples ?? true)
            {
                var samplesMatch = fiveDoSample is not null
                    && operaSample is not null
                    && Math.Abs(operaSample.Samples - fiveDoSample.Samples)
                        <= compare.SampleTolerance;
                if (AddField(
                    fields,
                    "samples",
                    fiveDoSample?.Samples.ToString(CultureInfo.InvariantCulture),
                    operaSample?.Samples.ToString(CultureInfo.InvariantCulture),
                    samplesMatch,
                    title, frame, ref allowedCount, ref divergedCount,
                    fiveDoSample is null || operaSample is null,
                    diagnostic: !samplesMatch
                        ? $"sample delta {operaSample?.Samples - fiveDoSample?.Samples}; "
                            + $"tolerance {compare.SampleTolerance}"
                        : null))
                {
                    nonOffsetDivergedCount++;
                }
            }

            if (compare.CompareAudio || compareAudio)
            {
                if (AddField(
                    fields,
                    "audioHash",
                    fiveDoSample?.AudioHash is { } fiveDoAudio ? HarnessCommon.Hex(fiveDoAudio) : null,
                    operaSample?.AudioHash is { } operaAudio ? HarnessCommon.Hex(operaAudio) : null,
                    fiveDoSample is not null
                        && operaSample is not null
                        && fiveDoSample.AudioHash == operaSample.AudioHash,
                    title, frame, ref allowedCount, ref divergedCount,
                    fiveDoSample is null || operaSample is null))
                {
                    nonOffsetDivergedCount++;
                }
            }

            if (compare.ComparePc || comparePc)
            {
                var tolerance = pcTolerance ?? compare.PcTolerance;
                var pcMatch = fiveDoSample?.Pc is { } left && operaSample?.Pc is { } right
                    && Math.Abs((long)right - left) <= tolerance;
                if (AddField(
                    fields,
                    "pc",
                    fiveDoSample?.Pc is { } fiveDoPc ? HarnessCommon.Hex(fiveDoPc) : null,
                    operaSample?.Pc is { } operaPc ? HarnessCommon.Hex(operaPc) : null,
                    pcMatch,
                    title, frame, ref allowedCount, ref divergedCount,
                    fiveDoSample is null || operaSample is null,
                    diagnostic: !pcMatch && operaSample?.Pc is null
                        ? "reference PC unavailable (use the debug core)"
                        : null))
                {
                    nonOffsetDivergedCount++;
                }
            }

            var cpsrRequired = compare.CompareCpsr;
            if ((cpsrRequired || comparePc)
                && (cpsrRequired
                    || (fiveDoSample?.Cpsr is not null && operaSample?.Cpsr is not null)))
            {
                var cpsrMatch = fiveDoSample?.Cpsr is { } left && operaSample?.Cpsr is { } right
                    && left == right;
                if (AddField(
                    fields,
                    "cpsr",
                    fiveDoSample?.Cpsr is { } fiveDoCpsr ? HarnessCommon.Hex(fiveDoCpsr) : null,
                    operaSample?.Cpsr is { } operaCpsr ? HarnessCommon.Hex(operaCpsr) : null,
                    cpsrMatch,
                    title, frame, ref allowedCount, ref divergedCount,
                    fiveDoSample is null || operaSample is null,
                    diagnostic: !cpsrMatch && operaSample?.Cpsr is null
                        ? "reference CPSR unavailable (rebuild the debug core)"
                        : null))
                {
                    nonOffsetDivergedCount++;
                }
            }

            if (compare.CompareCd || compareCd)
            {
                var countMatch = fiveDoSample?.CdCommandCount is { } leftCount
                    && operaSample?.CdCommandCount is { } rightCount
                    && leftCount == rightCount;
                if (AddField(
                    fields,
                    "cdCommandCount",
                    fiveDoSample?.CdCommandCount?.ToString(CultureInfo.InvariantCulture),
                    operaSample?.CdCommandCount?.ToString(CultureInfo.InvariantCulture),
                    countMatch,
                    title, frame, ref allowedCount, ref divergedCount,
                    fiveDoSample is null || operaSample is null,
                    diagnostic: !countMatch && operaSample?.CdCommandCount is null
                        ? "reference CD transcript unavailable (rebuild the debug core)"
                        : null))
                {
                    nonOffsetDivergedCount++;
                }

                var hashMatch = fiveDoSample?.CdCommandHash is { } leftHash
                    && operaSample?.CdCommandHash is { } rightHash
                    && leftHash == rightHash;
                if (AddField(
                    fields,
                    "cdCommandHash",
                    fiveDoSample?.CdCommandHash is { } fiveDoHash ? HarnessCommon.Hex(fiveDoHash) : null,
                    operaSample?.CdCommandHash is { } operaHash ? HarnessCommon.Hex(operaHash) : null,
                    hashMatch,
                    title, frame, ref allowedCount, ref divergedCount,
                    fiveDoSample is null || operaSample is null,
                    diagnostic: !hashMatch && operaSample?.CdCommandHash is null
                        ? "reference CD transcript unavailable (rebuild the debug core)"
                        : null))
                {
                    nonOffsetDivergedCount++;
                }
            }

            if ((compare.CompareCd || compareCd)
                && frame == framesToCompare[^1]
                && fiveDo.CdCommands.Length > 0
                && opera.CdCommands.Length > 0)
            {
                var sequence = CompareCdCommandSequence(fiveDo.CdCommands, opera.CdCommands);
                if (AddField(
                    fields,
                    "cdCommands",
                    sequence.FiveDo,
                    sequence.Opera,
                    sequence.Match,
                    title, frame, ref allowedCount, ref divergedCount,
                    missing: false,
                    diagnostic: sequence.Diagnostic))
                {
                    nonOffsetDivergedCount++;
                }
            }

            checkpoints.Add(new DiffCheckpointResult
            {
                Frame = frame,
                Match = fields.All(field => field.Match || field.AllowedReason is not null),
                Fields = [.. fields],
                FiveDoImage = fiveDoSample?.ImagePath,
                OperaImage = operaSample?.ImagePath,
                FiveDoPc = fiveDoSample?.Pc,
                OperaPc = operaSample?.Pc,
                PcDelta = fiveDoSample?.Pc is { } leftPc && operaSample?.Pc is { } rightPc
                    ? (long)rightPc - leftPc
                    : null,
            });
        }

        var status = divergedCount > 0
            ? nonOffsetDivergedCount == 0 && offsetCount > 0
                ? "Offset"
                : "Diverged"
            : allowedCount > 0
                ? "Allowed"
                : checkpoints.Count == 0
                    ? "Error"
                    : "Match";
        var pcDeltas = checkpoints
            .Where(checkpoint => checkpoint.PcDelta is not null)
            .Select(checkpoint => Math.Abs(checkpoint.PcDelta!.Value))
            .ToArray();
        var firstDivergence = FindFirstDivergence(checkpoints);

        return new DiffTitleResult
        {
            Id = title.Id,
            Name = title.Name ?? title.Id,
            Disc = fiveDo.Disc,
            Status = status,
            Frames = frames,
            Error = checkpoints.Count == 0 ? "no checkpoints were captured" : null,
            FiveDo = fiveDo,
            Opera = opera,
            Checkpoints = [.. checkpoints],
            AllowedCount = allowedCount,
            DivergedCount = divergedCount,
            OffsetCount = offsetCount,
            MaxPcDelta = pcDeltas.Length == 0 ? null : pcDeltas.Max(),
            FirstDivergence = firstDivergence,
        };
    }

    /// <summary>Builds the aggregate report.</summary>
    public static DiffReport BuildReport(
        DiffManifest manifest,
        string manifestPath,
        DiffTitleResult[] titles,
        DiffProvenance provenance)
    {
        var fields = titles.SelectMany(title => title.Checkpoints)
            .SelectMany(checkpoint => checkpoint.Fields)
            .ToArray();
        var summary = new DiffSummary
        {
            Titles = titles.Length,
            Matched = titles.Count(title => string.Equals(title.Status, "Match", StringComparison.Ordinal)),
            Allowed = titles.Count(title => string.Equals(title.Status, "Allowed", StringComparison.Ordinal)),
            Diverged = titles.Count(title => string.Equals(title.Status, "Diverged", StringComparison.Ordinal)),
            Offset = titles.Count(title => string.Equals(title.Status, "Offset", StringComparison.Ordinal)),
            Errors = titles.Count(title => string.Equals(title.Status, "Error", StringComparison.Ordinal)),
            Skipped = titles.Count(title => string.Equals(title.Status, "Skipped", StringComparison.Ordinal)),
            Checkpoints = titles.Sum(title => title.Checkpoints.Length),
            Fields = fields.Length,
            AllowedFields = fields.Count(field => field.AllowedReason is not null),
            DivergedFields = fields.Count(field => !field.Match && field.AllowedReason is null),
            FirstDivergenceTitles = titles.Count(title => title.FirstDivergence is not null),
        };

        return new DiffReport
        {
            ManifestName = manifest.Name,
            ManifestPath = manifestPath,
            ManifestHash = HarnessCommon.Md5File(manifestPath),
            GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Provenance = provenance,
            Titles = titles,
            Summary = summary,
        };
    }

    private static (bool Match, string FiveDo, string Opera, string? Diagnostic)
        CompareCdCommandSequence(byte[] fiveDo, byte[] opera)
    {
        var common = Math.Min(fiveDo.Length, opera.Length);
        for (var i = 0; i < common; i++)
        {
            if (fiveDo[i] != opera[i])
            {
                return (
                    false,
                    $"{fiveDo.Length} opcodes; {DescribeCdCommands(fiveDo, i)}",
                    $"{opera.Length} opcodes; {DescribeCdCommands(opera, i)}",
                    $"first sequence divergence at command {i}: "
                    + $"5DO 0x{fiveDo[i]:x2}, Opera 0x{opera[i]:x2}");
            }
        }

        if (fiveDo.Length == opera.Length)
        {
            return (
                true,
                $"{fiveDo.Length} opcodes",
                $"{opera.Length} opcodes",
                null);
        }

        return (
            false,
            $"{fiveDo.Length} opcodes; {DescribeCdCommands(fiveDo, common)}",
            $"{opera.Length} opcodes; {DescribeCdCommands(opera, common)}",
            $"sequence prefixes match for {common} commands; "
            + $"5DO has {fiveDo.Length}, Opera has {opera.Length}");
    }

    private static string DescribeCdCommands(byte[] commands, int index)
    {
        if (commands.Length == 0)
        {
            return "[]";
        }

        var start = Math.Max(0, index - 3);
        var end = Math.Min(commands.Length, index + 4);
        var builder = new StringBuilder();
        builder.Append('[').Append(start).Append("..").Append(end - 1).Append("] ");
        for (var i = start; i < end; i++)
        {
            if (i > start)
            {
                builder.Append(' ');
            }

            builder.Append(commands[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static DiffFirstDivergence? FindFirstDivergence(
        List<DiffCheckpointResult> checkpoints)
    {
        foreach (var checkpoint in checkpoints)
        {
            foreach (var field in checkpoint.Fields)
            {
                if (!field.Match && field.AllowedReason is null)
                {
                    return new DiffFirstDivergence
                    {
                        Frame = checkpoint.Frame,
                        Field = field.Field,
                        FiveDo = field.FiveDo,
                        Opera = field.Opera,
                        Diagnostic = field.Diagnostic,
                        Issue = field.Issue,
                    };
                }
            }
        }

        return null;
    }

    private static bool AddField(
        List<DiffFieldResult> fields,
        string field,
        string? fiveDo,
        string? opera,
        bool match,
        DiffTitle title,
        long frame,
        ref int allowedCount,
        ref int divergedCount,
        bool missing,
        string? diagnostic = null)
    {
        DiffAllowance? allowance = match ? null : FindAllowance(title, field, frame);
        if (match)
        {
            // No accounting.
        }
        else if (allowance is not null)
        {
            allowedCount++;
        }
        else
        {
            divergedCount++;
        }

        fields.Add(new DiffFieldResult
        {
            Field = field,
            Match = match,
            FiveDo = fiveDo,
            Opera = opera,
            AllowedReason = allowance?.Reason,
            Issue = allowance?.Issue ?? (missing && !match ? "missing sample" : null),
            Diagnostic = diagnostic,
        });
        return !match && allowance is null;
    }

    private static (long Frame, int Offset)? FindOffset(
        Dictionary<long, CaptureSample> fiveDo,
        Dictionary<long, CaptureSample> opera,
        long frame,
        int window)
    {
        if (window <= 0 || !fiveDo.TryGetValue(frame, out var sample))
        {
            return null;
        }

        for (var delta = 1; delta <= window; delta++)
        {
            foreach (var candidate in new[] { frame - delta, frame + delta })
            {
                if (opera.TryGetValue(candidate, out var other)
                    && other.FrameHash == sample.FrameHash)
                {
                    return (candidate, (int)(candidate - frame));
                }
            }
        }

        return null;
    }

    private static DiffAllowance? FindAllowance(DiffTitle title, string field, long frame)
    {
        foreach (var allowance in title.Allow ?? [])
        {
            if (!string.Equals(allowance.Field, field, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (allowance.Checkpoint is { } checkpoint && checkpoint != frame)
            {
                continue;
            }

            if (allowance.From is { } from && frame < from)
            {
                continue;
            }

            if (allowance.To is { } to && frame > to)
            {
                continue;
            }

            return allowance;
        }

        return null;
    }
}
