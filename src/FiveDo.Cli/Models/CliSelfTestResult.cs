using System.Collections.Generic;

namespace FiveDo.Cli;


internal sealed record CliSelfTestResult(bool Ok, IReadOnlyList<CliSelfTestCheck> Checks);
