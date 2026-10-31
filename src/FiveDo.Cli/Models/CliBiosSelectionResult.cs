using System.Collections.Generic;

namespace FiveDo.Cli;


internal sealed record CliBiosSelectionResult(
    bool Ok,
    string GameId,
    string GameName,
    string Regions,
    string Reason,
    CliBiosProfileResult? Primary,
    CliBiosProfileResult? Secondary,
    IReadOnlyList<string> Warnings);
