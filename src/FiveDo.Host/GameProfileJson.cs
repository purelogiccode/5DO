using System;
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

        if (!double.IsFinite(profile.CpuClockMultiplier)
            || profile.CpuClockMultiplier is < 0.5 or > 4.0)
        {
            profile.CpuClockMultiplier = 1.0;
        }

        if (!float.IsFinite(profile.CdAudioVolume) || profile.CdAudioVolume is < 0 or > 1)
        {
            profile.CdAudioVolume = 0.8f;
        }
    }
}
