using System.Collections.Generic;
using System.Text.Json.Serialization;
using FiveDo.Host;

namespace FiveDo.Cli;

/// <summary>
/// Source-generated JSON metadata for every CLI response.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CliErrorResult))]
[JsonSerializable(typeof(CliRunResult))]
[JsonSerializable(typeof(CliInfoResult))]
[JsonSerializable(typeof(CliBiosInfoResult))]
[JsonSerializable(typeof(CliBiosProfileResult))]
[JsonSerializable(typeof(CliBiosScanResult))]
[JsonSerializable(typeof(CliBiosSelectionResult))]
[JsonSerializable(typeof(CliAudioResult))]
[JsonSerializable(typeof(CliBenchResult))]
[JsonSerializable(typeof(CliSelfTestResult))]
[JsonSerializable(typeof(CliFsListResult))]
[JsonSerializable(typeof(CliFsExtractResult))]
internal sealed partial class CliJsonContext : JsonSerializerContext;

/// <summary>
/// Source-generated JSON metadata for host records that the CLI emits verbatim.
/// </summary>
/// <remarks>
/// This context intentionally keeps the default PascalCase property names so the
/// historical <c>state inspect</c> and <c>state slots</c> contracts do not change.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(StateInfo))]
[JsonSerializable(typeof(List<SaveStateSlotInfo>))]
[JsonSerializable(typeof(IReadOnlyList<SaveStateSlotInfo>))]
internal sealed partial class CliHostJsonContext : JsonSerializerContext;
