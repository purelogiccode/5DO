using System.Collections.Generic;
using System.Text.Json.Serialization;
using FiveDo.Host;

namespace FiveDo.Cli;


internal sealed record CliFsExtractResult(bool Ok, int Extracted, string Output);
