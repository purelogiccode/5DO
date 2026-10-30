
namespace FiveDo.Cli;


internal sealed record CliAudioResult(
    bool Ok,
    string Path,
    int SampleRate,
    int Channels,
    int BitsPerSample,
    long Frames,
    double DurationSeconds,
    long DataBytes,
    int Peak,
    double Rms,
    long ClippedFrames,
    long SilentFrames,
    double DcOffsetLeft,
    double DcOffsetRight);
