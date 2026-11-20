using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        ("Orbatak", InputDeviceType.OrbatakTrackball),
    ];

    // Retail Gamegun titles keep a pad on port 1 for menus, calibration, and
    // non-lightgun sections. The gun is placed on port 2, matching the real
    // daisy-chained Gamegun + controller layout these games expect.
    private static readonly (string Prefix, string Spec)[] KnownSpecs =
    [
        ("Crime Patrol", "1=joypad,2=lightgun"),
        ("Crime Patrol 2", "1=joypad,2=lightgun"),
        ("Corpse Killer", "1=joypad,2=lightgun"),
        // Demolition Man ignores the pad at boot while a Gamegun is attached.
        // Start with the pad, then connect the Gamegun from the Quick Menu or F1.
        ("Demolition Man", "1=joypad"),
        ("Drug Wars", "1=joypad,2=lightgun"),
        ("Mad Dog II", "1=joypad,2=lightgun"),
        ("Mad Dog McCree", "1=joypad,2=lightgun"),
        ("Shootout at Old Tucson", "1=joypad,2=lightgun"),
        ("Space Pirates", "1=joypad,2=lightgun"),
        ("The Last Bounty Hunter", "1=joypad,2=lightgun"),
        ("Who Shot Johnny Rock", "1=joypad,2=lightgun"),
    ];

    /// <summary>Resolves the automatic device list for a title.</summary>
    /// <param name="title">Game title, with or without a region suffix.</param>
    /// <param name="databaseSpec">Optional game-database device list such as <c>1=lightgun</c>.</param>
    /// <returns>Eight device names, or <see langword="null"/> when the title needs no override.</returns>
    public static IReadOnlyList<string>? TryGetInputDevices(string? title, string? databaseSpec = null)
    {
        IReadOnlyList<string>? fromDatabase = ParseDeviceSpec(databaseSpec);
        if (fromDatabase is not null)
        {
            return fromDatabase;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        foreach (var (prefix, spec) in KnownSpecs)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return ParseDeviceSpec(spec);
            }
        }

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

    /// <summary>Parses a compact per-port device list from the game database.</summary>
    /// <param name="spec">Spec such as <c>1=lightgun,2=mouse</c>; ports are one-based.</param>
    /// <returns>Eight device names, or <see langword="null"/> when the spec is empty or invalid.</returns>
    public static IReadOnlyList<string>? ParseDeviceSpec(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return null;
        }

        var devices = new string[PbusEncoder.MaxPorts];
        Array.Fill(devices, InputDeviceType.Joypad.ToString());
        var assigned = false;
        foreach (string entry in spec.Split(
                     [',', ';'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)
                || port < 1
                || port > devices.Length
                || !TryParseDevice(parts[1], out InputDeviceType device))
            {
                continue;
            }

            devices[port - 1] = device.ToString();
            assigned = true;
        }

        return assigned ? devices : null;
    }

    private static bool TryParseDevice(string value, out InputDeviceType device)
    {
        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        foreach (InputDeviceType candidate in Enum.GetValues<InputDeviceType>())
        {
            if (string.Equals(
                    new string(candidate.ToString().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant(),
                    normalized,
                    StringComparison.Ordinal))
            {
                device = candidate;
                return true;
            }
        }

        device = InputDeviceType.Joypad;
        return false;
    }
}
