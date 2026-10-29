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

    /// <summary>Validates the tuning values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a clock or size is invalid.</exception>
    public void Validate()
    {
        ValidateArmClock(ArmClock);
        ValidateCpuBackend(CpuBackend);
    }

    private static void ValidateArmClock(int armClock)
    {
        if (armClock <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(armClock), armClock, "The ARM clock must be positive.");
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
