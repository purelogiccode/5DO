using System.Text.Json.Serialization;

namespace FiveDo.DiffHarness;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(DiffManifest))]
[JsonSerializable(typeof(DiffDefaults))]
[JsonSerializable(typeof(DiffTitle))]
[JsonSerializable(typeof(DiffMachineOptions))]
[JsonSerializable(typeof(DiffCompareOptions))]
[JsonSerializable(typeof(DiffAllowance))]
[JsonSerializable(typeof(CaptureResult))]
[JsonSerializable(typeof(CaptureSample))]
[JsonSerializable(typeof(DiffReport))]
[JsonSerializable(typeof(DiffProvenance))]
[JsonSerializable(typeof(DiffTitleResult))]
[JsonSerializable(typeof(DiffFirstDivergence))]
[JsonSerializable(typeof(DiffCheckpointResult))]
[JsonSerializable(typeof(DiffFieldResult))]
[JsonSerializable(typeof(DiffSummary))]
[JsonSerializable(typeof(FiveDoRunJson))]
[JsonSerializable(typeof(FiveDoCheckpointJson))]
internal sealed partial class DiffJsonContext : JsonSerializerContext;
