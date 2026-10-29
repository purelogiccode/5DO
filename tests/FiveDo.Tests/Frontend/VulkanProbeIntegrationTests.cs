using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FiveDo.Frontend.Avalonia;

namespace FiveDo.Tests.Frontend;

/// <summary>
/// Opt-in integration test for the headless Vulkan presentation backend.
/// </summary>
/// <remarks>
/// The test is disabled by default because it requires a Vulkan 1.0 device and driver.
/// Run it with <c>FIVEDO_VULKAN_TESTS=1</c>. It executes the built frontend probe so it
/// exercises the same command-line path as the manual <c>--vk-probe</c> validation.
/// </remarks>
public sealed class VulkanProbeIntegrationTests
{
    [Fact]
    public void Probe_ProducesCpuParityAndRunsAt4K()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_VULKAN_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var frontend = FindFrontendAssembly();
        var root = FindRepositoryRoot();
        var temporary = Path.Combine(root, "artifacts", "vk-probe-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var parityPath = Path.Combine(temporary, "parity.json");
            var capturePath = Path.Combine(temporary, "parity.png");
            RunProbe(
                frontend,
                parityPath,
                capturePath,
                size: "320x240",
                frames: 20,
                warmup: 5,
                filter: "accurate");

            GlProbeReport parity = ReadReport(parityPath);
            Assert.True(parity.Ok, parity.Error);
            Assert.Equal("Vulkan", parity.Backend);
            Assert.NotNull(parity.Parity);
            Assert.True(parity.Parity!.Value.Matches);
            Assert.Equal(0, parity.Parity.Value.DifferentPixels);
            Assert.False(parity.Parity.Value.VerticalFlip);
            Assert.Equal(0, parity.RendererAllocatedBytesPerFrame);

            var fourKPath = Path.Combine(temporary, "4k.json");
            RunProbe(
                frontend,
                fourKPath,
                capturePath: null,
                size: "3840x2160",
                frames: 10,
                warmup: 5,
                filter: "sharp-bilinear");

            GlProbeReport fourK = ReadReport(fourKPath);
            Assert.True(fourK.Ok, fourK.Error);
            Assert.Equal(3840, fourK.OutputWidth);
            Assert.Equal(2160, fourK.OutputHeight);
            Assert.Equal(0, fourK.RendererAllocatedBytesPerFrame);
            Assert.NotNull(fourK.Gpu);
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
        start.ArgumentList.Add("--vk-probe");
        start.ArgumentList.Add(reportPath);
        start.ArgumentList.Add("--vk-probe-size");
        start.ArgumentList.Add(size);
        start.ArgumentList.Add("--vk-probe-frames");
        start.ArgumentList.Add(frames.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--vk-probe-warmup");
        start.ArgumentList.Add(warmup.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--vk-probe-filter");
        start.ArgumentList.Add(filter);
        start.ArgumentList.Add("--vk-probe-timeout");
        start.ArgumentList.Add("120");
        if (capturePath is not null)
        {
            start.ArgumentList.Add("--vk-probe-capture");
            start.ArgumentList.Add(capturePath);
        }

        using Process process = Process.Start(start)
                                ?? throw new InvalidOperationException("The Vulkan probe process could not be started.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(180_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("The Vulkan probe did not exit within 180 seconds.");
        }

        stdout.Wait();
        stderr.Wait();
        Assert.True(
            process.ExitCode == 0,
            $"The Vulkan probe exited with {process.ExitCode}.{Environment.NewLine}"
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
                "net10.0",
                "FiveDo.Frontend.Avalonia.dll");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            "Build the frontend before running the Vulkan integration test (dotnet build -c Release).");
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
