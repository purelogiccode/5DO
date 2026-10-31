using System.Text.Json.Serialization;

namespace FiveDo.Diagnostics.Statistics;

/// <summary>Source-generated JSON metadata for the trim-safe stats payload.</summary>
[JsonSerializable(typeof(ApplicationStatsPayload))]
internal sealed partial class ApplicationStatsJsonContext : JsonSerializerContext;
