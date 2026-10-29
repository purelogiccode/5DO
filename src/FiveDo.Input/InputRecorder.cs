using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace FiveDo.Input;

/// <summary>
/// Records controller input as frame-ordered entries that <see cref="InputScript"/> can replay.
/// </summary>
/// <remarks>
/// Entries are coalesced: a state is written only when it differs from the previous
/// recorded state, so a long idle run produces one line. The recorder is safe to call
/// from the emulation thread while the UI starts or stops it.
/// </remarks>
public sealed class InputRecorder
{
    private static readonly ControllerButton[] ButtonOrder =
    [
        ControllerButton.A,
        ControllerButton.B,
        ControllerButton.C,
        ControllerButton.P,
        ControllerButton.X,
        ControllerButton.L,
        ControllerButton.R,
        ControllerButton.Up,
        ControllerButton.Down,
        ControllerButton.Left,
        ControllerButton.Right,
    ];

    private readonly Lock _gate = new();
    private readonly List<InputScriptEntry> _entries = [];
    private ControllerState _lastState;
    private long _firstFrame = -1;
    private long _lastFrame = -1;

    /// <summary>Gets the number of frames covered by the recording.</summary>
    public long Frames
    {
        get
        {
            lock (_gate)
            {
                return _lastFrame < 0 ? 0 : _lastFrame - _firstFrame + 1;
            }
        }
    }

    /// <summary>Gets the number of script entries written so far.</summary>
    public int EntryCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>Records the controller state applied to one emulated frame.</summary>
    /// <param name="frame">Zero-based emulated frame number.</param>
    /// <param name="state">Controller state sampled for that frame.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="frame"/> is negative, is earlier than the previous
    /// recorded frame, or exceeds the script format's 32-bit frame limit.
    /// </exception>
    public void Record(long frame, ControllerState state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, int.MaxValue);
        lock (_gate)
        {
            if (frame < _lastFrame)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frame),
                    frame,
                    "Frames must be recorded in nondecreasing order.");
            }

            if (_entries.Count == 0)
            {
                _firstFrame = frame;
            }

            if (_entries.Count == 0 || state != _lastState)
            {
                _entries.Add(new InputScriptEntry((int)frame, state));
                _lastState = state;
            }

            _lastFrame = frame;
        }
    }

    /// <summary>Formats the recording as an <see cref="InputScript"/> document.</summary>
    /// <returns>A script beginning with a comment header.</returns>
    public string ToScript()
    {
        lock (_gate)
        {
            var text = new StringBuilder();
            text.AppendLine("# 5DO input recording");
            text.AppendLine("# frame buttons");
            foreach (InputScriptEntry entry in _entries)
            {
                text.Append(entry.Frame.ToString(CultureInfo.InvariantCulture));
                text.Append(' ');
                text.AppendLine(FormatButtons(entry.State.Buttons));
            }

            return text.ToString();
        }
    }

    /// <summary>Writes the recording to a script file, creating its directory when needed.</summary>
    /// <param name="path">Destination script path.</param>
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

    /// <summary>Formats a button mask using the names accepted by <see cref="InputScript"/>.</summary>
    /// <param name="buttons">Buttons to format.</param>
    /// <returns>A comma-separated button list, or <c>None</c> for an empty mask.</returns>
    public static string FormatButtons(ControllerButton buttons)
    {
        if (buttons == ControllerButton.None)
        {
            return "None";
        }

        var text = new StringBuilder();
        foreach (ControllerButton button in ButtonOrder)
        {
            if ((buttons & button) == ControllerButton.None)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(',');
            }

            text.Append(button.ToString());
        }

        return text.ToString();
    }
}
