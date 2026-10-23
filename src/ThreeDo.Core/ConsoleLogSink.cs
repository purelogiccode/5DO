using System;
using System.IO;

namespace ThreeDo.Core;

/// <summary>
/// Writes diagnostic messages to a text writer, normally the console.
/// </summary>
public sealed class ConsoleLogSink : ILogSink
{
    private readonly TextWriter _writer;

    /// <summary>Initializes a new instance of the <see cref="ConsoleLogSink"/> class.</summary>
    /// <param name="writer">Destination writer. Defaults to <see cref="Console.Error"/>.</param>
    public ConsoleLogSink(TextWriter? writer = null)
    {
        _writer = writer ?? Console.Error;
    }

    /// <inheritdoc />
    public void Write(LogLevel level, LogCategory category, string message)
    {
        _writer.WriteLine($"[{level}] {category}: {message}");
    }
}
