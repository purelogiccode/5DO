using System;
using System.Collections.Generic;
using System.Linq;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Host;

/// <summary>
/// A per-game snapshot of the machine and presentation options that can differ by title.
/// </summary>
/// <remarks>
/// The profile is stored in <see cref="AppSettings.GameProfiles"/> keyed by 4DO game ID.
/// Scalar timing/CD-DA fields apply to a copied settings object at machine creation; the
/// BIOS/fix/filter/input/NVRAM fields update the corresponding per-game overrides. The
/// live settings are never mutated by <see cref="AppSettings.CreateEffectiveForGame"/>.
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

    /// <summary>Gets or sets the pinned BIOS MD5, or an empty string for automatic selection.</summary>
    public string BiosMd5 { get; set; } = string.Empty;

    /// <summary>Gets or sets the per-game fix mask, or <see langword="null"/> for automatic fixes.</summary>
    public FixMode? FixMode { get; set; }

    /// <summary>Gets or sets the per-game video filter, or <see langword="null"/> for the global filter.</summary>
    public VideoFilter? Filter { get; set; }

    /// <summary>Gets or sets the per-game high-resolution mode, or <see langword="null"/> for the global mode.</summary>
    public bool? HighResolution { get; set; }

    /// <summary>Gets or sets the per-game presentation crop, or <see langword="null"/> for the global crop.</summary>
    public CropInsets? Overscan { get; set; }

    /// <summary>Gets or sets the six per-game PBUS devices, or an empty list for the global list.</summary>
    public IList<string> InputDevices { get; set; } = new List<string>();

    /// <summary>Gets or sets the per-game NVRAM storage mode, or <see langword="null"/> for the global mode.</summary>
    public NvramStorageMode? NvramStorageMode { get; set; }

    /// <summary>Gets or sets the per-game NVRAM version (0-9), or <see langword="null"/> for the global version.</summary>
    public int? NvramVersion { get; set; }

    /// <summary>Captures the current global timing, CD-DA, and presentation options.</summary>
    /// <param name="settings">Source settings.</param>
    /// <param name="gameId">Optional game ID used to snapshot per-game overrides.</param>
    /// <returns>A profile snapshot.</returns>
    public static GameProfile FromSettings(AppSettings settings, string? gameId = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var profile = new GameProfile
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
            HighResolution = settings.HighResolution,
            Overscan = settings.ResolveOverscan(),
            NvramStorageMode = settings.NvramStorageMode,
            NvramVersion = settings.NvramVersion,
        };

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            profile.BiosMd5 = settings.GetBiosOverride(gameId) ?? string.Empty;
            profile.FixMode = settings.GetFixModeOverride(gameId);
            profile.Filter = settings.GetFilterOverride(gameId);
            profile.InputDevices = settings.GetInputDeviceOverride(gameId)?.ToList() ?? [];
        }

        return profile;
    }

    /// <summary>Applies this profile to the supplied settings.</summary>
    /// <param name="settings">Destination settings.</param>
    /// <param name="gameId">Optional game ID used to apply per-game overrides.</param>
    public void ApplyTo(AppSettings settings, string? gameId = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
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

        if (string.IsNullOrWhiteSpace(gameId))
        {
            return;
        }

        settings.SetBiosOverride(gameId, string.IsNullOrWhiteSpace(BiosMd5) ? null : BiosMd5);
        settings.SetFixModeOverride(gameId, FixMode);
        settings.SetFilterOverride(gameId, Filter);
        if (HighResolution is { } highResolution)
        {
            settings.HighResolution = highResolution;
        }

        if (Overscan is { } overscan)
        {
            settings.SetOverscan(overscan);
        }

        settings.SetInputDeviceOverride(
            gameId,
            InputDevices.Count == 0 ? null : new List<string>(InputDevices));
        if (NvramStorageMode is { } nvramStorageMode)
        {
            settings.NvramStorageMode = nvramStorageMode;
        }

        if (NvramVersion is { } nvramVersion)
        {
            settings.NvramVersion = nvramVersion;
        }
    }
}
