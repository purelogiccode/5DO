using System;

namespace FiveDo.Core;

/// <summary>
/// Describes one machine option that can change guest-visible emulation.
/// </summary>
/// <remarks>
/// Definitions are exposed by <see cref="MachineOptionRegistry"/> and drive validation,
/// save-state coverage, CLI/Settings/profile parity tests, and the generated option
/// reference. Purely host-side presentation settings (filters, overlays, window sizes)
/// are deliberately excluded.
/// </remarks>
public sealed class MachineOptionDefinition
{
    internal MachineOptionDefinition(
        string key,
        string displayName,
        string description,
        string defaultValue,
        string range,
        string cliOption,
        string settingsProperty,
        string profileProperty,
        string tuningProperty,
        MachineOptionKind kind,
        Type? enumType = null,
        string comparisonLabel = "value")
    {
        Key = key;
        DisplayName = displayName;
        Description = description;
        DefaultValue = defaultValue;
        Range = range;
        CliOption = cliOption;
        SettingsProperty = settingsProperty;
        ProfileProperty = profileProperty;
        TuningProperty = tuningProperty;
        Kind = kind;
        EnumType = enumType;
        ComparisonLabel = comparisonLabel;
    }

    /// <summary>Gets the stable camel-case registry key.</summary>
    public string Key { get; }

    /// <summary>Gets the human-readable option name used in warnings and documentation.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the option description.</summary>
    public string Description { get; }

    /// <summary>Gets the default value as text.</summary>
    public string DefaultValue { get; }

    /// <summary>Gets the accepted range or value list.</summary>
    public string Range { get; }

    /// <summary>Gets the CLI flag that sets the option.</summary>
    public string CliOption { get; }

    /// <summary>Gets the <see cref="object"/> property name used by desktop settings.</summary>
    public string SettingsProperty { get; }

    /// <summary>Gets the per-game profile property name.</summary>
    public string ProfileProperty { get; }

    /// <summary>Gets the <see cref="EmulatorTuning"/> property name.</summary>
    public string TuningProperty { get; }

    /// <summary>Gets the value kind.</summary>
    public MachineOptionKind Kind { get; }

    /// <summary>Gets the enum type for <see cref="MachineOptionKind.Enum"/> options.</summary>
    public Type? EnumType { get; }

    /// <summary>Gets the noun used by "differs from the machine ..." mismatch warnings.</summary>
    public string ComparisonLabel { get; }

    /// <summary>Gets a value indicating whether the option is recorded in the Options state section.</summary>
    /// <remarks>Every registered machine option is recorded; the property exists so the registry contract is explicit.</remarks>
    public bool RecordedInState { get; } = true;
}

/// <summary>Classifies a machine option value.</summary>
public enum MachineOptionKind
{
    /// <summary>A named enum value.</summary>
    Enum,

    /// <summary>A whole number with a range.</summary>
    Number,

    /// <summary>A boolean flag.</summary>
    Boolean,

    /// <summary>A bit mask.</summary>
    Flags,

    /// <summary>An unsigned 32-bit seed that may be absent.</summary>
    Seed,
}
