using System;

namespace FiveDo.Host;


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
