using System.Runtime.CompilerServices;

namespace FiveDo.Core;

/// <summary>
/// A filtered diagnostic logger.
/// </summary>
/// <remarks>
/// <see cref="Null"/> is the default logger for code that has no host sink. A logger instance
/// belongs to its owner; no mutable global log state is used.
/// </remarks>
public sealed class Log
{
    /// <summary>Gets a logger that discards every message.</summary>
    public static Log Null { get; } = new(NullLogSink.Instance, LogLevel.None, LogCategory.None);

    /// <summary>Initializes a new instance of the <see cref="Log"/> class.</summary>
    /// <param name="sink">Destination sink.</param>
    /// <param name="minimumLevel">Minimum enabled severity.</param>
    /// <param name="enabledCategories">Enabled category mask.</param>
    public Log(ILogSink sink, LogLevel minimumLevel = LogLevel.Info, LogCategory enabledCategories = LogCategory.All)
    {
        Sink = sink;
        MinimumLevel = minimumLevel;
        EnabledCategories = enabledCategories;
    }

    /// <summary>Gets the destination sink.</summary>
    public ILogSink Sink { get; }

    /// <summary>Gets the minimum enabled severity.</summary>
    public LogLevel MinimumLevel { get; init; }

    /// <summary>Gets the enabled category mask.</summary>
    public LogCategory EnabledCategories { get; init; }

    /// <summary>Checks whether a message would be emitted.</summary>
    /// <param name="level">Severity.</param>
    /// <param name="category">Category.</param>
    /// <returns><see langword="true"/> when the message is enabled.</returns>
    public bool IsEnabled(LogLevel level, LogCategory category)
    {
        return level != LogLevel.None
            && level >= MinimumLevel
            && (category & EnabledCategories) != LogCategory.None;
    }

    /// <summary>Writes one formatted message if enabled.</summary>
    /// <param name="level">Severity.</param>
    /// <param name="category">Category.</param>
    /// <param name="message">Message.</param>
    public void Write(LogLevel level, LogCategory category, string message)
    {
        if (IsEnabled(level, category))
        {
            Sink.Write(level, category, message);
        }
    }

    /// <summary>Writes one interpolated message, formatting only when enabled.</summary>
    /// <param name="level">Severity.</param>
    /// <param name="category">Category.</param>
    /// <param name="handler">Interpolated message handler.</param>
    public void Write(
        LogLevel level,
        LogCategory category,
        [InterpolatedStringHandlerArgument("", "level", "category")] ref LogInterpolatedStringHandler handler)
    {
        if (handler.IsEnabled)
        {
            Sink.Write(level, category, handler.ToString());
        }
    }
}
