using System.Collections.Generic;
using System.Text.Json.Serialization;
using FiveDo.Host;

namespace FiveDo.Cli;

/// <summary>
/// Structured JSON responses emitted by the headless CLI.
/// </summary>
/// <remarks>
/// These records exist so the CLI can serialize through source-generated metadata
/// instead of reflection. Keep the property names in sync with the documented
/// <c>--json</c> contract; the context uses camelCase to preserve the historical
/// lower-camel field names.
/// </remarks>
internal sealed record CliErrorResult(bool Ok, string Error);

internal sealed record CliRunResult(
    bool Ok,
    long Frames,
    double ElapsedSeconds,
    int CyclesPerFrame,
    int SourceWidth,
    int SourceHeight,
    uint VdlModulo,
    uint Pc,
    long Instructions,
    uint FrameHash,
    long Samples,
    string DiscHash,
    string? GameId,
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
    IReadOnlyList<string> Warnings);

internal sealed record CliAudioResult(
    bool Ok,
    string Path,
    int SampleRate,
    int Channels,
    int BitsPerSample,
    long Frames,
    double DurationSeconds,
    long DataBytes,
    int Peak,
    double Rms,
    long ClippedFrames,
    long SilentFrames,
    double DcOffsetLeft,
    double DcOffsetRight);

internal sealed record CliInfoResult(
    bool Ok,
    string Path,
    int SectorCount,
    string VolumeLabel,
    uint VolumeBlockCount,
    uint BlockSize,
    string GameId,
    uint FixMode,
    bool Known,
    string Name,
    string Publisher,
    string ReleaseYear,
    string Regions);

internal sealed record CliBiosInfoResult(
    bool Ok,
    string Path,
    int Size,
    string Md5,
    bool Anvil,
    int AnvilKind);

internal sealed record CliBenchResult(
    bool Ok,
    int Frames,
    double Seconds,
    double FramesPerSecond,
    int CyclesPerFrame,
    long Instructions,
    uint FrameHash);

internal sealed record CliSelfTestCheck(string Name, bool Ok);

internal sealed record CliSelfTestResult(bool Ok, IReadOnlyList<CliSelfTestCheck> Checks);

internal sealed record CliFsListEntry(string Path, bool Directory, ulong Size, uint Lba);

internal sealed record CliFsListResult(bool Ok, IReadOnlyList<CliFsListEntry> Entries);

internal sealed record CliFsExtractResult(bool Ok, int Extracted, string Output);

/// <summary>
/// Source-generated JSON metadata for every CLI response.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CliErrorResult))]
[JsonSerializable(typeof(CliRunResult))]
[JsonSerializable(typeof(CliInfoResult))]
[JsonSerializable(typeof(CliBiosInfoResult))]
[JsonSerializable(typeof(CliAudioResult))]
[JsonSerializable(typeof(CliBenchResult))]
[JsonSerializable(typeof(CliSelfTestResult))]
[JsonSerializable(typeof(CliFsListResult))]
[JsonSerializable(typeof(CliFsExtractResult))]
internal sealed partial class CliJsonContext : JsonSerializerContext;

/// <summary>
/// Source-generated JSON metadata for host records that the CLI emits verbatim.
/// </summary>
/// <remarks>
/// This context intentionally keeps the default PascalCase property names so the
/// historical <c>state inspect</c> and <c>state slots</c> contracts do not change.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(StateInfo))]
[JsonSerializable(typeof(List<SaveStateSlotInfo>))]
[JsonSerializable(typeof(IReadOnlyList<SaveStateSlotInfo>))]
internal sealed partial class CliHostJsonContext : JsonSerializerContext;
