namespace FiveDo.Core;

/// <summary>
/// A partially populated machine option snapshot read from a save state.
/// </summary>
/// <remarks>
/// Null means the state predates the option. <see cref="RandomSeedSpecified"/> separates
/// "the state recorded the default seed" from "the state did not record a seed".
/// </remarks>
public sealed class MachineOptionSnapshot
{
    /// <summary>Gets the ARM timing mode, or <see langword="null"/> when absent.</summary>
    public ArmTimingMode? ArmTiming { get; init; }

    /// <summary>Gets the CLIO timer mode, or <see langword="null"/> when absent.</summary>
    public ClioTimerMode? ClioTimers { get; init; }

    /// <summary>Gets the CEL timing mode, or <see langword="null"/> when absent.</summary>
    public CelTimingMode? CelTiming { get; init; }

    /// <summary>Gets the CD-ROM timing mode, or <see langword="null"/> when absent.</summary>
    public CdromTimingMode? CdromTiming { get; init; }

    /// <summary>Gets the CD-ROM speed, or <see langword="null"/> when absent.</summary>
    public CdSpeed? CdSpeed { get; init; }

    /// <summary>Gets the DRAM/VRAM capacity, or <see langword="null"/> when absent.</summary>
    public MemoryConfiguration? MemoryConfiguration { get; init; }

    /// <summary>Gets the video region, or <see langword="null"/> when absent.</summary>
    public VideoRegion? Region { get; init; }

    /// <summary>Gets the ARM clock in hertz, or <see langword="null"/> when absent.</summary>
    public int? ArmClock { get; init; }

    /// <summary>Gets the recorded DSP seed, or <see langword="null"/> when the default was recorded.</summary>
    public uint? RandomSeed { get; init; }

    /// <summary>Gets a value indicating whether the state recorded a random-seed field.</summary>
    public bool RandomSeedSpecified { get; init; }

    /// <summary>Gets a value indicating whether high-resolution mode was enabled, or <see langword="null"/> when absent.</summary>
    public bool? HighResolution { get; init; }

    /// <summary>Gets a value indicating whether Opera DIPIR semantics were enabled, or <see langword="null"/> when absent.</summary>
    public bool? OperaDipir { get; init; }

    /// <summary>Gets a value indicating whether Opera soft reset was enabled, or <see langword="null"/> when absent.</summary>
    public bool? OperaSoftReset { get; init; }

    /// <summary>Gets the fix-mode mask, or <see langword="null"/> when absent.</summary>
    public FixMode? FixMode { get; init; }

    /// <summary>Gets the CPU backend, or <see langword="null"/> when absent.</summary>
    public CpuBackendKind? CpuBackend { get; init; }
}
