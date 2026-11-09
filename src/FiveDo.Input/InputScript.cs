using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Input;

/// <summary>
/// Parses controller and pointer scripts used by the CLI, harness, and tests.
/// </summary>
/// <remarks>
/// <para>Each non-comment line is <c>frame buttons</c>, optionally followed by one
/// pointer token, for example:</para>
/// <code>
/// # frame  buttons
/// 0        None
/// 120      A
/// 180      A,Down
/// 240      None
/// 300      @mouse 40 -20 left
/// 320      @lightgun 160 120 trigger
/// </code>
/// <para>
/// Frames may be decimal or <c>0x</c>-prefixed. Button names are the
/// <see cref="ControllerButton"/> names, case-insensitive, separated by commas, plus
/// signs, or vertical bars. <c>None</c> (or an empty field) releases every button.
/// The pad state persists until the next entry.
/// </para>
/// <para>
/// Pointer tokens start with <c>@</c> and may appear after the pad buttons:
/// <c>@mouse dx dy [left,middle,right,shift]</c>,
/// <c>@lightgun x y [trigger,option,reload,offscreen]</c>,
/// <c>@arcadegun x y [trigger,service,coins,start,holster,offscreen]</c>, and
/// <c>@trackball dx dy [start1,start2,coin1,coin2,service]</c>.
/// <c>@gun</c>, <c>@arcade</c>, and <c>@orbatak</c> are aliases. A line without a
/// pointer token releases every pointer button. Mouse and trackball motion is applied
/// for one frame; buttons persist until the next entry.
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

            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            if (!TryParseFrame(tokens[0], out var frame))
            {
                throw new FormatException($"Line {lineNumber}: invalid frame '{tokens[0]}'.");
            }

            var pointerIndex = FindPointerToken(tokens);
            var state = pointerIndex < 0
                ? ParseButtons(JoinTokens(tokens, 1, tokens.Length - 1), lineNumber)
                : ParseButtons(JoinTokens(tokens, 1, pointerIndex - 1), lineNumber);
            var pointer = pointerIndex < 0
                ? InputScriptPointer.Empty
                : ParsePointer(tokens, pointerIndex, lineNumber);
            entries.Add(new InputScriptEntry(frame, state, pointer));
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
            result[i] = new InputScriptEntry(frame, entries[i].State, entries[i].PointerState);
        }

        return result;
    }

    private static int FindPointerToken(string[] tokens)
    {
        for (var i = 1; i < tokens.Length; i++)
        {
            if (tokens[i].StartsWith('@'))
            {
                return i;
            }
        }

        return -1;
    }

    private static string JoinTokens(string[] tokens, int start, int count)
    {
        return count <= 0 ? string.Empty : string.Join(' ', tokens, start, count);
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

    private static bool TryParseSigned(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private static ControllerState ParseButtons(string text, int lineNumber)
    {
        if (text.Length == 0 || string.Equals(text, "None", StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        ControllerButton buttons = ControllerButton.None;
        foreach (var part in SplitParts(text))
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

    private static InputScriptPointer ParsePointer(string[] tokens, int index, int lineNumber)
    {
        var kindText = tokens[index][1..];
        if (tokens.Length < index + 3)
        {
            throw new FormatException(
                $"Line {lineNumber}: '{tokens[index]}' needs two coordinates.");
        }

        if (!TryParseSigned(tokens[index + 1], out var x)
            || !TryParseSigned(tokens[index + 2], out var y))
        {
            throw new FormatException(
                $"Line {lineNumber}: '{tokens[index]}' has invalid coordinates.");
        }

        var parts = new List<string>();
        for (var i = index + 3; i < tokens.Length; i++)
        {
            parts.AddRange(SplitParts(tokens[i]));
        }

        return kindText.ToLowerInvariant() switch
        {
            "mouse" => ParseMouse(x, y, parts, lineNumber),
            "lightgun" or "gun" => ParseLightgun(x, y, parts, lineNumber),
            "arcadegun" or "arcade" or "arcade-lightgun" =>
                ParseArcadeLightgun(x, y, parts, lineNumber),
            "trackball" or "orbatak" => ParseTrackball(x, y, parts, lineNumber),
            _ => throw new FormatException(
                $"Line {lineNumber}: unknown pointer '{tokens[index]}'."),
        };
    }

    private static InputScriptPointer ParseMouse(
        int x,
        int y,
        List<string> parts,
        int lineNumber)
    {
        var left = false;
        var middle = false;
        var right = false;
        var shift = false;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "left":
                    left = true;
                    break;
                case "middle":
                    middle = true;
                    break;
                case "right":
                    right = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "none":
                    break;
                default:
                    throw new FormatException(
                        $"Line {lineNumber}: unknown mouse button '{part}'.");
            }
        }

        return InputScriptPointer.FromMouse(new MouseState(x, y, left, middle, right, shift));
    }

    private static InputScriptPointer ParseLightgun(
        int x,
        int y,
        List<string> parts,
        int lineNumber)
    {
        var trigger = false;
        var option = false;
        var reload = false;
        var offScreen = false;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "trigger":
                    trigger = true;
                    break;
                case "option":
                    option = true;
                    break;
                case "reload":
                    reload = true;
                    break;
                case "offscreen":
                    offScreen = true;
                    break;
                case "none":
                    break;
                default:
                    throw new FormatException(
                        $"Line {lineNumber}: unknown lightgun button '{part}'.");
            }
        }

        return InputScriptPointer.FromLightgun(
            new LightgunState(x, y, trigger, option, reload, offScreen));
    }

    private static InputScriptPointer ParseArcadeLightgun(
        int x,
        int y,
        List<string> parts,
        int lineNumber)
    {
        var trigger = false;
        var service = false;
        var coins = false;
        var start = false;
        var holster = false;
        var offScreen = false;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "trigger":
                    trigger = true;
                    break;
                case "service":
                    service = true;
                    break;
                case "coins":
                    coins = true;
                    break;
                case "start":
                    start = true;
                    break;
                case "holster":
                    holster = true;
                    break;
                case "offscreen":
                    offScreen = true;
                    break;
                case "none":
                    break;
                default:
                    throw new FormatException(
                        $"Line {lineNumber}: unknown arcade lightgun button '{part}'.");
            }
        }

        return InputScriptPointer.FromArcadeLightgun(
            new ArcadeLightgunState(x, y, trigger, service, coins, start, holster, offScreen));
    }

    private static InputScriptPointer ParseTrackball(
        int x,
        int y,
        List<string> parts,
        int lineNumber)
    {
        var startP1 = false;
        var startP2 = false;
        var coinP1 = false;
        var coinP2 = false;
        var service = false;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "start1":
                    startP1 = true;
                    break;
                case "start2":
                    startP2 = true;
                    break;
                case "coin1":
                    coinP1 = true;
                    break;
                case "coin2":
                    coinP2 = true;
                    break;
                case "service":
                    service = true;
                    break;
                case "none":
                    break;
                default:
                    throw new FormatException(
                        $"Line {lineNumber}: unknown trackball button '{part}'.");
            }
        }

        return InputScriptPointer.FromTrackball(
            new OrbatakTrackballState(x, y),
            new OrbatakButtonsState(startP1, startP2, coinP1, coinP2, service));
    }

    private static string[] SplitParts(string text)
    {
        return text.Split(
            [',', '+', '|'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    /// <summary>Gets the inner PBUS source so a host can configure devices.</summary>
    public PbusSource Source => _inner;

    /// <summary>Gets the most recently applied frame.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Public diagnostic state consumed by host tooling.")]
    public long CurrentFrame { get; private set; } = -1;

    /// <summary>Gets the most recently applied controller state.</summary>
    public ControllerState CurrentState { get; private set; }

    /// <summary>Gets the most recently applied pointer state.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Public diagnostic state consumed by host tooling.")]
    public InputScriptPointer CurrentPointerState { get; private set; }

    /// <summary>Applies the last script entry whose frame is at or before <paramref name="frame"/>.</summary>
    /// <param name="frame">Emulated frame about to run.</param>
    public void AdvanceTo(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        var changed = false;
        while (_index + 1 < _entries.Length && _entries[_index + 1].Frame <= frame)
        {
            _index++;
            changed = true;
        }

        CurrentFrame = frame;
        CurrentState = _index >= 0 ? _entries[_index].State : default;
        CurrentPointerState = _index >= 0 ? _entries[_index].PointerState : InputScriptPointer.Empty;
        _inner[0] = CurrentState;
        if (changed)
        {
            ApplyPointer(CurrentPointerState);
        }
    }

    /// <inheritdoc />
    public int GetPbusData(Span<byte> destination)
    {
        var length = _inner.GetPbusData(destination);
        // Mouse and trackball motion is a one-frame impulse. Buttons stay held until
        // the next script entry, but relative movement must not repeat every frame.
        _inner.Devices.ClearTransientMotion();
        return length;
    }

    private void ApplyPointer(InputScriptPointer pointer)
    {
        InputDeviceSet devices = _inner.Devices;
        for (var port = 0; port < devices.PortCount; port++)
        {
            devices.SetMouse(port, default);
            devices.SetLightgun(port, default);
            devices.SetArcadeLightgun(port, default);
            devices.SetTrackball(port, default);
            devices.SetTrackballButtons(port, default);
        }

        for (var port = 0; port < devices.PortCount; port++)
        {
            InputDeviceType device = devices.GetDevice(port);
            switch (pointer.Kind)
            {
                case InputScriptPointerKind.Mouse when device == InputDeviceType.Mouse:
                    devices.SetMouse(port, pointer.Mouse);
                    break;
                case InputScriptPointerKind.Lightgun when device == InputDeviceType.Lightgun:
                    devices.SetLightgun(port, pointer.Lightgun);
                    break;
                case InputScriptPointerKind.ArcadeLightgun when device == InputDeviceType.ArcadeLightgun:
                    devices.SetArcadeLightgun(port, pointer.ArcadeLightgun);
                    break;
                case InputScriptPointerKind.Trackball when device == InputDeviceType.OrbatakTrackball:
                    devices.SetTrackball(port, pointer.Trackball);
                    devices.SetTrackballButtons(port, pointer.TrackballButtons);
                    break;
            }
        }
    }
}
