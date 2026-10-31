using FiveDo.Storage;

namespace FiveDo.Cli;


internal sealed record CliBiosProfileResult(
    string Path,
    string FileName,
    string Md5,
    long Size,
    string Kind,
    string Name,
    string Territory,
    string VideoStandard,
    string Region,
    bool Kanji,
    bool Anvil,
    bool Patched,
    int Preference,
    string Notes)
{
    internal static CliBiosProfileResult FromProfile(BiosProfile profile)
    {
        return new CliBiosProfileResult(
            profile.Path,
            profile.FileName,
            profile.Md5,
            profile.Size,
            profile.Kind.ToString(),
            profile.Name,
            profile.Territory.ToString(),
            profile.VideoStandard.ToString(),
            profile.RegionLabel,
            profile.IsKanji,
            profile.IsAnvil,
            profile.IsPatched,
            profile.Preference,
            profile.Notes);
    }
}
