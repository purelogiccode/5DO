using System.Collections.Generic;
using System.Linq;
using FiveDo.Host;

namespace FiveDo.Cli;


internal sealed record CliBiosDiscoveryFolder(
    string Path,
    string Source,
    bool Exists,
    int PrimaryCount,
    int SecondaryCount,
    IReadOnlyList<CliBiosProfileResult> Primaries,
    IReadOnlyList<string> Errors);

internal sealed record CliBiosDiscoveryResult(
    bool Ok,
    bool HasBios,
    string? PreferredPath,
    string? PreferredName,
    string? PreferredRegion,
    int PrimaryCount,
    IReadOnlyList<CliBiosDiscoveryFolder> Folders)
{
    internal static CliBiosDiscoveryResult From(BiosDiscoveryResult result)
    {
        return new CliBiosDiscoveryResult(
            Ok: true,
            HasBios: result.HasBios,
            PreferredPath: result.PreferredPrimary?.Path,
            PreferredName: result.PreferredPrimary?.Name,
            PreferredRegion: result.PreferredPrimary?.RegionLabel,
            PrimaryCount: result.Primaries.Count,
            Folders: result.Folders.Select(folder => new CliBiosDiscoveryFolder(
                Path: folder.Path,
                Source: folder.Source,
                Exists: folder.Exists,
                PrimaryCount: folder.Primaries.Count,
                SecondaryCount: folder.Secondaries.Count,
                Primaries: folder.Primaries.Select(CliBiosProfileResult.FromProfile).ToArray(),
                Errors: folder.Errors)).ToArray());
    }
}
