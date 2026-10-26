using System;
using System.Runtime.CompilerServices;

namespace FiveDo.Core;

/// <summary>
/// An interpolated-string handler that formats only when the target log entry is enabled.
/// </summary>
[InterpolatedStringHandler]
public ref struct LogInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _builder;

    /// <summary>Initializes a new instance of the <see cref="LogInterpolatedStringHandler"/> struct.</summary>
    /// <param name="literalLength">Number of literal characters in the interpolated string.</param>
    /// <param name="formattedCount">Number of interpolation holes.</param>
    /// <param name="log">Target log.</param>
    /// <param name="level">Severity.</param>
    /// <param name="category">Category.</param>
    public LogInterpolatedStringHandler(
        int literalLength,
        int formattedCount,
        Log log,
        LogLevel level,
        LogCategory category)
    {
        IsEnabled = log.IsEnabled(level, category);
        _builder = IsEnabled
            ? new DefaultInterpolatedStringHandler(literalLength, formattedCount)
            : default;
    }

    /// <summary>Gets a value indicating whether formatting is enabled.</summary>
    public bool IsEnabled { get; }

    /// <summary>Appends a literal string.</summary>
    /// <param name="value">Literal text.</param>
    public void AppendLiteral(string value)
    {
        if (IsEnabled)
        {
            _builder.AppendLiteral(value);
        }
    }

    /// <summary>Appends a formatted value.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">Value to append.</param>
    public void AppendFormatted<T>(T value)
    {
        if (IsEnabled)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <summary>Appends a formatted value with an alignment.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">Value to append.</param>
    /// <param name="alignment">Minimum field width.</param>
    public void AppendFormatted<T>(T value, int alignment)
    {
        if (IsEnabled)
        {
            _builder.AppendFormatted(value, alignment);
        }
    }

    /// <summary>Appends a formatted value with a format string.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">Value to append.</param>
    /// <param name="format">Format string.</param>
    public void AppendFormatted<T>(T value, string? format)
    {
        if (IsEnabled)
        {
            _builder.AppendFormatted(value, format);
        }
    }

    /// <summary>Appends a formatted value with an alignment and format string.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">Value to append.</param>
    /// <param name="alignment">Minimum field width.</param>
    /// <param name="format">Format string.</param>
    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (IsEnabled)
        {
            _builder.AppendFormatted(value, alignment, format);
        }
    }

    /// <summary>Appends a character span.</summary>
    /// <param name="value">Text to append.</param>
    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        if (IsEnabled)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsEnabled ? _builder.ToString() : string.Empty;
    }
}
