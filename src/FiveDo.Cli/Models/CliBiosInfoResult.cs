
namespace FiveDo.Cli;


internal sealed record CliBiosInfoResult(
    bool Ok,
    string Path,
    int Size,
    string Md5,
    bool Anvil,
    int AnvilKind,
    string Name,
    string Kind,
    string Territory,
    string VideoStandard,
    string Region,
    bool Kanji,
    bool Patched,
    int Preference,
    string Notes,
    string Md5Bios2);
