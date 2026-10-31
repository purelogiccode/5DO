using System;

namespace FiveDo.Core;

/// <summary>
/// Maps <see cref="EmulationProfile"/> values to machine timing options.
/// </summary>
/// <remarks>
/// Profiles never change per-game fixes, high-resolution rendering, the CPU backend, or the
/// random seed. The <see cref="EmulationProfile.Performance"/> profile deliberately trades
/// accuracy for speed with a 1.5x CPU clock; it is opt-in and is not a compatibility
/// guarantee.
/// </remarks>
public static class EmulationProfiles
{
    /// <summary>Gets the CPU clock multiplier used by the performance profile.</summary>
    public const double PerformanceCpuClockMultiplier = 1.5;

    /// <summary>Applies a profile to a tuning record, preserving unrelated options.</summary>
    /// <param name="profile">Profile to apply.</param>
    /// <param name="tuning">Base tuning. <see langword="null"/> uses <see cref="EmulatorTuning.Default"/>.</param>
    /// <returns>A new tuning record with the profile's timing options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the profile is unknown.</exception>
    public static EmulatorTuning Apply(EmulationProfile profile, EmulatorTuning? tuning = null)
    {
        var result = tuning ?? EmulatorTuning.Default;
        return profile switch
        {
            EmulationProfile.Compatible => result with
            {
                ArmTiming = ArmTimingMode.FreeDoCompatible,
                ClioTimers = ClioTimerMode.FreeDoCompatible,
                CelTiming = CelTimingMode.FreeDoCompatible,
                CdromTiming = CdromTimingMode.FreeDoCompatible,
                CdSpeed = CdSpeed.X2,
                ArmClock = EmulatorConstants.ArmClock,
            },
            EmulationProfile.Accuracy => result with
            {
                ArmTiming = ArmTimingMode.Arm60Datasheet,
                ClioTimers = ClioTimerMode.OperaScheduler,
                CelTiming = CelTimingMode.OperaBusy,
                CdromTiming = CdromTimingMode.OperaCycleBased,
                CdSpeed = CdSpeed.X2,
                ArmClock = EmulatorConstants.ArmClock,
            },
            EmulationProfile.Performance => result with
            {
                ArmTiming = ArmTimingMode.FreeDoCompatible,
                ClioTimers = ClioTimerMode.FreeDoCompatible,
                CelTiming = CelTimingMode.FreeDoCompatible,
                CdromTiming = CdromTimingMode.FreeDoCompatible,
                CdSpeed = CdSpeed.X2,
                ArmClock = (int)Math.Round(EmulatorConstants.ArmClock * PerformanceCpuClockMultiplier),
            },
            EmulationProfile.Custom => result,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown emulation profile."),
        };
    }

    /// <summary>Determines which profile matches the timing options in a tuning record.</summary>
    /// <param name="tuning">Tuning to inspect.</param>
    /// <returns>The matching profile, or <see cref="EmulationProfile.Custom"/>.</returns>
    public static EmulationProfile Detect(EmulatorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        foreach (EmulationProfile profile in new[]
                 {
                     EmulationProfile.Compatible,
                     EmulationProfile.Accuracy,
                     EmulationProfile.Performance,
                 })
        {
            if (Matches(profile, tuning))
            {
                return profile;
            }
        }

        return EmulationProfile.Custom;
    }

    /// <summary>Tests whether a tuning record's timing options match a profile.</summary>
    /// <param name="profile">Profile to compare.</param>
    /// <param name="tuning">Tuning to inspect.</param>
    /// <returns><see langword="true"/> when every timing option matches.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the profile is unknown.</exception>
    public static bool Matches(EmulationProfile profile, EmulatorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        if (profile == EmulationProfile.Custom)
        {
            return true;
        }

        EmulatorTuning expected = Apply(profile, tuning);
        return tuning.ArmTiming == expected.ArmTiming
            && tuning.ClioTimers == expected.ClioTimers
            && tuning.CelTiming == expected.CelTiming
            && tuning.CdromTiming == expected.CdromTiming
            && tuning.CdSpeed == expected.CdSpeed
            && tuning.ArmClock == expected.ArmClock;
    }

    /// <summary>Validates a profile value.</summary>
    /// <param name="profile">Profile to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the profile is unknown.</exception>
    public static void Validate(EmulationProfile profile)
    {
        if (profile is not EmulationProfile.Compatible
            and not EmulationProfile.Accuracy
            and not EmulationProfile.Performance
            and not EmulationProfile.Custom)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown emulation profile.");
        }
    }
}
