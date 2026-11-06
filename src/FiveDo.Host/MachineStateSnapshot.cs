using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Machine;

namespace FiveDo.Host;

/// <summary>
/// A reusable, uncompressed machine-state snapshot for per-frame host features.
/// </summary>
/// <remarks>
/// <para>
/// The latency run-ahead path captures and restores the machine once per displayed frame.
/// It therefore cannot use the compressed rewind keyframes: Brotli and the section CRC pass
/// cost more than the work they save. The snapshot keeps one <see cref="StateWriter"/>
/// alive, disables compression and checksums, and restores directly from the writer's
/// memory so a steady-state frame performs no large temporary allocations after warm-up.
/// </para>
/// <para>
/// A snapshot is only valid until the next <see cref="Capture"/> call or a machine change
/// made outside this object. The owner is responsible for invalidating it after resets,
/// state loads, movie/TAS seeks, and rewind requests.
/// </para>
/// </remarks>
public sealed class MachineStateSnapshot
{
    private static readonly StateSaveOptions SnapshotOptions = new()
    {
        CompressionEnabled = false,
        ChecksumsEnabled = false,
        InitialCapacity = 4 * 1024 * 1024,
    };

    private readonly StateWriter _writer = new(SnapshotOptions.InitialCapacity);

    /// <summary>Gets a value indicating whether a snapshot has been captured.</summary>
    public bool IsValid { get; private set; }

    /// <summary>Gets the captured snapshot length in bytes.</summary>
    public int Length => IsValid ? _writer.Length : 0;

    /// <summary>Captures the complete machine state into the reusable buffer.</summary>
    /// <param name="machine">Machine to capture.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="machine"/> is null.</exception>
    public void Capture(FiveDoMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _writer.Reset();
        _writer.CompressionEnabled = false;
        _writer.ChecksumsEnabled = false;
        // Stream.Null discards the copy; the caller restores from WrittenMemory directly.
        machine.SaveState(Stream.Null, SnapshotOptions, discHash: null, reusableWriter: _writer);
        IsValid = true;
    }

    /// <summary>Restores the captured state into a machine.</summary>
    /// <param name="machine">Machine to restore.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="machine"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no snapshot has been captured.</exception>
    public void Restore(FiveDoMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        if (!IsValid)
        {
            throw new InvalidOperationException("No machine snapshot has been captured.");
        }

        machine.LoadState(_writer.WrittenMemory);
    }

    /// <summary>Marks the snapshot as stale without releasing its buffer.</summary>
    public void Invalidate()
    {
        IsValid = false;
    }
}
