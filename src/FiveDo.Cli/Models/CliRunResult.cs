using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FiveDo.Cli;


internal sealed record CliRunResult(
    bool Ok,
    long Frames,
    double ElapsedSeconds,
    int CyclesPerFrame,
    int SourceWidth,
    int SourceHeight,
    string Region,
    int FieldRate,
    uint VdlModulo,
    uint Pc,
    long Instructions,
    uint FrameHash,
    long Samples,
    string DiscHash,
    string? GameId,
    string Profile,
    string ArmTiming,
    string ClioTimers,
    string CelTiming,
    ulong CelBusyCycles,
    string CdromTiming,
    string CdSpeed,
    int CpuClock,
    uint FixMode,
    bool DspRunning,
    uint DspPc,
    uint DspSema,
    uint DspSeed,
    byte CdStatus,
    byte CdPoll,
    string? Autopilot,
    double? AutopilotLineX,
    double? AutopilotRoadCoverage,
    int? AutopilotSteering,
    int? AutopilotObstacle,
    bool? AutopilotRecovery,
    bool? AutopilotCrashRestart,
    bool? AutopilotMenuAdvance,
    int? AutopilotStaticFrames,
    bool? AutopilotPacing,
    bool? AutopilotUpset,
    double[]? AutopilotLookahead,
    double? AudioDurationSeconds,
    int? AudioPeak,
    double? AudioRms,
    long? AudioClippedFrames,
    long? AudioSilentFrames,
    double? AudioDcOffsetLeft,
    double? AudioDcOffsetRight,
    long? AudioDroppedSamples,
    long? AudioUnderrunBuffers,
    long? AudioQueuedBuffers,
    int? AudioBufferedFrames,
    double? AudioLatencyMilliseconds,
    string? BiosPath,
    string? Bios2Path,
    string? BiosMd5,
    string? Bios2Md5,
    string? BiosName,
    string? BiosRegion,
    string? BiosSelection,
    string BiosSource,
    string? NvramPath,
    string NvramMode,
    int NvramVersion,
    uint? AudioHash,
    IReadOnlyList<CliCheckpointResult> Checkpoints,
    IReadOnlyList<string> Warnings,
    int CheatCount,
    long CheatWrites,
    string? CheatPath,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? MoviePath = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? MovieFrames = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? MovieVerification = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? MovieFailures = null,
    string InputDevices = "1=joypad,2=joypad,3=joypad,4=joypad,5=joypad,6=joypad,7=joypad,8=joypad",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? GifPath = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? GifFrames = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CliFiqResult? Fiq = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CliDspResult? DspStats = null,
    bool OperaDipir = false,
    bool OperaSoftReset = false,
    bool SwiHle = false,
    long SwiInstructions = 0,
    long SwiHleInstructions = 0,
    uint LastSwiComment = 0,
    string CpuBackend = "Interpreter",
    uint? RandomSeed = null,
    bool HighResolution = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CliCycleClassResult>? CycleClasses = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CliTimerStatResult>? TimerStats = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CliSwiCommentResult>? SwiComments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    long JitBlocksCompiled = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    long JitBlocksExecuted = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int JitBlockCount = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool JitCompileFailed = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    double JitCompileSeconds = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    long JitCompiledDataProcessing = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    long JitCompiledLoads = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    long JitCompiledStores = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    long JitCompiledBranches = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool JitFallback = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? JitLastFailure = null,
    string Memory = "Dram2MbVram1Mb");

/// <summary>One SWI comment observed during a diagnostic run.</summary>
/// <param name="Comment">The 24-bit SWI comment.</param>
/// <param name="Count">Number of SWIs with that comment.</param>
internal sealed record CliSwiCommentResult(uint Comment, long Count);
