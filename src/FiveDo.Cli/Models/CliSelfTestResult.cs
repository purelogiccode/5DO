using System.Collections.Generic;
using System.Text.Json.Serialization;
using FiveDo.Host;

namespace FiveDo.Cli;


internal sealed record CliSelfTestResult(bool Ok, IReadOnlyList<CliSelfTestCheck> Checks);
