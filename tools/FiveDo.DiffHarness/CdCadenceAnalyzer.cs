using System;
using System.Globalization;
using System.Text;

namespace FiveDo.DiffHarness;

/// <summary>
/// Analyzes two CD command transcripts as a stream: common prefix, first opcode
/// mismatch, and the largest step in the cumulative cycle delta.
/// </summary>
/// <remarks>
/// The per-command cycle, PC, and retired-instruction stamps separate three cases:
/// a real opcode divergence, an in-flight/extra-command cadence difference, and a
/// guest wait loop. When the step PCs match on both engines, the guest took the
/// same code path and simply retired more instructions waiting; that is phase
/// evidence, not a command-table divergence.
/// </remarks>
internal static class CdCadenceAnalyzer
{
    /// <summary>Smallest cycle jump reported as a cadence step (80 microseconds at 12.5 MHz).</summary>
    public const long SignificantStepCycles = 1_000;

    /// <summary>Analyzes one title's CD command transcripts.</summary>
    public static CdCadenceAnalysis Analyze(
        byte[] fiveDo,
        byte[] opera,
        uint[]? fiveDoCycles = null,
        uint[]? operaCycles = null,
        uint[]? fiveDoPcs = null,
        uint[]? operaPcs = null,
        long[]? fiveDoInstructions = null,
        long[]? operaInstructions = null)
    {
        ArgumentNullException.ThrowIfNull(fiveDo);
        ArgumentNullException.ThrowIfNull(opera);

        var common = Math.Min(fiveDo.Length, opera.Length);
        var firstMismatch = -1;
        for (var i = 0; i < common; i++)
        {
            if (fiveDo[i] != opera[i])
            {
                firstMismatch = i;
                break;
            }
        }

        var kind = firstMismatch >= 0
            ? CdCadenceKind.SequenceDiverged
            : fiveDo.Length == opera.Length
                ? CdCadenceKind.Equal
                : CdCadenceKind.PrefixAhead;

        var fiveDoCyclesSpan = fiveDoCycles.AsSpan();
        var operaCyclesSpan = operaCycles.AsSpan();
        var fiveDoPcsSpan = fiveDoPcs.AsSpan();
        var operaPcsSpan = operaPcs.AsSpan();
        var fiveDoInstructionsSpan = fiveDoInstructions.AsSpan();
        var operaInstructionsSpan = operaInstructions.AsSpan();

        CdCadenceStep? largestStep = null;
        var lastMatched = firstMismatch >= 0 ? firstMismatch : common;
        var lastCommonCycleDelta = LastDelta(fiveDoCyclesSpan, operaCyclesSpan, lastMatched - 1);
        var lastCommonInstructionDelta = LastDelta(fiveDoInstructionsSpan, operaInstructionsSpan, lastMatched - 1);

        if (common > 1)
        {
            for (var i = 1; i < common; i++)
            {
                if (i >= fiveDoCyclesSpan.Length || i >= operaCyclesSpan.Length)
                {
                    break;
                }

                var previousDelta = (long)fiveDoCyclesSpan[i - 1] - operaCyclesSpan[i - 1];
                var currentDelta = (long)fiveDoCyclesSpan[i] - operaCyclesSpan[i];
                var jump = currentDelta - previousDelta;
                if (largestStep is not null
                    && Math.Abs(jump) <= Math.Abs(largestStep.CycleJump))
                {
                    continue;
                }

                var instructionJump = InstructionDelta(
                    fiveDoInstructionsSpan,
                    operaInstructionsSpan,
                    i - 1,
                    i);
                largestStep = new CdCadenceStep
                {
                    Index = i,
                    CycleJump = jump,
                    CumulativeCycleDelta = currentDelta,
                    FiveDoOpcode = i < fiveDo.Length ? fiveDo[i] : null,
                    OperaOpcode = i < opera.Length ? opera[i] : null,
                    FiveDoPc = i < fiveDoPcsSpan.Length ? fiveDoPcsSpan[i] : null,
                    OperaPc = i < operaPcsSpan.Length ? operaPcsSpan[i] : null,
                    InstructionJump = instructionJump,
                    FiveDoInstructions = i < fiveDoInstructionsSpan.Length
                        ? fiveDoInstructionsSpan[i]
                        : null,
                    OperaInstructions = i < operaInstructionsSpan.Length
                        ? operaInstructionsSpan[i]
                        : null,
                };
            }
        }

        if (largestStep is not null && Math.Abs(largestStep.CycleJump) < SignificantStepCycles)
        {
            largestStep = null;
        }

        return new CdCadenceAnalysis
        {
            FiveDoCount = fiveDo.Length,
            OperaCount = opera.Length,
            CommonCount = common,
            Kind = kind,
            FirstMismatchIndex = firstMismatch >= 0 ? firstMismatch : null,
            FiveDoMismatchOpcode = firstMismatch >= 0 ? fiveDo[firstMismatch] : null,
            OperaMismatchOpcode = firstMismatch >= 0 ? opera[firstMismatch] : null,
            LastCommonCycleDelta = lastCommonCycleDelta,
            LastCommonInstructionDelta = lastCommonInstructionDelta,
            LargestStep = largestStep,
        };
    }

    /// <summary>Builds the human-readable cadence diagnostic used in reports.</summary>
    public static string Describe(CdCadenceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var text = new StringBuilder();
        switch (analysis.Kind)
        {
            case CdCadenceKind.SequenceDiverged when analysis.FirstMismatchIndex is { } index:
                text.Append(CultureInfo.InvariantCulture,
                    $"first sequence divergence at command {index}: ");
                text.Append("5DO 0x").Append(FormatOpcode(analysis.FiveDoMismatchOpcode));
                text.Append(", Opera 0x").Append(FormatOpcode(analysis.OperaMismatchOpcode));
                break;
            case CdCadenceKind.PrefixAhead:
                text.Append(CultureInfo.InvariantCulture,
                    $"sequence prefixes match for {analysis.CommonCount} commands; ");
                text.Append(CultureInfo.InvariantCulture,
                    $"5DO has {analysis.FiveDoCount}, Opera has {analysis.OperaCount}");
                break;
            default:
                text.Append(CultureInfo.InvariantCulture,
                    $"{analysis.CommonCount} commands with identical opcodes");
                break;
        }

        if (analysis.LargestStep is { } step)
        {
            text.Append("; largest cadence step ")
                .Append(step.CycleJump.ToString("+#;-#;0", CultureInfo.InvariantCulture))
                .Append(" cycles at command ")
                .Append(step.Index.ToString(CultureInfo.InvariantCulture));
            if (step.FiveDoPc is { } fiveDoPc && step.OperaPc is { } operaPc)
            {
                text.Append(" (PC ").Append(Hex(fiveDoPc)).Append(" / ").Append(Hex(operaPc)).Append(')');
            }

            if (step.InstructionJump is { } instructionJump)
            {
                text.Append(", retired-instruction step ")
                    .Append(instructionJump.ToString("+#;-#;0", CultureInfo.InvariantCulture));
            }
        }
        else
        {
            text.Append("; no cadence step above ")
                .Append(SignificantStepCycles.ToString(CultureInfo.InvariantCulture))
                .Append(" cycles");
        }

        text.Append("; last matched command cycle delta ")
            .Append(analysis.LastCommonCycleDelta.ToString("+#;-#;0", CultureInfo.InvariantCulture));
        if (analysis.LastCommonInstructionDelta is { } lastInstructions)
        {
            text.Append(", instruction delta ")
                .Append(lastInstructions.ToString("+#;-#;0", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static long LastDelta(ReadOnlySpan<uint> fiveDo, ReadOnlySpan<uint> opera, int index)
    {
        return index >= 0 && index < fiveDo.Length && index < opera.Length
            ? (long)fiveDo[index] - opera[index]
            : 0;
    }

    private static long? LastDelta(ReadOnlySpan<long> fiveDo, ReadOnlySpan<long> opera, int index)
    {
        return index >= 0 && index < fiveDo.Length && index < opera.Length
            ? fiveDo[index] - opera[index]
            : null;
    }

    private static long? InstructionDelta(
        ReadOnlySpan<long> fiveDo,
        ReadOnlySpan<long> opera,
        int previousIndex,
        int index)
    {
        if (previousIndex < 0
            || index >= fiveDo.Length
            || index >= opera.Length
            || previousIndex >= fiveDo.Length
            || previousIndex >= opera.Length)
        {
            return null;
        }

        var previous = fiveDo[previousIndex] - opera[previousIndex];
        var current = fiveDo[index] - opera[index];
        return current - previous;
    }

    private static string Hex(uint value) => "0x" + value.ToString("x8", CultureInfo.InvariantCulture);

    private static string FormatOpcode(byte? opcode) => opcode is { } value
        ? value.ToString("x2", CultureInfo.InvariantCulture)
        : "--";
}

/// <summary>One CD command transcript comparison.</summary>
internal sealed class CdCadenceAnalysis
{
    /// <summary>Gets the number of 5DO commands.</summary>
    public int FiveDoCount { get; init; }

    /// <summary>Gets the number of Opera commands.</summary>
    public int OperaCount { get; init; }

    /// <summary>Gets the number of commands before the first mismatch or the shorter end.</summary>
    public int CommonCount { get; init; }

    /// <summary>Gets the comparison kind (<c>Equal</c>, <c>PrefixAhead</c>, or <c>SequenceDiverged</c>).</summary>
    public CdCadenceKind Kind { get; init; }

    /// <summary>Gets the first opcode mismatch index, when one exists.</summary>
    public int? FirstMismatchIndex { get; init; }

    /// <summary>Gets the 5DO opcode at the first mismatch.</summary>
    public byte? FiveDoMismatchOpcode { get; init; }

    /// <summary>Gets the Opera opcode at the first mismatch.</summary>
    public byte? OperaMismatchOpcode { get; init; }

    /// <summary>Gets the 5DO-minus-Opera cycle delta at the last common command.</summary>
    public long LastCommonCycleDelta { get; init; }

    /// <summary>Gets the 5DO-minus-Opera retired-instruction delta at the last common command.</summary>
    public long? LastCommonInstructionDelta { get; init; }

    /// <summary>Gets the largest cumulative-delta step, when it exceeds the significance threshold.</summary>
    public CdCadenceStep? LargestStep { get; init; }
}

/// <summary>The command boundary where the 5DO-minus-Opera cycle delta changed most.</summary>
internal sealed class CdCadenceStep
{
    /// <summary>Gets the command index after the step.</summary>
    public int Index { get; init; }

    /// <summary>Gets the signed cycle-delta jump at this command.</summary>
    public long CycleJump { get; init; }

    /// <summary>Gets the cumulative 5DO-minus-Opera cycle delta at this command.</summary>
    public long CumulativeCycleDelta { get; init; }

    /// <summary>Gets the 5DO opcode at this command.</summary>
    public byte? FiveDoOpcode { get; init; }

    /// <summary>Gets the Opera opcode at this command.</summary>
    public byte? OperaOpcode { get; init; }

    /// <summary>Gets the 5DO program counter at this command.</summary>
    public uint? FiveDoPc { get; init; }

    /// <summary>Gets the Opera program counter at this command.</summary>
    public uint? OperaPc { get; init; }

    /// <summary>Gets the signed jump in the retired-instruction delta across this step.</summary>
    public long? InstructionJump { get; init; }

    /// <summary>Gets the 5DO retired-instruction count at this command.</summary>
    public long? FiveDoInstructions { get; init; }

    /// <summary>Gets the Opera retired-instruction count at this command.</summary>
    public long? OperaInstructions { get; init; }

    /// <summary>Gets whether both engines issued the command from the same PC.</summary>
    public bool PcMatches => FiveDoPc is { } fiveDo && OperaPc is { } opera && fiveDo == opera;
}

/// <summary>The shape of a CD command transcript comparison.</summary>
internal enum CdCadenceKind
{
    /// <summary>The transcripts have equal length and identical opcodes.</summary>
    Equal,

    /// <summary>One transcript is a prefix of the other; the extra commands are cadence, not a sequence change.</summary>
    PrefixAhead,

    /// <summary>The opcodes differ at a common index.</summary>
    SequenceDiverged,
}
