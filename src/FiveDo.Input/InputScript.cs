using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Input;

/// <summary>
/// Parses simple controller scripts used by the CLI and tests.
/// </summary>
/// <remarks>
/// <para>Each non-comment line is <c>frame buttons</c>, for example:</para>
/// <code>
/// # frame  buttons
/// 0        None
/// 120      A
/// 180      A,Down
/// 240      None
/// </code>
/// <para>
/// Frames may be decimal or <c>0x</c>-prefixed. Button names are the
/// <see cref="ControllerButton"/> names, case-insensitive, separated by commas, plus
/// signs, or vertical bars. <c>None</c> (or an empty field) releases every button.
/// The state persists until the next entry.
/// </para>
/// </remarks>
public static class InputScript
{
    private static readonly Dictionary<string, ControllerButton> ButtonAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Start"] = ControllerButton.P,
            ["Play"] = ControllerButton.P,
            ["Pause"] = ControllerButton.P,
            ["Stop"] = ControllerButton.X,
        };

    /// <summary>Parses script text into frame-ordered entries.</summary>
    /// <param name="text">Script text.</param>
    /// <returns>Entries sorted by frame; duplicate frames keep the last entry.</returns>
    /// <exception cref="FormatException">Thrown when a line is malformed.</exception>
    public static IReadOnlyList<InputScriptEntry> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var entries = new List<InputScriptEntry>();
        var lineNumber = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var separator = line.IndexOfAny([' ', '\t']);
            var frameText = separator < 0 ? line : line[..separator];
            var buttonText = separator < 0 ? string.Empty : line[(separator + 1)..].Trim();
            if (!TryParseFrame(frameText, out var frame))
            {
                throw new FormatException($"Line {lineNumber}: invalid frame '{frameText}'.");
            }

            entries.Add(new InputScriptEntry(frame, ParseButtons(buttonText, lineNumber)));
        }

        entries.Sort(static (left, right) => left.Frame.CompareTo(right.Frame));
        return Coalesce(entries);
    }

    /// <summary>Loads and parses a script file.</summary>
    /// <param name="path">Script path.</param>
    /// <returns>Entries sorted by frame.</returns>
    public static IReadOnlyList<InputScriptEntry> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Shifts script frame numbers by a base frame, so a script written from zero can
    /// be applied after loading a save state.
    /// </summary>
    /// <param name="entries">Source entries.</param>
    /// <param name="baseFrame">Emulated frame count at the start of the run.</param>
    /// <returns>A new frame-ordered entry list.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseFrame"/> is negative.</exception>
    public static IReadOnlyList<InputScriptEntry> Offset(
        IReadOnlyList<InputScriptEntry> entries,
        long baseFrame)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegative(baseFrame);
        var result = new InputScriptEntry[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var frame = checked((int)(entries[i].Frame + baseFrame));
            result[i] = new InputScriptEntry(frame, entries[i].State);
        }

        return result;
    }

    private static bool TryParseFrame(string text, out int frame)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out frame);
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame)
            && frame >= 0;
    }

    private static ControllerState ParseButtons(string text, int lineNumber)
    {
        if (text.Length == 0 || string.Equals(text, "None", StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        ControllerButton buttons = ControllerButton.None;
        foreach (var part in text.Split([',', '+', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(part, "None", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ButtonAliases.TryGetValue(part, out ControllerButton alias))
            {
                buttons |= alias;
                continue;
            }

            if (!Enum.TryParse(part, ignoreCase: true, out ControllerButton button)
                || !Enum.IsDefined(button))
            {
                throw new FormatException($"Line {lineNumber}: unknown button '{part}'.");
            }

            buttons |= button;
        }

        return new ControllerState(buttons);
    }

    private static List<InputScriptEntry> Coalesce(List<InputScriptEntry> entries)
    {
        if (entries.Count < 2)
        {
            return entries;
        }

        var result = new List<InputScriptEntry>(entries.Count);
        foreach (InputScriptEntry entry in entries)
        {
            if (result.Count > 0 && result[^1].Frame == entry.Frame)
            {
                result[^1] = entry;
            }
            else
            {
                result.Add(entry);
            }
        }

        return result;
    }
}

/// <summary>
/// A PBUS source driven by <see cref="InputScript"/> entries. Call <see cref="AdvanceTo"/>
/// once per emulated frame before the machine samples input at V1.
/// </summary>
public sealed class ScriptedPbusSource : IPbusSource
{
    private readonly PbusSource _inner = new();
    private readonly InputScriptEntry[] _entries;
    private int _index = -1;

    /// <summary>Initializes a new instance of the <see cref="ScriptedPbusSource"/> class.</summary>
    /// <param name="entries">Frame-ordered entries from <see cref="InputScript"/>.</param>
    public ScriptedPbusSource(IEnumerable<InputScriptEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [.. entries];
        Array.Sort(_entries, static (left, right) => left.Frame.CompareTo(right.Frame));
    }

    /// <summary>Gets the most recently applied frame.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Public diagnostic state consumed by host tooling.")]
    public long CurrentFrame { get; private set; } = -1;

    /// <summary>Gets the most recently applied controller state.</summary>
    public ControllerState CurrentState { get; private set; }

    /// <summary>Applies the last script entry whose frame is at or before <paramref name="frame"/>.</summary>
    /// <param name="frame">Emulated frame about to run.</param>
    public void AdvanceTo(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        while (_index + 1 < _entries.Length && _entries[_index + 1].Frame <= frame)
        {
            _index++;
        }

        CurrentFrame = frame;
        CurrentState = _index >= 0 ? _entries[_index].State : default;
        _inner[0] = CurrentState;
    }

    /// <inheritdoc />
    public int GetPbusData(Span<byte> destination)
    {
        return _inner.GetPbusData(destination);
    }
}
