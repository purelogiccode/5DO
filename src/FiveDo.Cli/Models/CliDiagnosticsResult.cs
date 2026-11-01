using System.Collections.Generic;

namespace FiveDo.Cli;

internal sealed record CliDiagnosticsBundleResult(
    bool Ok,
    string Path,
    IReadOnlyList<string> Entries,
    int ScrubHits);
