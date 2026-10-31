using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>
/// The BIOS set selected for a disc by <see cref="BiosResolver"/>.
/// </summary>
/// <param name="PrimaryPath">Selected primary BIOS path, or <see langword="null"/>.</param>
/// <param name="SecondaryPath">Selected BIOS2/font path, or an empty string.</param>
/// <param name="Source">Selection source: <c>override</c>, <c>auto</c>, <c>configured</c>, or <c>none</c>.</param>
/// <param name="Reason">Human-readable selection explanation.</param>
/// <param name="Primary">Primary profile metadata when the image came from a library.</param>
/// <param name="Secondary">Secondary profile metadata when the image came from a library.</param>
/// <param name="Warnings">Non-fatal selection problems, such as a missing override or folder.</param>
public sealed record ResolvedBios(
    string? PrimaryPath,
    string SecondaryPath,
    string Source,
    string Reason,
    BiosProfile? Primary,
    BiosProfile? Secondary,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Gets a value indicating whether a primary BIOS was resolved.</summary>
    public bool HasPrimary => !string.IsNullOrWhiteSpace(PrimaryPath);

    /// <summary>Creates an empty resolution.</summary>
    /// <param name="reason">Why no BIOS was resolved.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>An empty resolution.</returns>
    public static ResolvedBios None(string reason, IReadOnlyList<string>? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new ResolvedBios(null, string.Empty, "none", reason, null, null, warnings ?? []);
    }
}

/// <summary>
/// Resolves the BIOS set for a game from per-game overrides, automatic region selection,
/// or the configured fallback path.
/// </summary>
public static class BiosResolver
{
    /// <summary>Resolves a BIOS set.</summary>
    /// <param name="settings">Current settings.</param>
    /// <param name="gameId">Current 4DO game ID, or <see langword="null"/>.</param>
    /// <param name="game">Current game metadata, or <see langword="null"/>.</param>
    /// <param name="discName">Current disc path or file name, used for known-title requirements.</param>
    /// <param name="library">Optional pre-scanned library; otherwise the configured folder is scanned.</param>
    /// <returns>The selected BIOS set. Missing files and folders produce warnings, not exceptions.</returns>
    public static ResolvedBios Resolve(
        AppSettings settings,
        string? gameId,
        GameRecord? game,
        string? discName = null,
        BiosLibrary? library = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var warnings = new List<string>();
        var overrideMd5 = settings.GetBiosOverride(gameId);
        var autoFolder = settings.AutoBios && !string.IsNullOrWhiteSpace(settings.BiosFolder);
        var folder = ResolveBiosFolder(settings);
        if (autoFolder && folder is not null && !Directory.Exists(folder))
        {
            warnings.Add($"BIOS folder '{folder}' does not exist.");
        }

        if (library is null
            && folder is not null
            && (autoFolder || !string.IsNullOrWhiteSpace(overrideMd5))
            && Directory.Exists(folder))
        {
            try
            {
                library = BiosLibrary.Scan(folder);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                warnings.Add($"BIOS folder '{folder}' could not be scanned: {exception.Message}");
            }
        }
        if (!string.IsNullOrWhiteSpace(overrideMd5))
        {
            BiosProfile? overridePrimary = library?.FindPrimary(overrideMd5);
            if (overridePrimary is not null)
            {
                BiosSelection selection = library!.SelectForGame(
                    game,
                    new BiosSelectionOptions(PreferredMd5: overrideMd5, DiscName: discName));
                return new ResolvedBios(
                    selection.Primary?.Path ?? overridePrimary.Path,
                    selection.Secondary?.Path ?? ResolveConfiguredSecondary(settings),
                    "override",
                    selection.Reason,
                    selection.Primary ?? overridePrimary,
                    selection.Secondary,
                    warnings);
            }

            warnings.Add($"The per-game BIOS override {overrideMd5[..Math.Min(8, overrideMd5.Length)]} was not found in the BIOS folder.");
        }

        if (autoFolder && library is not null)
        {
            BiosSelection selection = library.SelectForGame(
                game,
                new BiosSelectionOptions(DiscName: discName));
            if (selection.HasBios)
            {
                return new ResolvedBios(
                    selection.Primary!.Path,
                    selection.Secondary?.Path ?? string.Empty,
                    "auto",
                    selection.Reason,
                    selection.Primary,
                    selection.Secondary,
                    warnings);
            }

            warnings.Add(selection.Reason);
        }

        if (!string.IsNullOrWhiteSpace(settings.BiosPath) && File.Exists(settings.BiosPath))
        {
            return new ResolvedBios(
                settings.BiosPath,
                ResolveConfiguredSecondary(settings),
                "configured",
                "Using the configured BIOS.",
                Primary: null,
                Secondary: null,
                warnings);
        }

        if (!string.IsNullOrWhiteSpace(settings.BiosPath))
        {
            warnings.Add($"The configured BIOS '{settings.BiosPath}' does not exist.");
        }

        return ResolvedBios.None("No usable BIOS was configured or selected.", warnings);
    }

    private static string? ResolveBiosFolder(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BiosFolder))
        {
            return settings.BiosFolder;
        }

        if (!string.IsNullOrWhiteSpace(settings.BiosPath))
        {
            return Path.GetDirectoryName(Path.GetFullPath(settings.BiosPath));
        }

        return null;
    }

    private static string ResolveConfiguredSecondary(AppSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.Bios2Path) && File.Exists(settings.Bios2Path)
            ? settings.Bios2Path
            : string.Empty;
    }
}
