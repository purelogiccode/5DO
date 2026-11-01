using System.Collections.Generic;

namespace FiveDo.Cli;

internal sealed record CliDiscPlaylistEntry(int Index, string Path, string? Title, string DisplayName);

internal sealed record CliDiscPlaylistResult(
    bool Ok,
    string Path,
    int Current,
    int Count,
    IReadOnlyList<CliDiscPlaylistEntry> Entries);
