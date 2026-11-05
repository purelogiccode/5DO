using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Tests.Core;

public sealed class MachineOptionRegistryTests
{
    [Fact]
    public void Registry_EveryOptionIsReachableFromEverySurface()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cliOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MachineOptionDefinition definition in MachineOptionRegistry.Options)
        {
            Assert.True(keys.Add(definition.Key), $"Duplicate machine option key {definition.Key}.");
            Assert.True(
                cliOptions.Add(definition.CliOption),
                $"Duplicate CLI option {definition.CliOption}.");
            Assert.StartsWith("--", definition.CliOption, StringComparison.Ordinal);
            Assert.NotNull(typeof(EmulatorTuning).GetProperty(definition.TuningProperty));
            Assert.NotNull(typeof(AppSettings).GetProperty(definition.SettingsProperty));
            Assert.NotNull(typeof(GameProfile).GetProperty(definition.ProfileProperty));
            Assert.True(definition.RecordedInState);
        }
    }

    [Fact]
    public void Registry_DescribeAndMarkdownCoverEveryOption()
    {
        IReadOnlyDictionary<string, string> description = MachineOptionRegistry.Describe(new EmulatorTuning());
        var markdown = MachineOptionRegistry.ToMarkdown();

        Assert.Equal(MachineOptionRegistry.Options.Count, description.Count);
        foreach (MachineOptionDefinition definition in MachineOptionRegistry.Options)
        {
            Assert.True(description.ContainsKey(definition.Key));
            Assert.Contains(definition.Key, markdown, StringComparison.Ordinal);
            Assert.Contains(definition.CliOption, markdown, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MachineOptionsDocumentation_IsCurrent()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null
            && !File.Exists(Path.Combine(directory, "docs", "MachineOptions.md")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        var path = Path.Combine(directory, "docs", "MachineOptions.md");
        var actual = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var expected = MachineOptionRegistry.ToMarkdown()
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(expected.TrimEnd('\n'), actual.TrimEnd('\n'));
    }

    [Fact]
    public void Registry_UnknownKeyThrowsTypedError()
    {
        UnknownMachineOptionException exception = Assert.Throws<UnknownMachineOptionException>(
            () => MachineOptionRegistry.Get("not-a-machine-option"));

        Assert.Equal("not-a-machine-option", exception.OptionKey);
    }

    [Fact]
    public void Registry_InvalidValueThrowsTypedErrorWithTheOptionKey()
    {
        var tuning = new EmulatorTuning { ArmTiming = (ArmTimingMode)99 };

        MachineOptionValidationException exception = Assert.Throws<MachineOptionValidationException>(
            tuning.Validate);

        Assert.Equal("armTiming", exception.OptionKey);
    }

    [Fact]
    public void Registry_CompareReportsEveryChangedOption()
    {
        EmulatorTuning baseline = CreateNonDefaultTuning();
        MachineOptionSnapshot snapshot = CreateSnapshot(baseline);

        Assert.Empty(MachineOptionRegistry.Compare(baseline, snapshot));

        EmulatorTuning changed = baseline with
        {
            ArmClock = 12_500_000,
            Region = VideoRegion.Pal2,
            ArmTiming = ArmTimingMode.FreeDoCompatible,
            ClioTimers = ClioTimerMode.FreeDoCompatible,
            CelTiming = CelTimingMode.FreeDoCompatible,
            CdromTiming = CdromTimingMode.FreeDoCompatible,
            CdSpeed = CdSpeed.X32,
            MemoryConfiguration = MemoryConfiguration.Dram15MbVram1Mb,
            RandomSeed = null,
            FixMode = FixMode.None,
            CpuBackend = CpuBackendKind.Interpreter,
            HighResolution = false,
            OperaDipir = false,
            OperaSoftReset = false,
        };

        IReadOnlyList<MachineOptionMismatch> mismatches = MachineOptionRegistry.Compare(changed, snapshot);
        // The CPU backend has a single supported value, so it cannot differ.
        string[] expectedKeys = MachineOptionRegistry.Options
            .Select(definition => definition.Key)
            .Where(key => !string.Equals(key, "cpuBackend", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            expectedKeys,
            mismatches.Select(mismatch => mismatch.Key),
            StringComparer.Ordinal);
        Assert.All(mismatches, mismatch => Assert.Contains("differs from the machine", mismatch.Message));
    }

    [Fact]
    public void GameProfile_RoundTripsEveryMachineOption()
    {
        var settings = CreateNonDefaultSettings();
        settings.SetFixModeOverride("A1B2C3D4", FixMode.BitTiming5);
        GameProfile profile = GameProfile.FromSettings(settings, "a1b2c3d4");
        var applied = new AppSettings();

        profile.ApplyTo(applied, "a1b2c3d4");

        Assert.Equal(settings.ArmTiming, applied.ArmTiming);
        Assert.Equal(settings.ClioTimers, applied.ClioTimers);
        Assert.Equal(settings.CelTiming, applied.CelTiming);
        Assert.Equal(settings.CdromTiming, applied.CdromTiming);
        Assert.Equal(settings.CdSpeed, applied.CdSpeed);
        Assert.Equal(settings.MemoryConfiguration, applied.MemoryConfiguration);
        Assert.Equal(settings.CpuClockMultiplier, applied.CpuClockMultiplier);
        Assert.Equal(settings.Region, applied.Region);
        Assert.Equal(settings.RandomSeed, applied.RandomSeed);
        Assert.Equal(settings.CpuBackend, applied.CpuBackend);
        Assert.Equal(settings.HighResolution, applied.HighResolution);
        Assert.Equal(settings.OperaDipir, applied.OperaDipir);
        Assert.Equal(settings.OperaSoftReset, applied.OperaSoftReset);
        Assert.Equal(FixMode.BitTiming5, applied.GetFixModeOverride("a1b2c3d4"));
        Assert.Equal(settings.CreateEmulatorTuning(), applied.CreateEmulatorTuning());
    }

    [Fact]
    public void SettingsStore_RoundTripsEveryMachineOption()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-options-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            AppSettings settings = CreateNonDefaultSettings();

            AppSettingsStore.Save(settings, path);
            AppSettings loaded = AppSettingsStore.Load(path);

            Assert.Equal(EmulatorConstants.ArmClock * 2, loaded.ResolveArmClock());
            MachineOptionRegistry.Validate(loaded.CreateEmulatorTuning());
            Assert.Equal(settings.ArmTiming, loaded.ArmTiming);
            Assert.Equal(settings.ClioTimers, loaded.ClioTimers);
            Assert.Equal(settings.CelTiming, loaded.CelTiming);
            Assert.Equal(settings.CdromTiming, loaded.CdromTiming);
            Assert.Equal(settings.CdSpeed, loaded.CdSpeed);
            Assert.Equal(settings.MemoryConfiguration, loaded.MemoryConfiguration);
            Assert.Equal(settings.Region, loaded.Region);
            Assert.Equal(settings.RandomSeed, loaded.RandomSeed);
            Assert.Equal(settings.CpuBackend, loaded.CpuBackend);
            Assert.Equal(settings.HighResolution, loaded.HighResolution);
            Assert.Equal(settings.OperaDipir, loaded.OperaDipir);
            Assert.Equal(settings.OperaSoftReset, loaded.OperaSoftReset);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static EmulatorTuning CreateNonDefaultTuning()
    {
        return new EmulatorTuning
        {
            ArmClock = 25_000_000,
            Region = VideoRegion.Pal1,
            ArmTiming = ArmTimingMode.Arm60Datasheet,
            ClioTimers = ClioTimerMode.OperaScheduler,
            CelTiming = CelTimingMode.OperaBusy,
            CdromTiming = CdromTimingMode.OperaCycleBased,
            CdSpeed = CdSpeed.X8,
            MemoryConfiguration = MemoryConfiguration.Dram8MbVram2Mb,
            RandomSeed = 0x1234_5678,
            FixMode = FixMode.BitTiming5,
            CpuBackend = CpuBackendKind.Interpreter,
            HighResolution = true,
            OperaDipir = true,
            OperaSoftReset = true,
        };
    }

    private static MachineOptionSnapshot CreateSnapshot(EmulatorTuning tuning)
    {
        return new MachineOptionSnapshot
        {
            ArmClock = tuning.ArmClock,
            Region = tuning.Region,
            ArmTiming = tuning.ArmTiming,
            ClioTimers = tuning.ClioTimers,
            CelTiming = tuning.CelTiming,
            CdromTiming = tuning.CdromTiming,
            CdSpeed = tuning.CdSpeed,
            MemoryConfiguration = tuning.MemoryConfiguration,
            RandomSeed = tuning.RandomSeed,
            RandomSeedSpecified = true,
            FixMode = tuning.FixMode,
            CpuBackend = tuning.CpuBackend,
            HighResolution = tuning.HighResolution,
            OperaDipir = tuning.OperaDipir,
            OperaSoftReset = tuning.OperaSoftReset,
        };
    }

    private static AppSettings CreateNonDefaultSettings()
    {
        return new AppSettings
        {
            CpuClockMultiplier = 2.0,
            Region = VideoRegion.Pal2,
            ArmTiming = ArmTimingMode.Arm60Datasheet,
            ClioTimers = ClioTimerMode.OperaScheduler,
            CelTiming = CelTimingMode.OperaBusy,
            CdromTiming = CdromTimingMode.OperaCycleBased,
            CdSpeed = CdSpeed.X16,
            MemoryConfiguration = MemoryConfiguration.Dram4MbVram1Mb,
            RandomSeed = 0xDEAD_BEEF,
            CpuBackend = CpuBackendKind.Interpreter,
            HighResolution = true,
            OperaDipir = true,
            OperaSoftReset = true,
        };
    }
}
