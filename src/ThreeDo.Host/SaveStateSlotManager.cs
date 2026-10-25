using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThreeDo.Core;
using ThreeDo.Machine;

namespace ThreeDo.Host;

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
public sealed record SaveStateSlotInfo(
    int Slot,
    string StatePath,
    bool Exists,
    string GameId,
    string GameName,
    DateTimeOffset? SavedAtUtc,
    long FramesExecuted,
    long FileSize,
    string Error)
{
    /// <summary>Gets the one-based slot number used by the UI.</summary>
    public int DisplaySlot => Slot + 1;

    /// <summary>Gets the game name, or a placeholder for an unnamed state.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(GameName) ? "(unnamed)" : GameName;

    /// <summary>Gets the local save time, or an empty string when metadata is missing.</summary>
    public string DisplaySavedAt => SavedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
}

/// <summary>
/// Manages numbered quick-save slots under one directory.
/// </summary>
/// <remarks>
/// Each slot is a native <c>.tdos</c> state plus an optional JSON sidecar with the game
/// name and save time. The state header remains authoritative for the game ID and frame
/// count; the sidecar is metadata only and may be missing for states copied from elsewhere.
/// Writes are atomic and loading never mutates the slot index on failure.
/// </remarks>
public sealed class SaveStateSlotManager
{
    private const string StateExtension = ".tdos";
    private const string MetadataExtension = ".json";

    private static readonly JsonSerializerOptions MetadataOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Initializes a new instance of the <see cref="SaveStateSlotManager"/> class.</summary>
    /// <param name="directory">Directory that holds the slot files.</param>
    /// <param name="slotCount">Number of slots. Defaults to ten.</param>
    /// <exception cref="ArgumentException">Thrown when the directory is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the slot count is not positive.</exception>
    public SaveStateSlotManager(string directory, int slotCount = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(slotCount, 1);
        Directory = Path.GetFullPath(directory);
        SlotCount = slotCount;
    }

    /// <summary>Gets the absolute slot directory.</summary>
    public string Directory { get; }

    /// <summary>Gets the number of slots.</summary>
    public int SlotCount { get; }

    /// <summary>Gets the current slot. Use <see cref="Select"/>, <see cref="Next"/>, or <see cref="Previous"/> to change it.</summary>
    public int CurrentSlot { get; private set; }

    /// <summary>Gets the state file path for a slot.</summary>
    /// <param name="slot">Zero-based slot number.</param>
    /// <returns>The state path.</returns>
    public string GetStatePath(int slot)
    {
        ValidateSlot(slot);
        return Path.Combine(Directory, $"slot-{slot:D2}{StateExtension}");
    }

    /// <summary>Gets the metadata sidecar path for a slot.</summary>
    /// <param name="slot">Zero-based slot number.</param>
    /// <returns>The metadata path.</returns>
    public string GetMetadataPath(int slot)
    {
        ValidateSlot(slot);
        return Path.Combine(Directory, $"slot-{slot:D2}{MetadataExtension}");
    }

    /// <summary>Checks whether a slot contains a state file.</summary>
    /// <param name="slot">Zero-based slot number.</param>
    /// <returns><see langword="true"/> when the state file exists.</returns>
    public bool Exists(int slot) => File.Exists(GetStatePath(slot));

    /// <summary>Selects a slot.</summary>
    /// <param name="slot">Zero-based slot number.</param>
    /// <returns>The selected slot.</returns>
    public int Select(int slot)
    {
        ValidateSlot(slot);
        CurrentSlot = slot;
        return CurrentSlot;
    }

    /// <summary>Advances to the next slot, wrapping at the end.</summary>
    /// <param name="existingOnly">When true, skip empty slots; the current slot is kept when none exist.</param>
    /// <returns>The selected slot.</returns>
    public int Next(bool existingOnly = false) => Move(1, existingOnly);

    /// <summary>Moves to the previous slot, wrapping at the start.</summary>
    /// <param name="existingOnly">When true, skip empty slots; the current slot is kept when none exist.</param>
    /// <returns>The selected slot.</returns>
    public int Previous(bool existingOnly = false) => Move(-1, existingOnly);

    /// <summary>Saves the machine to a slot and writes its metadata.</summary>
    /// <param name="machine">Machine to save.</param>
    /// <param name="gameId">Game ID stored in the state header.</param>
    /// <param name="gameName">Display name stored in the sidecar.</param>
    /// <param name="slot">Slot to write, or <see langword="null"/> for <see cref="CurrentSlot"/>.</param>
    /// <returns>The written slot information.</returns>
    public SaveStateSlotInfo Save(
        ThreeDoMachine machine,
        string? gameId = null,
        string? gameName = null,
        int? slot = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var target = slot ?? CurrentSlot;
        ValidateSlot(target);
        System.IO.Directory.CreateDirectory(Directory);

        machine.SaveStateToFile(GetStatePath(target), gameId);
        var metadata = new SaveStateSlotMetadata(
            target,
            gameId ?? string.Empty,
            gameName ?? string.Empty,
            DateTimeOffset.UtcNow,
            machine.FramesExecuted);
        WriteMetadata(target, metadata);
        CurrentSlot = target;
        return Inspect(target);
    }

    /// <summary>Loads a slot into the machine.</summary>
    /// <param name="machine">Machine to load.</param>
    /// <param name="slot">Slot to read, or <see langword="null"/> for <see cref="CurrentSlot"/>.</param>
    /// <returns>The loaded slot information.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the slot has no state file.</exception>
    public SaveStateSlotInfo Load(ThreeDoMachine machine, int? slot = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var target = slot ?? CurrentSlot;
        ValidateSlot(target);
        var path = GetStatePath(target);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Save slot {target} is empty.", path);
        }

        machine.LoadStateFromFile(path);
        CurrentSlot = target;
        return Inspect(target);
    }

    /// <summary>Deletes a slot's state and metadata.</summary>
    /// <param name="slot">Zero-based slot number.</param>
    /// <returns><see langword="true"/> when a state file was removed.</returns>
    public bool Delete(int slot)
    {
        ValidateSlot(slot);
        var statePath = GetStatePath(slot);
        var removed = File.Exists(statePath);
        File.Delete(statePath);
        File.Delete(GetMetadataPath(slot));
        return removed;
    }

    /// <summary>Lists every slot, including empty ones.</summary>
    /// <returns>Slot information ordered by slot number.</returns>
    public IReadOnlyList<SaveStateSlotInfo> List()
    {
        var result = new List<SaveStateSlotInfo>(SlotCount);
        for (var slot = 0; slot < SlotCount; slot++)
        {
            result.Add(Inspect(slot));
        }

        return result;
    }

    private SaveStateSlotInfo Inspect(int slot)
    {
        var statePath = GetStatePath(slot);
        if (!File.Exists(statePath))
        {
            return new SaveStateSlotInfo(slot, statePath, false, string.Empty, string.Empty, null, -1, 0, string.Empty);
        }

        var info = new FileInfo(statePath);
        var metadata = ReadMetadata(slot);
        try
        {
            var state = StateInspector.ReadHeader(statePath);
            return new SaveStateSlotInfo(
                slot,
                statePath,
                true,
                state.DiscHash,
                metadata?.GameName ?? string.Empty,
                metadata?.SavedAtUtc,
                state.FramesExecuted,
                info.Length,
                string.Empty);
        }
        catch (Exception exception) when (exception is InvalidStateException or IOException or UnauthorizedAccessException)
        {
            return new SaveStateSlotInfo(
                slot,
                statePath,
                true,
                metadata?.GameId ?? string.Empty,
                metadata?.GameName ?? string.Empty,
                metadata?.SavedAtUtc,
                -1,
                info.Length,
                exception.Message);
        }
    }

    private int Move(int delta, bool existingOnly)
    {
        if (!existingOnly)
        {
            CurrentSlot = (CurrentSlot + delta + SlotCount) % SlotCount;
            return CurrentSlot;
        }

        for (var step = 1; step <= SlotCount; step++)
        {
            var candidate = (CurrentSlot + (delta * step) + (SlotCount * SlotCount)) % SlotCount;
            if (Exists(candidate))
            {
                CurrentSlot = candidate;
                return CurrentSlot;
            }
        }

        return CurrentSlot;
    }

    private void WriteMetadata(int slot, SaveStateSlotMetadata metadata)
    {
        var path = GetMetadataPath(slot);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(metadata, MetadataOptions));
        File.Move(temporary, path, overwrite: true);
    }

    private SaveStateSlotMetadata? ReadMetadata(int slot)
    {
        var path = GetMetadataPath(slot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SaveStateSlotMetadata>(File.ReadAllText(path), MetadataOptions);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void ValidateSlot(int slot)
    {
        if (slot < 0 || slot >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"The slot must be between 0 and {SlotCount - 1}.");
        }
    }

    private sealed record SaveStateSlotMetadata(
        int Slot,
        string GameId,
        string GameName,
        DateTimeOffset SavedAtUtc,
        long FramesExecuted);
}
