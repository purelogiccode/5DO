using System.Collections.Generic;

namespace FiveDo.Cli;

internal sealed record CliDiscTrackResult(
    int Number,
    string Type,
    int DataSize,
    int Frames,
    int Start,
    int PayloadOffset);

internal sealed record CliDiscTracksResult(
    bool Ok,
    string Path,
    IReadOnlyList<CliDiscTrackResult> Tracks);
