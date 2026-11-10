using System;
using System.IO;
using FiveDo.Host;
using FiveDo.Video;

namespace FiveDo.Tests.Host;

public sealed class ScreenshotMetadataTests
{
    [Fact]
    public void Capture_SaveAndLoad_RoundtripsSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-shot-meta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var settings = new AppSettings
            {
                Renderer = VideoRendererKind.Direct3D11,
                Filter = VideoFilter.Crt,
                Aspect = AspectMode.Integer,
                HighResolution = true,
                Region = FiveDo.Core.VideoRegion.Pal1,
                OverscanLeft = 1,
                OverscanTop = 2,
                OverscanRight = 3,
                OverscanBottom = 4,
                VideoBorderPixels = 8,
                VoidAreaColor = "#FF202020",
                VoidAreaPattern = "Checker",
                Profile = FiveDo.Core.EmulationProfile.Accuracy,
                ArmTiming = FiveDo.Core.ArmTimingMode.Arm60Datasheet,
                ClioTimers = FiveDo.Core.ClioTimerMode.OperaScheduler,
                CelTiming = FiveDo.Core.CelTimingMode.OperaBusy,
                CdromTiming = FiveDo.Core.CdromTimingMode.OperaCycleBased,
                CdSpeed = FiveDo.Core.CdSpeed.X8,
                CpuClockMultiplier = 1.25,
                Deinterlace = DeinterlaceMode.Weave,
            };
            string png = Path.Combine(directory, "Gex (USA)-20261102-120000-f000123.png");
            File.WriteAllBytes(png, [1, 2, 3]);

            ScreenshotMetadata.Capture(settings, "Gex (USA)", "A1B2C3D4", 123, DeinterlaceMode.Weave, true).Save(png);
            ScreenshotMetadata? loaded = ScreenshotMetadata.Load(png);

            Assert.NotNull(loaded);
            Assert.Equal(ScreenshotMetadata.CurrentVersion, loaded.Version);
            Assert.Equal("Gex (USA)", loaded.GameName);
            Assert.Equal("A1B2C3D4", loaded.GameId);
            Assert.Equal(123, loaded.Frame);
            Assert.Equal("Direct3D11", loaded.Renderer);
            Assert.Equal("Crt", loaded.Filter);
            Assert.Equal("Integer", loaded.Aspect);
            Assert.True(loaded.HighResolution);
            Assert.Equal("Pal1", loaded.Region);
            Assert.Equal(new CropInsets(1, 2, 3, 4), new CropInsets(
                loaded.OverscanLeft,
                loaded.OverscanTop,
                loaded.OverscanRight,
                loaded.OverscanBottom));
            Assert.Equal(8, loaded.VideoBorderPixels);
            Assert.Equal("#FF202020", loaded.VoidAreaColor);
            Assert.Equal("Checker", loaded.VoidAreaPattern);
            Assert.Equal("Accuracy", loaded.Profile);
            Assert.Equal(1.25, loaded.CpuClockMultiplier);
            Assert.Equal("Weave", loaded.Deinterlace);
            Assert.True(loaded.Interlaced);
            Assert.Contains("Direct3D11/Crt", loaded.Summary(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_FallsBackFromNativeCompanionAndRejectsInvalidSidecars()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-shot-meta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string png = Path.Combine(directory, "Gex (USA)-20261102-120000-f000123.png");
            string native = Path.Combine(directory, "Gex (USA)-20261102-120000-f000123-native.png");
            File.WriteAllBytes(png, [1]);
            File.WriteAllBytes(native, [1]);
            ScreenshotMetadata.Capture(new AppSettings(), "Gex (USA)", string.Empty, 123).Save(png);

            Assert.NotNull(ScreenshotMetadata.Load(native));

            string invalid = Path.Combine(directory, "invalid.png");
            File.WriteAllBytes(invalid, [1]);
            File.WriteAllText(ScreenshotMetadata.GetSidecarPath(invalid), "{not json");
            Assert.Null(ScreenshotMetadata.Load(invalid));

            string missing = Path.Combine(directory, "missing.png");
            Assert.Null(ScreenshotMetadata.Load(missing));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
