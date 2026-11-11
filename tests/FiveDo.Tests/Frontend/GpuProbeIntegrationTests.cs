using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FiveDo.Frontend.Avalonia;

namespace FiveDo.Tests.Frontend;

/// <summary>
/// Opt-in GPU integration test for the real WGL and ANGLE/GLES presentation paths.
/// </summary>
/// <remarks>
/// The test is disabled by default because it opens an Avalonia window and needs a
/// desktop OpenGL/ANGLE context. Run it on a GPU machine with
/// <c>FIVEDO_GPU_TESTS=1</c>. It executes the built frontend probe rather than
/// creating a context in-process so it exercises the same path as the manual
/// <c>--gl-probe</c> validation.
/// </remarks>
public sealed class GpuProbeIntegrationTests
{
    private static readonly string[] Backends = ["wgl", "angle"];

    [Fact]
    public void Probe_ProducesIdenticalAccurateFramesOnWglAndAngle()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_GPU_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var frontend = FindFrontendAssembly();
        var root = FindRepositoryRoot();
        var temporary = Path.Combine(root, "artifacts", "gpu-probe-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            foreach (var backend in Backends)
            {
                var reportPath = Path.Combine(temporary, $"parity-{backend}.json");
                var capturePath = Path.Combine(temporary, $"parity-{backend}.png");
                RunProbe(
                    frontend,
                    backend,
                    reportPath,
                    capturePath,
                    size: "320x240",
                    frames: 20,
                    warmup: 5,
                    filter: "accurate");

                GlProbeReport report = ReadReport(reportPath);
                Assert.True(report.Ok, $"{backend} probe failed: {report.Error}");
                Assert.NotNull(report.Parity);
                Assert.True(report.Parity!.Value.Compared);
                Assert.True(report.Parity.Value.Matches, $"{backend} parity mismatch.");
                Assert.Equal(0, report.Parity.Value.DifferentPixels);
                Assert.False(report.Parity.Value.VerticalFlip, $"{backend} output was vertically flipped.");
                Assert.Equal(320, report.OutputWidth);
                Assert.Equal(240, report.OutputHeight);
                Assert.Equal(0, report.RendererAllocatedBytesPerFrame);
            }

            // A short 4K pass validates the real two-stage pipeline under both
            // backends without making this opt-in test timing-sensitive.
            foreach (var backend in Backends)
            {
                var reportPath = Path.Combine(temporary, $"4k-{backend}.json");
                RunProbe(
                    frontend,
                    backend,
                    reportPath,
                    capturePath: null,
                    size: "3840x2160",
                    frames: 10,
                    warmup: 5,
                    filter: "sharp-bilinear");

                GlProbeReport report = ReadReport(reportPath);
                Assert.True(report.Ok, $"{backend} 4K probe failed: {report.Error}");
                Assert.Equal(3840, report.OutputWidth);
                Assert.Equal(2160, report.OutputHeight);
                Assert.Equal(0, report.RendererAllocatedBytesPerFrame);
            }

            // HQx exercises the RG32UI rule texture and the GLES/ANGLE shader path.
            foreach (var backend in Backends)
            {
                var reportPath = Path.Combine(temporary, $"hqx-{backend}.json");
                RunProbe(
                    frontend,
                    backend,
                    reportPath,
                    capturePath: null,
                    size: "640x480",
                    frames: 10,
                    warmup: 5,
                    filter: "hqx");

                GlProbeReport report = ReadReport(reportPath);
                Assert.True(report.Ok, $"{backend} HQx probe failed: {report.Error}");
                Assert.False(report.FilterFallback, $"{backend} HQx probe fell back.");
                Assert.Equal(0, report.RendererAllocatedBytesPerFrame);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(temporary, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void RunProbe(
        string frontend,
        string backend,
        string reportPath,
        string? capturePath,
        string size,
        int frames,
        int warmup,
        string filter)
    {
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = FindRepositoryRoot(),
        };
        start.ArgumentList.Add(frontend);
        start.ArgumentList.Add("--gl-backend");
        start.ArgumentList.Add(backend);
        start.ArgumentList.Add("--gl-probe");
        start.ArgumentList.Add(reportPath);
        start.ArgumentList.Add("--gl-probe-size");
        start.ArgumentList.Add(size);
        start.ArgumentList.Add("--gl-probe-frames");
        start.ArgumentList.Add(frames.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--gl-probe-warmup");
        start.ArgumentList.Add(warmup.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--gl-probe-filter");
        start.ArgumentList.Add(filter);
        start.ArgumentList.Add("--gl-probe-offscreen");
        start.ArgumentList.Add("--gl-probe-timeout");
        start.ArgumentList.Add("120");
        if (capturePath is not null)
        {
            start.ArgumentList.Add("--gl-probe-capture");
            start.ArgumentList.Add(capturePath);
        }

        using Process process = Process.Start(start)
                                ?? throw new InvalidOperationException("The frontend probe process could not be started.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(180_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"The {backend} probe did not exit within 180 seconds.");
        }

        stdout.Wait();
        stderr.Wait();
        Assert.True(
            process.ExitCode == 0,
            $"The {backend} probe exited with {process.ExitCode}.{Environment.NewLine}"
            + $"{stdout.Result}{Environment.NewLine}{stderr.Result}");
    }

    private static GlProbeReport ReadReport(string path)
    {
        Assert.True(File.Exists(path), $"Probe report '{path}' was not written.");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, GlProbeJsonContext.Default.GlProbeReport)
            ?? throw new InvalidOperationException("The probe report could not be deserialized.");
    }

    private static string FindFrontendAssembly()
    {
        var root = FindRepositoryRoot();
        foreach (var configuration in new[] { "Release", "Debug" })
        {
            var path = Path.Combine(
                root,
                "src",
                "FiveDo.Frontend.Avalonia",
                "bin",
                configuration,
                "net11.0",
                "FiveDo.Frontend.Avalonia.dll");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            "Build the frontend before running the GPU integration test (dotnet build -c Release).");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "5DO.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
