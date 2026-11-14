using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Input;
using MoonSharp.Interpreter;

namespace FiveDo.Scripting;

/// <summary>
/// Compiles a deterministic Lua input script into frame-stamped controller entries.
/// </summary>
/// <remarks>
/// <para>
/// Scripts run once at authoring time and emit a plain <see cref="InputScriptEntry"/>
/// list. Replay uses the normal movie or scripted-PBUS path, so determinism is provided
/// by the recorded movie rather than by the Lua runtime. The sandbox exposes no file,
/// clock, process, or network APIs.
/// </para>
/// <para>
/// The API is intentionally small:
/// </para>
/// <code>
/// frame(120, "A")
/// hold(180, 240, "Down,A")
/// press(300, "P", 2)
/// release(360)
/// </code>
/// <para>
/// The same functions are also available as <c>input.frame</c>, <c>input.hold</c>,
/// <c>input.press</c>, and <c>input.release</c>. Buttons accept the normal 3DO names
/// and the comma/plus/bar separators used by input scripts.
/// </para>
/// </remarks>
public static class LuaInputScript
{
    /// <summary>Default upper bound on an emitted frame number.</summary>
    public const int DefaultMaxFrame = 10_000_000;

    /// <summary>Compiles Lua source into frame-ordered controller entries.</summary>
    /// <param name="source">Lua source text.</param>
    /// <param name="chunkName">Friendly name used in syntax errors.</param>
    /// <param name="maxFrames">Largest accepted frame number.</param>
    /// <returns>Coalesced entries sorted by frame.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the script is invalid or emits an invalid frame.</exception>
    public static IReadOnlyList<InputScriptEntry> Compile(
        string source,
        string chunkName = "input.lua",
        int maxFrames = DefaultMaxFrame)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(maxFrames);
        var script = new Script(CoreModules.Preset_HardSandbox);
        var states = new SortedDictionary<int, ControllerState>();
        var emitter = new Emitter(states, maxFrames);

        script.Globals["frame"] = (Action<int, string>)emitter.Frame;
        script.Globals["hold"] = (Action<int, int, string>)emitter.Hold;
        script.Globals["press"] = (Action<int, string, int>)emitter.Press;
        script.Globals["release"] = (Action<int>)emitter.Release;
        var input = new Table(script)
        {
            ["frame"] = (Action<int, string>)emitter.Frame,
            ["hold"] = (Action<int, int, string>)emitter.Hold,
            ["press"] = (Action<int, string, int>)emitter.Press,
            ["release"] = (Action<int>)emitter.Release,
        };
        script.Globals["input"] = input;

        try
        {
            script.DoString(source, null, chunkName);
        }
        catch (InterpreterException exception)
        {
            throw new FormatException(
                $"Lua input script '{chunkName}' failed: {exception.DecoratedMessage}",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                $"Lua input script '{chunkName}' emitted invalid input: {exception.Message}",
                exception);
        }

        return Coalesce(states);
    }

    /// <summary>Loads and compiles a Lua input script file.</summary>
    /// <param name="path">Lua file path.</param>
    /// <param name="maxFrames">Largest accepted frame number.</param>
    /// <returns>Coalesced entries sorted by frame.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="FormatException">Thrown when the script is invalid.</exception>
    public static IReadOnlyList<InputScriptEntry> Load(string path, int maxFrames = DefaultMaxFrame)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Compile(File.ReadAllText(path), Path.GetFileName(path), maxFrames);
    }

    private static List<InputScriptEntry> Coalesce(SortedDictionary<int, ControllerState> states)
    {
        if (states.Count == 0)
        {
            return [];
        }

        var entries = new List<InputScriptEntry>(states.Count);
        ControllerState previous = default;
        var first = true;
        foreach (KeyValuePair<int, ControllerState> pair in states)
        {
            if (!first && pair.Value == previous)
            {
                continue;
            }

            entries.Add(new InputScriptEntry(pair.Key, pair.Value));
            previous = pair.Value;
            first = false;
        }

        return entries;
    }

    private sealed class Emitter(SortedDictionary<int, ControllerState> states, int maxFrames)
    {
        public void Frame(int frame, string? buttons)
        {
            Set(frame, Parse(buttons));
        }

        public void Hold(int from, int to, string? buttons)
        {
            if (to < from)
            {
                throw new ArgumentException($"hold({from}, {to}) ends before it starts.", nameof(to));
            }

            if (to > maxFrames)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(to),
                    to,
                    $"The hold end exceeds the {maxFrames} limit.");
            }

            Set(from, Parse(buttons));
            if (to < maxFrames)
            {
                Set(to + 1, default);
            }
        }

        public void Press(int frame, string? buttons, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
            Set(frame, Parse(buttons));
            if (count > maxFrames - frame)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    count,
                    $"press({frame}, ..., {count}) exceeds the {maxFrames} limit.");
            }

            Set(frame + count, default);
        }

        public void Release(int frame)
        {
            Set(frame, default);
        }

        private void Set(int frame, ControllerState state)
        {
            if (frame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "A frame cannot be negative.");
            }

            if (frame > maxFrames)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frame),
                    frame,
                    $"The frame exceeds the {maxFrames} limit.");
            }

            states[frame] = state;
        }

        private static ControllerState Parse(string? buttons)
        {
            return string.IsNullOrWhiteSpace(buttons)
                ? default
                : InputScript.ParseButtons(buttons);
        }
    }
}
