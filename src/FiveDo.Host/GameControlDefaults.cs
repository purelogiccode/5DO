using System;
using System.Collections.Generic;
using FiveDo.Input;

namespace FiveDo.Host;

/// <summary>
/// Known per-title controller defaults that save users from choosing a peripheral manually.
/// </summary>
/// <remarks>
/// Only titles that require a non-joypad device are listed. The match is a case-insensitive
/// title prefix so regional suffixes such as <c>(USA)</c> or <c>(Europe)</c> do not matter.
/// Users can always replace the automatic choice with a per-game override.
/// </remarks>
public static class GameControlDefaults
{
    private static readonly (string Prefix, InputDeviceType Device)[] KnownDevices =
    [
        ("Crime Patrol", InputDeviceType.Lightgun),
        ("Crime Patrol 2", InputDeviceType.Lightgun),
        ("Drug Wars", InputDeviceType.Lightgun),
        ("Mad Dog II", InputDeviceType.Lightgun),
        ("Mad Dog McCree", InputDeviceType.Lightgun),
        ("Who Shot Johnny Rock", InputDeviceType.Lightgun),
        ("The Last Bounty Hunter", InputDeviceType.Lightgun),
        ("Shootout at Old Tucson", InputDeviceType.Lightgun),
        ("Space Pirates", InputDeviceType.Lightgun),
        ("Orbatak", InputDeviceType.OrbatakTrackball),
    ];

    /// <summary>Resolves the automatic device list for a title.</summary>
    /// <param name="title">Game title, with or without a region suffix.</param>
    /// <returns>Eight device names, or <see langword="null"/> when the title needs no override.</returns>
    public static IReadOnlyList<string>? TryGetInputDevices(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        foreach (var (prefix, device) in KnownDevices)
        {
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var devices = new string[PbusEncoder.MaxPorts];
            devices[0] = device.ToString();
            for (var i = 1; i < devices.Length; i++)
            {
                devices[i] = InputDeviceType.Joypad.ToString();
            }

            return devices;
        }

        return null;
    }
}
