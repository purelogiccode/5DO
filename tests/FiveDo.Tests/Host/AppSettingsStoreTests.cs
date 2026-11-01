using System;
using System.IO;
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
        };

        AppSettingsStore.Save(settings, path);
        string json = File.ReadAllText(path);
        Assert.Contains($"\"SettingsVersion\": {AppSettings.CurrentVersion}", json, StringComparison.Ordinal);

        AppSettings loaded = AppSettingsStore.Load(path);
        Assert.Equal(AppSettings.CurrentVersion, loaded.SettingsVersion);
        Assert.Equal(VideoRendererKind.Vulkan, loaded.Renderer);
        Assert.Equal(1.5, loaded.CpuClockMultiplier);
    }

    [Fact]
    public void Load_ValidatesEnumerationsAndNumericRanges()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(
            path,
            """{"SettingsVersion":1,"Renderer":9999,"CpuClockMultiplier":-1,"OsdOpacity":5.0,"OsdDurationMilliseconds":1}""");

        AppSettings settings = AppSettingsStore.Load(path);

        Assert.Equal(VideoRendererKind.OpenGl, settings.Renderer);
        Assert.Equal(1.0, settings.CpuClockMultiplier);
        Assert.InRange(settings.OsdOpacity, 0.1, 1.0);
        Assert.InRange(settings.OsdDurationMilliseconds, 250, 30_000);
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
