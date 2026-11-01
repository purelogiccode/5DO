using System;

namespace FiveDo.Core;

/// <summary>
/// Immutable machine-wide configuration.
/// </summary>
/// <remarks>
/// Mutable runtime compatibility flags live in <see cref="CompatibilityState"/>, not here.
/// </remarks>
public sealed record EmulatorTuning
{
    /// <summary>Gets the default tuning used by the emulator and tests.</summary>
    public static EmulatorTuning Default { get; } = new();

    /// <summary>Gets the ARM60 clock in hertz. FreeDO: <c>ARM_CLOCK</c>.</summary>
    public int ArmClock { get; init; } = EmulatorConstants.ArmClock;

    /// <summary>Gets the ARM60 N-cycle class. The default is the FreeDO-compatible value.</summary>
    /// <remarks>
    /// See <see cref="ArmTimingMode"/>. This option changes instruction timing, so save
    /// states record the mode and report a warning when a state was written with another
    /// value.
    /// </remarks>
    public ArmTimingMode ArmTiming { get; init; } = ArmTimingMode.FreeDoCompatible;

    /// <summary>
    /// Gets the CLIO timer scan model. The default is the FreeDO-compatible all-timer
    /// scan; <see cref="ClioTimerMode.OperaScheduler"/> walks one timer slot at a time
    /// with an explicit cascade carry.
    /// </summary>
    public ClioTimerMode ClioTimers { get; init; } = ClioTimerMode.FreeDoCompatible;

    /// <summary>
    /// Gets the MADAM CEL timing model. The default draws synchronously like FreeDO;
    /// <see cref="CelTimingMode.OperaBusy"/> keeps the CEL status busy until an estimated
    /// pixel/texel deadline.
    /// </summary>
    public CelTimingMode CelTiming { get; init; } = CelTimingMode.FreeDoCompatible;

    /// <summary>
    /// Gets the CD-ROM data-sector timing model. The default transfers sectors immediately
    /// like FreeDO; <see cref="CdromTimingMode.OperaCycleBased"/> schedules each sector at the
    /// stock 2x drive speed and reports the drive busy while it is not ready.
    /// </summary>
    public CdromTimingMode CdromTiming { get; init; } = CdromTimingMode.FreeDoCompatible;

    /// <summary>
    /// Gets the effective CD-ROM read speed used by
    /// <see cref="CdromTimingMode.OperaCycleBased"/>. The stock 3DO drive is 2x;
    /// <see cref="CdSpeed.Unbounded"/> removes the host-side sector delay.
    /// </summary>
    public CdSpeed CdSpeed { get; init; } = CdSpeed.X2;

    /// <summary>
    /// Gets the optional initial DSP PRNG seed. <see langword="null"/> keeps FreeDO's
    /// default seed (<c>0xa5a5a5a5</c>); a value makes fresh runs reproducible from a
    /// known seed.
    /// </summary>
    public uint? RandomSeed { get; init; }

    /// <summary>Gets the per-game compatibility fixes.</summary>
    public FixMode FixMode { get; init; } = FixMode.None;

    /// <summary>Gets the guest CPU backend. The interpreter is the default and the reference implementation.</summary>
    public CpuBackendKind CpuBackend { get; init; } = CpuBackendKind.Interpreter;

    /// <summary>Gets a value indicating whether the future high-resolution CEL mode is enabled.</summary>
    /// <remarks>High-resolution rendering is not implemented in v1; the flag is reserved for M11.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Reserved public tuning flag for the future high-resolution mode.")]
    public bool HighResolution { get; init; }

    /// <summary>
    /// Gets a value indicating whether CLIO DIPIR latches and XBUS access aborts are
    /// enabled. Opt-in until OP-SYS-03 supplies the matching soft-reset path.
    /// </summary>
    public bool OperaDipir { get; init; }

    /// <summary>
    /// Gets a value indicating whether CLIO CSTAT soft resets use Opera's ARM/MADAM/DSP
    /// reset path. Opt-in until the reset transcript is validated.
    /// </summary>
    public bool OperaSoftReset { get; init; }

    /// <summary>Validates the tuning values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a clock or size is invalid.</exception>
    public void Validate()
    {
        ValidateArmClock(ArmClock);
        ValidateArmTiming(ArmTiming);
        ValidateClioTimers(ClioTimers);
        ValidateCelTiming(CelTiming);
        ValidateCdromTiming(CdromTiming);
        ValidateCdSpeed(CdSpeed);
        ValidateCpuBackend(CpuBackend);
    }

    private static void ValidateArmClock(int armClock)
    {
        if (armClock <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(armClock), armClock, "The ARM clock must be positive.");
        }
    }

    private static void ValidateArmTiming(ArmTimingMode armTiming)
    {
        if (armTiming is not ArmTimingMode.FreeDoCompatible and not ArmTimingMode.Arm60Datasheet)
        {
            throw new ArgumentOutOfRangeException(
                nameof(armTiming),
                armTiming,
                "Unknown ARM timing mode.");
        }
    }

    private static void ValidateClioTimers(ClioTimerMode clioTimers)
    {
        if (clioTimers is not ClioTimerMode.FreeDoCompatible and not ClioTimerMode.OperaScheduler)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clioTimers),
                clioTimers,
                "Unknown CLIO timer mode.");
        }
    }

    private static void ValidateCelTiming(CelTimingMode celTiming)
    {
        if (celTiming is not CelTimingMode.FreeDoCompatible and not CelTimingMode.OperaBusy)
        {
            throw new ArgumentOutOfRangeException(
                nameof(celTiming),
                celTiming,
                "Unknown CEL timing mode.");
        }
    }

    private static void ValidateCdromTiming(CdromTimingMode cdromTiming)
    {
        if (cdromTiming is not CdromTimingMode.FreeDoCompatible
            and not CdromTimingMode.OperaCycleBased)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cdromTiming),
                cdromTiming,
                "Unknown CD-ROM timing mode.");
        }
    }

    private static void ValidateCdSpeed(CdSpeed cdSpeed)
    {
        if (cdSpeed is not CdSpeed.Unbounded
            and not CdSpeed.X1
            and not CdSpeed.X2
            and not CdSpeed.X4
            and not CdSpeed.X8
            and not CdSpeed.X16
            and not CdSpeed.X24
            and not CdSpeed.X32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cdSpeed),
                cdSpeed,
                "Unknown CD-ROM speed.");
        }
    }

    private static void ValidateCpuBackend(CpuBackendKind cpuBackend)
    {
        if (cpuBackend != CpuBackendKind.Interpreter)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cpuBackend),
                cpuBackend,
                "The managed IL JIT backend is not implemented until M12.");
        }
    }
}
