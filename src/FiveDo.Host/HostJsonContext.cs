using System;
using System.Text.Json.Serialization;

namespace FiveDo.Host;

/// <summary>
/// Source-generated JSON metadata for host settings files.
/// </summary>
/// <remarks>
/// The contexts in this file keep settings and save-slot metadata trim-safe. All
/// host JSON entry points must serialize through these contexts rather than the
/// reflection-based <c>JsonSerializer</c> overloads.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;

/// <summary>
/// Source-generated JSON metadata for save-slot sidecars.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SaveStateSlotMetadata))]
internal sealed partial class SaveStateSlotJsonContext : JsonSerializerContext;

/// <summary>
/// Metadata written next to a <c>.tdos</c> save-state file.
/// </summary>
/// <param name="Slot">Zero-based slot number.</param>
/// <param name="GameId">Recorded game ID.</param>
/// <param name="GameName">Recorded display name.</param>
/// <param name="SavedAtUtc">Save time in UTC.</param>
/// <param name="FramesExecuted">Frame count at save time.</param>
internal sealed record SaveStateSlotMetadata(
    int Slot,
    string GameId,
    string GameName,
    DateTimeOffset SavedAtUtc,
    long FramesExecuted);
