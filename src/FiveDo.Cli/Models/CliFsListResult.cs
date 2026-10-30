using System.Collections.Generic;

namespace FiveDo.Cli;


internal sealed record CliFsListResult(bool Ok, IReadOnlyList<CliFsListEntry> Entries);
