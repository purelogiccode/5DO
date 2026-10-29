using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FiveDo.Core;
using FiveDo.Diagnostics;
using FiveDo.Frontend.Avalonia.Controls;
using FiveDo.Video;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// A code-only window that measures the real OpenGL presentation path.
/// </summary>
/// <remarks>
/// The probe is intentionally independent of the emulator session, settings, BIOS,
/// and disc. It creates the same <see cref="FiveDo.Frontend.Avalonia.Controls.VideoSurface"/> used by the
/// desktop shell, submits synthetic 320x240 VDL frames, and records the per-frame
/// present and GPU times. It is used to validate the 4K output budget and the
/// platform swap-interval path on real hardware.
/// </remarks>
internal sealed class GlProbeWindow : Window, IDisposable
{
    private const int SourceWidth = EmulatorConstants.VideoWidth;
    private const int SourceHeight = EmulatorConstants.VideoHeight;

    private readonly GlProbeOptions _options;
    private readonly Action<int> _exit;
    private readonly VideoSurface _video = new();
    private readonly VdlFrame _frame = new();
    private readonly List<double> _presentSamples;
    private readonly List<double> _gpuSamples;
    private readonly Stopwatch _elapsed = new();
    private DispatcherTimer? _timeout;
    private RendererCapabilities? _capabilities;
    private GlProbeParity? _parity;
    private long _startAllocatedBytes;
    private long? _measuredAllocatedBytes;
    private int _submitted;
    private int _presented;
    private int _measured;
    private bool _started;
    private bool _measuring;
    private bool _finished;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="GlProbeWindow"/> class.</summary>
    /// <param name="options">Probe options.</param>
    /// <param name="exit">Callback that shuts down the application with an exit code.</param>
    internal GlProbeWindow(GlProbeOptions options, Action<int> exit)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(exit);
        _options = options;
        _exit = exit;
        _presentSamples = new List<double>(options.Frames);
        _gpuSamples = new List<double>(options.Frames);

        Title = "5DO GPU probe";
        Width = options.Offscreen ? 640 : options.Width;
        Height = options.Offscreen ? 360 : options.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Black;
        Content = _video;

        _video.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        _video.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
        ToolTip.SetTip(
            _video,
            "Synthetic GPU probe output used to measure presentation timing and CPU/GPU parity.");
        _video.UseOffscreenProbe = options.Offscreen;
        _video.OffscreenProbeWidth = options.Width;
        _video.OffscreenProbeHeight = options.Height;
        _video.MeasureAllocations = true;
        _video.Settings = new RendererSettings
        {
            Filter = options.Filter,
            Aspect = AspectMode.Stretch,
            Vsync = options.Vsync,
            IntegerScale = false,
        };

        InitializeSyntheticFrame();
        _video.OpenGlReady += OnOpenGlReady;
        _video.OpenGlFailed += OnOpenGlFailed;
        _video.FramePresented += OnFramePresented;
        _video.CaptureCompleted += OnCaptureCompleted;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // The requested size is in physical pixels. Convert it to Avalonia's logical
        // units so a 150/200% DPI desktop still measures a true 4K framebuffer. An
        // offscreen probe keeps the window small and renders the requested size into
        // an FBO, so it works even when the desktop is smaller than 4K.
        if (!_options.Offscreen)
        {
            var scaling = RenderScaling;
            if (scaling > 0 && Math.Abs(scaling - 1.0) > 0.001)
            {
                Width = Math.Max(1.0, _options.Width / scaling);
                Height = Math.Max(1.0, _options.Height / scaling);
            }
        }

        // The timeout covers GL initialization as well as warm-up/measurement, so a
        // missing or hung context cannot leave the probe process running forever.
        _timeout = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_options.TimeoutSeconds),
        };
        _timeout.Tick += (_, _) =>
        {
            _timeout?.Stop();
            Finish(false, $"The probe timed out after {_options.TimeoutSeconds} seconds.");
        };
        _timeout.Start();

        // Let the resize settle before the first measured frame.
        DispatcherTimer.RunOnce(StartProbe, TimeSpan.FromMilliseconds(400));
    }

    private void OnOpenGlReady(object? sender, OpenGlReadyEventArgs e)
    {
        _capabilities = e.Capabilities;
    }

    private void OnOpenGlFailed(object? sender, OpenGlFailedEventArgs e)
    {
        Finish(false, e.Exception.Message);
    }

    private void StartProbe()
    {
        if (_finished)
        {
            return;
        }

        if (_capabilities is null)
        {
            // OpenGL initialization may still be in flight on a slow driver. Retry
            // until the overall timeout fires rather than failing on a fixed delay.
            DispatcherTimer.RunOnce(StartProbe, TimeSpan.FromMilliseconds(100));
            return;
        }

        _started = true;
        SubmitNext();
    }

    private void SubmitNext()
    {
        if (_finished || _submitted >= _options.WarmupFrames + _options.Frames)
        {
            return;
        }

        FillSyntheticFrame(_submitted);
        _submitted++;
        _video.SubmitFrame(_frame);
    }

    private void OnFramePresented(object? sender, FramePresentedEventArgs e)
    {
        var presentMilliseconds = e.PresentMilliseconds;
        var gpuMilliseconds = e.GpuMilliseconds;
        if (_finished || !_started)
        {
            return;
        }

        _presented++;
        if (_presented <= _options.WarmupFrames)
        {
            SubmitNext();
            return;
        }

        if (!_measuring)
        {
            _measuring = true;
            _video.ResetAllocationCounters();
            _startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            _elapsed.Restart();
            _presentSamples.Clear();
            _gpuSamples.Clear();
        }

        _presentSamples.Add(presentMilliseconds);
        if (gpuMilliseconds is not null)
        {
            _gpuSamples.Add(gpuMilliseconds.Value);
        }

        _measured++;
        if (_measured >= _options.Frames)
        {
            BeginCaptureOrFinish();
            return;
        }

        SubmitNext();
    }

    private void BeginCaptureOrFinish()
    {
        _measuredAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - _startAllocatedBytes;
        if (_options.CapturePath is null)
        {
            Finish(true, null);
            return;
        }

        _video.RequestCapture();
    }

    private void OnCaptureCompleted(object? sender, CaptureCompletedEventArgs e)
    {
        var pixels = e.Pixels;
        var width = e.Width;
        var height = e.Height;
        if (_finished)
        {
            return;
        }

        try
        {
            PngDumper.WriteBgra(pixels, width, height, _options.CapturePath!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Serilog.Log.Error(exception, "GPU probe capture failed.");
            Finish(false, $"The probe capture failed: {exception.Message}");
            return;
        }

        _parity = ComputeParity(pixels, width, height);
        Finish(true, null);
    }

    private GlProbeParity? ComputeParity(byte[] gpuPixels, int width, int height)
    {
        // The parity oracle is only meaningful for a 1:1 Accurate presentation;
        // upscaled/filtered outputs are compared visually, not byte-for-byte.
        if (width != SourceWidth || height != SourceHeight || _options.Filter != VideoFilter.Accurate)
        {
            return null;
        }

        var cpu = new FrameBuffer(width, height);
        FrameAssembler.Assemble(_frame, cpu, width, height);
        Span<byte> expected = cpu.PixelSpan;
        var direct = CountDifferentPixels(gpuPixels, expected, width, height, flipVertically: false);
        var flipped = CountDifferentPixels(gpuPixels, expected, width, height, flipVertically: true);
        var useFlip = flipped < direct;
        var different = Math.Min(direct, flipped);
        var gpuHash = HashGpuPixels(gpuPixels, width, height, useFlip);
        var cpuHash = HashPixels(expected, width, height, flipVertically: false);
        return new GlProbeParity(
            Compared: true,
            Matches: different == 0,
            DifferentPixels: different,
            CpuHash: cpuHash,
            GpuHash: gpuHash,
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

    private static uint HashGpuPixels(byte[] pixels, int width, int height, bool flipVertically)
        => HashPixels(pixels, width, height, flipVertically);

    private void InitializeSyntheticFrame()
    {
        _frame.SourceWidth = SourceWidth;
        _frame.SourceHeight = SourceHeight;
        for (var y = 0; y < SourceHeight; y++)
        {
            VdlLine line = _frame[y];
            line.Width = SourceWidth;
            line.OutControl = 0;
            line.Background = 0;
            for (var i = 0; i < VdlLine.ClutSize; i++)
            {
                line.ClutR[i] = (byte)(i * 8);
                line.ClutG[i] = (byte)(255 - (i * 8));
                line.ClutB[i] = (byte)(((i * 3) & 0x1f) * 8);
            }
        }
    }

    private void FillSyntheticFrame(int phase)
    {
        for (var y = 0; y < SourceHeight; y++)
        {
            Span<ushort> pixels = _frame[y].Pixels;
            var row = (y + phase) & 0x1f;
            for (var x = 0; x < SourceWidth; x++)
            {
                var column = (x + phase) & 0x1f;
                var xor = (x ^ y) & 0x1f;
                pixels[x] = (ushort)(column | (row << 5) | (xor << 10));
            }
        }
    }

    private void Finish(bool success, string? error)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _elapsed.Stop();
        _timeout?.Stop();

        GlProbeReport report = BuildReport(success, error);
        try
        {
            var directory = Path.GetDirectoryName(_options.OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _options.OutputPath,
                JsonSerializer.Serialize(report, GlProbeJsonContext.Default.GlProbeReport) + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Error(exception, "GPU probe report failed.");
            _exit(2);
            return;
        }

        _exit(success ? 0 : 2);
    }

    private GlProbeReport BuildReport(bool success, string? error)
    {
        var allocated = _measuredAllocatedBytes
            ?? Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - _startAllocatedBytes);
        var frames = Math.Max(1, _measured);
        var elapsedSeconds = _elapsed.Elapsed.TotalSeconds;
        RendererCapabilities? capabilities = _capabilities;
        return new GlProbeReport(
            Ok: success,
            Error: error,
            RequestedBackend: Program.RequestedGlBackend.ToString(),
            Backend: capabilities?.Name,
            Vendor: _video.GlVendor,
            Renderer: _video.GlRenderer,
            Api: capabilities?.Api.ToString(),
            MajorVersion: capabilities?.MajorVersion ?? 0,
            MinorVersion: capabilities?.MinorVersion ?? 0,
            MaxTextureSize: capabilities?.MaxTextureSize ?? 0,
            SupportsIntegerTextures: capabilities?.SupportsIntegerTextures ?? false,
            SupportsVsync: capabilities?.SupportsVsync ?? false,
            VsyncRequested: _options.Vsync,
            VsyncActive: _video.VsyncActive,
            VsyncBackend: _video.VsyncBackend,
            SourceWidth: SourceWidth,
            SourceHeight: SourceHeight,
            OutputWidth: _video.RenderedWidth,
            OutputHeight: _video.RenderedHeight,
            Filter: _options.Filter.ToString(),
            Offscreen: _options.Offscreen,
            CapturePath: _options.CapturePath,
            Parity: _parity,
            Frames: _measured,
            WarmupFrames: _options.WarmupFrames,
            ElapsedSeconds: Round(elapsedSeconds),
            Fps: Round(elapsedSeconds > 0 ? _measured / elapsedSeconds : 0),
            AllocatedBytes: allocated,
            AllocatedBytesPerFrame: allocated / frames,
            RendererAllocatedBytes: _video.RendererAllocatedBytes,
            RendererAllocatedBytesPerFrame: _video.RendererAllocatedBytes / frames,
            RendererOnlyAllocatedBytes: _video.RendererOnlyAllocatedBytes,
            RendererOnlyAllocatedBytesPerFrame: _video.RendererOnlyAllocatedBytes / frames,
            RendererMeasureAllocations: _video.RendererMeasureAllocations,
            RendererPresentAllocatedBytes: _video.LastRendererPresentAllocatedBytes,
            UploadAllocatedBytes: _video.LastRendererUploadAllocatedBytes,
            PaletteAllocatedBytes: _video.LastRendererPaletteAllocatedBytes,
            ScaleAllocatedBytes: _video.LastRendererScaleAllocatedBytes,
            Present: Summarize(_presentSamples),
            Gpu: _gpuSamples.Count == 0 ? null : Summarize(_gpuSamples));
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

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private void OnClosed(object? sender, EventArgs e) => Dispose();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timeout?.Stop();
        _video.OpenGlReady -= OnOpenGlReady;
        _video.OpenGlFailed -= OnOpenGlFailed;
        _video.FramePresented -= OnFramePresented;
        _video.CaptureCompleted -= OnCaptureCompleted;
        _video.Dispose();
    }
}

/// <summary>Source-generated JSON metadata for the GPU probe report.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(GlProbeReport))]
internal sealed partial class GlProbeJsonContext : JsonSerializerContext;
