
namespace FiveDo.Cli;


internal sealed record CliFsListEntry(string Path, bool Directory, ulong Size, uint Lba);
