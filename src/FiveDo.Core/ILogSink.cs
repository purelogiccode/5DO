namespace FiveDo.Core;

/// <summary>
/// Receives formatted diagnostic messages.
/// </summary>
public interface ILogSink
{
    /// <summary>Writes one message.</summary>
    /// <param name="level">Severity.</param>
    /// <param name="category">Category.</param>
    /// <param name="message">Formatted message.</param>
    void Write(LogLevel level, LogCategory category, string message);
}
