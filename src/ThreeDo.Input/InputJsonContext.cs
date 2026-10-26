using System.Text.Json.Serialization;

namespace ThreeDo.Input;

/// <summary>
/// Source-generated JSON metadata for input-profile files.
/// </summary>
/// <remarks>
/// Serializing through this context keeps input profiles trim-safe; do not add
/// reflection-based <c>JsonSerializer</c> overloads to the profile store.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(InputProfile))]
internal sealed partial class InputProfileJsonContext : JsonSerializerContext;
