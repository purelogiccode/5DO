using System.Collections.Generic;
using System.Text.Json.Serialization;
using FiveDo.Host;

namespace FiveDo.Cli;


internal sealed record CliFsListEntry(string Path, bool Directory, ulong Size, uint Lba);
