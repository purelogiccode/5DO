using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FiveDo.Core;
using FiveDo.Diagnostics;
using FiveDo.Video;
using FiveDo.Video.Vulkan;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Runs the headless Vulkan presentation probe and writes the shared GPU probe report.
/// </summary>
internal static class VkProbeRunner
{
    private const int SourceWidth = EmulatorConstants.VideoWidth;
    private const int SourceHeight = EmulatorConstants.VideoHeight;

    /// <summary>Runs one probe request.</summary>
    /// <param name="options">Probe options.</param>
    /// <returns>Process exit code: 0 on success, 2 on failure.</returns>
    internal static int Run(VkProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var frame = new VdlFrame();
        SyntheticVdlFrame.Initialize(frame);
        var presentSamples = new List<double>(options.Frames);
        var gpuSamples = new List<double>(options.Frames);
        var stopwatch = new Stopwatch();
        long allocatedBytes = 0;
        var measured = 0;
        VulkanOffscreenRenderer? renderer = null;
        byte[]? capturedPixels = null;
        try
        {
            renderer = new VulkanOffscreenRenderer
            {
                Settings = new RendererSettings
                {
                    Filter = options.Filter,
                    Aspect = AspectMode.Stretch,
                    IntegerScale = false,
                    Vsync = false,
                },
            };
            renderer.Initialize(options.Width, options.Height);
            for (var i = 0; i < options.WarmupFrames + options.Frames; i++)
            {
                SyntheticVdlFrame.Fill(frame, i);
                if (i == options.WarmupFrames)
                {
                    allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                    stopwatch.Restart();
                    presentSamples.Clear();
                    gpuSamples.Clear();
                }

                var start = Stopwatch.GetTimestamp();
                renderer.Present(frame);
                var presentMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                if (i < options.WarmupFrames)
                {
                    continue;
                }

                presentSamples.Add(presentMs);
                if (renderer.GpuMilliseconds is { } gpuMs)
                {
                    gpuSamples.Add(gpuMs);
                }

                measured++;
                if (stopwatch.Elapsed.TotalSeconds > options.TimeoutSeconds)
                {
                    throw new TimeoutException(
                        $"The Vulkan probe timed out after {options.TimeoutSeconds} seconds.");
                }
            }

            stopwatch.Stop();
            allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytes;
            if (options.CapturePath is not null || IsParityCandidate(options))
            {
                capturedPixels = new byte[checked(options.Width * options.Height * 4)];
                renderer.ReadPixels(capturedPixels, out _, out _);
                if (options.CapturePath is not null)
                {
                    PngDumper.WriteBgra(capturedPixels, options.Width, options.Height, options.CapturePath);
                }
            }

            GlProbeParity? parity = capturedPixels is null
                ? null
                : ComputeParity(capturedPixels, frame, options.Width, options.Height, options.Filter);
            GlProbeReport report = BuildReport(
                options,
                renderer,
                presentSamples,
                gpuSamples,
                measured,
                stopwatch.Elapsed.TotalSeconds,
                allocatedBytes,
                parity,
                error: null);
            WriteReport(options.OutputPath, report);
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Serilog.Log.Error(exception, "Vulkan probe failed.");
            try
            {
                GlProbeReport report = BuildReport(
                    options,
                    renderer,
                    presentSamples,
                    gpuSamples,
                    measured,
                    stopwatch.Elapsed.TotalSeconds,
                    allocatedBytes,
                    parity: null,
                    error: exception.Message);
                WriteReport(options.OutputPath, report);
            }
            catch (Exception reportException) when (reportException is IOException or UnauthorizedAccessException)
            {
                Serilog.Log.Error(reportException, "Vulkan probe report failed.");
            }

            return 2;
        }
        finally
        {
            renderer?.Dispose();
        }
    }

    private static bool IsParityCandidate(VkProbeOptions options)
        => options.Width == SourceWidth
            && options.Height == SourceHeight
            && options.Filter == VideoFilter.Accurate;

    private static GlProbeReport BuildReport(
        VkProbeOptions options,
        VulkanOffscreenRenderer? renderer,
        List<double> presentSamples,
        List<double> gpuSamples,
        int measured,
        double elapsedSeconds,
        long allocatedBytes,
        GlProbeParity? parity,
        string? error)
    {
        var frames = Math.Max(1, measured);
        RendererCapabilities capabilities = renderer?.Capabilities ?? default;
        return new GlProbeReport(
            Ok: error is null,
            Error: error,
            RequestedBackend: "Vulkan",
            Backend: "Vulkan",
            Vendor: "Vulkan",
            Renderer: renderer?.DeviceName,
            Api: capabilities.Api.ToString(),
            MajorVersion: capabilities.MajorVersion,
            MinorVersion: capabilities.MinorVersion,
            MaxTextureSize: capabilities.MaxTextureSize,
            SupportsIntegerTextures: capabilities.SupportsIntegerTextures,
            SupportsVsync: false,
            VsyncRequested: false,
            VsyncActive: false,
            VsyncBackend: null,
            SourceWidth: SourceWidth,
            SourceHeight: SourceHeight,
            OutputWidth: options.Width,
            OutputHeight: options.Height,
            Filter: options.Filter.ToString(),
            Offscreen: true,
            CapturePath: options.CapturePath,
            Parity: parity,
            Frames: measured,
            WarmupFrames: options.WarmupFrames,
            ElapsedSeconds: Round(elapsedSeconds),
            Fps: Round(elapsedSeconds > 0 ? measured / elapsedSeconds : 0),
            AllocatedBytes: allocatedBytes,
            AllocatedBytesPerFrame: allocatedBytes / frames,
            RendererAllocatedBytes: allocatedBytes,
            RendererAllocatedBytesPerFrame: allocatedBytes / frames,
            RendererOnlyAllocatedBytes: allocatedBytes,
            RendererOnlyAllocatedBytesPerFrame: allocatedBytes / frames,
            RendererMeasureAllocations: true,
            RendererPresentAllocatedBytes: 0,
            UploadAllocatedBytes: 0,
            PaletteAllocatedBytes: 0,
            ScaleAllocatedBytes: 0,
            Present: Summarize(presentSamples),
            Gpu: Summarize(gpuSamples),
            FilterFallback: renderer?.FilterFallback ?? false);
    }

    private static GlProbeParity? ComputeParity(
        byte[] gpuPixels,
        VdlFrame frame,
        int width,
        int height,
        VideoFilter filter)
    {
        if (width != SourceWidth || height != SourceHeight || filter != VideoFilter.Accurate)
        {
            return null;
        }

        var cpu = new FrameBuffer(width, height);
        FrameAssembler.Assemble(frame, cpu, width, height);
        Span<byte> expected = cpu.PixelSpan;
        var direct = CountDifferentPixels(gpuPixels, expected, width, height, flipVertically: false);
        var flipped = CountDifferentPixels(gpuPixels, expected, width, height, flipVertically: true);
        var useFlip = flipped < direct;
        return new GlProbeParity(
            Compared: true,
            Matches: Math.Min(direct, flipped) == 0,
            DifferentPixels: Math.Min(direct, flipped),
            CpuHash: HashPixels(expected, width, height, flipVertically: false),
            GpuHash: HashPixels(gpuPixels, width, height, useFlip),
            VerticalFlip: useFlip);
    }

    private static int CountDifferentPixels(
        ReadOnlySpan<byte> gpuPixels,
        ReadOnlySpan<byte> cpuPixels,
        int width,
        int height,
        bool flipVertically)
    {
        var different = 0;
        for (var y = 0; y < height; y++)
        {
            var sourceRow = (flipVertically ? height - 1 - y : y) * width * 4;
            var targetRow = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var source = sourceRow + (x * 4);
                var target = targetRow + (x * 4);
                if (gpuPixels[source] != cpuPixels[target]
                    || gpuPixels[source + 1] != cpuPixels[target + 1]
                    || gpuPixels[source + 2] != cpuPixels[target + 2])
                {
                    different++;
                }
            }
        }

        return different;
    }

    private static uint HashPixels(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        bool flipVertically)
    {
        var hash = 2166136261u;
        for (var y = 0; y < height; y++)
        {
            var row = (flipVertically ? height - 1 - y : y) * width * 4;
            for (var x = 0; x < width * 4; x++)
            {
                hash = (hash ^ pixels[row + x]) * 16777619u;
            }
        }

        return hash;
    }

    private static GlProbeTiming? Summarize(List<double> samples)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        FrameTimingSummary summary = FrameTiming.Summarize(samples.ToArray());
        return new GlProbeTiming(
            Round(summary.MeanMs),
            Round(summary.P50Ms),
            Round(summary.P95Ms),
            Round(summary.P99Ms),
            Round(summary.MaxMs));
    }

    private static void WriteReport(string path, GlProbeReport report)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(report, GlProbeJsonContext.Default.GlProbeReport) + Environment.NewLine);
    }

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
