using System;
using System.IO;
using FiveDo.Video;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Command-line options for the Vulkan presentation probe.
/// </summary>
/// <remarks>
/// The probe renders the same synthetic frames as the OpenGL probe through the
/// optional <c>FiveDo.Video.Vulkan</c> backend and writes timing, capability, and parity
/// JSON. <c>--vk-probe</c> runs headless; <c>--vk-window-probe</c> opens a Win32 window
/// and presents through a native swapchain. Neither path requires Avalonia's render loop.
/// </remarks>
internal sealed record VkProbeOptions(
    string OutputPath,
    string? CapturePath,
    int Width,
    int Height,
    int Frames,
    int WarmupFrames,
    VideoFilter Filter,
    int TimeoutSeconds)
{
    /// <summary>Default output width for the Vulkan probe.</summary>
    public const int DefaultWidth = 1920;

    /// <summary>Default output height for the Vulkan probe.</summary>
    public const int DefaultHeight = 1080;

    /// <summary>Default number of measured frames.</summary>
    public const int DefaultFrames = 60;

    /// <summary>Default number of unmeasured warm-up frames.</summary>
    public const int DefaultWarmupFrames = 10;

    /// <summary>Default probe timeout in seconds.</summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>Gets a value indicating whether the probe presents to a native window swapchain.</summary>
    public bool Window { get; init; }

    /// <summary>Parses the headless Vulkan probe options from the process arguments.</summary>
    /// <param name="args">Process arguments.</param>
    /// <param name="options">Parsed options on success.</param>
    /// <param name="error">Validation error on failure.</param>
    /// <returns><see langword="true"/> when a valid probe request was found.</returns>
    internal static bool TryParse(string[] args, out VkProbeOptions? options, out string? error)
    {
        return TryParseCore(args, "--vk-probe", window: false, out options, out error);
    }

    /// <summary>Parses the native-window Vulkan probe options from the process arguments.</summary>
    /// <param name="args">Process arguments.</param>
    /// <param name="options">Parsed options on success.</param>
    /// <param name="error">Validation error on failure.</param>
    /// <returns><see langword="true"/> when a valid window probe request was found.</returns>
    internal static bool TryParseWindow(string[] args, out VkProbeOptions? options, out string? error)
    {
        return TryParseCore(args, "--vk-window-probe", window: true, out options, out error);
    }

    private static bool TryParseCore(
        string[] args,
        string optionName,
        bool window,
        out VkProbeOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        var output = Program.FindOptionValue(args, optionName);
        if (string.IsNullOrWhiteSpace(output))
        {
            error = $"{optionName} requires an output JSON path.";
            return false;
        }

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(output);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Serilog.Log.Debug(exception, "Invalid {Option} output path {Path}.", optionName, output);
            error = $"{optionName} output path is invalid: {exception.Message}";
            return false;
        }

        string? capturePath = null;
        var capture = Find("-capture");
        if (capture is not null)
        {
            try
            {
                capturePath = Path.GetFullPath(capture);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Serilog.Log.Debug(exception, "Invalid {Option}-capture path {Path}.", optionName, capture);
                error = $"{optionName}-capture path is invalid: {exception.Message}";
                return false;
            }
        }

        var width = DefaultWidth;
        var height = DefaultHeight;
        var size = Find("-size");
        if (size is not null && !GlProbeOptions.TryParseSize(size, out width, out height))
        {
            error = $"{optionName}-size must be WIDTHxHEIGHT, not '{size}'.";
            return false;
        }

        if (!GlProbeOptions.TryParseRange(
                Find("-frames"),
                DefaultFrames,
                1,
                100_000,
                optionName + "-frames",
                out var frames,
                out error))
        {
            return false;
        }

        if (!GlProbeOptions.TryParseRange(
                Find("-warmup"),
                DefaultWarmupFrames,
                0,
                10_000,
                optionName + "-warmup",
                out var warmup,
                out error))
        {
            return false;
        }

        if (!GlProbeOptions.TryParseRange(
                Find("-timeout"),
                DefaultTimeoutSeconds,
                1,
                600,
                optionName + "-timeout",
                out var timeout,
                out error))
        {
            return false;
        }

        VideoFilter filter = VideoFilter.Accurate;
        var filterText = Find("-filter");
        if (filterText is not null && !GlProbeOptions.TryParseFilter(filterText, out filter))
        {
            error = $"{optionName}-filter does not recognize '{filterText}'.";
            return false;
        }

        if (width is < 16 or > 16384 || height is < 16 or > 16384)
        {
            error = "The Vulkan probe output size must be between 16x16 and 16384x16384.";
            return false;
        }

        options = new VkProbeOptions(outputPath, capturePath, width, height, frames, warmup, filter, timeout)
        {
            Window = window,
        };
        return true;

        string? Find(string suffix)
        {
            return Program.FindOptionValue(args, optionName + suffix)
                   ?? Program.FindOptionValue(args, "--vk-probe" + suffix);
        }
    }
}
