using System.Collections.Generic;

namespace FiveDo.DiffHarness;

/// <summary>Manifest describing a differential run.</summary>
internal sealed class DiffManifest
{
    /// <summary>Gets the manifest schema version.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Gets the human-readable manifest name.</summary>
    public string Name { get; init; } = "unnamed";

    /// <summary>Gets the defaults applied to every title.</summary>
    public DiffDefaults? Defaults { get; init; } = new();

    /// <summary>Gets named roots used by <c>${name}</c> substitutions in paths.</summary>
    public Dictionary<string, string>? Roots { get; init; } = [];

    /// <summary>Gets the titles to run.</summary>
    public DiffTitle[]? Titles { get; init; } = [];
}

/// <summary>Defaults shared by every title in a manifest.</summary>
internal sealed class DiffDefaults
{
    /// <summary>Gets the BIOS image path.</summary>
    public string Bios { get; init; } = "bios/panafz1.bin";

    /// <summary>Gets the number of frames to run when a title does not override it.</summary>
    public int? Frames { get; init; } = 3000;

    /// <summary>Gets the checkpoint frames. A null value means the final frame only.</summary>
    public int[]? Checkpoints { get; init; }

    /// <summary>Gets the optional input script path.</summary>
    public string? InputScript { get; init; }

    /// <summary>Gets the machine options.</summary>
    public DiffMachineOptions? Options { get; init; } = new();

    /// <summary>Gets the optional explicit 5DO CLI path.</summary>
    public string? Cli { get; init; }

    /// <summary>Gets the optional explicit OperaCore library path.</summary>
    public string? OperaCore { get; init; }

    /// <summary>Gets the per-title timeout in seconds.</summary>
    public int? TimeoutSeconds { get; init; } = 180;

    /// <summary>Gets the comparison policy.</summary>
    public DiffCompareOptions? Compare { get; init; } = new();

    /// <summary>Gets the work directory for staged Opera runtimes and ISOs.</summary>
    public string WorkDirectory { get; init; } = "artifacts/diff/work";
}

/// <summary>One disc in a manifest.</summary>
internal sealed class DiffTitle
{
    /// <summary>Gets the stable title identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the display name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the disc image path used by 5DO.</summary>
    public string Disc { get; init; } = string.Empty;

    /// <summary>Gets the optional ISO path used by the Opera reference core.</summary>
    public string? Iso { get; init; }

    /// <summary>Gets the optional BIOS override.</summary>
    public string? Bios { get; init; }

    /// <summary>Gets the optional frame-count override.</summary>
    public int? Frames { get; init; }

    /// <summary>Gets the optional checkpoint override.</summary>
    public int[]? Checkpoints { get; init; }

    /// <summary>Gets the optional input-script override.</summary>
    public string? InputScript { get; init; }

    /// <summary>Gets the optional machine-option override.</summary>
    public DiffMachineOptions? Options { get; init; }

    /// <summary>Gets the known-divergence allowlist.</summary>
    public DiffAllowance[]? Allow { get; init; } = [];
}

/// <summary>Machine options passed to the 5DO CLI and mirrored by the reference core.</summary>
internal sealed class DiffMachineOptions
{
    /// <summary>Gets the emulation profile.</summary>
    public string? Profile { get; init; }

    /// <summary>Gets the ARM timing mode.</summary>
    public string? ArmTiming { get; init; }

    /// <summary>Gets the CLIO timer mode.</summary>
    public string? ClioTimers { get; init; }

    /// <summary>Gets the CEL timing mode.</summary>
    public string? CelTiming { get; init; }

    /// <summary>Gets the CD-ROM timing mode.</summary>
    public string? CdTiming { get; init; }

    /// <summary>Gets the CD speed.</summary>
    public string? CdSpeed { get; init; }

    /// <summary>Gets the CPU clock multiplier.</summary>
    public string? CpuClock { get; init; }

    /// <summary>Gets the random seed.</summary>
    public string? RandomSeed { get; init; }

    /// <summary>Gets whether high-resolution rendering is enabled.</summary>
    public bool? HighResolution { get; init; }

    /// <summary>Gets an explicit compatibility fix mask.</summary>
    public string? FixMode { get; init; }

    /// <summary>Gets whether automatic game fixes are disabled.</summary>
    public bool? NoGameFixes { get; init; }

    /// <summary>Gets whether checkpoint state hashes are recorded.</summary>
    public bool? StateHash { get; init; }

    /// <summary>Gets additional raw CLI arguments.</summary>
    public string[]? ExtraArgs { get; init; } = [];
}

/// <summary>Comparison policy.</summary>
internal sealed class DiffCompareOptions
{
    /// <summary>Gets whether the audio hash is part of the pass/fail decision.</summary>
    /// <remarks>
    /// The bundled OperaCore seeds its PRNG from wall-clock time, so audio hashes are
    /// informational until the reference accepts a fixed seed.
    /// </remarks>
    public bool CompareAudio { get; init; }

    /// <summary>Gets whether the ARM program counter is compared when both sides expose it.</summary>
    public bool ComparePc { get; init; }

    /// <summary>Gets the allowed absolute PC delta in bytes when PC comparison is enabled.</summary>
    public long PcTolerance { get; init; }

    /// <summary>Gets whether the ARM CPSR is compared when both sides expose it.</summary>
    public bool CompareCpsr { get; init; }

    /// <summary>Gets whether the cumulative CD command count and hash are compared.</summary>
    public bool CompareCd { get; init; }

    /// <summary>Gets whether the DSP sample count is compared.</summary>
    public bool? CompareSamples { get; init; } = true;

    /// <summary>
    /// Gets the allowed absolute DSP sample-count delta. Opera paces fields at 59.94 Hz
    /// while 5DO uses 60 Hz, so a 300-frame run differs by about 212 samples.
    /// </summary>
    public long SampleTolerance { get; init; }

    /// <summary>
    /// Gets the frame window used to diagnose frame-hash offsets. A non-zero window
    /// makes the harness report when one engine's hash matches the other engine's
    /// neighboring frame, distinguishing scheduling drift from pixel divergence.
    /// </summary>
    public int FrameWindow { get; init; }
}

/// <summary>A known, reasoned divergence.</summary>
internal sealed class DiffAllowance
{
    /// <summary>Gets the field name (<c>frameHash</c>, <c>audioHash</c>, <c>pc</c>, <c>samples</c>).</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>Gets the exact checkpoint frame, or null to use the range.</summary>
    public int? Checkpoint { get; init; }

    /// <summary>Gets the inclusive start of the checkpoint range.</summary>
    public int? From { get; init; }

    /// <summary>Gets the inclusive end of the checkpoint range.</summary>
    public int? To { get; init; }

    /// <summary>Gets the reason for the known divergence.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets the tracking issue or feature identifier.</summary>
    public string? Issue { get; init; }
}

/// <summary>One deterministic engine capture.</summary>
internal sealed class CaptureResult
{
    /// <summary>Gets the engine name (<c>5do</c> or <c>opera</c>).</summary>
    public string Engine { get; init; } = string.Empty;

    /// <summary>Gets whether the capture completed.</summary>
    public bool Ok { get; init; }

    /// <summary>Gets the failure detail, when any.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the disc path used for the run.</summary>
    public string Disc { get; init; } = string.Empty;

    /// <summary>Gets the ISO path used for the Opera run.</summary>
    public string? Iso { get; init; }

    /// <summary>Gets the BIOS path used for the run.</summary>
    public string Bios { get; init; } = string.Empty;

    /// <summary>Gets the BIOS MD5.</summary>
    public string? BiosMd5 { get; init; }

    /// <summary>Gets the reference core path.</summary>
    public string? CorePath { get; init; }

    /// <summary>Gets the reference core MD5.</summary>
    public string? CoreMd5 { get; init; }

    /// <summary>Gets the requested frame count.</summary>
    public int FramesRequested { get; init; }

    /// <summary>Gets the completed frame count.</summary>
    public long FramesRun { get; init; }

    /// <summary>Gets the capture wall time in seconds.</summary>
    public double ElapsedSeconds { get; init; }

    /// <summary>Gets the 5DO profile derived from the options.</summary>
    public string? Profile { get; init; }

    /// <summary>Gets the final frame width.</summary>
    public int? Width { get; init; }

    /// <summary>Gets the final frame height.</summary>
    public int? Height { get; init; }

    /// <summary>Gets the checkpoint samples.</summary>
    public CaptureSample[] Checkpoints { get; init; } = [];

    /// <summary>Gets engine warnings and error lines.</summary>
    public string[] Warnings { get; init; } = [];

    /// <summary>Gets the path to the engine's stderr log.</summary>
    public string? LogPath { get; init; }

    /// <summary>Gets the deterministic PRNG seed used for this capture, when one was applied.</summary>
    public uint? Seed { get; init; }
}

/// <summary>One checkpoint sample.</summary>
internal sealed class CaptureSample
{
    /// <summary>Gets the completed frame count.</summary>
    public long Frame { get; init; }

    /// <summary>Gets the ARM program counter, when the engine exposes it.</summary>
    public uint? Pc { get; init; }

    /// <summary>Gets the ARM current program status register, when the engine exposes it.</summary>
    public uint? Cpsr { get; init; }

    /// <summary>Gets the retired ARM instruction count, when the engine exposes it.</summary>
    public long? Instructions { get; init; }

    /// <summary>Gets the normalized BGRA8888 frame hash.</summary>
    public uint FrameHash { get; init; }

    /// <summary>Gets the DSP sample count.</summary>
    public long Samples { get; init; }

    /// <summary>Gets the cumulative PCM hash.</summary>
    public uint? AudioHash { get; init; }

    /// <summary>Gets the save-state hash, when requested.</summary>
    public uint? StateHash { get; init; }

    /// <summary>Gets the reference ARM-only state hash, when the debug core exposes it.</summary>
    public uint? ArmStateHash { get; init; }

    /// <summary>Gets the CD-ROM status byte.</summary>
    public int? CdStatus { get; init; }

    /// <summary>Gets the CD-ROM poll byte.</summary>
    public int? CdPoll { get; init; }

    /// <summary>Gets the cumulative hash of guest CD command opcodes.</summary>
    public uint? CdCommandHash { get; init; }

    /// <summary>Gets the number of guest CD commands issued.</summary>
    public int? CdCommandCount { get; init; }

    /// <summary>Gets the DSP program counter.</summary>
    public uint? DspPc { get; init; }

    /// <summary>Gets the packed DSP semaphore.</summary>
    public uint? DspSema { get; init; }

    /// <summary>Gets the DSP PRNG state.</summary>
    public uint? DspSeed { get; init; }

    /// <summary>Gets the remaining CEL busy cycles.</summary>
    public ulong? CelBusyCycles { get; init; }

    /// <summary>Gets the optional checkpoint image path.</summary>
    public string? ImagePath { get; init; }
}

/// <summary>Comparison report.</summary>
internal sealed class DiffReport
{
    /// <summary>Gets the report schema version.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Gets the manifest name.</summary>
    public string ManifestName { get; init; } = string.Empty;

    /// <summary>Gets the manifest path.</summary>
    public string ManifestPath { get; init; } = string.Empty;

    /// <summary>Gets the manifest SHA-256.</summary>
    public string ManifestHash { get; init; } = string.Empty;

    /// <summary>Gets the report generation time in UTC.</summary>
    public string GeneratedUtc { get; init; } = string.Empty;

    /// <summary>Gets the build and input provenance.</summary>
    public DiffProvenance Provenance { get; init; } = new();

    /// <summary>Gets the per-title results.</summary>
    public DiffTitleResult[] Titles { get; init; } = [];

    /// <summary>Gets the aggregate counts.</summary>
    public DiffSummary Summary { get; init; } = new();
}

/// <summary>Provenance for a differential run.</summary>
internal sealed class DiffProvenance
{
    /// <summary>Gets the current git commit, when available.</summary>
    public string? GitCommit { get; init; }

    /// <summary>Gets the 5DO CLI path.</summary>
    public string? CliPath { get; init; }

    /// <summary>Gets the 5DO CLI version.</summary>
    public string? CliVersion { get; init; }

    /// <summary>Gets the OperaCore path.</summary>
    public string? OperaCorePath { get; init; }

    /// <summary>Gets the OperaCore MD5.</summary>
    public string? OperaCoreMd5 { get; init; }

    /// <summary>Gets the manifest BIOS path.</summary>
    public string? BiosPath { get; init; }

    /// <summary>Gets the manifest BIOS MD5.</summary>
    public string? BiosMd5 { get; init; }

    /// <summary>Gets the ARM timing-mode override applied to every 5DO capture, when any.</summary>
    public string? ArmTimingOverride { get; init; }

    /// <summary>Gets the CLIO timer-mode override applied to every 5DO capture, when any.</summary>
    public string? ClioTimersOverride { get; init; }

    /// <summary>Gets the CEL timing-mode override applied to every 5DO capture, when any.</summary>
    public string? CelTimingOverride { get; init; }

    /// <summary>Gets the CD-ROM timing-mode override applied to every 5DO capture, when any.</summary>
    public string? CdTimingOverride { get; init; }

    /// <summary>Gets the emulation-profile override applied to every 5DO capture, when any.</summary>
    public string? ProfileOverride { get; init; }

    /// <summary>Gets the fixed PRNG seed applied to both engines.</summary>
    public uint? Seed { get; init; }

    /// <summary>Gets the host operating system.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Gets the runtime description.</summary>
    public string Runtime { get; init; } = string.Empty;
}

/// <summary>One title's comparison result.</summary>
internal sealed class DiffTitleResult
{
    /// <summary>Gets the title identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the disc path.</summary>
    public string Disc { get; init; } = string.Empty;

    /// <summary>Gets the status (<c>Match</c>, <c>Allowed</c>, <c>Diverged</c>, <c>Error</c>, <c>Skipped</c>).</summary>
    public string Status { get; init; } = "Skipped";

    /// <summary>Gets the requested frame count.</summary>
    public int Frames { get; init; }

    /// <summary>Gets the failure detail, when any.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the 5DO capture.</summary>
    public CaptureResult? FiveDo { get; init; }

    /// <summary>Gets the Opera capture.</summary>
    public CaptureResult? Opera { get; init; }

    /// <summary>Gets the checkpoint comparisons.</summary>
    public DiffCheckpointResult[] Checkpoints { get; init; } = [];

    /// <summary>Gets how many field comparisons were allowed by the manifest.</summary>
    public int AllowedCount { get; init; }

    /// <summary>Gets how many field comparisons diverged without an allowance.</summary>
    public int DivergedCount { get; init; }

    /// <summary>Gets how many frame-hash divergences matched a neighboring frame instead.</summary>
    public int OffsetCount { get; init; }

    /// <summary>Gets the largest absolute Opera-minus-5DO PC delta across checkpoints.</summary>
    public long? MaxPcDelta { get; init; }

    /// <summary>Gets the first unexplained field divergence, when one exists.</summary>
    public DiffFirstDivergence? FirstDivergence { get; init; }
}

/// <summary>The first checkpoint field that diverged without an allowance.</summary>
internal sealed class DiffFirstDivergence
{
    /// <summary>Gets the checkpoint frame.</summary>
    public long Frame { get; init; }

    /// <summary>Gets the field name.</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>Gets the 5DO value, formatted for the report.</summary>
    public string? FiveDo { get; init; }

    /// <summary>Gets the Opera value, formatted for the report.</summary>
    public string? Opera { get; init; }

    /// <summary>Gets an explanatory diagnostic, when available.</summary>
    public string? Diagnostic { get; init; }

    /// <summary>Gets the tracking issue, when the divergence is known.</summary>
    public string? Issue { get; init; }
}

/// <summary>One checkpoint's comparison.</summary>
internal sealed class DiffCheckpointResult
{
    /// <summary>Gets the checkpoint frame.</summary>
    public long Frame { get; init; }

    /// <summary>Gets whether every compared field matched or was allowed.</summary>
    public bool Match { get; init; }

    /// <summary>Gets the compared fields.</summary>
    public DiffFieldResult[] Fields { get; init; } = [];

    /// <summary>Gets the optional 5DO checkpoint image path.</summary>
    public string? FiveDoImage { get; init; }

    /// <summary>Gets the optional Opera checkpoint image path.</summary>
    public string? OperaImage { get; init; }

    /// <summary>Gets the 5DO program counter, when available.</summary>
    public uint? FiveDoPc { get; init; }

    /// <summary>Gets the Opera program counter, when available.</summary>
    public uint? OperaPc { get; init; }

    /// <summary>Gets the Opera-minus-5DO PC delta, when both are available.</summary>
    public long? PcDelta { get; init; }
}

/// <summary>One field comparison.</summary>
internal sealed class DiffFieldResult
{
    /// <summary>Gets the field name.</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>Gets whether the field matched.</summary>
    public bool Match { get; init; }

    /// <summary>Gets the 5DO value, formatted for the report.</summary>
    public string? FiveDo { get; init; }

    /// <summary>Gets the Opera value, formatted for the report.</summary>
    public string? Opera { get; init; }

    /// <summary>Gets the allowance reason, when the divergence is known.</summary>
    public string? AllowedReason { get; init; }

    /// <summary>Gets the allowance issue, when the divergence is known.</summary>
    public string? Issue { get; init; }

    /// <summary>Gets an explanatory diagnostic, such as a matching neighboring frame.</summary>
    public string? Diagnostic { get; init; }
}

/// <summary>Aggregate comparison counts.</summary>
internal sealed class DiffSummary
{
    /// <summary>Gets the number of titles in the manifest.</summary>
    public int Titles { get; init; }

    /// <summary>Gets the number of titles that matched.</summary>
    public int Matched { get; init; }

    /// <summary>Gets the number of titles with only allowed divergences.</summary>
    public int Allowed { get; init; }

    /// <summary>Gets the number of titles with unexplained divergences.</summary>
    public int Diverged { get; init; }

    /// <summary>Gets the number of titles whose only divergence was a frame offset.</summary>
    public int Offset { get; init; }

    /// <summary>Gets the number of titles that could not be captured.</summary>
    public int Errors { get; init; }

    /// <summary>Gets the number of skipped titles.</summary>
    public int Skipped { get; init; }

    /// <summary>Gets the total checkpoint comparisons.</summary>
    public int Checkpoints { get; init; }

    /// <summary>Gets the total field comparisons.</summary>
    public int Fields { get; init; }

    /// <summary>Gets the total allowed field divergences.</summary>
    public int AllowedFields { get; init; }

    /// <summary>Gets the total unexplained field divergences.</summary>
    public int DivergedFields { get; init; }

    /// <summary>Gets the number of titles with a reported first divergence.</summary>
    public int FirstDivergenceTitles { get; init; }

    /// <summary>Gets the report verdict.</summary>
    public bool Passed => Diverged == 0 && Offset == 0 && Errors == 0;
}

/// <summary>Options for one harness invocation.</summary>
internal sealed class HarnessOptions
{
    /// <summary>Gets or sets the manifest path.</summary>
    public string ManifestPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the output directory.</summary>
    public string OutputDirectory { get; set; } = "artifacts/diff";

    /// <summary>Gets or sets the explicit 5DO CLI path.</summary>
    public string? CliPath { get; set; }

    /// <summary>Gets or sets the explicit OperaCore path.</summary>
    public string? OperaCorePath { get; set; }

    /// <summary>Gets or sets a BIOS override.</summary>
    public string? BiosPath { get; set; }

    /// <summary>Gets or sets a frame-count override.</summary>
    public int? Frames { get; set; }

    /// <summary>Gets or sets the title filters.</summary>
    public string[] TitleFilters { get; set; } = [];

    /// <summary>Gets or sets the maximum number of titles to run.</summary>
    public int Limit { get; set; }

    /// <summary>Gets or sets whether existing captures are reused.</summary>
    public bool Resume { get; set; }

    /// <summary>Gets or sets whether the Opera side is skipped.</summary>
    public bool NoOpera { get; set; }

    /// <summary>Gets or sets whether checkpoint images are disabled.</summary>
    public bool NoImages { get; set; }

    /// <summary>Gets or sets whether staged ISOs are deleted after a successful capture.</summary>
    public bool CleanupIso { get; set; }

    /// <summary>Gets or sets whether PC comparison is forced on.</summary>
    public bool ComparePc { get; set; }

    /// <summary>Gets or sets whether CD command comparison is forced on.</summary>
    public bool CompareCd { get; set; }

    /// <summary>Gets or sets whether audio comparison is forced on.</summary>
    public bool CompareAudio { get; set; }

    /// <summary>Gets or sets the deterministic PRNG seed override.</summary>
    public uint? Seed { get; set; }

    /// <summary>Gets or sets the PC tolerance override in bytes.</summary>
    public long? PcTolerance { get; set; }

    /// <summary>Gets or sets whether existing captures are overwritten.</summary>
    public bool Force { get; set; }

    /// <summary>Gets or sets an explicit ARM timing-mode override for every 5DO capture.</summary>
    public string? ArmTiming { get; set; }

    /// <summary>Gets or sets an explicit CLIO timer-mode override for every 5DO capture.</summary>
    public string? ClioTimers { get; set; }

    /// <summary>Gets or sets an explicit CEL timing-mode override for every 5DO capture.</summary>
    public string? CelTiming { get; set; }

    /// <summary>Gets or sets an explicit CD-ROM timing-mode override for every 5DO capture.</summary>
    public string? CdTiming { get; set; }

    /// <summary>Gets or sets an explicit emulation-profile override for every 5DO capture.</summary>
    public string? Profile { get; set; }

    /// <summary>Gets or sets the per-run timeout override.</summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>Gets or sets the report file name stem.</summary>
    public string ReportName { get; set; } = "report";
}
