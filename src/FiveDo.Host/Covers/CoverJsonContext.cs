using System.Text.Json.Serialization;

namespace FiveDo.Host;

/// <summary>Source-generated JSON metadata for the cover catalog and cache sidecar.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CoverCatalog))]
[JsonSerializable(typeof(CoverCatalogCacheMetadata))]
internal sealed partial class CoverCatalogJsonContext : JsonSerializerContext;
