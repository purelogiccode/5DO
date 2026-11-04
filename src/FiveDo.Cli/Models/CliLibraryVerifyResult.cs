using System.Collections.Generic;

namespace FiveDo.Cli;

internal sealed record CliLibraryVerifyEntry(
    string Path,
    string Status,
    string GameName,
    string RomName,
    int Tracks,
    int MatchedTracks,
    string Crc32,
    string Md5,
    string Sha1,
    string Detail);

internal sealed record CliLibraryVerifyResult(
    bool Ok,
    string DatPath,
    int Total,
    int Verified,
    int Mismatch,
    int ReadErrors,
    int NoDat,
    IReadOnlyList<CliLibraryVerifyEntry> Entries);
