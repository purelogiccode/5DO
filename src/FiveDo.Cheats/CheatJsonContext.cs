using System.Text.Json.Serialization;

namespace FiveDo.Cheats;

/// <summary>Source-generated JSON metadata for per-game cheat files.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CheatDocument))]
public sealed partial class CheatJsonContext : JsonSerializerContext;
