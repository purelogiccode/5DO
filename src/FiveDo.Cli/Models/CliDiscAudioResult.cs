namespace FiveDo.Cli;

/// <summary>Result of extracting one CD-DA track to a WAV file.</summary>
internal sealed record CliDiscAudioResult(
    bool Ok,
    string Path,
    int Track,
    long Frames,
    long Bytes,
    string Output);
