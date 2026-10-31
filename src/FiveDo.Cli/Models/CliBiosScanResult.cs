using System.Collections.Generic;

namespace FiveDo.Cli;


internal sealed record CliBiosScanResult(
    bool Ok,
    string Folder,
    int PrimaryCount,
    int SecondaryCount,
    int ErrorCount,
    IReadOnlyList<CliBiosProfileResult> Profiles,
    IReadOnlyList<string> Errors);
