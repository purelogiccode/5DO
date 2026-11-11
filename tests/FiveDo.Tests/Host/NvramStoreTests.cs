using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Storage;
using FiveDo.Video;

namespace FiveDo.Tests.Host;

public sealed class NvramStoreTests
{
    [Fact]
    public void LoadOrCreate_ReturnsFreshImageWhenMissing()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.ram");
        var store = new NvramStore(path);

        NvramImage image = store.LoadOrCreate();

        Assert.Equal(EmulatorConstants.NvramSize, image.Data.Length);
        Assert.Equal((byte)'n', image.Data.Span[40]);
        Assert.Equal((byte)'v', image.Data.Span[41]);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void MarkChanged_DebouncesWrites()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.ram");
        var clock = new ManualTimeProvider();
        using var store = new NvramStore(path, clock);
        var image = NvramImage.CreateFresh();
        var bytes = image.ToArray();
        bytes[100] = 0xAA;

        store.MarkChanged(bytes);
        Assert.False(store.FlushIfDue());
        Assert.False(File.Exists(path));

        clock.Advance(TimeSpan.FromMilliseconds(249));
        Assert.False(store.FlushIfDue());

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(store.FlushIfDue());
        Assert.True(File.Exists(path));
        Assert.Equal(0xAA, File.ReadAllBytes(path)[100]);
        Assert.False(File.Exists(path + ".tmp"));
        Assert.False(store.FlushIfDue());
    }

    [Fact]
    public void Flush_WritesImmediatelyAndDisposeFlushes()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.ram");
        var store = new NvramStore(path);
        var bytes = NvramImage.CreateFresh().ToArray();
        bytes[200] = 0x55;

        store.MarkChanged(bytes);
        Assert.True(store.Flush());
        Assert.Equal(0x55, File.ReadAllBytes(path)[200]);
        store.Dispose();
    }

    [Fact]
    public void Save_CopiesOverADeleteLockedImage()
    {
        if (!OperatingSystem.IsWindows())
        {
            // The file-share semantics that block File.Move while allowing File.Copy
            // are only enforced on Windows.
            return;
        }

        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.ram");
        using var store = new NvramStore(path);
        var first = NvramImage.CreateFresh().ToArray();
        first[200] = 0x11;
        store.Save(first);

        var second = NvramImage.CreateFresh().ToArray();
        second[200] = 0x22;
        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            store.Save(second);
        }

        Assert.Equal(0x22, File.ReadAllBytes(path)[200]);
    }

    [Fact]
    public void LoadOrCreate_DoesNotThrowWhenTheInvalidImageCannotBeMoved()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.ram");
        File.WriteAllBytes(path, new byte[100]);

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var store = new NvramStore(path);
            NvramImage image = store.LoadOrCreate();

            Assert.Equal(EmulatorConstants.NvramSize, image.Data.Length);
        }

        Assert.False(File.Exists(path + ".bad"));
    }

    [Fact]
    public void LoadOrCreate_PreservesInvalidImageAsBackup()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.ram");
        File.WriteAllBytes(path, new byte[100]);
        var store = new NvramStore(path);

        NvramImage image = store.LoadOrCreate();

        Assert.Equal(EmulatorConstants.NvramSize, image.Data.Length);
        Assert.True(File.Exists(path + ".bad"));
    }

    [Fact]
    public void LoadOrCreate_UsesAFallbackWhenThePrimaryImageIsMissing()
    {
        using var directory = new TempDirectory();
        var fallbackPath = Path.Combine(directory.Path, "nvram.ram");
        var primaryPath = Path.Combine(directory.Path, "nvram", "per-game", "game.0.ram");
        var fallback = NvramImage.CreateFresh().ToArray();
        fallback[123] = 0x42;
        File.WriteAllBytes(fallbackPath, fallback);
        var store = new NvramStore(primaryPath, [fallbackPath]);

        NvramImage image = store.LoadOrCreate();

        Assert.Equal(0x42, image.Data.Span[123]);
        Assert.False(File.Exists(primaryPath));
    }

    [Fact]
    public void Save_WritesThePrimaryImageInsteadOfTheFallback()
    {
        using var directory = new TempDirectory();
        var fallbackPath = Path.Combine(directory.Path, "nvram.ram");
        var primaryPath = Path.Combine(directory.Path, "nvram", "per-game", "game.0.ram");
        File.WriteAllBytes(fallbackPath, NvramImage.CreateFresh().ToArray());
        using var store = new NvramStore(primaryPath, [fallbackPath]);
        var image = NvramImage.CreateFresh().ToArray();
        image[200] = 0x77;

        store.Save(image);

        Assert.True(File.Exists(primaryPath));
        Assert.Equal(0x77, File.ReadAllBytes(primaryPath)[200]);
        Assert.Equal(0, File.ReadAllBytes(fallbackPath)[200]);
    }

    [Fact]
    public void Reset_WritesAFreshImageToThePrimaryPath()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.ram");
        var image = NvramImage.CreateFresh().ToArray();
        image[300] = 0xEE;
        File.WriteAllBytes(path, image);
        using var store = new NvramStore(path);

        store.Reset();

        var reset = File.ReadAllBytes(path);
        Assert.Equal(EmulatorConstants.NvramSize, reset.Length);
        Assert.Equal(0, reset[300]);
        Assert.Equal((byte)'n', reset[40]);
    }

    [Fact]
    public void LoadOrCreate_DoesNotUseAFallbackWhenThePrimaryImageIsCorrupt()
    {
        using var directory = new TempDirectory();
        var fallbackPath = Path.Combine(directory.Path, "nvram.ram");
        var primaryPath = Path.Combine(directory.Path, "nvram", "per-game", "game.0.ram");
        Directory.CreateDirectory(Path.GetDirectoryName(primaryPath)!);
        var fallback = NvramImage.CreateFresh().ToArray();
        fallback[123] = 0x42;
        File.WriteAllBytes(fallbackPath, fallback);
        File.WriteAllBytes(primaryPath, new byte[100]);
        var store = new NvramStore(primaryPath, [fallbackPath]);

        NvramImage image = store.LoadOrCreate();

        Assert.Equal(0, image.Data.Span[123]);
        Assert.True(File.Exists(primaryPath + ".bad"));
    }

    [Fact]
    public void PerGameStoresKeepDifferentImagesIsolated()
    {
        using var directory = new TempDirectory();
        var settings = new AppSettings
        {
            NvramDirectory = directory.Path,
            NvramStorageMode = NvramStorageMode.PerGame,
        };
        var firstBytes = NvramImage.CreateFresh().ToArray();
        firstBytes[10] = 0x11;
        var secondBytes = NvramImage.CreateFresh().ToArray();
        secondBytes[10] = 0x22;

        using (NvramStore first = settings.CreateNvramStore("game-a"))
        {
            first.Save(firstBytes);
        }

        using (NvramStore second = settings.CreateNvramStore("game-b"))
        {
            second.Save(secondBytes);
        }

        using (NvramStore first = settings.CreateNvramStore("game-a"))
        {
            Assert.Equal(0x11, first.LoadOrCreate().Data.Span[10]);
        }

        using (NvramStore second = settings.CreateNvramStore("game-b"))
        {
            Assert.Equal(0x22, second.LoadOrCreate().Data.Span[10]);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        internal void Advance(TimeSpan delta)
        {
            _now += delta;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fivedo-nvram-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void AppSettings_DefaultToTheOptimizedVideoAndOutputConfiguration()
    {
        var settings = new AppSettings();

        Assert.Equal(VideoRendererKind.OpenGl, settings.Renderer);
        Assert.Equal(VideoFilter.SharpBilinear, settings.Filter);
        Assert.Equal(OutputResolution.Window, settings.OutputResolution);
        Assert.Equal(FrameSkipMode.Auto, settings.FrameSkip);
        Assert.True(settings.Vsync);
        Assert.True(settings.IntegerScale);
        Assert.False(settings.HighResolution);
        Assert.Equal(EmulationProfile.Compatible, settings.Profile);
        Assert.Equal(ArmTimingMode.FreeDoCompatible, settings.ArmTiming);
        Assert.Equal(CdromTimingMode.FreeDoCompatible, settings.CdromTiming);
        Assert.Equal(CdSpeed.X2, settings.CdSpeed);
        Assert.Equal(NvramStorageMode.Global, settings.NvramStorageMode);
        Assert.Equal(0, settings.NvramVersion);
        Assert.Equal(string.Empty, settings.NvramDirectory);
        Assert.Equal(1.0, settings.CpuClockMultiplier);
        Assert.Equal(EmulatorConstants.ArmClock, settings.ResolveArmClock());
        Assert.False(settings.ShowOverlay);
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(4.1)]
    [InlineData(double.NaN)]
    public void AppSettings_RejectsInvalidCpuClockMultipliers(double multiplier)
    {
        var settings = new AppSettings { CpuClockMultiplier = multiplier };

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ResolveArmClock());
    }

    [Fact]
    public void AppSettings_ApplyProfileUpdatesTheTimingOptionsAndDetectsTheProfile()
    {
        var settings = new AppSettings();

        settings.ApplyProfile(EmulationProfile.Accuracy);

        Assert.Equal(EmulationProfile.Accuracy, settings.Profile);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, settings.ArmTiming);
        Assert.Equal(ClioTimerMode.OperaScheduler, settings.ClioTimers);
        Assert.Equal(CelTimingMode.OperaBusy, settings.CelTiming);
        Assert.Equal(CdromTimingMode.OperaCycleBased, settings.CdromTiming);
        Assert.Equal(CdSpeed.X2, settings.CdSpeed);
        Assert.Equal(1.0, settings.CpuClockMultiplier);
        Assert.Equal(EmulationProfile.Accuracy, settings.DetectProfile());
    }

    [Fact]
    public void AppSettings_ManualTimingChangeBecomesCustom()
    {
        var settings = new AppSettings();
        settings.ApplyProfile(EmulationProfile.Accuracy);

        settings.ArmTiming = ArmTimingMode.FreeDoCompatible;

        Assert.Equal(EmulationProfile.Custom, settings.DetectProfile());
    }

    [Fact]
    public void AppSettings_PerformanceProfileUsesTheOverclock()
    {
        var settings = new AppSettings();

        settings.ApplyProfile(EmulationProfile.Performance);

        Assert.Equal(EmulationProfiles.PerformanceCpuClockMultiplier, settings.CpuClockMultiplier);
        Assert.Equal(CdSpeed.X2, settings.CdSpeed);
        Assert.Equal(EmulationProfile.Performance, settings.DetectProfile());
    }

    [Fact]
    public void AppSettings_ProfileRoundtripsWhenTheOptionsMatch()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings();
        settings.ApplyProfile(EmulationProfile.Accuracy);

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(EmulationProfile.Accuracy, loaded.Profile);
        Assert.Equal(EmulationProfile.Accuracy, loaded.DetectProfile());
    }

    [Fact]
    public void AppSettings_ExplicitCustomProfileSurvivesAReload()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings
        {
            Profile = EmulationProfile.Custom,
        };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(EmulationProfile.Custom, loaded.Profile);
        Assert.Equal(EmulationProfile.Compatible, loaded.DetectProfile());
    }

    [Fact]
    public void AppSettings_RoundtripsAndDerivesRendererSettings()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings
        {
            BiosPath = "bios.bin",
            Filter = VideoFilter.Crt,
            Aspect = AspectMode.Integer,
            Volume = 0.5f,
            HighResolution = true,
            Profile = EmulationProfile.Accuracy,
            ArmTiming = ArmTimingMode.Arm60Datasheet,
            ClioTimers = ClioTimerMode.OperaScheduler,
            CelTiming = CelTimingMode.OperaBusy,
            CdromTiming = CdromTimingMode.OperaCycleBased,
            CdSpeed = CdSpeed.X8,
            NvramStorageMode = NvramStorageMode.PerGame,
            NvramVersion = 4,
            NvramDirectory = "nvram-data",
            CpuClockMultiplier = 2.0,
            RandomSeed = 0x1234,
            RewindEnabled = false,
            RewindSeconds = 120,
            FastForwardMultiplier = 8,
            SaveSlot = 3,
            SaveSlotCount = 12,
            AutoGameFixes = false,
            ShowOverlay = true,
            SaveNativeScreenshot = true,
            FrameSkip = FrameSkipMode.OneInThree,
            Bloom = 0.7f,
            FirstRunCompleted = true,
            DonationEmail = "donor@example.com",
            HasDonated = true,
            DonationLastCheckedUtc = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc),
        };
        settings.Input.Players[0].Keyboard["Q"] = FiveDo.Input.ControllerButton.C;
        settings.RecentDiscs.Add("game.iso");
        settings.LibraryFolders.Add("games");
        settings.ShowDuplicateLibraryEntries = true;
        settings.FixModeOverrides["9F7D72BC"] = FixMode.BitTiming6;
        settings.SetFilterOverride("9F7D72BC", VideoFilter.Hqx);

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal("bios.bin", loaded.BiosPath);
        Assert.Equal(VideoFilter.Crt, loaded.Filter);
        Assert.Equal(AspectMode.Integer, loaded.Aspect);
        Assert.Equal(0.5f, loaded.Volume);
        Assert.True(loaded.HighResolution);
        Assert.Equal(EmulationProfile.Custom, loaded.Profile);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, loaded.ArmTiming);
        Assert.Equal(ClioTimerMode.OperaScheduler, loaded.ClioTimers);
        Assert.Equal(CelTimingMode.OperaBusy, loaded.CelTiming);
        Assert.Equal(CdromTimingMode.OperaCycleBased, loaded.CdromTiming);
        Assert.Equal(CdSpeed.X8, loaded.CdSpeed);
        Assert.Equal(NvramStorageMode.PerGame, loaded.NvramStorageMode);
        Assert.Equal(4, loaded.NvramVersion);
        Assert.Equal("nvram-data", loaded.NvramDirectory);
        Assert.Equal(2.0, loaded.CpuClockMultiplier);
        Assert.Equal(25_000_000, loaded.ResolveArmClock());
        Assert.Equal(0x1234u, loaded.RandomSeed);
        Assert.False(loaded.RewindEnabled);
        Assert.Equal(120, loaded.RewindSeconds);
        Assert.Equal(8, loaded.FastForwardMultiplier);
        Assert.False(loaded.ToRewindOptions().Enabled);
        Assert.Equal(120, loaded.ToRewindOptions().Seconds);
        Assert.Equal(8, loaded.ResolveFastForwardMultiplier());
        Assert.Equal(3, loaded.SaveSlot);
        Assert.Equal(12, loaded.SaveSlotCount);
        Assert.False(loaded.AutoGameFixes);
        Assert.True(loaded.ShowOverlay);
        Assert.True(loaded.SaveNativeScreenshot);
        Assert.Equal(FrameSkipMode.OneInThree, loaded.FrameSkip);
        Assert.Equal(0.7f, loaded.Bloom);
        Assert.Equal(0.7f, loaded.ToRendererSettings().Bloom);
        Assert.True(loaded.FirstRunCompleted);
        Assert.Equal("donor@example.com", loaded.DonationEmail);
        Assert.True(loaded.HasDonated);
        Assert.Equal(
            new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc),
            loaded.DonationLastCheckedUtc);
        Assert.Equal(FiveDo.Input.ControllerButton.C, loaded.Input.Players[0].Keyboard["Q"]);
        Assert.Equal("game.iso", Assert.Single(loaded.RecentDiscs));
        Assert.Equal("games", Assert.Single(loaded.LibraryFolders));
        Assert.True(loaded.ShowDuplicateLibraryEntries);
        Assert.Equal(FixMode.BitTiming6, loaded.GetFixModeOverride("9F7D72BC"));
        Assert.Equal(VideoFilter.Crt, loaded.Filter);
        Assert.Equal(VideoFilter.Hqx, loaded.ResolveFilter("9F7D72BC"));
        Assert.Equal(VideoFilter.Crt, loaded.ResolveFilter("UNKNOWN"));
        Assert.Equal(VideoFilter.Hqx, loaded.ToRendererSettings("9F7D72BC").Filter);
        Assert.Equal(VideoFilter.Crt, loaded.ToRendererSettings().Filter);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void AppSettings_ValidatesRewindAndFastForwardOptions()
    {
        var settings = new AppSettings { RewindSeconds = 29 };
        Assert.Throws<ArgumentOutOfRangeException>(settings.ToRewindOptions);

        settings.RewindSeconds = 301;
        Assert.Throws<ArgumentOutOfRangeException>(settings.ToRewindOptions);

        settings.FastForwardMultiplier = 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ResolveFastForwardMultiplier());

        settings.FastForwardMultiplier = 11;
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ResolveFastForwardMultiplier());
    }

    [Fact]
    public void AppSettings_RoundtripsCustomAspectAndRendererSettings()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings
        {
            Aspect = AspectMode.Custom,
            CustomAspect = 1.6,
            PreferOpenGl = false,
            OutputResolution = OutputResolution.FullHd1080,
        };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(AspectMode.Custom, loaded.Aspect);
        Assert.Equal(1.6, loaded.CustomAspect);
        Assert.Equal(VideoRendererKind.Software, loaded.Renderer);
        Assert.False(loaded.PreferOpenGl);
        Assert.Equal(OutputResolution.FullHd1080, loaded.OutputResolution);
        Assert.Equal(AspectMode.Custom, loaded.ToRendererSettings().Aspect);
        Assert.Equal(1.6, loaded.ToRendererSettings().CustomAspect);
        Assert.Equal(
            OutputResolution.FullHd1080,
            loaded.ToRendererSettings().OutputResolution);
    }

    [Theory]
    [InlineData(AspectMode.FourThree)]
    [InlineData(AspectMode.FiveFour)]
    [InlineData(AspectMode.SixteenNine)]
    [InlineData(AspectMode.SixteenTen)]
    [InlineData(AspectMode.TwentyOneNine)]
    public void AppSettings_RoundtripsPresetAspectModes(AspectMode aspect)
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings { Aspect = aspect };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(aspect, loaded.Aspect);
        Assert.Equal(aspect, loaded.ToRendererSettings().Aspect);
    }

    [Theory]
    [InlineData(true, VideoRendererKind.OpenGl)]
    [InlineData(false, VideoRendererKind.Software)]
    public void AppSettings_MigratesLegacyPreferOpenGl(bool preferOpenGl, VideoRendererKind expected)
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(
            path,
            $$"""
            {
              "PreferOpenGl": {{(preferOpenGl ? "true" : "false")}}
            }
            """);

        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(expected, loaded.Renderer);
        Assert.Equal(preferOpenGl, loaded.PreferOpenGl);
    }

    [Fact]
    public void AppSettings_RoundtripsVulkanRenderer()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings { Renderer = VideoRendererKind.Vulkan };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(VideoRendererKind.Vulkan, loaded.Renderer);
        Assert.False(loaded.PreferOpenGl);
        Assert.DoesNotContain("PreferOpenGl", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_RoundtripsTheHqx4xFilterName()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings { Filter = VideoFilter.Hqx4x };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(VideoFilter.Hqx4x, loaded.Filter);
        Assert.Contains("\"Hqx4x\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_AddsNewDefaultHotkeysToAnOlderProfile()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(
            path,
            """
            {
              "Input": {
                "Hotkeys": {
                  "SaveState": "F2"
                }
              }
            }
            """);

        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal("F2", loaded.Input.Hotkeys["SaveState"]);
        Assert.Equal("F6", loaded.Input.Hotkeys["SlotPrevious"]);
        Assert.Equal("F7", loaded.Input.Hotkeys["SlotNext"]);
        Assert.Equal("F9", loaded.Input.Hotkeys["FrameAdvance"]);
    }

    [Fact]
    public void AppSettings_InvalidEmulationValuesFallBackToDefaults()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(
            path,
            """
            {
              "CpuClockMultiplier": 8.0,
              "Profile": 99,
              "ArmTiming": 99,
              "ClioTimers": 99,
              "CelTiming": 99,
              "CdromTiming": 99,
              "CdSpeed": 3,
              "NvramStorageMode": 99,
              "NvramVersion": 12
            }
            """);

        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(1.0, loaded.CpuClockMultiplier);
        Assert.Equal(EmulationProfile.Compatible, loaded.Profile);
        Assert.Equal(ArmTimingMode.FreeDoCompatible, loaded.ArmTiming);
        Assert.Equal(ClioTimerMode.FreeDoCompatible, loaded.ClioTimers);
        Assert.Equal(CelTimingMode.FreeDoCompatible, loaded.CelTiming);
        Assert.Equal(CdromTimingMode.FreeDoCompatible, loaded.CdromTiming);
        Assert.Equal(CdSpeed.X2, loaded.CdSpeed);
        Assert.Equal(NvramStorageMode.Global, loaded.NvramStorageMode);
        Assert.Equal(0, loaded.NvramVersion);
    }

    [Fact]
    public void AppSettings_OlderFilesWithoutNvramFieldsUseTheSharedDefaults()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, """{ "BiosPath": "bios.bin" }""");

        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal("bios.bin", loaded.BiosPath);
        Assert.Equal(NvramStorageMode.Global, loaded.NvramStorageMode);
        Assert.Equal(0, loaded.NvramVersion);
        Assert.Equal(string.Empty, loaded.NvramDirectory);
    }

    [Fact]
    public void AppSettings_InvalidJsonFallsBackToDefaults()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "not json");

        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(VideoFilter.SharpBilinear, loaded.Filter);
    }
}
