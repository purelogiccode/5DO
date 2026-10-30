using System;
using System.Globalization;

namespace FiveDo.Host;


/// <summary>Describes one save-state slot.</summary>
/// <param name="Slot">Zero-based slot number.</param>
/// <param name="StatePath">State file path.</param>
/// <param name="Exists">Whether a state file exists in the slot.</param>
/// <param name="GameId">Recorded game ID, or empty.</param>
/// <param name="GameName">Recorded display name, or empty.</param>
/// <param name="SavedAtUtc">Metadata write time, or <see langword="null"/> for states without metadata.</param>
/// <param name="FramesExecuted">Recorded frame count, or -1 when the state cannot be read.</param>
/// <param name="FileSize">State file size in bytes, or 0.</param>
/// <param name="Error">Why the state could not be inspected, or empty.</param>
/// <param name="ThumbnailPath">PNG thumbnail path, or empty when no thumbnail exists.</param>
public sealed record SaveStateSlotInfo(
    int Slot,
    string StatePath,
    bool Exists,
    string GameId,
    string GameName,
    DateTimeOffset? SavedAtUtc,
    long FramesExecuted,
    long FileSize,
    string Error,
    string ThumbnailPath = "")
{
    /// <summary>Gets the one-based slot number used by the UI.</summary>
    public int DisplaySlot => Slot + 1;

    /// <summary>Gets the game name, or a placeholder for an unnamed state.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(GameName) ? "(unnamed)" : GameName;

    /// <summary>Gets the local save time, or an empty string when metadata is missing.</summary>
    public string DisplaySavedAt => SavedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Gets a value indicating whether a screenshot thumbnail is available.</summary>
    public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailPath);
}
