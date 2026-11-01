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
    /// Version 2 adds the CLIO timer mode; version 3 adds the MADAM CEL timing mode;
    /// version 4 adds the CD-ROM timing mode; version 5 adds the CD-ROM speed; version 6
    /// adds the video region. Older states default the missing fields to FreeDO-compatible,
    /// the stock 2x drive, and NTSC.
    /// </remarks>
    public ushort Version => 6;

    /// <summary>Gets the ARM timing mode read from a state, or <see langword="null"/> when absent.</summary>
    internal ArmTimingMode? LoadedArmTiming { get; private set; }

    /// <summary>Gets the CLIO timer mode read from a state, or <see langword="null"/> when absent.</summary>
    internal ClioTimerMode? LoadedClioTimers { get; private set; }

    /// <summary>Gets the CEL timing mode read from a state, or <see langword="null"/> when absent.</summary>
    internal CelTimingMode? LoadedCelTiming { get; private set; }

    /// <summary>Gets the CD-ROM timing mode read from a state, or <see langword="null"/> when absent.</summary>
    internal CdromTimingMode? LoadedCdromTiming { get; private set; }

    /// <summary>Gets the CD-ROM speed read from a state, or <see langword="null"/> when absent.</summary>
    internal CdSpeed? LoadedCdSpeed { get; private set; }

    /// <summary>Gets the video region read from a state, or <see langword="null"/> when absent.</summary>
    internal VideoRegion? LoadedRegion { get; private set; }

    /// <summary>Writes the option snapshot.</summary>
    /// <param name="writer">State writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteUInt32((uint)_tuning.ArmTiming);
        writer.WriteUInt32((uint)_tuning.ClioTimers);
        writer.WriteUInt32((uint)_tuning.CelTiming);
        writer.WriteUInt32((uint)_tuning.CdromTiming);
        writer.WriteUInt32((uint)_tuning.CdSpeed);
        writer.WriteUInt32((uint)_tuning.Region);
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
            LoadedCdromTiming = null;
            LoadedCdSpeed = null;
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
            LoadedCdromTiming = null;
            LoadedCdSpeed = null;
            return;
        }

        var celTiming = (CelTimingMode)reader.ReadUInt32();
        if (celTiming is not CelTimingMode.FreeDoCompatible and not CelTimingMode.OperaBusy)
        {
            throw new InvalidStateException($"Unknown CEL timing mode {(uint)celTiming} in the save state.");
        }

        LoadedCelTiming = celTiming;

        if (reader is StateSectionReader { Version: 3 })
        {
            LoadedCdromTiming = null;
            LoadedCdSpeed = null;
            return;
        }

        var cdromTiming = (CdromTimingMode)reader.ReadUInt32();
        if (cdromTiming is not CdromTimingMode.FreeDoCompatible
            and not CdromTimingMode.OperaCycleBased)
        {
            throw new InvalidStateException($"Unknown CD-ROM timing mode {(uint)cdromTiming} in the save state.");
        }

        LoadedCdromTiming = cdromTiming;

        if (reader is StateSectionReader { Version: 4 })
        {
            LoadedCdSpeed = null;
            return;
        }

        var cdSpeed = (CdSpeed)reader.ReadUInt32();
        if (cdSpeed is not CdSpeed.Unbounded
            and not CdSpeed.X1
            and not CdSpeed.X2
            and not CdSpeed.X4
            and not CdSpeed.X8
            and not CdSpeed.X16
            and not CdSpeed.X24
            and not CdSpeed.X32)
        {
            throw new InvalidStateException($"Unknown CD-ROM speed {(uint)cdSpeed} in the save state.");
        }

        LoadedCdSpeed = cdSpeed;

        if (reader is StateSectionReader { Version: 5 })
        {
            LoadedRegion = null;
            return;
        }

        var region = (VideoRegion)reader.ReadUInt32();
        if (region is not VideoRegion.Ntsc and not VideoRegion.Pal1 and not VideoRegion.Pal2)
        {
            throw new InvalidStateException($"Unknown video region {(uint)region} in the save state.");
        }

        LoadedRegion = region;
    }
}
