namespace FiveDo.Cli;

internal sealed record CliDiscConvertResult(
    bool Ok,
    string Source,
    string Output,
    string Format,
    int Sectors,
    long Bytes,
    string SourceHash,
    string OutputHash);
