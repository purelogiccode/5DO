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
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;

    private readonly List<CdromCommandEntry> _entries = [];
    private readonly List<byte> _issuedOpcodes = [];
    private readonly List<ulong> _issuedCycles = [];
    private readonly List<uint> _issuedProgramCounters = [];
    private readonly List<long> _issuedInstructions = [];
    private CdromDevice? _device;
    private uint _hash = FnvOffsetBasis;
    private uint _issuedHash = FnvOffsetBasis;
    private int _issuedCount;
    private Action<byte>? _previousOpcodeObserver;
    private Action<byte>? _issuedObserver;

    /// <summary>Gets the captured entries in execution order.</summary>
    public IReadOnlyList<CdromCommandEntry> Entries => _entries;

    /// <summary>Gets the number of captured commands.</summary>
    public int Count => _entries.Count;

    /// <summary>Gets the number of commands observed at issue time.</summary>
    /// <remarks>
    /// Issue-time counting matches the reference core's command hook. A command
    /// that is still executing at a checkpoint is counted here but not in
    /// <see cref="Count"/>, which only records completed commands.
    /// </remarks>
    public int IssuedCount => _issuedCount;

    /// <summary>Gets the cumulative FNV-1a hash of command opcodes at issue time.</summary>
    public uint IssuedHash => _issuedHash;

    /// <summary>Gets the command opcodes in issue order.</summary>
    /// <remarks>
    /// The reference core exposes the same sequence. The differential harness uses
    /// this list to report the first divergent command, not just count/hash drift.
    /// </remarks>
    public IReadOnlyList<byte> IssuedOpcodes => _issuedOpcodes;

    /// <summary>Gets the ARM cycle at which each command opcode was issued.</summary>
    /// <remarks>
    /// The differential harness aligns this list with the reference core's
    /// <c>sg_debug_cd_cycle_at</c> to tell in-flight timing drift apart from a real
    /// command-sequence divergence.
    /// </remarks>
    public IReadOnlyList<ulong> IssuedCycles => _issuedCycles;

    /// <summary>Gets the CPU program counter at which each command opcode was issued.</summary>
    /// <remarks>
    /// The differential harness aligns this list with the reference core's
    /// <c>sg_debug_cd_pc_at</c>. A PC that matches while the cycle stamp differs
    /// means the guest is idle in the same place; a PC difference means the guest
    /// took a different code path.
    /// </remarks>
    public IReadOnlyList<uint> IssuedProgramCounters => _issuedProgramCounters;

    /// <summary>Gets the retired ARM instruction count when each command opcode was issued.</summary>
    /// <remarks>
    /// The differential harness aligns this list with the reference core's
    /// <c>sg_debug_cd_instructions_at</c>. Comparing it against
    /// <see cref="IssuedCycles"/> separates extra guest work from extra idle time.
    /// </remarks>
    public IReadOnlyList<long> IssuedInstructions => _issuedInstructions;

    /// <summary>
    /// Gets the cumulative FNV-1a hash of completed command opcodes in execution order.
    /// </summary>
    /// <remarks>
    /// This is the transcript hash used for diagnostics. Differential runs use
    /// <see cref="IssuedHash"/> because the reference core observes the opcode when
    /// it is written, before the command completes.
    /// </remarks>
    public uint Hash => _hash;

    /// <summary>Attaches the recorder to a device. A previous attachment is replaced.</summary>
    /// <param name="device">Device to observe.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is null.</exception>
    public void Attach(CdromDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _previousOpcodeObserver = device.CommandOpcodeObserver;
        _issuedObserver = opcode =>
        {
            _previousOpcodeObserver?.Invoke(opcode);
            CaptureIssued(opcode);
        };
        device.CommandOpcodeObserver = _issuedObserver;
        device.CommandCompleted = Capture;
    }

    /// <summary>Detaches from the current device, if any.</summary>
    public void Detach()
    {
        if (_device is not null && ReferenceEquals(_device.CommandCompleted, (Action)Capture))
        {
            _device.CommandCompleted = null;
        }

        if (_device is not null
            && _issuedObserver is not null
            && ReferenceEquals(_device.CommandOpcodeObserver, _issuedObserver))
        {
            _device.CommandOpcodeObserver = _previousOpcodeObserver;
        }

        _device = null;
        _issuedObserver = null;
        _previousOpcodeObserver = null;
    }

    /// <summary>Removes all captured entries without detaching.</summary>
    public void Clear()
    {
        _entries.Clear();
        _issuedOpcodes.Clear();
        _issuedCycles.Clear();
        _issuedProgramCounters.Clear();
        _issuedInstructions.Clear();
        _hash = FnvOffsetBasis;
        _issuedHash = FnvOffsetBasis;
        _issuedCount = 0;
    }

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

    private void CaptureIssued(byte opcode)
    {
        _issuedOpcodes.Add(opcode);
        _issuedCycles.Add(_device?.Cycles ?? 0);
        _issuedProgramCounters.Add(_device?.ProgramCounterProvider?.Invoke() ?? 0);
        _issuedInstructions.Add(_device?.InstructionCountProvider?.Invoke() ?? 0);
        _issuedCount++;
        _issuedHash = (_issuedHash ^ opcode) * FnvPrime;
    }

    private void Capture()
    {
        CdromDevice? device = _device;
        if (device is null)
        {
            return;
        }

        var entry = new CdromCommandEntry(
            device.FrameProvider?.Invoke() ?? -1,
            device.CommandBytes[0],
            device.CommandBytes.ToArray(),
            device.StatusBytes.ToArray(),
            device.Poll,
            device.DriveStatus);
        _entries.Add(entry);
        _hash = (_hash ^ entry.Opcode) * FnvPrime;
    }
}
