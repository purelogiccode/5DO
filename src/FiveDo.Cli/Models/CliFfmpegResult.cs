namespace FiveDo.Cli;

/// <summary>Result of the <c>ffmpeg</c> command.</summary>
/// <param name="Ok">Whether FFmpeg is installed and usable.</param>
/// <param name="Path">Installed executable path.</param>
/// <param name="Version">FFmpeg version.</param>
/// <param name="Platform">Pinned platform description.</param>
/// <param name="SourceUrl">Download source.</param>
/// <param name="Sha256">Verified SHA-256.</param>
/// <param name="Bytes">Executable size in bytes.</param>
/// <param name="Error">Failure detail, or null.</param>
internal sealed record CliFfmpegResult(
    bool Ok,
    string? Path,
    string? Version,
    string? Platform,
    string? SourceUrl,
    string? Sha256,
    long? Bytes,
    string? Error);
