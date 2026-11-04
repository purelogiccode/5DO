using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FiveDo.Core;

/// <summary>
/// The single source of truth for machine options.
/// </summary>
/// <remarks>
/// Every option that can change guest-visible emulation is registered here. The registry
/// validates values, maps options to their CLI flag, desktop setting, per-game profile
/// property, and <see cref="EmulatorTuning"/> property, formats warning/documentation text,
/// and describes which values are recorded in save states.
/// </remarks>
public static class MachineOptionRegistry
{
    private static readonly MachineOptionDefinition[] Definitions =
    [
        new(
            "cpuClock",
            "ARM clock",
            "ARM60 clock multiplier. 1x is the stock 12.5 MHz; 0.5x-4x is supported.",
            "1x (12.5 MHz)",
            "0.5x-4x (6.25-50 MHz)",
            "--cpu-clock",
            "CpuClockMultiplier",
            "CpuClockMultiplier",
            nameof(EmulatorTuning.ArmClock),
            MachineOptionKind.Number,
            comparisonLabel: "clock"),
        new(
            "region",
            "video region",
            "NTSC 320x240 at 60 Hz, PAL1 320x288 at 50 Hz, or PAL2 384x288 at 50 Hz.",
            "Ntsc",
            "Ntsc, Pal1, Pal2",
            "--region",
            "Region",
            "Region",
            nameof(EmulatorTuning.Region),
            MachineOptionKind.Enum,
            typeof(VideoRegion),
            comparisonLabel: "region"),
        new(
            "armTiming",
            "ARM timing",
            "ARM60 N-cycle class: FreeDO-compatible N=4 or the ARM60 datasheet N=2.",
            "FreeDoCompatible",
            "FreeDoCompatible, Arm60Datasheet",
            "--arm-timing",
            "ArmTiming",
            "ArmTiming",
            nameof(EmulatorTuning.ArmTiming),
            MachineOptionKind.Enum,
            typeof(ArmTimingMode),
            comparisonLabel: "timing"),
        new(
            "clioTimers",
            "CLIO timer mode",
            "CLIO timer scan: FreeDO's all-timer scan or Opera's per-slot scheduler.",
            "FreeDoCompatible",
            "FreeDoCompatible, OperaScheduler",
            "--clio-timers",
            "ClioTimers",
            "ClioTimers",
            nameof(EmulatorTuning.ClioTimers),
            MachineOptionKind.Enum,
            typeof(ClioTimerMode),
            comparisonLabel: "mode"),
        new(
            "celTiming",
            "CEL timing mode",
            "MADAM CEL timing: synchronous FreeDO draws or Opera's pixel/texel busy deadline.",
            "FreeDoCompatible",
            "FreeDoCompatible, OperaBusy",
            "--cel-timing",
            "CelTiming",
            "CelTiming",
            nameof(EmulatorTuning.CelTiming),
            MachineOptionKind.Enum,
            typeof(CelTimingMode),
            comparisonLabel: "mode"),
        new(
            "cdromTiming",
            "CD-ROM timing mode",
            "CD-ROM data-sector timing: immediate FreeDO transfers or Opera's cycle-based readiness.",
            "FreeDoCompatible",
            "FreeDoCompatible, OperaCycleBased",
            "--cd-timing",
            "CdromTiming",
            "CdromTiming",
            nameof(EmulatorTuning.CdromTiming),
            MachineOptionKind.Enum,
            typeof(CdromTimingMode),
            comparisonLabel: "mode"),
        new(
            "cdSpeed",
            "CD-ROM speed",
            "Effective read speed for the cycle-based CD-ROM model. The stock 3DO drive is 2x.",
            "X2",
            "Unbounded, X1, X2, X4, X8, X16, X24, X32",
            "--cd-speed",
            "CdSpeed",
            "CdSpeed",
            nameof(EmulatorTuning.CdSpeed),
            MachineOptionKind.Enum,
            typeof(CdSpeed),
            comparisonLabel: "speed"),
        new(
            "randomSeed",
            "random seed",
            "Initial DSP PRNG seed. Blank uses FreeDO's default 0xa5a5a5a5.",
            "default (0xa5a5a5a5)",
            "default or a 32-bit value",
            "--random-seed",
            "RandomSeed",
            "RandomSeed",
            nameof(EmulatorTuning.RandomSeed),
            MachineOptionKind.Seed,
            comparisonLabel: "seed"),
        new(
            "fixMode",
            "fix mode",
            "Per-game 4DO compatibility fix mask.",
            "None",
            "0x00000000-0xFFFFFFFF",
            "--fix-mode",
            "FixModeOverrides",
            "FixMode",
            nameof(EmulatorTuning.FixMode),
            MachineOptionKind.Flags,
            comparisonLabel: "mode"),
        new(
            "cpuBackend",
            "CPU backend",
            "Guest ARM60 backend. The interpreter is the reference and the only implemented backend.",
            "Interpreter",
            "Interpreter",
            "--cpu-backend",
            "CpuBackend",
            "CpuBackend",
            nameof(EmulatorTuning.CpuBackend),
            MachineOptionKind.Enum,
            typeof(CpuBackendKind),
            comparisonLabel: "backend"),
        new(
            "highResolution",
            "high-resolution mode",
            "Opt-in 640x480 CEL/VDLP rendering.",
            "disabled",
            "enabled, disabled",
            "--high-res",
            "HighResolution",
            "HighResolution",
            nameof(EmulatorTuning.HighResolution),
            MachineOptionKind.Boolean,
            comparisonLabel: "mode"),
        new(
            "operaDipir",
            "Opera DIPIR",
            "CLIO DIPIR latches and XBUS access aborts.",
            "disabled",
            "enabled, disabled",
            "--opera-dipir",
            "OperaDipir",
            "OperaDipir",
            nameof(EmulatorTuning.OperaDipir),
            MachineOptionKind.Boolean,
            comparisonLabel: "setting"),
        new(
            "operaSoftReset",
            "Opera soft reset",
            "CLIO CSTAT soft resets use Opera's ARM/MADAM/DSP reset path.",
            "disabled",
            "enabled, disabled",
            "--opera-soft-reset",
            "OperaSoftReset",
            "OperaSoftReset",
            nameof(EmulatorTuning.OperaSoftReset),
            MachineOptionKind.Boolean,
            comparisonLabel: "setting"),
    ];

    private static readonly Dictionary<string, MachineOptionDefinition> ByKey =
        Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets every registered machine option in documentation order.</summary>
    public static IReadOnlyList<MachineOptionDefinition> Options { get; } = Array.AsReadOnly(Definitions);

    /// <summary>Gets a definition by key.</summary>
    /// <param name="key">Stable registry key.</param>
    /// <returns>The definition.</returns>
    /// <exception cref="UnknownMachineOptionException">Thrown when the key is unknown.</exception>
    public static MachineOptionDefinition Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ByKey.TryGetValue(key, out MachineOptionDefinition? definition)
            ? definition
            : throw new UnknownMachineOptionException(key);
    }

    /// <summary>Validates every option in a tuning record.</summary>
    /// <param name="tuning">Tuning to validate.</param>
    /// <exception cref="MachineOptionValidationException">Thrown when an option is invalid.</exception>
    public static void Validate(EmulatorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        if (tuning.ArmClock <= 0)
        {
            throw Invalid("cpuClock", tuning.ArmClock, "The ARM clock must be positive.");
        }

        ValidateEnum("region", tuning.Region);
        ValidateEnum("armTiming", tuning.ArmTiming);
        ValidateEnum("clioTimers", tuning.ClioTimers);
        ValidateEnum("celTiming", tuning.CelTiming);
        ValidateEnum("cdromTiming", tuning.CdromTiming);
        ValidateEnum("cdSpeed", tuning.CdSpeed);
        ValidateEnum("cpuBackend", tuning.CpuBackend);
        if (tuning.CpuBackend != CpuBackendKind.Interpreter)
        {
            throw Invalid(
                "cpuBackend",
                tuning.CpuBackend,
                "The managed IL JIT backend is not implemented until M12.");
        }
    }

    /// <summary>Formats one option from a tuning record using the canonical text form.</summary>
    /// <param name="key">Stable registry key.</param>
    /// <param name="tuning">Source tuning.</param>
    /// <returns>The formatted value.</returns>
    /// <exception cref="UnknownMachineOptionException">Thrown when the key is unknown.</exception>
    public static string FormatValue(string key, EmulatorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        return Get(key).Key switch
        {
            "cpuClock" => tuning.ArmClock.ToString(CultureInfo.InvariantCulture),
            "region" => tuning.Region.ToString(),
            "armTiming" => tuning.ArmTiming.ToString(),
            "clioTimers" => tuning.ClioTimers.ToString(),
            "celTiming" => tuning.CelTiming.ToString(),
            "cdromTiming" => tuning.CdromTiming.ToString(),
            "cdSpeed" => tuning.CdSpeed.ToString(),
            "randomSeed" => FormatSeed(tuning.RandomSeed),
            "fixMode" => FormatFixMode(tuning.FixMode),
            "cpuBackend" => tuning.CpuBackend.ToString(),
            "highResolution" => FormatFlag(tuning.HighResolution),
            "operaDipir" => FormatFlag(tuning.OperaDipir),
            "operaSoftReset" => FormatFlag(tuning.OperaSoftReset),
            _ => throw new UnknownMachineOptionException(key),
        };
    }

    /// <summary>Describes every option in a tuning record.</summary>
    /// <param name="tuning">Source tuning.</param>
    /// <returns>An ordered key/value snapshot.</returns>
    public static IReadOnlyDictionary<string, string> Describe(EmulatorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var values = new Dictionary<string, string>(Definitions.Length, StringComparer.Ordinal);
        foreach (MachineOptionDefinition definition in Definitions)
        {
            values[definition.Key] = FormatValue(definition.Key, tuning);
        }

        return values;
    }

    /// <summary>
    /// Compares a state snapshot with the running machine and returns one mismatch per
    /// recorded option that differs. Options missing from the state are skipped.
    /// </summary>
    /// <param name="tuning">Current machine tuning.</param>
    /// <param name="snapshot">Options read from a save state.</param>
    /// <returns>Mismatch descriptions in registry order.</returns>
    public static IReadOnlyList<MachineOptionMismatch> Compare(
        EmulatorTuning tuning,
        MachineOptionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(snapshot);
        var mismatches = new List<MachineOptionMismatch>();
        foreach (MachineOptionDefinition definition in Definitions)
        {
            var saved = FormatSnapshotValue(definition.Key, snapshot);
            if (saved is null)
            {
                continue;
            }

            var current = FormatValue(definition.Key, tuning);
            if (!string.Equals(saved, current, StringComparison.Ordinal))
            {
                mismatches.Add(new MachineOptionMismatch(
                    definition.Key,
                    definition.DisplayName,
                    definition.ComparisonLabel,
                    saved,
                    current));
            }
        }

        return mismatches;
    }

    /// <summary>Generates the Markdown machine-option reference.</summary>
    /// <returns>A deterministic Markdown document.</returns>
    public static string ToMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Machine options");
        builder.AppendLine();
        builder.AppendLine("This file is generated from `MachineOptionRegistry` by `5do settings options --format markdown`.");
        builder.AppendLine("Do not edit the table by hand; update the registry and regenerate the file.");
        builder.AppendLine();
        builder.AppendLine("Each option is validated by the machine, reachable from the CLI, desktop settings, and per-game profiles, and recorded in save states.");
        builder.AppendLine();
        builder.AppendLine("| Key | CLI | Settings | Profile | Type | Default | Range | State | Description |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (MachineOptionDefinition definition in Definitions)
        {
            builder.Append('|')
                .Append(' ').Append(Escape(definition.Key)).Append(" |")
                .Append(' ').Append(Escape(definition.CliOption)).Append(" |")
                .Append(' ').Append(Escape(definition.SettingsProperty)).Append(" |")
                .Append(' ').Append(Escape(definition.ProfileProperty)).Append(" |")
                .Append(' ').Append(Escape(FormatKind(definition))).Append(" |")
                .Append(' ').Append(Escape(definition.DefaultValue)).Append(" |")
                .Append(' ').Append(Escape(definition.Range)).Append(" |")
                .Append(' ').Append(definition.RecordedInState ? "yes" : "no").Append(" |")
                .Append(' ').Append(Escape(definition.Description)).Append(" |")
                .AppendLine();
        }

        return builder.ToString();
    }

    private static void ValidateEnum<T>(string key, T value)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw Invalid(key, value, $"Unknown {Get(key).DisplayName} value.");
        }
    }

    private static string? FormatSnapshotValue(string key, MachineOptionSnapshot snapshot)
    {
        return key switch
        {
            "cpuClock" => snapshot.ArmClock?.ToString(CultureInfo.InvariantCulture),
            "region" => snapshot.Region?.ToString(),
            "armTiming" => snapshot.ArmTiming?.ToString(),
            "clioTimers" => snapshot.ClioTimers?.ToString(),
            "celTiming" => snapshot.CelTiming?.ToString(),
            "cdromTiming" => snapshot.CdromTiming?.ToString(),
            "cdSpeed" => snapshot.CdSpeed?.ToString(),
            "randomSeed" => snapshot.RandomSeedSpecified ? FormatSeed(snapshot.RandomSeed) : null,
            "fixMode" => snapshot.FixMode is { } fixMode ? FormatFixMode(fixMode) : null,
            "cpuBackend" => snapshot.CpuBackend?.ToString(),
            "highResolution" => snapshot.HighResolution is { } highResolution
                ? FormatFlag(highResolution)
                : null,
            "operaDipir" => snapshot.OperaDipir is { } operaDipir ? FormatFlag(operaDipir) : null,
            "operaSoftReset" => snapshot.OperaSoftReset is { } softReset ? FormatFlag(softReset) : null,
            _ => throw new UnknownMachineOptionException(key),
        };
    }

    private static MachineOptionValidationException Invalid(string key, object? value, string message)
    {
        return new MachineOptionValidationException(key, value, message);
    }

    private static string FormatSeed(uint? seed) =>
        seed is { } value ? $"0x{value:x8}" : "default";

    private static string FormatFixMode(FixMode fixMode) => $"0x{(uint)fixMode:x8}";

    private static string FormatFlag(bool value) => value ? "enabled" : "disabled";

    private static string FormatKind(MachineOptionDefinition definition)
    {
        return definition.Kind switch
        {
            MachineOptionKind.Enum => $"enum ({definition.EnumType?.Name ?? "unknown"})",
            MachineOptionKind.Number => "number",
            MachineOptionKind.Boolean => "boolean",
            MachineOptionKind.Flags => "flags",
            MachineOptionKind.Seed => "seed",
            _ => "unknown",
        };
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}

/// <summary>Describes one option that differs between a save state and the running machine.</summary>
/// <param name="Key">Stable registry key.</param>
/// <param name="DisplayName">Human-readable option name.</param>
/// <param name="ComparisonLabel">Noun used in the warning.</param>
/// <param name="SavedValue">Value recorded in the state.</param>
/// <param name="CurrentValue">Value on the running machine.</param>
public sealed record MachineOptionMismatch(
    string Key,
    string DisplayName,
    string ComparisonLabel,
    string SavedValue,
    string CurrentValue)
{
    /// <summary>Gets the user-facing warning text.</summary>
    public string Message =>
        $"State {DisplayName} {SavedValue} differs from the machine {ComparisonLabel} {CurrentValue}.";
}
