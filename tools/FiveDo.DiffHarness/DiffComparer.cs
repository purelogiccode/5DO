using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
                if (AddField(
                    fields,
                    "samples",
                    fiveDoSample?.Samples.ToString(CultureInfo.InvariantCulture),
                    operaSample?.Samples.ToString(CultureInfo.InvariantCulture),
                    fiveDoSample is not null
                        && operaSample is not null
                        && fiveDoSample.Samples == operaSample.Samples,
                    title, frame, ref allowedCount, ref divergedCount,
                    fiveDoSample is null || operaSample is null))
                {
                    nonOffsetDivergedCount++;
                }
            }

            if (compare.CompareAudio)
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
