using System.Text.Json.Serialization;

namespace FiveDo.Tas;

/// <summary>
/// Source-generated JSON metadata for movie headers. The CLI and desktop shell publish
/// trimmed builds, so movie headers never use reflection-based serialization.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MovieHeader))]
internal sealed partial class MovieJsonContext : JsonSerializerContext;
