using System;
using System.Collections.Generic;

namespace FiveDo.Cli;


internal sealed record CliNvramPathResult(
    bool Ok,
    string Path,
    string Mode,
    int Version,
    string? GameId,
    bool Exists,
    long Length);

internal sealed record CliNvramEntry(
    string Path,
    string Mode,
    int Version,
    string? GameId,
    long Length,
    DateTime? LastWriteTimeUtc);

internal sealed record CliNvramListResult(
    bool Ok,
    string Directory,
    IReadOnlyList<CliNvramEntry> Files);

internal sealed record CliNvramActionResult(
    bool Ok,
    string Action,
    string Path,
    string? Source,
    long Length);
