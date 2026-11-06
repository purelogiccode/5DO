using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>Source-generated JSON metadata for exported game profiles.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(GameProfile))]
internal sealed partial class GameProfileJsonContext : JsonSerializerContext;

/// <summary>Imports and exports per-game profiles as JSON.</summary>
public static class GameProfileJson
{
    /// <summary>Serializes a profile.</summary>
    /// <param name="profile">Profile to export.</param>
    /// <returns>Indented JSON.</returns>
    public static string Serialize(GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, GameProfileJsonContext.Default.GameProfile);
    }

    /// <summary>Deserializes and normalizes a profile.</summary>
    /// <param name="json">JSON text.</param>
    /// <returns>The imported profile.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is empty.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed.</exception>
    public static GameProfile Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        GameProfile? profile = JsonSerializer.Deserialize(
            json,
            GameProfileJsonContext.Default.GameProfile);
        if (profile is null)
        {
            throw new JsonException("The profile JSON was empty.");
        }

        Normalize(profile);
        return profile;
    }

    private static string NormalizeMd5(string? md5)
    {
        if (string.IsNullOrWhiteSpace(md5))
        {
            return string.Empty;
        }

        var normalized = md5.Trim().ToUpperInvariant();
        if (normalized.Length != 32)
        {
            return string.Empty;
        }

        foreach (var character in normalized)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return string.Empty;
            }
        }

        return normalized;
    }

    private static void Normalize(GameProfile profile)
    {
        if (!Enum.IsDefined(profile.Profile))
        {
            profile.Profile = EmulationProfile.Custom;
        }

        if (!Enum.IsDefined(profile.Region))
        {
            profile.Region = VideoRegion.Ntsc;
        }

        if (!Enum.IsDefined(profile.ArmTiming))
        {
            profile.ArmTiming = ArmTimingMode.FreeDoCompatible;
        }

        if (!Enum.IsDefined(profile.ClioTimers))
        {
            profile.ClioTimers = ClioTimerMode.FreeDoCompatible;
        }

        if (!Enum.IsDefined(profile.CelTiming))
        {
            profile.CelTiming = CelTimingMode.FreeDoCompatible;
        }

        if (!Enum.IsDefined(profile.CdromTiming))
        {
            profile.CdromTiming = CdromTimingMode.FreeDoCompatible;
        }

        if (!Enum.IsDefined(profile.CdSpeed))
        {
            profile.CdSpeed = CdSpeed.X2;
        }

        if (!Enum.IsDefined(profile.MemoryConfiguration))
        {
            profile.MemoryConfiguration = MemoryConfiguration.Dram2MbVram1Mb;
        }

        if (!Enum.IsDefined(profile.CpuBackend))
        {
            profile.CpuBackend = CpuBackendKind.Interpreter;
        }

        if (!double.IsFinite(profile.CpuClockMultiplier)
            || profile.CpuClockMultiplier is < 0.5 or > 4.0)
        {
            profile.CpuClockMultiplier = 1.0;
        }

        if (!float.IsFinite(profile.CdAudioVolume) || profile.CdAudioVolume is < 0 or > 1)
        {
            profile.CdAudioVolume = 0.8f;
        }

        if (profile.RunAheadFrames is < 0 or > AppSettings.MaxRunAheadFrames)
        {
            profile.RunAheadFrames = null;
        }

        if (profile.FrameDelayFrames is < 0 or > AppSettings.MaxFrameDelayFrames)
        {
            profile.FrameDelayFrames = null;
        }

        if (profile.AudioSyncMilliseconds is < AppSettings.MinAudioSyncMilliseconds
            or > AppSettings.MaxAudioSyncMilliseconds)
        {
            profile.AudioSyncMilliseconds = null;
        }

        profile.BiosMd5 = NormalizeMd5(profile.BiosMd5);
        if (profile.FixMode is { } fixMode && !Enum.IsDefined(fixMode))
        {
            profile.FixMode = null;
        }

        if (profile.Filter is { } filter && !Enum.IsDefined(filter))
        {
            profile.Filter = null;
        }

        if (profile.Deinterlace is { } deinterlace && !Enum.IsDefined(deinterlace))
        {
            profile.Deinterlace = null;
        }

        if (profile.NvramStorageMode is { } nvramMode && !Enum.IsDefined(nvramMode))
        {
            profile.NvramStorageMode = null;
        }

        if (profile.NvramVersion is { } nvramVersion && nvramVersion is < 0 or > 9)
        {
            profile.NvramVersion = null;
        }

        if (profile.Overscan is { } overscan)
        {
            profile.Overscan = overscan.Clamp();
        }

        profile.InputDevices = profile.InputDevices.Count == 0
            ? []
            : AppSettings.NormalizeInputDevices(profile.InputDevices).ToList();
    }
}
