
namespace FiveDo.Cli;


/// <summary>
/// Structured JSON responses emitted by the headless CLI.
/// </summary>
/// <remarks>
/// These records exist so the CLI can serialize through source-generated metadata
/// instead of reflection. Keep the property names in sync with the documented
/// <c>--json</c> contract; the context uses camelCase to preserve the historical
/// lower-camel field names.
/// </remarks>
internal sealed record CliErrorResult(bool Ok, string Error);
