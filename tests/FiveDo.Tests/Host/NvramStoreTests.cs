using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Storage;

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

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan delta) => _now += delta;
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
    public void AppSettings_RoundtripsAndDerivesRendererSettings()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings
        {
            BiosPath = "bios.bin",
            Filter = FiveDo.Video.VideoFilter.Crt,
            Aspect = FiveDo.Video.AspectMode.Integer,
            Volume = 0.5f,
            HighResolution = true,
            SaveSlot = 3,
            SaveSlotCount = 12,
            AutoGameFixes = false,
            ShowOverlay = true,
            SaveNativeScreenshot = true,
            Bloom = 0.7f,
            FirstRunCompleted = true,
        };
        settings.Input.Players[0].Keyboard["Q"] = FiveDo.Input.ControllerButton.C;
        settings.RecentDiscs.Add("game.iso");
        settings.LibraryFolders.Add("games");
        settings.FixModeOverrides["9F7D72BC"] = FixMode.BitTiming6;
        settings.SetFilterOverride("9F7D72BC", FiveDo.Video.VideoFilter.Hqx);

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal("bios.bin", loaded.BiosPath);
        Assert.Equal(FiveDo.Video.VideoFilter.Crt, loaded.Filter);
        Assert.Equal(FiveDo.Video.AspectMode.Integer, loaded.Aspect);
        Assert.Equal(0.5f, loaded.Volume);
        Assert.True(loaded.HighResolution);
        Assert.Equal(3, loaded.SaveSlot);
        Assert.Equal(12, loaded.SaveSlotCount);
        Assert.False(loaded.AutoGameFixes);
        Assert.True(loaded.ShowOverlay);
        Assert.True(loaded.SaveNativeScreenshot);
        Assert.Equal(0.7f, loaded.Bloom);
        Assert.Equal(0.7f, loaded.ToRendererSettings().Bloom);
        Assert.True(loaded.FirstRunCompleted);
        Assert.Equal(FiveDo.Input.ControllerButton.C, loaded.Input.Players[0].Keyboard["Q"]);
        Assert.Equal("game.iso", Assert.Single(loaded.RecentDiscs));
        Assert.Equal("games", Assert.Single(loaded.LibraryFolders));
        Assert.Equal(FixMode.BitTiming6, loaded.GetFixModeOverride("9F7D72BC"));
        Assert.Equal(FiveDo.Video.VideoFilter.Crt, loaded.Filter);
        Assert.Equal(FiveDo.Video.VideoFilter.Hqx, loaded.ResolveFilter("9F7D72BC"));
        Assert.Equal(FiveDo.Video.VideoFilter.Crt, loaded.ResolveFilter("UNKNOWN"));
        Assert.Equal(FiveDo.Video.VideoFilter.Hqx, loaded.ToRendererSettings("9F7D72BC").Filter);
        Assert.Equal(FiveDo.Video.VideoFilter.Crt, loaded.ToRendererSettings().Filter);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void AppSettings_RoundtripsCustomAspectAndRendererSettings()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var settings = new AppSettings
        {
            Aspect = FiveDo.Video.AspectMode.Custom,
            CustomAspect = 1.6,
            PreferOpenGl = false,
            OutputResolution = FiveDo.Video.OutputResolution.FullHd1080,
        };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(FiveDo.Video.AspectMode.Custom, loaded.Aspect);
        Assert.Equal(1.6, loaded.CustomAspect);
        Assert.Equal(VideoRendererKind.Software, loaded.Renderer);
        Assert.False(loaded.PreferOpenGl);
        Assert.Equal(FiveDo.Video.OutputResolution.FullHd1080, loaded.OutputResolution);
        Assert.Equal(FiveDo.Video.AspectMode.Custom, loaded.ToRendererSettings().Aspect);
        Assert.Equal(1.6, loaded.ToRendererSettings().CustomAspect);
        Assert.Equal(
            FiveDo.Video.OutputResolution.FullHd1080,
            loaded.ToRendererSettings().OutputResolution);
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
        var settings = new AppSettings { Filter = FiveDo.Video.VideoFilter.Hqx4x };

        AppSettingsStore.Save(settings, path);
        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(FiveDo.Video.VideoFilter.Hqx4x, loaded.Filter);
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
    public void AppSettings_InvalidJsonFallsBackToDefaults()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "not json");

        AppSettings loaded = AppSettingsStore.Load(path);

        Assert.Equal(FiveDo.Video.VideoFilter.SharpBilinear, loaded.Filter);
    }
}
