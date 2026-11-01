using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>
/// A per-game snapshot of the emulation timing and CD-DA mix options.
/// </summary>
/// <remarks>
/// The profile is stored in <see cref="AppSettings.GameProfiles"/> keyed by 4DO game ID.
/// Other per-game overrides (BIOS, fix mode, and video filter) remain in their existing
/// dictionaries; this type covers the options that require a machine restart.
/// </remarks>
public sealed class GameProfile
{
    /// <summary>Gets or sets the timing profile label recorded with the snapshot.</summary>
    public EmulationProfile Profile { get; set; } = EmulationProfile.Custom;

    /// <summary>Gets or sets the video region.</summary>
    public VideoRegion Region { get; set; } = VideoRegion.Ntsc;

    /// <summary>Gets or sets the ARM instruction timing mode.</summary>
    public ArmTimingMode ArmTiming { get; set; } = ArmTimingMode.FreeDoCompatible;

    /// <summary>Gets or sets the CLIO timer scheduler mode.</summary>
    public ClioTimerMode ClioTimers { get; set; } = ClioTimerMode.FreeDoCompatible;

    /// <summary>Gets or sets the CEL busy timing mode.</summary>
    public CelTimingMode CelTiming { get; set; } = CelTimingMode.FreeDoCompatible;

    /// <summary>Gets or sets the CD-ROM timing mode.</summary>
    public CdromTimingMode CdromTiming { get; set; } = CdromTimingMode.FreeDoCompatible;

    /// <summary>Gets or sets the CD-ROM speed.</summary>
    public CdSpeed CdSpeed { get; set; } = CdSpeed.X2;

    /// <summary>Gets or sets the CPU clock multiplier (0.5-4).</summary>
    public double CpuClockMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the CD-DA mix level (0-1).</summary>
    public float CdAudioVolume { get; set; } = 0.8f;

    /// <summary>Gets or sets a value indicating whether CD-DA mixing is muted.</summary>
    public bool CdAudioMuted { get; set; }

    /// <summary>Captures the current global timing and CD-DA options.</summary>
    /// <param name="settings">Source settings.</param>
    /// <returns>A profile snapshot.</returns>
    public static GameProfile FromSettings(AppSettings settings)
    {
        System.ArgumentNullException.ThrowIfNull(settings);
        return new GameProfile
        {
            Profile = settings.Profile,
            Region = settings.Region,
            ArmTiming = settings.ArmTiming,
            ClioTimers = settings.ClioTimers,
            CelTiming = settings.CelTiming,
            CdromTiming = settings.CdromTiming,
            CdSpeed = settings.CdSpeed,
            CpuClockMultiplier = settings.CpuClockMultiplier,
            CdAudioVolume = settings.CdAudioVolume,
            CdAudioMuted = settings.CdAudioMuted,
        };
    }

    /// <summary>Applies this profile to the supplied settings.</summary>
    /// <param name="settings">Destination settings.</param>
    public void ApplyTo(AppSettings settings)
    {
        System.ArgumentNullException.ThrowIfNull(settings);
        settings.Profile = Profile;
        settings.Region = Region;
        settings.ArmTiming = ArmTiming;
        settings.ClioTimers = ClioTimers;
        settings.CelTiming = CelTiming;
        settings.CdromTiming = CdromTiming;
        settings.CdSpeed = CdSpeed;
        settings.CpuClockMultiplier = CpuClockMultiplier;
        settings.CdAudioVolume = CdAudioVolume;
        settings.CdAudioMuted = CdAudioMuted;
    }
}
