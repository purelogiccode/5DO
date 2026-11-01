using System.Collections.Generic;

namespace FiveDo.Cli;

/// <summary>JSON contract for <c>movie info</c>.</summary>
internal sealed record CliMovieInfoResult(
    bool Ok,
    string Path,
    int FormatVersion,
    string EmulatorVersion,
    string CreatedUtc,
    string? GameName,
    string? GameId,
    string? DiscHash,
    string? DiscFile,
    string? BiosMd5,
    string? BiosName,
    long StartFrame,
    int FrameCount,
    long EndFrame,
    uint? FinalFrameHash,
    uint? FinalProgramCounter,
    uint? FinalStateHash,
    bool HasState,
    int StateBytes,
    long FileBytes,
    IReadOnlyDictionary<string, string>? Options,
    string? Notes);

/// <summary>JSON contract for <c>movie branch</c>.</summary>
internal sealed record CliMovieActionResult(
    bool Ok,
    string Path,
    int FrameCount,
    string? Message);
