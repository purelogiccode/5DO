using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Input;

namespace FiveDo.Tests.Host;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void Load_MigratesAnUnversionedFileAndStampsTheCurrentVersion()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, """{"PreferOpenGl":false,"CpuClockMultiplier":9.0}""");

        AppSettings settings = AppSettingsStore.Load(path);

        Assert.Equal(AppSettings.CurrentVersion, settings.SettingsVersion);
        Assert.Equal(VideoRendererKind.Software, settings.Renderer);
        Assert.Equal(1.0, settings.CpuClockMultiplier);
    }

    [Fact]
    public void Load_BacksUpAMalformedFileAndUsesDefaults()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string invalid = "{ this is not json";
        File.WriteAllText(path, invalid);

        AppSettings settings = AppSettingsStore.Load(path);

        Assert.Equal(AppSettings.CurrentVersion, settings.SettingsVersion);
        string backup = Assert.Single(Directory.GetFiles(temp.Path, "settings.json.invalid-*.bak"));
        Assert.Equal(invalid, File.ReadAllText(backup));
    }

    [Fact]
    public void Save_WritesTheCurrentVersionAndRoundtrips()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings
        {
            Renderer = VideoRendererKind.Vulkan,
            CpuClockMultiplier = 1.5,
            CdAudioVolume = 0.35f,
            CdAudioMuted = true,
        };

        AppSettingsStore.Save(settings, path);
        string json = File.ReadAllText(path);
        Assert.Contains($"\"SettingsVersion\": {AppSettings.CurrentVersion}", json, StringComparison.Ordinal);

        AppSettings loaded = AppSettingsStore.Load(path);
        Assert.Equal(AppSettings.CurrentVersion, loaded.SettingsVersion);
        Assert.Equal(VideoRendererKind.Vulkan, loaded.Renderer);
        Assert.Equal(1.5, loaded.CpuClockMultiplier);
        Assert.Equal(0.35, loaded.CdAudioVolume, 3);
        Assert.True(loaded.CdAudioMuted);
    }

    [Fact]
    public void Load_ValidatesEnumerationsAndNumericRanges()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(
            path,
            """{"SettingsVersion":1,"Renderer":9999,"CpuClockMultiplier":-1,"OsdOpacity":5.0,"OsdDurationMilliseconds":1,"CdAudioVolume":5.0}""");

        AppSettings settings = AppSettingsStore.Load(path);

        Assert.Equal(VideoRendererKind.OpenGl, settings.Renderer);
        Assert.Equal(1.0, settings.CpuClockMultiplier);
        Assert.InRange(settings.OsdOpacity, 0.1, 1.0);
        Assert.InRange(settings.OsdDurationMilliseconds, 250, 30_000);
        Assert.Equal(0.8, settings.CdAudioVolume, 3);
    }

    [Fact]
    public void GameProfiles_RoundtripAndApplyByGameId()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings
        {
            AutoGameFixes = true,
        };
        settings.SetGameProfile(
            "a1b2c3d4",
            new GameProfile
            {
                Profile = EmulationProfile.Accuracy,
                Region = VideoRegion.Pal1,
                ArmTiming = ArmTimingMode.Arm60Datasheet,
                ClioTimers = ClioTimerMode.OperaScheduler,
                CelTiming = CelTimingMode.OperaBusy,
                CdromTiming = CdromTimingMode.OperaCycleBased,
                CdSpeed = CdSpeed.X4,
                CpuClockMultiplier = 1.25,
                CdAudioVolume = 0.4f,
                CdAudioMuted = true,
            });

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);
        GameProfile? profile = loaded.GetGameProfile("A1B2C3D4");
        Assert.NotNull(profile);
        Assert.Equal(EmulationProfile.Accuracy, profile.Profile);
        Assert.Equal(VideoRegion.Pal1, profile.Region);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, profile.ArmTiming);
        Assert.Equal(CdSpeed.X4, profile.CdSpeed);
        Assert.True(profile.CdAudioMuted);

        Assert.True(loaded.ApplyGameProfile("a1b2c3d4"));
        Assert.Equal(VideoRegion.Pal1, loaded.Region);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, loaded.ArmTiming);
        Assert.Equal(ClioTimerMode.OperaScheduler, loaded.ClioTimers);
        Assert.Equal(1.25, loaded.CpuClockMultiplier);
        Assert.Equal(0.4, loaded.CdAudioVolume, 3);

        loaded.SetGameProfile("A1B2C3D4", null);
        Assert.Null(loaded.GetGameProfile("a1b2c3d4"));
    }

    [Fact]
    public void Profiles_ListOnlyDirectoriesWithSettings()
    {
        using var temp = new TemporaryDirectory();
        var profiles = Path.Combine(temp.Path, "profiles");
        Directory.CreateDirectory(Path.Combine(profiles, "work"));
        Directory.CreateDirectory(Path.Combine(profiles, "empty"));
        File.WriteAllText(Path.Combine(profiles, "work", "settings.json"), "{}");

        Assert.Equal(["work"], AppSettingsStore.ListProfiles(profiles));
        Assert.Equal(
            Path.Combine(profiles, "work", "settings.json"),
            AppSettingsStore.GetProfilePath("work", profiles));
    }

    [Fact]
    public void InputDeviceOverrides_WinOverGlobalAndSurviveRoundtrip()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings();
        settings.SetInputDevice(0, InputDeviceType.Flightstick);
        Assert.Equal("Flightstick", settings.GetInputDevice(0).ToString());
        Assert.Equal("Flightstick", settings.ResolveInputDevices("A1B2C3D4")[0]);

        settings.SetInputDeviceOverride(
            "a1b2c3d4",
            ["Mouse", "Lightgun", "bogus", "Joypad", "Joypad", "Joypad"]);
        Assert.Equal("Mouse", settings.ResolveInputDevices("A1B2C3D4")[0]);
        Assert.Equal("Lightgun", settings.ResolveInputDevices("A1B2C3D4")[1]);
        Assert.Equal("Joypad", settings.ResolveInputDevices("A1B2C3D4")[2]);

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal("Mouse", loaded.ResolveInputDevices("a1b2c3d4")[0]);
        Assert.Equal("Flightstick", loaded.ResolveInputDevices("other")[0]);

        loaded.SetInputDeviceOverride("A1B2C3D4", null);
        Assert.Null(loaded.GetInputDeviceOverride("a1b2c3d4"));
        Assert.Equal("Flightstick", loaded.ResolveInputDevices("A1B2C3D4")[0]);
    }

    [Fact]
    public void AutoInputDevices_SeedKnownLightgunAndTrackballTitles()
    {
        var settings = new AppSettings();

        Assert.True(settings.EnsureAutoInputDeviceOverride("11111111", "Crime Patrol (USA)"));
        Assert.Equal("Lightgun", settings.GetInputDeviceOverride("11111111")![0]);
        Assert.False(settings.EnsureAutoInputDeviceOverride("11111111", "Crime Patrol (USA)"));

        Assert.True(settings.EnsureAutoInputDeviceOverride("22222222", "Orbatak (USA)"));
        Assert.Equal("OrbatakTrackball", settings.GetInputDeviceOverride("22222222")![0]);

        Assert.False(settings.EnsureAutoInputDeviceOverride("33333333", "Gex (USA, Europe)"));
        Assert.Null(settings.GetInputDeviceOverride("33333333"));

        settings.AutoInputDevices = false;
        Assert.False(settings.EnsureAutoInputDeviceOverride("44444444", "Mad Dog II (USA)"));
    }

    [Fact]
    public void Overscan_RoundtripsAndClamps()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { OverscanPixels = 8 };

        AppSettingsStore.Save(settings, path);
        Assert.Equal(8, AppSettingsStore.Load(path).OverscanPixels);

        settings.OverscanPixels = 99;
        AppSettingsStore.Save(settings, path);
        Assert.Equal(32, AppSettingsStore.Load(path).OverscanPixels);

        settings.OverscanPixels = -5;
        AppSettingsStore.Save(settings, path);
        Assert.Equal(0, AppSettingsStore.Load(path).OverscanPixels);
    }

    [Fact]
    public void AudioDeviceAndBuffer_RoundtripAndClamp()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings
        {
            AudioDevice = "  OpenAL Soft on Speakers  ",
            AudioBufferMilliseconds = 120,
        };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal("OpenAL Soft on Speakers", loaded.AudioDevice);
        Assert.Equal(120, loaded.AudioBufferMilliseconds);

        loaded.AudioBufferMilliseconds = 900;
        AppSettingsStore.Save(loaded, path);
        Assert.Equal(500, AppSettingsStore.Load(path).AudioBufferMilliseconds);

        loaded.AudioBufferMilliseconds = 1;
        AppSettingsStore.Save(loaded, path);
        Assert.Equal(20, AppSettingsStore.Load(path).AudioBufferMilliseconds);
    }

    [Fact]
    public void PointerSettings_RoutePortsAndMapCoordinates()
    {
        var settings = new AppSettings();
        Assert.True(settings.RoutesPointerTo(1));
        Assert.True(settings.RoutesPointerTo(6));
        settings.PointerPort = 2;
        Assert.False(settings.RoutesPointerTo(1));
        Assert.True(settings.RoutesPointerTo(2));
        Assert.False(settings.RoutesPointerTo(6));

        Assert.Equal((160, 120), settings.MapPointer(0.5, 0.5, 320, 240));
        Assert.Equal((0, 0), settings.MapPointer(0.0, 0.0, 320, 240));
        Assert.Equal((319, 239), settings.MapPointer(1.0, 1.0, 320, 240));

        settings.PointerOffsetX = 20;
        settings.PointerOffsetY = -10;
        Assert.Equal((180, 110), settings.MapPointer(0.5, 0.5, 320, 240));

        settings.PointerScale = 2.0;
        settings.PointerOffsetX = 0;
        settings.PointerOffsetY = 0;
        Assert.Equal((0, 0), settings.MapPointer(0.0, 0.0, 320, 240));
        Assert.Equal((320 - 1, 240 - 1), settings.MapPointer(1.0, 1.0, 320, 240));
    }

    [Fact]
    public void PointerSettings_AreClampedWhenTheFileLoads()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings
        {
            PointerPort = 9,
            PointerScale = 9.0,
            PointerOffsetX = 999,
            PointerOffsetY = -999,
        };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(6, loaded.PointerPort);
        Assert.Equal(2.0, loaded.PointerScale);
        Assert.Equal(160, loaded.PointerOffsetX);
        Assert.Equal(-120, loaded.PointerOffsetY);
    }

    [Fact]
    public void GameProfileJson_RoundtripsAndNormalizes()
    {
        var profile = new GameProfile
        {
            Profile = EmulationProfile.Accuracy,
            Region = VideoRegion.Pal2,
            ArmTiming = ArmTimingMode.Arm60Datasheet,
            ClioTimers = ClioTimerMode.OperaScheduler,
            CelTiming = CelTimingMode.OperaBusy,
            CdromTiming = CdromTimingMode.OperaCycleBased,
            CdSpeed = CdSpeed.X8,
            CpuClockMultiplier = 1.5,
            CdAudioVolume = 0.4f,
            CdAudioMuted = true,
        };

        var json = GameProfileJson.Serialize(profile);
        GameProfile loaded = GameProfileJson.Deserialize(json);

        Assert.Equal(EmulationProfile.Accuracy, loaded.Profile);
        Assert.Equal(VideoRegion.Pal2, loaded.Region);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, loaded.ArmTiming);
        Assert.Equal(CdSpeed.X8, loaded.CdSpeed);
        Assert.Equal(1.5, loaded.CpuClockMultiplier);
        Assert.Equal(0.4f, loaded.CdAudioVolume);
        Assert.True(loaded.CdAudioMuted);

        var invalid = json
            .Replace("\"CpuClockMultiplier\": 1.5", "\"CpuClockMultiplier\": 99")
            .Replace("\"CdAudioVolume\": 0.4", "\"CdAudioVolume\": 9");
        GameProfile normalized = GameProfileJson.Deserialize(invalid);
        Assert.Equal(1.0, normalized.CpuClockMultiplier);
        Assert.Equal(0.8f, normalized.CdAudioVolume);
    }

    [Fact]
    public void CreateEffectiveForGame_AppliesProfileWithoutChangingLiveSettings()
    {
        var settings = new AppSettings
        {
            Region = VideoRegion.Ntsc,
            CdSpeed = CdSpeed.X2,
            ArmTiming = ArmTimingMode.FreeDoCompatible,
            CdAudioVolume = 1f,
        };
        settings.SetGameProfile(
            "A1B2C3D4",
            new GameProfile
            {
                Profile = EmulationProfile.Accuracy,
                Region = VideoRegion.Pal1,
                ArmTiming = ArmTimingMode.Arm60Datasheet,
                CdSpeed = CdSpeed.X4,
                CdAudioVolume = 0.25f,
                CdAudioMuted = true,
            });

        AppSettings effective = settings.CreateEffectiveForGame("a1b2c3d4");

        Assert.Equal(VideoRegion.Pal1, effective.Region);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, effective.ArmTiming);
        Assert.Equal(CdSpeed.X4, effective.CdSpeed);
        Assert.Equal(0.25f, effective.CdAudioVolume);
        Assert.True(effective.CdAudioMuted);

        Assert.Equal(VideoRegion.Ntsc, settings.Region);
        Assert.Equal(ArmTimingMode.FreeDoCompatible, settings.ArmTiming);
        Assert.Equal(CdSpeed.X2, settings.CdSpeed);
        Assert.Equal(1f, settings.CdAudioVolume);
        Assert.False(settings.CdAudioMuted);

        AppSettings noProfile = settings.CreateEffectiveForGame("other");
        Assert.Equal(VideoRegion.Ntsc, noProfile.Region);
        Assert.False(noProfile.CdAudioMuted);
    }

    [Fact]
    public void RegionOverrides_WinOverAutomaticAndGlobalChoices()
    {
        var settings = new AppSettings
        {
            AutoRegion = true,
            Region = VideoRegion.Ntsc,
        };
        Assert.Equal(VideoRegion.Pal1, settings.ResolveRegionForGame("ABCD1234", VideoRegion.Pal1));
        Assert.Null(settings.GetRegionOverride("ABCD1234"));

        settings.SetRegionOverride("abcd1234", VideoRegion.Pal2);
        Assert.Equal(VideoRegion.Pal2, settings.GetRegionOverride("ABCD1234"));
        Assert.Equal(VideoRegion.Pal2, settings.ResolveRegionForGame("abcd1234", VideoRegion.Pal1));
        Assert.Equal(VideoRegion.Ntsc, settings.ResolveRegionForGame("other", VideoRegion.Ntsc));

        settings.AutoRegion = false;
        Assert.Equal(VideoRegion.Pal2, settings.ResolveRegionForGame("ABCD1234", VideoRegion.Pal1));
        Assert.Equal(VideoRegion.Ntsc, settings.ResolveRegionForGame("other", VideoRegion.Pal1));

        settings.SetRegionOverride("ABCD1234", null);
        Assert.Null(settings.GetRegionOverride("ABCD1234"));
    }

    [Fact]
    public void RegionOverrides_RoundtripThroughTheSettingsFile()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings();
        settings.SetRegionOverride("A1B2C3D4", VideoRegion.Pal1);

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(VideoRegion.Pal1, loaded.GetRegionOverride("a1b2c3d4"));
    }

    [Fact]
    public void ResolveRegion_HonorsAutomaticAndManualModes()
    {
        var automatic = new AppSettings { AutoRegion = true, Region = VideoRegion.Ntsc };
        Assert.Equal(VideoRegion.Pal1, automatic.ResolveRegion(VideoRegion.Pal1));

        var manual = new AppSettings { AutoRegion = false, Region = VideoRegion.Ntsc };
        Assert.Equal(VideoRegion.Ntsc, manual.ResolveRegion(VideoRegion.Pal1));
    }

    [Fact]
    public void Load_ClampsTheInputDisplayPlayer()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        AppSettingsStore.Save(new AppSettings { InputDisplayPlayer = 12 }, path);
        Assert.Equal(6, AppSettingsStore.Load(path).InputDisplayPlayer);

        AppSettingsStore.Save(new AppSettings { InputDisplayPlayer = -3 }, path);
        Assert.Equal(1, AppSettingsStore.Load(path).InputDisplayPlayer);
    }

    [Fact]
    public void InspectAndMigrate_ReportAndRewriteTheSchemaVersion()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, """{"PreferOpenGl":false,"CpuClockMultiplier":1.5}""");

        SettingsFileInfo before = AppSettingsStore.Inspect(path);
        Assert.True(before.Exists);
        Assert.True(before.Valid);
        Assert.Equal(0, before.Version);

        Assert.Equal(0, AppSettingsStore.Migrate(path));
        SettingsFileInfo after = AppSettingsStore.Inspect(path);
        Assert.Equal(AppSettings.CurrentVersion, after.Version);
        Assert.Contains(
            $"\"SettingsVersion\": {AppSettings.CurrentVersion}",
            File.ReadAllText(path),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Migrate_RejectsMalformedJsonWithoutRewritingIt()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string invalid = "{ not json";
        File.WriteAllText(path, invalid);

        SettingsFileInfo info = AppSettingsStore.Inspect(path);
        Assert.True(info.Exists);
        Assert.False(info.Valid);
        Assert.NotNull(info.Error);
        Assert.Throws<InvalidDataException>(() => AppSettingsStore.Migrate(path));
        Assert.Equal(invalid, File.ReadAllText(path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fivedo-settings-{Guid.NewGuid():N}");
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
}
