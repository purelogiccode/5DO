using System.Collections.Generic;

namespace FiveDo.Cli;

/// <summary>Result shared by the <c>gamedb</c> subcommands.</summary>
internal sealed record CliGameDbResult(
    bool Ok,
    string Command,
    string Path = "",
    string OutPath = "",
    int DatabaseRecords = 0,
    int Found = 0,
    int FoundUnique = 0,
    int Scanned = 0,
    int ScannedUnique = 0,
    int Unique = 0,
    int Known = 0,
    int Matched = 0,
    int Missing = 0,
    double Coverage = 0,
    int Added = 0,
    int Updated = 0,
    int Written = 0,
    IReadOnlyList<string>? Duplicates = null,
    IReadOnlyList<string>? Errors = null);
