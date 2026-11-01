using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Host;

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
