using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FiveDo.Diagnostics;

namespace FiveDo.Tests.Diagnostics;

public sealed class DiagnosticsBundleTests
{
    [Fact]
    public void Create_WritesScrubbedSettingsLogsAndManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fivedo-bundle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var jsonRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);
            File.WriteAllText(
                settingsPath,
                $$"""
                {
                  "DiscPath": "{{jsonRoot}}\\game.chd",
                  "RecentDiscs": ["{{jsonRoot}}\\game.chd"],
                  "BiosFolder": "{{jsonRoot}}\\bios",
                  "AudioDevice": "Speakers",
                  "Volume": 0.75,
                  "Notes": "user {{Environment.UserName}}"
                }
                """);
            var logs = Path.Combine(root, "logs");
            Directory.CreateDirectory(logs);
            File.WriteAllText(
                Path.Combine(logs, "5do-01.log"),
                $"INFO starting {root} for {Environment.UserName}{Environment.NewLine}");
            var frame = Path.Combine(root, "frame.png");
            File.WriteAllBytes(frame, [1, 2, 3, 4]);

            var output = Path.Combine(root, "bundle.zip");
            DiagnosticsBundleResult result = DiagnosticsBundle.Create(
                output,
                new DiagnosticsBundleOptions
                {
                    SettingsPath = settingsPath,
                    LogDirectory = logs,
                    FramePath = frame,
                    Notes = "crash in " + root,
                });

            Assert.True(File.Exists(result.Path));
            Assert.Contains("manifest.json", result.Entries, StringComparer.Ordinal);
            Assert.Contains("environment.txt", result.Entries, StringComparer.Ordinal);
            Assert.Contains("settings.json", result.Entries, StringComparer.Ordinal);
            Assert.Contains("logs/5do-01.log", result.Entries, StringComparer.Ordinal);
            Assert.Contains("frame.png", result.Entries, StringComparer.Ordinal);
            Assert.True(result.ScrubHits > 0);

            using ZipArchive archive = ZipFile.OpenRead(output);
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.NotNull(archive.GetEntry("environment.txt"));
            Assert.NotNull(archive.GetEntry("settings.json"));
            Assert.NotNull(archive.GetEntry("logs/5do-01.log"));
            Assert.NotNull(archive.GetEntry("frame.png"));

            var settings = ReadText(archive, "settings.json");
            Assert.DoesNotContain("DiscPath", settings, StringComparison.Ordinal);
            Assert.DoesNotContain("RecentDiscs", settings, StringComparison.Ordinal);
            Assert.DoesNotContain("BiosFolder", settings, StringComparison.Ordinal);
            Assert.DoesNotContain("AudioDevice", settings, StringComparison.Ordinal);
            Assert.DoesNotContain(root, settings, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("0.75", settings, StringComparison.Ordinal);

            var log = ReadText(archive, "logs/5do-01.log");
            Assert.DoesNotContain(root, log, StringComparison.OrdinalIgnoreCase);
            // ScrubText deliberately ignores user names shorter than three characters
            // to avoid replacing common substrings; the Linux test VM uses "vm".
            if (Environment.UserName.Length >= 3)
            {
                Assert.DoesNotContain(Environment.UserName, log, StringComparison.Ordinal);
            }

            var environment = ReadText(archive, "environment.txt");
            if (Environment.UserName.Length >= 3)
            {
                Assert.DoesNotContain(Environment.UserName, environment, StringComparison.Ordinal);
            }

            Assert.Contains("Application Version:", environment, StringComparison.Ordinal);

            var manifest = ReadText(archive, "manifest.json");
            Assert.Contains("\"schemaVersion\": 1", manifest, StringComparison.Ordinal);
            Assert.Contains("\"settingsIncluded\": true", manifest, StringComparison.Ordinal);
            Assert.DoesNotContain(root, manifest, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_WorksWithoutOptionalEvidence()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fivedo-bundle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var output = Path.Combine(root, "bundle.zip");
            DiagnosticsBundleResult result = DiagnosticsBundle.Create(output);

            Assert.Equal(
                ["environment.txt", "manifest.json"],
                result.Entries.OrderBy(name => name, StringComparer.Ordinal),
                StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string ReadText(ZipArchive archive, string name)
    {
        ZipArchiveEntry entry = archive.GetEntry(name) ?? throw new InvalidDataException(name);
        using Stream stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
