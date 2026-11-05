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

    /// <summary>Gets the video region and its field timing.</summary>
    /// <remarks>
    /// NTSC is the default. PAL changes the field clock, visible height, and VDL timing
    /// and should be combined with a PAL BIOS for European titles.
    /// </remarks>
    public VideoRegion Region { get; init; } = VideoRegion.Ntsc;

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
    /// Gets the DRAM/VRAM capacity layout. The default is the stock 2 MB DRAM / 1 MB VRAM
    /// machine. Expanded layouts change the CPU-visible RAM boundary and the VRAM base and
    /// are reported to the BIOS through the MADAM RED SYSBITS register.
    /// </summary>
    public MemoryConfiguration MemoryConfiguration { get; init; } = MemoryConfiguration.Dram2MbVram1Mb;

    /// <summary>
    /// Gets the optional initial DSP PRNG seed. <see langword="null"/> keeps FreeDO's
    /// default seed (<c>0xa5a5a5a5</c>); a value makes fresh runs reproducible from a
    /// known seed.
    /// </summary>
    public uint? RandomSeed { get; init; }

    /// <summary>Gets the per-game compatibility fixes.</summary>
    public FixMode FixMode { get; init; } = FixMode.None;

    /// <summary>Gets the guest CPU backend. The interpreter is the default and the reference implementation.</summary>
    /// <remarks>
    /// The managed IL JIT is an optional accelerator for hot straight-line blocks. It
    /// preserves architectural state and cycle counts, and falls back to the interpreter
    /// for unsupported instructions, self-modifying code, and runtimes without dynamic code.
    /// </remarks>
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

    /// <summary>
    /// Gets a value indicating whether Portfolio OS fixed-point math SWIs are handled on
    /// the host instead of running the guest OS implementation.
    /// </summary>
    /// <remarks>
    /// This is Opera's <c>opera_swi_hle</c> option. It is a performance optimization and is
    /// disabled by default; the host routines mirror <c>opera_fixedpoint_math.c</c>
    /// bit-for-bit, including Opera's <c>sqrt_frac16</c> restoring loop. SWIs outside the
    /// Opera HLE set still trap to the guest vector.
    /// </remarks>
    public bool SwiHle { get; init; }

    /// <summary>Validates the tuning values.</summary>
    /// <exception cref="MachineOptionValidationException">Thrown when an option is invalid.</exception>
    public void Validate()
    {
        MachineOptionRegistry.Validate(this);
    }
}
