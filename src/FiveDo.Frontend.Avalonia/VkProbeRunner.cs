using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        return options.Window ? RunWindow(options) : RunOffscreen(options);
    }

    private static int RunOffscreen(VkProbeOptions options)
    {
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
                error: null,
                offscreen: true,
                vsyncRequested: false,
                vsyncActive: false,
                vsyncBackend: null);
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
                    error: exception.Message,
                    offscreen: true,
                    vsyncRequested: false,
                    vsyncActive: false,
                    vsyncBackend: null);
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

    private static int RunWindow(VkProbeOptions options)
    {
        var frame = new VdlFrame();
        SyntheticVdlFrame.Initialize(frame);
        var presentSamples = new List<double>(options.Frames);
        var gpuSamples = new List<double>(options.Frames);
        var stopwatch = new Stopwatch();
        long allocatedBytes = 0;
        var measured = 0;
        VulkanOffscreenRenderer? renderer = null;
        Win32ProbeWindow? window = null;
        byte[]? capturedPixels = null;
        try
        {
            window = new Win32ProbeWindow(options.Width, options.Height);
            renderer = new VulkanOffscreenRenderer
            {
                Settings = new RendererSettings
                {
                    Filter = options.Filter,
                    Aspect = AspectMode.Stretch,
                    IntegerScale = false,
                    Vsync = true,
                },
            };
            renderer.InitializeSurface(window.Handle, options.Width, options.Height, vsync: true);
            for (var i = 0; i < options.WarmupFrames + options.Frames; i++)
            {
                window.PumpMessages();
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
                        $"The Vulkan window probe timed out after {options.TimeoutSeconds} seconds.");
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
                error: null,
                offscreen: false,
                vsyncRequested: true,
                vsyncActive: renderer.SwapchainActive && string.Equals(renderer.PresentMode, "fifo", StringComparison.Ordinal),
                vsyncBackend: renderer.PresentMode);
            WriteReport(options.OutputPath, report);
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Serilog.Log.Error(exception, "Vulkan window probe failed.");
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
                    error: exception.Message,
                    offscreen: false,
                    vsyncRequested: true,
                    vsyncActive: renderer?.SwapchainActive == true && string.Equals(renderer.PresentMode, "fifo", StringComparison.Ordinal),
                    vsyncBackend: renderer?.PresentMode);
                WriteReport(options.OutputPath, report);
            }
            catch (Exception reportException) when (reportException is IOException or UnauthorizedAccessException)
            {
                Serilog.Log.Error(reportException, "Vulkan window probe report failed.");
            }

            return 2;
        }
        finally
        {
            renderer?.Dispose();
            window?.Dispose();
        }
    }

    private static bool IsParityCandidate(VkProbeOptions options)
    {
        return options.Width == SourceWidth
               && options.Height == SourceHeight
               && options.Filter == VideoFilter.Accurate;
    }

    private static GlProbeReport BuildReport(
        VkProbeOptions options,
        VulkanOffscreenRenderer? renderer,
        List<double> presentSamples,
        List<double> gpuSamples,
        int measured,
        double elapsedSeconds,
        long allocatedBytes,
        GlProbeParity? parity,
        string? error,
        bool offscreen,
        bool vsyncRequested,
        bool vsyncActive,
        string? vsyncBackend)
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
            SupportsVsync: renderer?.Capabilities.SupportsVsync ?? false,
            VsyncRequested: vsyncRequested,
            VsyncActive: vsyncActive,
            VsyncBackend: vsyncBackend,
            SourceWidth: SourceWidth,
            SourceHeight: SourceHeight,
            OutputWidth: options.Width,
            OutputHeight: options.Height,
            Filter: options.Filter.ToString(),
            Offscreen: offscreen,
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

    private static double Round(double value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private sealed class Win32ProbeWindow : IDisposable
    {
        private const uint WsOverlappedWindow = 0x00CF0000;
        private const uint WsVisible = 0x10000000;
        private const uint WmDestroy = 0x0002;
        private const uint WmClose = 0x0010;
        private const uint PmRemove = 0x0001;
        private const uint IdiArrow = 32512;
        private const int SwShow = 5;
        private const int ErrorClassAlreadyExists = 1410;

        private nint _handle;
        private bool _closed;

        internal Win32ProbeWindow(int width, int height)
        {
            WndProc windowProc = WindowProc;
            var className = $"5DOVulkanProbeWindow{Environment.ProcessId}";
            nint instance = GetModuleHandle(null);
            WndClassEx windowClass = new()
            {
                CbSize = (uint)Marshal.SizeOf<WndClassEx>(),
                LpfnWndProc = windowProc,
                HInstance = instance,
                HCursor = LoadCursor(0, IdiArrow),
                LpszClassName = className,
            };
            if (RegisterClassEx(ref windowClass) == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorClassAlreadyExists)
                {
                    throw new InvalidOperationException($"RegisterClassEx failed with Win32 error {error}.");
                }
            }

            Rect rect = new() { Left = 0, Top = 0, Right = width, Bottom = height };
            if (!AdjustWindowRect(ref rect, WsOverlappedWindow, false))
            {
                throw new InvalidOperationException("AdjustWindowRect failed.");
            }

            _handle = CreateWindowEx(
                0,
                className,
                "5DO Vulkan Window Probe",
                WsOverlappedWindow | WsVisible,
                100,
                100,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top,
                0,
                0,
                instance,
                0);
            if (_handle == 0)
            {
                throw new InvalidOperationException(
                    $"CreateWindowEx failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            _ = ShowWindow(_handle, SwShow);
            _ = UpdateWindow(_handle);
        }

        internal nint Handle => _handle;

        internal void PumpMessages()
        {
            while (PeekMessage(out Msg message, 0, 0, 0, PmRemove))
            {
                if (message.Message == 0x0012)
                {
                    _closed = true;
                    continue;
                }

                _ = TranslateMessage(ref message);
                _ = DispatchMessage(ref message);
            }

            if (_closed)
            {
                throw new InvalidOperationException("The Vulkan window was closed.");
            }
        }

        public void Dispose()
        {
            if (_handle != 0)
            {
                _ = DestroyWindow(_handle);
                _handle = 0;
            }
        }

        private nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam)
        {
            switch (message)
            {
                case WmClose:
                    _ = DestroyWindow(hwnd);
                    return 0;
                case WmDestroy:
                    PostQuitMessage(0);
                    return 0;
                default:
                    return DefWindowProc(hwnd, message, wParam, lParam);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClassEx
        {
            public uint CbSize;
            public uint Style;
            public WndProc LpfnWndProc;
            public int CbClsExtra;
            public int CbWndExtra;
            public nint HInstance;
            public nint HIcon;
            public nint HCursor;
            public nint HbrBackground;
            public string? LpszMenuName;
            public string LpszClassName;
            public nint HIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Msg
        {
            public nint Hwnd;
            public uint Message;
            public nint WParam;
            public nint LParam;
            public uint Time;
            public Point Pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WndClassEx windowClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern nint CreateWindowEx(
            uint exStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            nint parent,
            nint menu,
            nint instance,
            nint parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint LoadCursor(nint instance, uint cursorName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(nint hwnd, int command);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(nint hwnd);

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(
            out Msg message,
            nint hwnd,
            uint filterMin,
            uint filterMax,
            uint remove);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref Msg message);

        [DllImport("user32.dll")]
        private static extern nint DispatchMessage(ref Msg message);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int exitCode);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(nint hwnd);

        [DllImport("user32.dll")]
        private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        private static extern bool AdjustWindowRect(ref Rect rect, uint style, bool menu);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern nint GetModuleHandle(string? moduleName);
    }
}
