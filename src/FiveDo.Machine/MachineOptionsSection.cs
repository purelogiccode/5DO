using System;
using FiveDo.Core;

namespace FiveDo.Machine;

/// <summary>
/// Records every machine option that affects guest-visible behavior in a save state.
/// </summary>
/// <remarks>
/// The section is optional for backward compatibility: states written before it existed
/// still load with the FreeDO-compatible defaults, and older builds ignore the unknown
/// section. Only options registered in <see cref="MachineOptionRegistry"/> belong here;
/// purely host-side presentation settings do not.
/// </remarks>
internal sealed class MachineOptionsSection : IStateSection
{
    private readonly EmulatorTuning _tuning;
    private readonly VideoRegion _region;
    private readonly FixMode _fixMode;
    private readonly MemoryConfiguration _memoryConfiguration;

    internal MachineOptionsSection(
        EmulatorTuning tuning,
        VideoRegion? region = null,
        FixMode? fixMode = null,
        MemoryConfiguration? memoryConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        _tuning = tuning;
        _region = region ?? tuning.Region;
        _fixMode = fixMode ?? tuning.FixMode;
        _memoryConfiguration = memoryConfiguration ?? tuning.MemoryConfiguration;
    }

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Options;

    /// <summary>Gets the save-state section version.</summary>
    /// <remarks>
    /// Version 2 adds the CLIO timer mode; version 3 adds the MADAM CEL timing mode;
    /// version 4 adds the CD-ROM timing mode; version 5 adds the CD-ROM speed; version 6
    /// adds the video region; version 7 records every registered machine option (ARM clock,
    /// random seed, high-resolution mode, Opera DIPIR, Opera soft reset, fix mask, and CPU
    /// backend); version 8 adds the DRAM/VRAM capacity; version 9 adds the Portfolio OS
    /// SWI HLE flag. Older states default the missing fields to the machine default.
    /// </remarks>
    public ushort Version => 9;

    /// <summary>Gets the option snapshot read from a state.</summary>
    internal MachineOptionSnapshot? Snapshot { get; private set; }

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
        writer.WriteUInt32((uint)_region);
        writer.WriteUInt32((uint)_tuning.ArmClock);
        writer.WriteBoolean(_tuning.RandomSeed.HasValue);
        writer.WriteUInt32(_tuning.RandomSeed ?? 0);
        writer.WriteBoolean(_tuning.HighResolution);
        writer.WriteBoolean(_tuning.OperaDipir);
        writer.WriteBoolean(_tuning.OperaSoftReset);
        writer.WriteUInt32((uint)_fixMode);
        writer.WriteUInt32((uint)_tuning.CpuBackend);
        writer.WriteUInt32((uint)_memoryConfiguration);
        writer.WriteBoolean(_tuning.SwiHle);
    }

    /// <summary>Reads the option snapshot.</summary>
    /// <param name="reader">State reader.</param>
    /// <exception cref="InvalidStateException">Thrown when an option value is unknown.</exception>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var version = reader is StateSectionReader section ? section.Version : Version;
        var armTiming = (ArmTimingMode)reader.ReadUInt32();
        if (armTiming is not ArmTimingMode.FreeDoCompatible and not ArmTimingMode.Arm60Datasheet)
        {
            throw new InvalidStateException($"Unknown ARM timing mode {(uint)armTiming} in the save state.");
        }

        if (version < 2)
        {
            Snapshot = new MachineOptionSnapshot { ArmTiming = armTiming };
            return;
        }

        var clioTimers = (ClioTimerMode)reader.ReadUInt32();
        if (clioTimers is not ClioTimerMode.FreeDoCompatible and not ClioTimerMode.OperaScheduler)
        {
            throw new InvalidStateException($"Unknown CLIO timer mode {(uint)clioTimers} in the save state.");
        }

        if (version < 3)
        {
            Snapshot = new MachineOptionSnapshot
            {
                ArmTiming = armTiming,
                ClioTimers = clioTimers,
            };
            return;
        }

        var celTiming = (CelTimingMode)reader.ReadUInt32();
        if (celTiming is not CelTimingMode.FreeDoCompatible and not CelTimingMode.OperaBusy)
        {
            throw new InvalidStateException($"Unknown CEL timing mode {(uint)celTiming} in the save state.");
        }

        if (version < 4)
        {
            Snapshot = new MachineOptionSnapshot
            {
                ArmTiming = armTiming,
                ClioTimers = clioTimers,
                CelTiming = celTiming,
            };
            return;
        }

        var cdromTiming = (CdromTimingMode)reader.ReadUInt32();
        if (cdromTiming is not CdromTimingMode.FreeDoCompatible
            and not CdromTimingMode.OperaCycleBased)
        {
            throw new InvalidStateException($"Unknown CD-ROM timing mode {(uint)cdromTiming} in the save state.");
        }

        if (version < 5)
        {
            Snapshot = new MachineOptionSnapshot
            {
                ArmTiming = armTiming,
                ClioTimers = clioTimers,
                CelTiming = celTiming,
                CdromTiming = cdromTiming,
            };
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

        if (version < 6)
        {
            Snapshot = new MachineOptionSnapshot
            {
                ArmTiming = armTiming,
                ClioTimers = clioTimers,
                CelTiming = celTiming,
                CdromTiming = cdromTiming,
                CdSpeed = cdSpeed,
            };
            return;
        }

        var region = (VideoRegion)reader.ReadUInt32();
        if (region is not VideoRegion.Ntsc and not VideoRegion.Pal1 and not VideoRegion.Pal2)
        {
            throw new InvalidStateException($"Unknown video region {(uint)region} in the save state.");
        }

        if (version < 7)
        {
            Snapshot = new MachineOptionSnapshot
            {
                ArmTiming = armTiming,
                ClioTimers = clioTimers,
                CelTiming = celTiming,
                CdromTiming = cdromTiming,
                CdSpeed = cdSpeed,
                Region = region,
            };
            return;
        }

        var armClock = reader.ReadUInt32();
        if (armClock == 0 || armClock > int.MaxValue)
        {
            throw new InvalidStateException($"Invalid ARM clock {armClock} in the save state.");
        }

        var hasRandomSeed = reader.ReadBoolean();
        var randomSeed = reader.ReadUInt32();
        var highResolution = reader.ReadBoolean();
        var operaDipir = reader.ReadBoolean();
        var operaSoftReset = reader.ReadBoolean();
        var fixMode = (FixMode)reader.ReadUInt32();
        var cpuBackend = (CpuBackendKind)reader.ReadUInt32();
        if (cpuBackend is not CpuBackendKind.Interpreter and not CpuBackendKind.Jit)
        {
            throw new InvalidStateException($"Unknown CPU backend {(uint)cpuBackend} in the save state.");
        }

        MemoryConfiguration? memoryConfiguration = null;
        if (version >= 8)
        {
            var memory = (MemoryConfiguration)reader.ReadUInt32();
            if (!Enum.IsDefined(memory))
            {
                throw new InvalidStateException(
                    $"Unknown memory configuration 0x{(uint)memory:X2} in the save state.");
            }

            memoryConfiguration = memory;
        }

        bool? swiHle = null;
        if (version >= 9)
        {
            swiHle = reader.ReadBoolean();
        }

        Snapshot = new MachineOptionSnapshot
        {
            ArmTiming = armTiming,
            ClioTimers = clioTimers,
            CelTiming = celTiming,
            CdromTiming = cdromTiming,
            CdSpeed = cdSpeed,
            Region = region,
            ArmClock = checked((int)armClock),
            RandomSeed = hasRandomSeed ? randomSeed : null,
            RandomSeedSpecified = true,
            HighResolution = highResolution,
            OperaDipir = operaDipir,
            OperaSoftReset = operaSoftReset,
            FixMode = fixMode,
            CpuBackend = cpuBackend,
            MemoryConfiguration = memoryConfiguration,
            SwiHle = swiHle,
        };
    }
}
