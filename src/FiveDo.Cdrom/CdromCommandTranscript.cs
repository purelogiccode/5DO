using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FiveDo.Cdrom;

/// <summary>One captured CD-ROM command and the status it produced.</summary>
/// <param name="Frame">Emulated frame when the command was issued, or -1.</param>
/// <param name="Opcode">Command opcode.</param>
/// <param name="Command">All seven command bytes.</param>
/// <param name="Status">Status response bytes at command completion.</param>
/// <param name="Poll">Poll byte after the command.</param>
/// <param name="DriveStatus">Drive-status byte after the command.</param>
public sealed record CdromCommandEntry(
    long Frame,
    byte Opcode,
    byte[] Command,
    byte[] Status,
    byte Poll,
    byte DriveStatus)
{
    /// <summary>Formats the entry as one trace line.</summary>
    /// <returns>A compact command/status transcript line.</returns>
    public string ToText()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"f={Frame} cmd={Opcode:x2}");
        foreach (var value in Command.AsSpan(1))
        {
            builder.Append(CultureInfo.InvariantCulture, $" {value:x2}");
        }

        builder.Append(" status=");
        foreach (var value in Status)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{value:x2}");
        }

        builder.Append(CultureInfo.InvariantCulture, $" poll={Poll:x2} drive={DriveStatus:x2}");
        return builder.ToString();
    }
}

/// <summary>
/// Captures the guest's CD command/status exchanges for diagnostics and regression tests.
/// </summary>
/// <remarks>
/// Attach a recorder to a <see cref="CdromDevice"/> before running. The device invokes the
/// recorder after each command handler, so the entry includes the produced status bytes,
/// poll bits, and drive status. The transcript is deterministic for a given disc and input.
/// </remarks>
public sealed class CdromCommandTranscript
{
    private readonly List<CdromCommandEntry> _entries = [];
    private CdromDevice? _device;

    /// <summary>Gets the captured entries in execution order.</summary>
    public IReadOnlyList<CdromCommandEntry> Entries => _entries;

    /// <summary>Gets the number of captured commands.</summary>
    public int Count => _entries.Count;

    /// <summary>Attaches the recorder to a device. A previous attachment is replaced.</summary>
    /// <param name="device">Device to observe.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is null.</exception>
    public void Attach(CdromDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        device.CommandCompleted = Capture;
    }

    /// <summary>Detaches from the current device, if any.</summary>
    public void Detach()
    {
        if (_device is not null && ReferenceEquals(_device.CommandCompleted, (Action)Capture))
        {
            _device.CommandCompleted = null;
        }

        _device = null;
    }

    /// <summary>Removes all captured entries without detaching.</summary>
    public void Clear() => _entries.Clear();

    /// <summary>Formats every entry as a newline-separated transcript.</summary>
    /// <returns>The transcript, with a trailing newline when non-empty.</returns>
    public string ToText()
    {
        if (_entries.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (CdromCommandEntry entry in _entries)
        {
            builder.AppendLine(entry.ToText());
        }

        return builder.ToString();
    }

    private void Capture()
    {
        CdromDevice? device = _device;
        if (device is null)
        {
            return;
        }

        _entries.Add(new CdromCommandEntry(
            device.FrameProvider?.Invoke() ?? -1,
            device.CommandBytes[0],
            device.CommandBytes.ToArray(),
            device.StatusBytes.ToArray(),
            device.Poll,
            device.DriveStatus));
    }
}
