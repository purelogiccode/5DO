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
        long? pcPhaseFrame = null;
        long? pcPhaseInstructionDelta = null;

        foreach (var frame in framesToCompare)
        {
            fiveDoSamples.TryGetValue(frame, out var fiveDoSample);
            operaSamples.TryGetValue(frame, out var operaSample);
            var fields = new List<DiffFieldResult>();
            var pcToleranceValue = pcTolerance ?? compare.PcTolerance;
            var instructionPhase = (compare.ComparePc || comparePc)
                && fiveDoSample?.Pc is not null
                && operaSample?.Pc is not null
                ? FindInstructionPhase(
                    fiveDoSamples,
                    operaSamples,
                    frame,
                    compare.FrameWindow,
                    pcToleranceValue)
                : null;
            var phaseDiagnostic = instructionPhase is { } phase
                ? DescribeInstructionPhase(frame, phase)
                : null;
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
                if (!pcMatch && instructionPhase is { } pcPhase && pcPhaseFrame is null)
                {
                    pcPhaseFrame = pcPhase.Frame;
                    pcPhaseInstructionDelta = pcPhase.InstructionDelta;
                }

                if (AddField(
                    fields,
                    "pc",
                    fiveDoSample?.Pc is { } fiveDoPc ? HarnessCommon.Hex(fiveDoPc) : null,
                    operaSample?.Pc is { } operaPc ? HarnessCommon.Hex(operaPc) : null,
                    pcMatch,
                    title, frame, ref allowedCount, ref divergedCount,
                    fiveDoSample is null || operaSample is null,
                    diagnostic: !pcMatch
                        ? operaSample?.Pc is null
                            ? "reference PC unavailable (use the debug core)"
                            : phaseDiagnostic
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
                    diagnostic: !cpsrMatch
                        ? operaSample?.Cpsr is null
                            ? "reference CPSR unavailable (rebuild the debug core)"
                            : phaseDiagnostic
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
                var sequence = CompareCdCommandSequence(
                    fiveDo.CdCommands,
                    opera.CdCommands,
                    fiveDo.CdCommandCycles,
                    opera.CdCommandCycles);
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
                FiveDoInstructions = fiveDoSample?.Instructions,
                OperaInstructions = operaSample?.Instructions,
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
        var firstBehavioralDivergence = FindFirstDivergence(checkpoints, behavioralOnly: true);

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
            PcPhaseFrame = pcPhaseFrame,
            PcPhaseInstructionDelta = pcPhaseInstructionDelta,
            FirstDivergence = firstDivergence,
            FirstBehavioralDivergence = firstBehavioralDivergence,
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
            PcPhaseTitles = titles.Count(title => title.PcPhaseFrame is not null),
            PhaseOnlyTitles = titles.Count(
                title => title.DivergedCount > 0 && title.FirstBehavioralDivergence is null),
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
        CompareCdCommandSequence(
            byte[] fiveDo,
            byte[] opera,
            uint[] fiveDoCycles,
            uint[] operaCycles)
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
                    + $"5DO 0x{fiveDo[i]:x2}, Opera 0x{opera[i]:x2}; "
                    + DescribeCdTiming(fiveDoCycles, operaCycles, i));
            }
        }

        if (fiveDo.Length == opera.Length)
        {
            return (
                true,
                $"{fiveDo.Length} opcodes",
                $"{opera.Length} opcodes",
                DescribeCdTiming(fiveDoCycles, operaCycles, common - 1));
        }

        return (
            false,
            $"{fiveDo.Length} opcodes; {DescribeCdCommands(fiveDo, common)}",
            $"{opera.Length} opcodes; {DescribeCdCommands(opera, common)}",
            $"sequence prefixes match for {common} commands; "
            + $"5DO has {fiveDo.Length}, Opera has {opera.Length}; "
            + DescribeCdTiming(fiveDoCycles, operaCycles, common - 1));
    }

    private static string DescribeCdTiming(uint[] fiveDoCycles, uint[] operaCycles, int index)
    {
        if (index < 0 || index >= fiveDoCycles.Length || index >= operaCycles.Length)
        {
            return "no cycle evidence";
        }

        var fiveDo = fiveDoCycles[index];
        var opera = operaCycles[index];
        var delta = (long)fiveDo - opera;
        return $"command {index} at 5DO cycle {fiveDo}, Opera cycle {opera} (delta {delta:+#;-#;0})";
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
        List<DiffCheckpointResult> checkpoints,
        bool behavioralOnly = false)
    {
        foreach (var checkpoint in checkpoints)
        {
            foreach (var field in checkpoint.Fields)
            {
                if (!field.Match && field.AllowedReason is null)
                {
                    if (behavioralOnly && !IsBehavioralField(field.Field))
                    {
                        continue;
                    }

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

    private static bool IsBehavioralField(string field) => field is
        "frameHash"
        or "samples"
        or "audioHash"
        or "stateHash"
        or "cdCommandCount"
        or "cdCommandHash"
        or "cdCommands";

    /// <summary>A neighboring reference frame whose retired-instruction count matches 5DO.</summary>
    private readonly record struct InstructionPhase(
        long Frame,
        long InstructionDelta,
        bool PcMatches,
        bool CpsrMatches);

    /// <summary>
    /// Finds a neighboring reference frame whose retired-instruction count aligns with
    /// 5DO at <paramref name="frame"/>. A field-rate boot difference moves the PC and
    /// CPSR by a whole sample while the instruction count stays within a fraction of a
    /// frame, so this distinguishes frame-boundary phase from a behavioral divergence.
    /// </summary>
    private static InstructionPhase? FindInstructionPhase(
        Dictionary<long, CaptureSample> fiveDoSamples,
        Dictionary<long, CaptureSample> operaSamples,
        long frame,
        int window,
        long pcTolerance)
    {
        if (window <= 0
            || !fiveDoSamples.TryGetValue(frame, out var fiveDo)
            || fiveDo.Instructions is not { } fiveDoInstructions
            || !operaSamples.TryGetValue(frame, out var opera)
            || opera.Instructions is not { } sameInstructions)
        {
            return null;
        }

        var sameDelta = Math.Abs(sameInstructions - fiveDoInstructions);
        if (sameDelta == 0)
        {
            return null;
        }

        // One frame retires roughly 85,000 instructions at the stock clock. Accept a
        // neighbor only when its count is within a tenth of a frame; a wider window
        // would hide real work differences (for example extra CD sector reads).
        var instructionsPerFrame = EstimateInstructionsPerFrame(fiveDoSamples, frame);
        var tolerance = Math.Max(1, instructionsPerFrame / 10);
        InstructionPhase? best = null;
        for (var offset = -window; offset <= window; offset++)
        {
            if (offset == 0
                || !operaSamples.TryGetValue(frame + offset, out var candidate)
                || candidate.Instructions is not { } candidateInstructions)
            {
                continue;
            }

            var delta = Math.Abs(candidateInstructions - fiveDoInstructions);
            if (delta >= sameDelta || delta > tolerance)
            {
                continue;
            }

            if (best is null || delta < Math.Abs(best.Value.InstructionDelta))
            {
                best = new InstructionPhase(
                    candidate.Frame,
                    candidateInstructions - fiveDoInstructions,
                    fiveDo.Pc is { } pc
                        && candidate.Pc is { } candidatePc
                        && Math.Abs((long)candidatePc - pc) <= pcTolerance,
                    fiveDo.Cpsr is { } cpsr && candidate.Cpsr == cpsr);
            }
        }

        return best;
    }

    private static long EstimateInstructionsPerFrame(
        Dictionary<long, CaptureSample> samples,
        long frame)
    {
        const long fallback = 85_000;
        if (!samples.TryGetValue(frame, out var current)
            || current.Instructions is not { } currentInstructions)
        {
            return fallback;
        }

        long bestDistance = long.MaxValue;
        long estimate = fallback;
        foreach (var (otherFrame, sample) in samples)
        {
            var distance = Math.Abs(otherFrame - frame);
            if (distance == 0
                || distance >= bestDistance
                || sample.Instructions is not { } instructions)
            {
                continue;
            }

            bestDistance = distance;
            estimate = Math.Abs(instructions - currentInstructions) / distance;
        }

        return estimate > 0 ? estimate : fallback;
    }

    private static string DescribeInstructionPhase(long frame, InstructionPhase phase)
    {
        var text = new StringBuilder("instruction phase: 5DO frame ")
            .Append(frame)
            .Append(" aligns with Opera frame ")
            .Append(phase.Frame)
            .Append(" (delta ")
            .Append(phase.InstructionDelta.ToString("+#;-#;0", CultureInfo.InvariantCulture))
            .Append(')');
        if (phase.PcMatches)
        {
            text.Append("; PC matches");
        }

        if (phase.CpsrMatches)
        {
            text.Append("; CPSR matches");
        }

        return text.ToString();
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
