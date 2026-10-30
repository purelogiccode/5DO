
namespace FiveDo.Cli;


internal sealed record CliBiosInfoResult(
    bool Ok,
    string Path,
    int Size,
    string Md5,
    bool Anvil,
    int AnvilKind);
