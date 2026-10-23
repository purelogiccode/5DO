using System;

namespace ThreeDo.Core;

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

    /// <summary>Gets a value indicating whether the future high-resolution CEL mode is enabled.</summary>
    /// <remarks>High-resolution rendering is not implemented in v1; the flag is reserved for M11.</remarks>
    public bool HighResolution { get; init; }

    /// <summary>Validates the tuning values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a clock or size is invalid.</exception>
    public void Validate()
    {
        if (ArmClock <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ArmClock), ArmClock, "The ARM clock must be positive.");
        }
    }
}
