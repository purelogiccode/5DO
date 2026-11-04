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
[JsonSerializable(typeof(CliFiqResult))]
[JsonSerializable(typeof(CliDspResult))]
[JsonSerializable(typeof(CliCheckpointResult))]
[JsonSerializable(typeof(IReadOnlyList<CliCheckpointResult>))]
[JsonSerializable(typeof(CliInfoResult))]
[JsonSerializable(typeof(CliDiscTracksResult))]
[JsonSerializable(typeof(CliDiscPlaylistResult))]
[JsonSerializable(typeof(CliDiscVerifyResult))]
[JsonSerializable(typeof(CliLibraryVerifyResult))]
[JsonSerializable(typeof(CliDiscDatResult))]
[JsonSerializable(typeof(CliDatTrackResult))]
[JsonSerializable(typeof(CliSettingsValidationResult))]
[JsonSerializable(typeof(CliSettingsMigrationResult))]
[JsonSerializable(typeof(CliSettingsProfilesResult))]
[JsonSerializable(typeof(IReadOnlyList<CliSettingsProfile>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(CliBiosInfoResult))]
[JsonSerializable(typeof(CliBiosProfileResult))]
[JsonSerializable(typeof(CliBiosScanResult))]
[JsonSerializable(typeof(CliBiosSelectionResult))]
[JsonSerializable(typeof(CliAudioResult))]
[JsonSerializable(typeof(CliDiagnosticsBundleResult))]
[JsonSerializable(typeof(CliAudioDevicesResult))]
[JsonSerializable(typeof(CliBenchResult))]
[JsonSerializable(typeof(CliSelfTestResult))]
[JsonSerializable(typeof(CliFsListResult))]
[JsonSerializable(typeof(CliFsExtractResult))]
[JsonSerializable(typeof(CliNvramPathResult))]
[JsonSerializable(typeof(CliNvramListResult))]
[JsonSerializable(typeof(CliNvramActionResult))]
[JsonSerializable(typeof(CliCheatListResult))]
[JsonSerializable(typeof(CliCheatActionResult))]
[JsonSerializable(typeof(CliMovieInfoResult))]
[JsonSerializable(typeof(CliMovieActionResult))]
[JsonSerializable(typeof(CliUpdateResult))]
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
[JsonSerializable(typeof(StateDiffResult))]
[JsonSerializable(typeof(List<SaveStateSlotInfo>))]
[JsonSerializable(typeof(IReadOnlyList<SaveStateSlotInfo>))]
internal sealed partial class CliHostJsonContext : JsonSerializerContext;
