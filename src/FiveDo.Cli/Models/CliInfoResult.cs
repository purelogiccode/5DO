
namespace FiveDo.Cli;


internal sealed record CliInfoResult(
    bool Ok,
    string Path,
    int SectorCount,
    string VolumeLabel,
    uint VolumeBlockCount,
    uint BlockSize,
    string GameId,
    uint FixMode,
    bool Known,
    string Name,
    string Publisher,
    string ReleaseYear,
    string Regions,
    string Revision = "",
    int? DiscNumber = null,
    int? DiscTotal = null,
    string Bios = "",
    string Bios2 = "",
    string InputDevices = "",
    string Nvram = "",
    string Cheats = "");
