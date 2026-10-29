using System.Text.Json.Serialization;

namespace FiveDo.Diagnostics.Statistics;

/// <summary>The JSON body accepted by the ApplicationStats <c>POST /stats</c> endpoint.</summary>
/// <param name="ApplicationId">Stable application identifier, for example <c>5do</c>.</param>
/// <param name="Version">Application version.</param>
public sealed record ApplicationStatsPayload(
    [property: JsonPropertyName("applicationId")] string ApplicationId,
    [property: JsonPropertyName("version")] string Version);
