using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>One folder that BIOS discovery inspects, and why it is inspected.</summary>
/// <param name="Path">Folder path.</param>
/// <param name="Source">Human-readable origin such as "5DO data folder".</param>
public sealed record BiosDiscoveryLocation(string Path, string Source);

/// <summary>The BIOS images found in one discovery folder.</summary>
/// <param name="Path">Absolute folder path.</param>
/// <param name="Source">Human-readable origin of the folder.</param>
/// <param name="Exists">Whether the folder exists.</param>
/// <param name="Primaries">Recognized primary BIOS images.</param>
/// <param name="Secondaries">Recognized BIOS2/Kanji images.</param>
/// <param name="Errors">Files that could not be classified, or a folder read error.</param>
public sealed record BiosDiscoveryFolder(
    string Path,
    string Source,
    bool Exists,
    IReadOnlyList<BiosProfile> Primaries,
    IReadOnlyList<BiosProfile> Secondaries,
    IReadOnlyList<string> Errors);

/// <summary>The result of scanning the default BIOS locations.</summary>
/// <param name="Folders">Per-folder results, in priority order.</param>
/// <param name="PreferredPrimary">The first primary image found in the highest-priority folder.</param>
public sealed record BiosDiscoveryResult(
    IReadOnlyList<BiosDiscoveryFolder> Folders,
    BiosProfile? PreferredPrimary)
{
    /// <summary>Gets a value indicating whether any usable primary BIOS was found.</summary>
    public bool HasBios => PreferredPrimary is not null;

    /// <summary>Gets every primary image found across all folders.</summary>
    public IReadOnlyList<BiosProfile> Primaries =>
        Folders.SelectMany(folder => folder.Primaries).ToArray();

    /// <summary>Gets every folder that exists.</summary>
    public IReadOnlyList<BiosDiscoveryFolder> ExistingFolders =>
        Folders.Where(folder => folder.Exists).ToArray();
}

/// <summary>
/// Finds BIOS images without asking the user to navigate a file picker first.
/// </summary>
/// <remarks>
/// 5DO never downloads or ships BIOS ROMs. Discovery only looks at folders the user
/// controls, in a fixed priority order, and classifies whatever it finds with
/// <see cref="BiosLibrary"/>. A missing folder is reported as missing rather than as an
/// error, because most of the default locations do not exist on a fresh install.
/// </remarks>
public static class BiosDiscovery
{
    /// <summary>The environment variable that adds a highest-priority BIOS folder.</summary>
    public const string BiosDirectoryEnvironmentVariable = "FIVEDO_BIOS_DIR";

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Returns the folders that are searched by default, highest priority first.
    /// </summary>
    /// <param name="overrideDirectory">
    /// Explicit highest-priority folder. When omitted, <c>FIVEDO_BIOS_DIR</c> is used.
    /// </param>
    /// <param name="dataDirectory">Data folder. Defaults to <see cref="PortablePaths.DataDirectory"/>.</param>
    /// <param name="applicationDirectory">
    /// Application folder. Defaults to <see cref="PortablePaths.ApplicationDirectory"/>.
    /// </param>
    /// <param name="userProfile">Home folder. Defaults to the current user's profile.</param>
    /// <param name="documentsDirectory">Documents folder. Defaults to the current user's Documents.</param>
    /// <returns>Distinct folders in search order.</returns>
    public static IReadOnlyList<BiosDiscoveryLocation> DefaultFolders(
        string? overrideDirectory = null,
        string? dataDirectory = null,
        string? applicationDirectory = null,
        string? userProfile = null,
        string? documentsDirectory = null)
    {
        overrideDirectory ??= Environment.GetEnvironmentVariable(BiosDirectoryEnvironmentVariable);
        dataDirectory ??= SafeResolve(() => PortablePaths.DataDirectory);
        applicationDirectory ??= SafeResolve(() => PortablePaths.ApplicationDirectory);
        userProfile ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        documentsDirectory ??= Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var candidates = new List<BiosDiscoveryLocation>();
        Add(candidates, overrideDirectory, BiosDirectoryEnvironmentVariable);
        Add(candidates, dataDirectory is null ? null : Path.Combine(dataDirectory, "bios"), "5DO data folder");
        Add(candidates, applicationDirectory is null ? null : Path.Combine(applicationDirectory, "bios"), "next to the executable");
        Add(candidates, string.IsNullOrEmpty(userProfile) ? null : Path.Combine(userProfile, "BIOS"), "home folder");
        Add(candidates, string.IsNullOrEmpty(documentsDirectory) ? null : Path.Combine(documentsDirectory, "BIOS"), "documents folder");

        return Distinct(candidates);
    }

    /// <summary>Scans the default folders.</summary>
    /// <param name="recursive">Whether subfolders are scanned.</param>
    /// <returns>The discovery result.</returns>
    public static BiosDiscoveryResult DiscoverDefault(bool recursive = false) =>
        Discover(DefaultFolders(), recursive);

    /// <summary>Scans explicit folders.</summary>
    /// <param name="locations">Folders to scan, highest priority first.</param>
    /// <param name="recursive">Whether subfolders are scanned.</param>
    /// <returns>The discovery result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="locations"/> is null.</exception>
    public static BiosDiscoveryResult Discover(
        IEnumerable<BiosDiscoveryLocation> locations,
        bool recursive = false)
    {
        ArgumentNullException.ThrowIfNull(locations);

        var folders = new List<BiosDiscoveryFolder>();
        foreach (BiosDiscoveryLocation location in locations)
        {
            if (string.IsNullOrWhiteSpace(location.Path))
            {
                continue;
            }

            string path;
            try
            {
                path = Path.GetFullPath(location.Path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Serilog.Log.Debug(exception, "Skipping an invalid BIOS discovery path {Path}.", location.Path);
                continue;
            }

            if (!Directory.Exists(path))
            {
                folders.Add(new BiosDiscoveryFolder(path, location.Source, false, [], [], []));
                continue;
            }

            try
            {
                BiosLibrary library = BiosLibrary.Scan(path, recursive: recursive);
                folders.Add(new BiosDiscoveryFolder(
                    path,
                    location.Source,
                    true,
                    library.Primaries,
                    library.Secondaries,
                    library.Errors));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException
                or ArgumentException)
            {
                folders.Add(new BiosDiscoveryFolder(path, location.Source, true, [], [], [exception.Message]));
            }
        }

        BiosProfile? preferred = null;
        foreach (BiosDiscoveryFolder folder in folders)
        {
            if (folder.Primaries.Count == 0)
            {
                continue;
            }

            preferred = folder.Primaries
                .OrderByDescending(profile => profile.Preference)
                .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .First();
            break;
        }

        return new BiosDiscoveryResult(folders, preferred);
    }

    private static void Add(List<BiosDiscoveryLocation> candidates, string? path, string source)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.Add(new BiosDiscoveryLocation(path, source));
        }
    }

    private static List<BiosDiscoveryLocation> Distinct(List<BiosDiscoveryLocation> candidates)
    {
        var seen = new HashSet<string>(PathComparer);
        var result = new List<BiosDiscoveryLocation>(candidates.Count);
        foreach (BiosDiscoveryLocation candidate in candidates)
        {
            string full;
            try
            {
                full = Path.GetFullPath(candidate.Path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Serilog.Log.Debug(exception, "Skipping a duplicate BIOS location that is not a valid path.");
                continue;
            }

            if (seen.Add(full))
            {
                result.Add(candidate with { Path = full });
            }
        }

        return result;
    }

    private static string? SafeResolve(Func<string> resolve)
    {
        try
        {
            return resolve();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException)
        {
            Serilog.Log.Debug(exception, "A default BIOS folder could not be resolved.");
            return null;
        }
    }
}
