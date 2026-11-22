using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Machine;

namespace FiveDo.Host;

/// <summary>Describes the result of an autosave restore attempt.</summary>
/// <param name="Loaded">Whether the state was loaded into the machine.</param>
/// <param name="Message">An actionable status or refusal message.</param>
public sealed record AutoSaveLoadResult(bool Loaded, string Message);

/// <summary>
/// Owns the single rolling autosave written by the desktop shell.
/// </summary>
/// <remarks>
/// The autosave lives in its own <c>autosave</c> directory with one private slot, so manual
/// slots are never touched. The state header records the disc and BIOS hashes; a restore is
/// refused when either differs from the current machine. Writes are atomic and reuse the
/// same metadata and thumbnail path as a manual slot.
/// </remarks>
public sealed class AutoSaveManager
{
    private const int AutoSaveSlot = 0;

    /// <summary>Initializes a manager under the configured save-state directory.</summary>
    /// <param name="saveDirectory">Directory that also holds the numbered manual slots.</param>
    /// <exception cref="ArgumentException">Thrown when the directory is empty.</exception>
    public AutoSaveManager(string saveDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        Directory = Path.Combine(Path.GetFullPath(saveDirectory), "autosave");
        _slots = new SaveStateSlotManager(Directory, slotCount: 1);
    }

    /// <summary>Gets the absolute autosave directory.</summary>
    public string Directory { get; }

    private SaveStateSlotManager _slots { get; }

    /// <summary>Gets the state file path.</summary>
    public string StatePath => _slots.GetStatePath(AutoSaveSlot);

    /// <summary>Gets the PNG thumbnail path. The file may not exist.</summary>
    public string ThumbnailPath => _slots.GetThumbnailPath(AutoSaveSlot);

    /// <summary>Gets a value indicating whether an autosave file exists.</summary>
    public bool Exists => _slots.Exists(AutoSaveSlot);

    /// <summary>Inspects the autosave without loading it.</summary>
    /// <returns>The slot information; an empty slot is reported when no file exists.</returns>
    public SaveStateSlotInfo Inspect()
    {
        return _slots.List()[AutoSaveSlot];
    }

    /// <summary>Writes or overwrites the rolling autosave.</summary>
    /// <param name="machine">Machine to save.</param>
    /// <param name="gameId">Disc game ID recorded in the state header.</param>
    /// <param name="gameName">Display name recorded in the sidecar.</param>
    /// <param name="achievementsProgress">Optional RetroAchievements progress section.</param>
    /// <returns>The written slot information.</returns>
    public SaveStateSlotInfo Save(
        FiveDoMachine machine,
        string? gameId = null,
        string? gameName = null,
        byte[]? achievementsProgress = null)
    {
        return _slots.Save(machine, gameId, gameName, AutoSaveSlot, achievementsProgress);
    }

    /// <summary>
    /// Loads the autosave when its disc and BIOS hashes match the current machine.
    /// </summary>
    /// <param name="machine">Machine to load into.</param>
    /// <param name="expectedGameId">Current disc game ID, or empty when unknown.</param>
    /// <param name="expectedBiosHash">Current effective BIOS MD5, or empty when unknown.</param>
    /// <param name="achievementsProgress">
    /// Receives the RetroAchievements section, or <see langword="null"/> when absent.
    /// </param>
    /// <returns>The restore result. A hash or format mismatch is reported, not thrown.</returns>
    public AutoSaveLoadResult TryLoad(
        FiveDoMachine machine,
        string? expectedGameId,
        string? expectedBiosHash,
        out byte[]? achievementsProgress)
    {
        ArgumentNullException.ThrowIfNull(machine);
        achievementsProgress = null;
        if (!Exists)
        {
            return new AutoSaveLoadResult(false, "No autosave is available.");
        }

        StateInfo header;
        try
        {
            header = StateInspector.ReadHeader(StatePath);
        }
        catch (Exception exception) when (exception is InvalidStateException
            or IOException
            or UnauthorizedAccessException)
        {
            return new AutoSaveLoadResult(
                false,
                $"The autosave could not be read: {exception.Message}");
        }

        if (!string.IsNullOrWhiteSpace(expectedGameId)
            && !string.IsNullOrWhiteSpace(header.DiscHash)
            && !string.Equals(header.DiscHash, expectedGameId, StringComparison.OrdinalIgnoreCase))
        {
            return new AutoSaveLoadResult(
                false,
                $"The autosave belongs to a different disc ({ShortHash(header.DiscHash)}). "
                + "Start the current disc and save again, or delete the autosave.");
        }

        if (!string.IsNullOrWhiteSpace(expectedBiosHash)
            && !string.IsNullOrWhiteSpace(header.BiosHash)
            && !string.Equals(header.BiosHash, expectedBiosHash, StringComparison.OrdinalIgnoreCase))
        {
            return new AutoSaveLoadResult(
                false,
                $"The autosave was written with a different BIOS ({ShortHash(header.BiosHash)}). "
                + "Load that BIOS or delete the autosave.");
        }

        try
        {
            SaveStateSlotInfo info = _slots.LoadWithProgress(
                machine,
                AutoSaveSlot,
                expectedGameId,
                out achievementsProgress);
            return new AutoSaveLoadResult(
                true,
                $"Resumed autosave at frame {info.FramesExecuted}.");
        }
        catch (Exception exception) when (exception is InvalidStateException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            achievementsProgress = null;
            return new AutoSaveLoadResult(
                false,
                $"The autosave could not be loaded: {exception.Message}");
        }
    }

    /// <summary>Deletes the autosave state, sidecar, and thumbnail.</summary>
    /// <returns><see langword="true"/> when a state file was removed.</returns>
    public bool Delete()
    {
        return _slots.Delete(AutoSaveSlot);
    }

    private static string ShortHash(string hash)
    {
        return hash.Length <= 8 ? hash : hash[..8];
    }
}
