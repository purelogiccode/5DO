using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Input;
using FiveDo.Video;

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
    public void Load_BacksUpANewerSchemaFileAndUsesTheSupportedDefaults()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string newer = """{"SettingsVersion":99,"BiosPath":"future.bin"}""";
        File.WriteAllText(path, newer);

        AppSettings settings = AppSettingsStore.Load(path);

        Assert.Equal(AppSettings.CurrentVersion, settings.SettingsVersion);
        Assert.Equal("future.bin", settings.BiosPath);
        string backup = path + ".v99.bak";
        Assert.True(File.Exists(backup));
        Assert.Equal(newer, File.ReadAllText(backup));
        Assert.Equal(newer, File.ReadAllText(path));
    }

    [Fact]
    public void Load_UsesDefaultsWhenTheFileCannotBeRead()
    {
        if (!OperatingSystem.IsWindows())
        {
            // FileShare.None is enforced for managed readers on Windows only.
            return;
        }

        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string json = """{"SettingsVersion":7,"BiosPath":"locked.bin"}""";
        File.WriteAllText(path, json);

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            AppSettings settings = AppSettingsStore.Load(path);

            Assert.Equal(AppSettings.CurrentVersion, settings.SettingsVersion);
            Assert.NotEqual("locked.bin", settings.BiosPath, StringComparer.Ordinal);
        }

        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Load_BacksUpTheMalformedPlayersFixtureAndUsesDefaults()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string malformed = """
            {
              "SettingsVersion": 7,
              "Input": {
                "Players": [
                  "@{Name=Player 1; Keyboard=; Gamepad=}"
                ]
              }
            }
            """;
        File.WriteAllText(path, malformed);

        AppSettings settings = AppSettingsStore.Load(path);

        Assert.Equal(AppSettings.CurrentVersion, settings.SettingsVersion);
        Assert.NotNull(settings.Input);
        Assert.Equal(PbusEncoder.MaxPorts, settings.Input.Players.Length);
        Assert.All(settings.Input.Players, player => Assert.NotNull(player));
        Assert.Single(Directory.GetFiles(temp.Path, "settings.json.invalid-*.bak"));
    }

    [Fact]
    public void Load_NormalizesNullAndShortPlayerArrays()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string json = """
            {
              "SettingsVersion": 7,
              "Input": {
                "Players": [
                  { "Name": null, "Keyboard": null, "Gamepad": null }
                ]
              }
            }
            """;
        File.WriteAllText(path, json);

        AppSettings settings = AppSettingsStore.Load(path);

        Assert.Equal(PbusEncoder.MaxPorts, settings.Input.Players.Length);
        Assert.Equal("Player 1", settings.Input.Players[0].Name);
        Assert.NotNull(settings.Input.Players[0].Keyboard);
        Assert.NotNull(settings.Input.Players[0].Gamepad);
        Assert.Equal("Player 2", settings.Input.Players[1].Name);
        Assert.NotNull(settings.Input.Players[1].Keyboard);
        Assert.NotNull(settings.Input.Players[1].Gamepad);
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
            CheckForUpdates = false,
            LibraryCardSize = 256,
            HideLightgunCrosshair = true,
            AllowUnsignedDiscs = true,
            RunAheadFrames = 2,
            FrameDelayFrames = 3,
            Vrr = true,
            AudioSyncMilliseconds = -40,
            AutoCrop = true,
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
        Assert.False(loaded.CheckForUpdates);
        Assert.Equal(256, loaded.LibraryCardSize);
        Assert.True(loaded.HideLightgunCrosshair);
        Assert.True(loaded.AllowUnsignedDiscs);
        Assert.Equal(2, loaded.RunAheadFrames);
        Assert.Equal(3, loaded.FrameDelayFrames);
        Assert.True(loaded.Vrr);
        Assert.Equal(-40, loaded.AudioSyncMilliseconds);
        Assert.True(loaded.AutoCrop);
    }

    [Fact]
    public void Save_ReturnsFalseWhenTheDestinationCannotBeWritten()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        Directory.CreateDirectory(path);

        Assert.False(AppSettingsStore.Save(new AppSettings(), path));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public void Save_CopiesOverADeleteLockedDestination()
    {
        if (!OperatingSystem.IsWindows())
        {
            // The file-share semantics that block File.Move while allowing File.Copy
            // are only enforced on Windows.
            return;
        }

        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        Assert.True(AppSettingsStore.Save(new AppSettings { BiosPath = "old.bin" }, path));

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            Assert.True(AppSettingsStore.Save(new AppSettings { BiosPath = "new.bin" }, path));
        }

        Assert.Equal("new.bin", AppSettingsStore.Load(path).BiosPath);
    }

    [Fact]
    public void Load_ValidatesEnumerationsAndNumericRanges()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(
            path,
            """{"SettingsVersion":1,"Renderer":9999,"CpuClockMultiplier":-1,"OsdOpacity":5.0,"OsdDurationMilliseconds":1,"CdAudioVolume":5.0,"LibraryCardSize":999,"RunAheadFrames":9,"FrameDelayFrames":99,"AudioSyncMilliseconds":9000}""");

        AppSettings settings = AppSettingsStore.Load(path);

        Assert.Equal(VideoRendererKind.OpenGl, settings.Renderer);
        Assert.Equal(1.0, settings.CpuClockMultiplier);
        Assert.InRange(settings.OsdOpacity, 0.1, 1.0);
        Assert.InRange(settings.OsdDurationMilliseconds, 250, 30_000);
        Assert.Equal(0.8, settings.CdAudioVolume, 3);
        Assert.Equal(AppSettings.DefaultLibraryCardSize, settings.LibraryCardSize);
        Assert.Equal(AppSettings.MaxRunAheadFrames, settings.RunAheadFrames);
        Assert.Equal(AppSettings.MaxFrameDelayFrames, settings.FrameDelayFrames);
        Assert.Equal(AppSettings.MaxAudioSyncMilliseconds, settings.AudioSyncMilliseconds);
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
                CheatBundle = "starter",
                Deinterlace = DeinterlaceMode.Weave,
                RunAheadFrames = 1,
                FrameDelayFrames = 2,
                Vrr = true,
                AudioSyncMilliseconds = 35,
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
        Assert.Equal("starter", profile.CheatBundle);
        Assert.Equal(DeinterlaceMode.Weave, profile.Deinterlace);
        Assert.Equal(1, profile.RunAheadFrames);
        Assert.Equal(2, profile.FrameDelayFrames);
        Assert.True(profile.Vrr);
        Assert.Equal(35, profile.AudioSyncMilliseconds);

        Assert.True(loaded.ApplyGameProfile("a1b2c3d4"));
        Assert.Equal(VideoRegion.Pal1, loaded.Region);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, loaded.ArmTiming);
        Assert.Equal(ClioTimerMode.OperaScheduler, loaded.ClioTimers);
        Assert.Equal(1.25, loaded.CpuClockMultiplier);
        Assert.Equal(0.4, loaded.CdAudioVolume, 3);
        Assert.Equal(DeinterlaceMode.Weave, loaded.Deinterlace);
        Assert.Equal(1, loaded.RunAheadFrames);
        Assert.Equal(2, loaded.FrameDelayFrames);
        Assert.True(loaded.Vrr);
        Assert.Equal(35, loaded.AudioSyncMilliseconds);

        loaded.SetGameProfile("A1B2C3D4", null);
        Assert.Null(loaded.GetGameProfile("a1b2c3d4"));
    }

    [Fact]
    public void ResolveInputDevices_UsesDatabaseDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal("Lightgun", settings.ResolveInputDevices("E8FBE61C")[0]);
        Assert.Equal("Joypad", settings.ResolveInputDevices("E8FBE61C")[1]);
        Assert.Equal("OrbatakTrackball", settings.ResolveInputDevices("B6B3F5B2")[0]);
        Assert.False(settings.EnsureAutoInputDeviceOverride("E8FBE61C", "Crime Patrol"));
        Assert.Null(settings.GetInputDeviceOverride("E8FBE61C"));
    }

    [Fact]
    public void GameProfileClone_IsIndependent()
    {
        var profile = new GameProfile
        {
            InputDevices = ["Lightgun", "Joypad"],
            CheatBundle = "starter",
        };

        GameProfile clone = profile.Clone();
        clone.InputDevices[0] = "Mouse";
        clone.CheatBundle = "other";

        Assert.Equal("Lightgun", profile.InputDevices[0]);
        Assert.Equal("starter", profile.CheatBundle);
    }

    [Fact]
    public void GameProfileChanges_ApplyToEffectiveSettingsImmediately()
    {
        var settings = new AppSettings();
        settings.SetGameProfile(
            "a1b2c3d4",
            new GameProfile
            {
                Filter = VideoFilter.Crt,
                InputDevices = ["Mouse", "Joypad"],
                FixMode = FixMode.BitTiming1,
                BiosMd5 = "0123456789abcdef0123456789abcdef",
                RunAheadFrames = 2,
                FrameDelayFrames = 1,
                Vrr = true,
                AudioSyncMilliseconds = -25,
            });

        AppSettings first = settings.CreateEffectiveForGame("A1B2C3D4");
        Assert.Equal(2, first.RunAheadFrames);
        Assert.Equal(1, first.FrameDelayFrames);
        Assert.True(first.Vrr);
        Assert.Equal(-25, first.AudioSyncMilliseconds);
        Assert.Equal(VideoFilter.Crt, first.ResolveFilter("A1B2C3D4"));
        Assert.Equal("Mouse", first.ResolveInputDevices("A1B2C3D4")[0]);
        Assert.Equal(FixMode.BitTiming1, first.ResolveFixMode("A1B2C3D4"));
        Assert.Equal("0123456789abcdef0123456789abcdef", first.GetBiosOverride("A1B2C3D4"));

        settings.SetGameProfile(
            "A1B2C3D4",
            new GameProfile
            {
                Filter = VideoFilter.Accurate,
                InputDevices = ["Lightgun", "Joypad"],
            });

        AppSettings second = settings.CreateEffectiveForGame("A1B2C3D4");
        Assert.Equal(VideoFilter.Accurate, second.ResolveFilter("A1B2C3D4"));
        Assert.Equal("Lightgun", second.ResolveInputDevices("A1B2C3D4")[0]);
        Assert.Null(second.GetBiosOverride("A1B2C3D4"));
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
    public void PerEdgeOverscanAndVoidBorder_RoundtripAndClamp()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings
        {
            OverscanLeft = 2,
            OverscanTop = 4,
            OverscanRight = 6,
            OverscanBottom = 8,
            VideoBorderPixels = 12,
            VoidAreaColor = "#ff202020",
            VoidAreaPattern = "Checker",
        };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(new CropInsets(2, 4, 6, 8), loaded.ResolveOverscan());
        Assert.Equal(12, loaded.VideoBorderPixels);
        Assert.Equal("#ff202020", loaded.VoidAreaColor);
        Assert.Equal(VoidPattern.Checker, loaded.ResolveVoidAreaPattern());

        settings.OverscanLeft = 99;
        settings.OverscanTop = -4;
        settings.VideoBorderPixels = 500;
        settings.VoidAreaColor = "bad";
        settings.VoidAreaPattern = "spiral";
        AppSettingsStore.Save(settings, path);
        loaded = AppSettingsStore.Load(path);

        Assert.Equal(new CropInsets(32, 0, 6, 8), loaded.ResolveOverscan());
        Assert.Equal(64, loaded.VideoBorderPixels);
        Assert.Equal(AppSettings.DefaultVoidAreaColor, loaded.VoidAreaColor);
        Assert.Equal(VoidPattern.Solid, loaded.ResolveVoidAreaPattern());
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

        Assert.Equal(PbusEncoder.MaxPorts, loaded.PointerPort);
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
            RandomSeed = 0x1234_5678,
            OperaDipir = true,
            OperaSoftReset = true,
            CpuBackend = CpuBackendKind.Interpreter,
            CdAudioVolume = 0.4f,
            CdAudioMuted = true,
            BiosMd5 = "0123456789ABCDEF0123456789ABCDEF",
            FixMode = FixMode.BitTiming5,
            Filter = VideoFilter.Crt,
            HighResolution = true,
            Overscan = new CropInsets(2, 3, 4, 5),
            InputDevices = ["Mouse", "Lightgun", "Joypad", "Joypad", "Joypad", "Joypad"],
            NvramStorageMode = NvramStorageMode.PerGame,
            NvramVersion = 3,
        };

        var json = GameProfileJson.Serialize(profile);
        GameProfile loaded = GameProfileJson.Deserialize(json);

        Assert.Equal(EmulationProfile.Accuracy, loaded.Profile);
        Assert.Equal(VideoRegion.Pal2, loaded.Region);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, loaded.ArmTiming);
        Assert.Equal(CdSpeed.X8, loaded.CdSpeed);
        Assert.Equal(1.5, loaded.CpuClockMultiplier);
        Assert.Equal(0x1234_5678u, loaded.RandomSeed);
        Assert.True(loaded.OperaDipir);
        Assert.True(loaded.OperaSoftReset);
        Assert.Equal(CpuBackendKind.Interpreter, loaded.CpuBackend);
        Assert.Equal(0.4f, loaded.CdAudioVolume);
        Assert.True(loaded.CdAudioMuted);
        Assert.Equal(new CropInsets(2, 3, 4, 5), loaded.Overscan);
        Assert.Equal("0123456789ABCDEF0123456789ABCDEF", loaded.BiosMd5);
        Assert.Equal(FixMode.BitTiming5, loaded.FixMode);
        Assert.Equal(VideoFilter.Crt, loaded.Filter);
        Assert.True(loaded.HighResolution);
        Assert.Equal("Mouse", loaded.InputDevices[0]);
        Assert.Equal(NvramStorageMode.PerGame, loaded.NvramStorageMode);
        Assert.Equal(3, loaded.NvramVersion);

        var invalidProfile = new GameProfile
        {
            CpuClockMultiplier = 99,
            CdAudioVolume = 9,
            BiosMd5 = "not-an-md5",
            FixMode = (FixMode)999,
            CpuBackend = (CpuBackendKind)99,
            Filter = (VideoFilter)999,
            Deinterlace = (DeinterlaceMode)99,
            InputDevices = ["Mouse", "bogus", "Joypad"],
            NvramStorageMode = (NvramStorageMode)99,
            NvramVersion = 99,
        };
        GameProfile normalized = GameProfileJson.Deserialize(
            GameProfileJson.Serialize(invalidProfile));
        Assert.Equal(1.0, normalized.CpuClockMultiplier);
        Assert.Equal(0.8f, normalized.CdAudioVolume);
        Assert.Equal(string.Empty, normalized.BiosMd5);
        Assert.Null(normalized.FixMode);
        Assert.Equal(CpuBackendKind.Interpreter, normalized.CpuBackend);
        Assert.Null(normalized.Filter);
        Assert.Null(normalized.Deinterlace);
        Assert.Equal(PbusEncoder.MaxPorts, normalized.InputDevices.Count);
        Assert.Equal("Mouse", normalized.InputDevices[0]);
        Assert.Equal("Joypad", normalized.InputDevices[1]);
        Assert.Null(normalized.NvramStorageMode);
        Assert.Null(normalized.NvramVersion);
    }

    [Fact]
    public void GameProfile_FromSettingsCapturesPerGameOverrides()
    {
        var settings = new AppSettings
        {
            HighResolution = true,
            RandomSeed = 0xCAFE_F00D,
            OperaDipir = true,
            OperaSoftReset = true,
            CpuBackend = CpuBackendKind.Interpreter,
            NvramStorageMode = NvramStorageMode.PerGame,
            NvramVersion = 4,
            OverscanLeft = 1,
            OverscanTop = 2,
            OverscanRight = 3,
            OverscanBottom = 4,
            Deinterlace = DeinterlaceMode.Weave,
        };
        settings.SetBiosOverride("A1B2C3D4", "0123456789ABCDEF0123456789ABCDEF");
        settings.SetFixModeOverride("A1B2C3D4", FixMode.BitTiming5);
        settings.SetFilterOverride("A1B2C3D4", VideoFilter.Crt);
        settings.SetInputDeviceOverride(
            "A1B2C3D4",
            ["Mouse", "Lightgun", "Joypad", "Joypad", "Joypad", "Joypad"]);

        GameProfile profile = GameProfile.FromSettings(settings, "a1b2c3d4");

        Assert.Equal("0123456789abcdef0123456789abcdef", profile.BiosMd5);
        Assert.Equal(FixMode.BitTiming5, profile.FixMode);
        Assert.Equal(VideoFilter.Crt, profile.Filter);
        Assert.True(profile.HighResolution);
        Assert.Equal(0xCAFE_F00Du, profile.RandomSeed);
        Assert.True(profile.OperaDipir);
        Assert.True(profile.OperaSoftReset);
        Assert.Equal(CpuBackendKind.Interpreter, profile.CpuBackend);
        Assert.Equal("Mouse", profile.InputDevices[0]);
        Assert.Equal(NvramStorageMode.PerGame, profile.NvramStorageMode);
        Assert.Equal(4, profile.NvramVersion);
        Assert.Equal(new CropInsets(1, 2, 3, 4), profile.Overscan);
        Assert.Equal(DeinterlaceMode.Weave, profile.Deinterlace);
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
                RandomSeed = 0x0BAD_F00D,
                OperaDipir = true,
                OperaSoftReset = true,
                CpuBackend = CpuBackendKind.Interpreter,
                CdAudioVolume = 0.25f,
                CdAudioMuted = true,
                BiosMd5 = "0123456789ABCDEF0123456789ABCDEF",
                FixMode = FixMode.BitTiming5,
                Filter = VideoFilter.Crt,
                Deinterlace = DeinterlaceMode.Blend,
                HighResolution = true,
                InputDevices = ["Mouse", "Lightgun", "Joypad", "Joypad", "Joypad", "Joypad"],
                NvramStorageMode = NvramStorageMode.PerGame,
                NvramVersion = 2,
                Overscan = new CropInsets(3, 5, 7, 9),
            });

        AppSettings effective = settings.CreateEffectiveForGame("a1b2c3d4");

        Assert.Equal(VideoRegion.Pal1, effective.Region);
        Assert.Equal(ArmTimingMode.Arm60Datasheet, effective.ArmTiming);
        Assert.Equal(CdSpeed.X4, effective.CdSpeed);
        Assert.Equal(0x0BAD_F00Du, effective.RandomSeed);
        Assert.True(effective.OperaDipir);
        Assert.True(effective.OperaSoftReset);
        Assert.Equal(CpuBackendKind.Interpreter, effective.CpuBackend);
        Assert.Equal(0.25f, effective.CdAudioVolume);
        Assert.True(effective.CdAudioMuted);
        Assert.Equal(new CropInsets(3, 5, 7, 9), effective.ResolveOverscan());
        Assert.Equal(CropInsets.None, settings.ResolveOverscan());
        Assert.Equal(
            "0123456789abcdef0123456789abcdef",
            effective.GetBiosOverride("a1b2c3d4"));
        Assert.Equal(FixMode.BitTiming5, effective.GetFixModeOverride("a1b2c3d4"));
        Assert.Equal(VideoFilter.Crt, effective.GetFilterOverride("a1b2c3d4"));
        Assert.Equal(DeinterlaceMode.Blend, effective.Deinterlace);
        Assert.True(effective.HighResolution);
        Assert.Equal("Mouse", effective.ResolveInputDevices("a1b2c3d4")[0]);
        Assert.Equal(NvramStorageMode.PerGame, effective.NvramStorageMode);
        Assert.Equal(2, effective.NvramVersion);

        Assert.Equal(VideoRegion.Ntsc, settings.Region);
        Assert.Equal(ArmTimingMode.FreeDoCompatible, settings.ArmTiming);
        Assert.Equal(CdSpeed.X2, settings.CdSpeed);
        Assert.Equal(1f, settings.CdAudioVolume);
        Assert.False(settings.CdAudioMuted);
        Assert.Null(settings.GetBiosOverride("a1b2c3d4"));
        Assert.Null(settings.GetFixModeOverride("a1b2c3d4"));
        Assert.Null(settings.GetFilterOverride("a1b2c3d4"));
        Assert.Equal(DeinterlaceMode.Auto, settings.Deinterlace);
        Assert.False(settings.HighResolution);
        Assert.Equal(NvramStorageMode.Global, settings.NvramStorageMode);
        Assert.Equal(0, settings.NvramVersion);

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
    public void DisplayRefreshRate_RoundtripsAndFallsBackWhenOutOfRange()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { DisplayRefreshRate = 144 };

        AppSettingsStore.Save(settings, path);
        Assert.Equal(144, AppSettingsStore.Load(path).DisplayRefreshRate);

        settings.DisplayRefreshRate = 0;
        AppSettingsStore.Save(settings, path);
        Assert.Equal(0, AppSettingsStore.Load(path).DisplayRefreshRate);

        settings.DisplayRefreshRate = 9000;
        AppSettingsStore.Save(settings, path);
        Assert.Equal(AppSettings.DefaultDisplayRefreshRate, AppSettingsStore.Load(path).DisplayRefreshRate);

        settings.DisplayRefreshRate = 5;
        AppSettingsStore.Save(settings, path);
        Assert.Equal(AppSettings.DefaultDisplayRefreshRate, AppSettingsStore.Load(path).DisplayRefreshRate);
    }

    [Fact]
    public void DisplayRefreshRate_ValidationAcceptsZeroAndTheSupportedRange()
    {
        var settings = new AppSettings();
        Assert.Equal(AppSettings.DefaultDisplayRefreshRate, settings.ResolveDisplayRefreshRate());

        settings.DisplayRefreshRate = 0;
        Assert.Equal(0, settings.ResolveDisplayRefreshRate());

        settings.DisplayRefreshRate = AppSettings.MinDisplayRefreshRate;
        Assert.Equal(AppSettings.MinDisplayRefreshRate, settings.ResolveDisplayRefreshRate());

        settings.DisplayRefreshRate = AppSettings.MaxDisplayRefreshRate;
        Assert.Equal(AppSettings.MaxDisplayRefreshRate, settings.ResolveDisplayRefreshRate());

        settings.DisplayRefreshRate = 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ResolveDisplayRefreshRate());

        settings.DisplayRefreshRate = AppSettings.MaxDisplayRefreshRate + 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ResolveDisplayRefreshRate());
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
    public void Migrate_BacksUpANewerSchemaFileBeforeRewriting()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string newer = """{"SettingsVersion":99,"BiosPath":"future.bin"}""";
        File.WriteAllText(path, newer);

        Assert.Equal(99, AppSettingsStore.Migrate(path));

        Assert.True(File.Exists(path + ".v99.bak"));
        Assert.Equal(newer, File.ReadAllText(path + ".v99.bak"));
        Assert.Equal(AppSettings.CurrentVersion, AppSettingsStore.Inspect(path).Version);
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
