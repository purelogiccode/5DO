using System.Collections.Generic;

namespace FiveDo.Cli;

internal sealed record CliDiscVerifyResult(
    bool Ok,
    string Path,
    int Sectors,
    int SectorsVerified,
    int Tracks,
    long BytesVerified,
    string Hash,
    IReadOnlyList<string> Errors);
