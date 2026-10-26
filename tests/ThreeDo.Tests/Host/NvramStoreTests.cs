using System;
using System.IO;
using ThreeDo.Core;
using ThreeDo.Host;
using ThreeDo.Storage;

namespace ThreeDo.Tests.Host;

public sealed class NvramStoreTests
{
    [Fact]
    public void LoadOrCreate_ReturnsFreshImageWhenMissing()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nvram.ram");
        var store = new NvramStore(path);

        var image = store.LoadOrCreate();

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

        var image = store.LoadOrCreate();

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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"threedo-nvram-{Guid.NewGuid():N}");
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
            Filter = ThreeDo.Video.VideoFilter.Crt,
            Aspect = ThreeDo.Video.AspectMode.Integer,
            Volume = 0.5f,
            HighResolution = true,
            SaveSlot = 3,
            SaveSlotCount = 12,
            AutoGameFixes = false,
            ShowOverlay = true,
            Bloom = 0.7f,
            FirstRunCompleted = true,
        };
        settings.Input.Players[0].Keyboard["Q"] = ThreeDo.Input.ControllerButton.C;
        settings.RecentDiscs.Add("game.iso");
        settings.LibraryFolders.Add("games");
        settings.FixModeOverrides["9F7D72BC"] = ThreeDo.Core.FixMode.BitTiming6;
        settings.SetFilterOverride("9F7D72BC", ThreeDo.Video.VideoFilter.Hqx);

        AppSettingsStore.Save(settings, path);
        var loaded = AppSettingsStore.Load(path);

        Assert.Equal("bios.bin", loaded.BiosPath);
        Assert.Equal(ThreeDo.Video.VideoFilter.Crt, loaded.Filter);
        Assert.Equal(ThreeDo.Video.AspectMode.Integer, loaded.Aspect);
        Assert.Equal(0.5f, loaded.Volume);
        Assert.True(loaded.HighResolution);
        Assert.Equal(3, loaded.SaveSlot);
        Assert.Equal(12, loaded.SaveSlotCount);
        Assert.False(loaded.AutoGameFixes);
        Assert.True(loaded.ShowOverlay);
        Assert.Equal(0.7f, loaded.Bloom);
        Assert.Equal(0.7f, loaded.ToRendererSettings().Bloom);
        Assert.True(loaded.FirstRunCompleted);
        Assert.Equal(ThreeDo.Input.ControllerButton.C, loaded.Input.Players[0].Keyboard["Q"]);
        Assert.Equal("game.iso", Assert.Single(loaded.RecentDiscs));
        Assert.Equal("games", Assert.Single(loaded.LibraryFolders));
        Assert.Equal(ThreeDo.Core.FixMode.BitTiming6, loaded.GetFixModeOverride("9F7D72BC"));
        Assert.Equal(ThreeDo.Video.VideoFilter.Crt, loaded.Filter);
        Assert.Equal(ThreeDo.Video.VideoFilter.Hqx, loaded.ResolveFilter("9F7D72BC"));
        Assert.Equal(ThreeDo.Video.VideoFilter.Crt, loaded.ResolveFilter("UNKNOWN"));
        Assert.Equal(ThreeDo.Video.VideoFilter.Hqx, loaded.ToRendererSettings("9F7D72BC").Filter);
        Assert.Equal(ThreeDo.Video.VideoFilter.Crt, loaded.ToRendererSettings().Filter);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void AppSettings_InvalidJsonFallsBackToDefaults()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "not json");

        var loaded = AppSettingsStore.Load(path);

        Assert.Equal(ThreeDo.Video.VideoFilter.SharpBilinear, loaded.Filter);
    }
}
