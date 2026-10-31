using System;
using FiveDo.Core;

namespace FiveDo.Machine;

/// <summary>
/// Records the machine options that affect emulated timing in a save state.
/// </summary>
/// <remarks>
/// The section is optional for backward compatibility: states written before it existed
/// still load with the FreeDO-compatible ARM timing defaults, and older builds ignore the
/// unknown section. Only options that change guest-visible behavior belong here; purely
/// host-side presentation settings do not.
/// </remarks>
internal sealed class MachineOptionsSection : IStateSection
{
    private readonly EmulatorTuning _tuning;

    internal MachineOptionsSection(EmulatorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        _tuning = tuning;
    }

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Options;

    /// <summary>Gets the save-state section version.</summary>
    /// <remarks>
    /// Version 2 adds the CLIO timer mode; version 3 adds the MADAM CEL timing mode.
    /// Version-1 and version-2 states default the missing fields to FreeDO-compatible.
    /// </remarks>
    public ushort Version => 3;

    /// <summary>Gets the ARM timing mode read from a state, or <see langword="null"/> when absent.</summary>
    internal ArmTimingMode? LoadedArmTiming { get; private set; }

    /// <summary>Gets the CLIO timer mode read from a state, or <see langword="null"/> when absent.</summary>
    internal ClioTimerMode? LoadedClioTimers { get; private set; }

    /// <summary>Gets the CEL timing mode read from a state, or <see langword="null"/> when absent.</summary>
    internal CelTimingMode? LoadedCelTiming { get; private set; }

    /// <summary>Writes the option snapshot.</summary>
    /// <param name="writer">State writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteUInt32((uint)_tuning.ArmTiming);
        writer.WriteUInt32((uint)_tuning.ClioTimers);
        writer.WriteUInt32((uint)_tuning.CelTiming);
    }

    /// <summary>Reads the option snapshot.</summary>
    /// <param name="reader">State reader.</param>
    /// <exception cref="InvalidStateException">Thrown when an option value is unknown.</exception>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var timing = (ArmTimingMode)reader.ReadUInt32();
        if (timing is not ArmTimingMode.FreeDoCompatible and not ArmTimingMode.Arm60Datasheet)
        {
            throw new InvalidStateException($"Unknown ARM timing mode {(uint)timing} in the save state.");
        }

        LoadedArmTiming = timing;

        if (reader is StateSectionReader { Version: 1 })
        {
            LoadedClioTimers = null;
            LoadedCelTiming = null;
            return;
        }

        var clioTimers = (ClioTimerMode)reader.ReadUInt32();
        if (clioTimers is not ClioTimerMode.FreeDoCompatible and not ClioTimerMode.OperaScheduler)
        {
            throw new InvalidStateException($"Unknown CLIO timer mode {(uint)clioTimers} in the save state.");
        }

        LoadedClioTimers = clioTimers;

        if (reader is StateSectionReader { Version: 2 })
        {
            LoadedCelTiming = null;
            return;
        }

        var celTiming = (CelTimingMode)reader.ReadUInt32();
        if (celTiming is not CelTimingMode.FreeDoCompatible and not CelTimingMode.OperaBusy)
        {
            throw new InvalidStateException($"Unknown CEL timing mode {(uint)celTiming} in the save state.");
        }

        LoadedCelTiming = celTiming;
    }
}
