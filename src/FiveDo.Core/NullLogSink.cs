namespace FiveDo.Core;

/// <summary>
/// A log sink that discards every message.
/// </summary>
public sealed class NullLogSink : ILogSink
{
    /// <summary>Gets the shared null sink.</summary>
    public static NullLogSink Instance { get; } = new();

    private NullLogSink()
    {
    }

    /// <inheritdoc />
    public void Write(LogLevel level, LogCategory category, string message)
    {
    }
}
