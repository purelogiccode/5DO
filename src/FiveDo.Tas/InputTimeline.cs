using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FiveDo.Input;

namespace FiveDo.Tas;

/// <summary>
/// An editable player-1 input timeline that can be exported from and applied to a movie.
/// </summary>
/// <remarks>
/// The timeline stores state changes rather than one entry per frame. Entries are sorted
/// by frame, duplicate frames keep the last value, and consecutive identical states are
/// coalesced. Applying the timeline rewrites player 1's buttons on every movie frame and
/// preserves the other controller slots and the embedded starting state.
/// </remarks>
public sealed class InputTimeline
{
    private readonly InputScriptEntry[] _entries;

    /// <summary>Initializes a timeline.</summary>
    /// <param name="entries">State changes, in any order.</param>
    /// <param name="frameCount">Number of frames the timeline covers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame count or an entry frame is negative.</exception>
    public InputTimeline(IEnumerable<InputScriptEntry> entries, int frameCount)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegative(frameCount);
        _entries = Normalize(entries, frameCount);
        FrameCount = frameCount;
    }

    /// <summary>Gets the timeline length in frames.</summary>
    public int FrameCount { get; }

    /// <summary>Gets the coalesced state changes.</summary>
    public IReadOnlyList<InputScriptEntry> Entries => _entries;

    /// <summary>Extracts player-1 state changes from a movie.</summary>
    /// <param name="movie">Source movie.</param>
    /// <returns>A timeline covering the movie's frames.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="movie"/> is null.</exception>
    public static InputTimeline FromMovie(Movie movie)
    {
        ArgumentNullException.ThrowIfNull(movie);
        var entries = new List<InputScriptEntry>();
        ControllerState previous = default;
        var first = true;
        for (var frame = 0; frame < movie.FrameCount; frame++)
        {
            ControllerState state = movie.GetControllers(frame)[0];
            if (first || state != previous)
            {
                entries.Add(new InputScriptEntry(frame, state));
                previous = state;
                first = false;
            }
        }

        return new InputTimeline(entries, movie.FrameCount);
    }

    /// <summary>Parses an input-script document as a timeline.</summary>
    /// <param name="text">Input-script text.</param>
    /// <param name="frameCount">Number of frames the timeline covers.</param>
    /// <returns>The parsed timeline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the script is malformed.</exception>
    public static InputTimeline Parse(string text, int frameCount)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new InputTimeline(InputScript.Parse(text), frameCount);
    }

    /// <summary>Loads an input-script document as a timeline.</summary>
    /// <param name="path">Script path.</param>
    /// <param name="frameCount">Number of frames the timeline covers.</param>
    /// <returns>The parsed timeline.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
    /// <exception cref="FormatException">Thrown when the script is malformed.</exception>
    public static InputTimeline Load(string path, int frameCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path), frameCount);
    }

    /// <summary>Returns a copy with one frame's player-1 buttons replaced.</summary>
    /// <param name="frame">Zero-based frame.</param>
    /// <param name="state">Replacement controller state.</param>
    /// <returns>A new timeline.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame is outside the timeline.</exception>
    public InputTimeline WithFrame(int frame, ControllerState state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frame, FrameCount);
        var entries = new List<InputScriptEntry>(_entries.Length + 1);
        var replaced = false;
        foreach (InputScriptEntry entry in _entries)
        {
            if (entry.Frame == frame)
            {
                entries.Add(new InputScriptEntry(frame, state));
                replaced = true;
            }
            else
            {
                entries.Add(entry);
            }
        }

        if (!replaced)
        {
            entries.Add(new InputScriptEntry(frame, state));
        }

        return new InputTimeline(entries, FrameCount);
    }

    /// <summary>Returns a copy with one button set or cleared on one frame.</summary>
    /// <param name="frame">Zero-based frame.</param>
    /// <param name="button">Button to change.</param>
    /// <param name="pressed">Whether the button is held.</param>
    /// <returns>A new timeline.</returns>
    public InputTimeline WithButton(int frame, ControllerButton button, bool pressed)
    {
        ControllerState state = StateAt(frame);
        return WithFrame(frame, state.Set(button, pressed));
    }

    /// <summary>Returns a shortened copy.</summary>
    /// <param name="frameCount">New length, 0 through the current length.</param>
    /// <returns>A new timeline.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is outside the timeline.</exception>
    public InputTimeline Trim(int frameCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frameCount, FrameCount);
        return new InputTimeline(_entries, frameCount);
    }

    /// <summary>Gets the controller state active on a frame.</summary>
    /// <param name="frame">Zero-based frame.</param>
    /// <returns>The active state, or an empty state before the first entry.</returns>
    public ControllerState StateAt(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ControllerState state = default;
        foreach (InputScriptEntry entry in _entries)
        {
            if (entry.Frame > frame)
            {
                break;
            }

            state = entry.State;
        }

        return state;
    }

    /// <summary>Applies the timeline to a movie's player-1 input.</summary>
    /// <param name="movie">Source movie; its state, header metadata, and other players are kept.</param>
    /// <returns>A movie with rewritten player-1 packets and cleared completion evidence.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="movie"/> is null.</exception>
    public Movie ApplyTo(Movie movie)
    {
        ArgumentNullException.ThrowIfNull(movie);
        var frames = new List<byte[]>(movie.FrameCount);
        var entryIndex = 0;
        ControllerState current = default;
        for (var frame = 0; frame < movie.FrameCount; frame++)
        {
            while (entryIndex < _entries.Length && _entries[entryIndex].Frame <= frame)
            {
                current = _entries[entryIndex].State;
                entryIndex++;
            }

            ControllerState[] controllers = movie.GetControllers(frame);
            controllers[0] = current;
            var packet = new byte[PbusEncoder.PacketSize];
            PbusEncoder.Encode(controllers, packet);
            frames.Add(packet);
        }

        MovieHeader header = movie.Header with
        {
            FrameCount = frames.Count,
            FinalFrameHash = null,
            FinalProgramCounter = null,
            FinalStateHash = null,
            HasState = movie.HasState,
        };
        return new Movie(header, frames, movie.State);
    }

    /// <summary>Formats the timeline as an input-script document.</summary>
    /// <returns>A UTF-8 text document using the normal frame/buttons syntax.</returns>
    public string ToScript()
    {
        var text = new StringBuilder();
        text.AppendLine("# 5DO input timeline");
        text.AppendLine("# frame buttons");
        foreach (InputScriptEntry entry in _entries)
        {
            text.Append(entry.Frame.ToString(CultureInfo.InvariantCulture));
            text.Append(' ');
            text.AppendLine(InputRecorder.FormatButtons(entry.State.Buttons));
        }

        return text.ToString();
    }

    /// <summary>Writes the timeline as an input-script document.</summary>
    /// <param name="path">Destination path.</param>
    /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToScript());
    }

    private static InputScriptEntry[] Normalize(IEnumerable<InputScriptEntry> entries, int frameCount)
    {
        var byFrame = new SortedDictionary<int, ControllerState>();
        foreach (InputScriptEntry entry in entries)
        {
            if (entry.Frame < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entries),
                    entry.Frame,
                    "A timeline entry frame cannot be negative.");
            }

            if (entry.Frame >= frameCount)
            {
                continue;
            }

            byFrame[entry.Frame] = entry.State;
        }

        var result = new List<InputScriptEntry>(byFrame.Count);
        ControllerState previous = default;
        var first = true;
        foreach (KeyValuePair<int, ControllerState> pair in byFrame)
        {
            if (!first && pair.Value == previous)
            {
                continue;
            }

            result.Add(new InputScriptEntry(pair.Key, pair.Value));
            previous = pair.Value;
            first = false;
        }

        return [.. result];
    }
}
