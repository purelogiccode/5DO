using System.Collections.Generic;

namespace FiveDo.Cli;

internal sealed record CliSettingsValidationResult(
    bool Ok,
    string Path,
    bool Exists,
    bool Valid,
    int Version,
    string? Error);

internal sealed record CliSettingsMigrationResult(
    bool Ok,
    string Path,
    int FromVersion,
    int ToVersion,
    bool Changed);

internal sealed record CliSettingsProfile(string Name, string Path, int Version);

internal sealed record CliSettingsProfilesResult(
    bool Ok,
    string Directory,
    IReadOnlyList<CliSettingsProfile> Profiles);

internal sealed record CliMachineOptionResult(
    string Key,
    string Cli,
    string Settings,
    string Profile,
    string Default,
    string Range,
    bool RecordedInState,
    string Description);

internal sealed record CliMachineOptionsResult(
    bool Ok,
    IReadOnlyList<CliMachineOptionResult> Options);
