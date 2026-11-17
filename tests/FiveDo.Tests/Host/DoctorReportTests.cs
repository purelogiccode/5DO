using System;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Storage;

namespace FiveDo.Tests.Host;

public sealed class DoctorReportTests
{
    [Fact]
    public void Collect_ReportsChecksAndBiosFolders()
    {
        using var folder = new TempFolder();

        DoctorResult result = DoctorReport.Collect(
            new BiosDiscoveryResult([], null),
            dataDirectory: folder.Path);

        Assert.Equal(FiveDoVersion.ProductName, result.Product);
        Assert.Equal(FiveDoVersion.Version, result.Version);
        Assert.NotEmpty(result.Checks);
        Assert.True(result.DataDirectoryWritable);
        Assert.Contains(result.Checks, check =>
            string.Equals(check.Name, "Data folder", StringComparison.Ordinal)
            && check.Status == DoctorStatus.Ok);
        Assert.Contains(result.Checks, check =>
            string.Equals(check.Name, "BIOS", StringComparison.Ordinal)
            && check.Status == DoctorStatus.Warn);
    }

    [Fact]
    public void Collect_UsesTheDiscoveredBios()
    {
        using var folder = new TempFolder();
        var profile = new BiosProfile(
            Path.Combine(folder.Path, "panafz1.bin"),
            "panafz1.bin",
            "f47264dd47fe30f73ab3c010015c155b",
            EmulatorConstants.RomBankSize,
            BiosImageKind.Primary,
            BiosTerritory.Usa,
            BiosVideoStandard.Ntsc,
            IsKanji: false,
            IsAnvil: false,
            IsPatched: false,
            Name: "Panasonic FZ-1",
            Notes: string.Empty,
            Preference: 50);
        var discovery = new BiosDiscoveryResult(
            [new BiosDiscoveryFolder(folder.Path, "test", true, [profile], [], [])],
            profile);

        DoctorResult result = DoctorReport.Collect(discovery, dataDirectory: folder.Path);

        DoctorCheck bios = result.Checks.Single(check =>
            string.Equals(check.Name, "BIOS", StringComparison.Ordinal));
        Assert.Equal(DoctorStatus.Ok, bios.Status);
        Assert.Contains("Panasonic FZ-1", bios.Detail, StringComparison.Ordinal);
        Assert.True(result.Bios.HasBios);
    }

    [Fact]
    public void FormatText_ContainsTheSectionsAndRows()
    {
        using var folder = new TempFolder();
        DoctorResult result = DoctorReport.Collect(
            new BiosDiscoveryResult([], null),
            dataDirectory: folder.Path);

        var text = DoctorReport.FormatText(result);

        Assert.Contains("doctor", text, StringComparison.Ordinal);
        Assert.Contains("Checks", text, StringComparison.Ordinal);
        Assert.Contains("[ok]", text, StringComparison.Ordinal);
        Assert.Contains("Data folder", text, StringComparison.Ordinal);
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fivedo-doctor-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
