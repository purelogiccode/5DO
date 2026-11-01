using System.Collections.Generic;

namespace FiveDo.Cli;

/// <summary>One cheat entry in a CLI cheat listing.</summary>
internal sealed record CliCheatEntry(
    string Name,
    string Description,
    string Code,
    bool Enabled,
    string? Error);

/// <summary>Result of <c>cheat list</c>.</summary>
internal sealed record CliCheatListResult(
    bool Ok,
    string Path,
    string GameId,
    IReadOnlyList<CliCheatEntry> Cheats);

/// <summary>Result of a cheat add, enable, disable, remove, import, or export action.</summary>
internal sealed record CliCheatActionResult(
    bool Ok,
    string Action,
    string Path,
    string GameId,
    string? Name,
    int Count);
