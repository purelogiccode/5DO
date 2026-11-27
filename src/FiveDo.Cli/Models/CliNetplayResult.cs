namespace FiveDo.Cli;

/// <summary>Result of a direct-IP netplay host or join run.</summary>
internal sealed record CliNetplayResult(
    bool Ok,
    string Role,
    string Host,
    int Port,
    long Frames,
    long StartFrame,
    string SessionId,
    string ContentHash,
    uint FinalFrameHash,
    uint FinalAudioHash,
    int Reconnects,
    long BytesSent,
    long BytesReceived,
    double Seconds,
    string? Failure,
    string Lobby = "",
    string RoomCode = "",
    string Transport = "");
