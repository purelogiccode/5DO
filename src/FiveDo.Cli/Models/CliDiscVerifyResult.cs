using System.Collections.Generic;

namespace FiveDo.Cli;

internal sealed record CliDatTrackResult(
    int Track,
    long Size,
    string Crc32,
    string Md5,
    string Sha1,
    bool Matched,
    string Game,
    string Rom);

internal sealed record CliDiscDatResult(
    string Path,
    bool KnownDump,
    string GameName,
    int MatchedTracks,
    IReadOnlyList<CliDatTrackResult> Tracks);

internal sealed record CliDiscVerifyResult(
    bool Ok,
    string Path,
    int Sectors,
    int SectorsVerified,
    int Tracks,
    long BytesVerified,
    string Hash,
    IReadOnlyList<string> Errors,
    CliDiscDatResult? Dat = null);
