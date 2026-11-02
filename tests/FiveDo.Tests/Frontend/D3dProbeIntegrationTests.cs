using System;
using System.IO;
using System.Text.Json;
using FiveDo.Frontend.Avalonia;
using FiveDo.Video;

namespace FiveDo.Tests.Frontend;

public sealed class D3dProbeIntegrationTests
{
    [Fact]
    public void Probe_WritesAnAccurateParityReport()
    {
        if (!OperatingSystem.IsWindows()
            || !string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_D3D_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var directory = new TempDirectory();
        var outputPath = Path.Combine(directory.Path, "probe.json");
        var options = new D3dProbeOptions(
            outputPath,
            CapturePath: null,
            Width: 320,
            Height: 240,
            Frames: 2,
            WarmupFrames: 1,
            VideoFilter.Accurate,
            TimeoutSeconds: 30);

        Assert.Equal(0, D3dProbeRunner.Run(options));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath));
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("Direct3D11", root.GetProperty("requestedBackend").GetString());
        Assert.True(root.GetProperty("parity").GetProperty("matches").GetBoolean());
        Assert.Equal(0, root.GetProperty("parity").GetProperty("differentPixels").GetInt32());
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "5do-d3d-probe-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Best-effort test cleanup.
            }
        }
    }
}
