using System;
using System.IO;
using FiveDo.Video;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Command-line options for the headless Vulkan presentation probe.
/// </summary>
/// <remarks>
/// The Vulkan probe renders the same synthetic frames as the OpenGL probe through the
/// optional <c>FiveDo.Video.Vulkan</c> backend and writes timing, capability, and parity
/// JSON. It does not open a window or require Avalonia's render loop.
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

    /// <summary>Parses the Vulkan probe options from the process arguments.</summary>
    /// <param name="args">Process arguments.</param>
    /// <param name="options">Parsed options on success.</param>
    /// <param name="error">Validation error on failure.</param>
    /// <returns><see langword="true"/> when a valid probe request was found.</returns>
    internal static bool TryParse(string[] args, out VkProbeOptions? options, out string? error)
    {
        options = null;
        error = null;

        var output = Program.FindOptionValue(args, "--vk-probe");
        if (string.IsNullOrWhiteSpace(output))
        {
            error = "--vk-probe requires an output JSON path.";
            return false;
        }

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(output);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"--vk-probe output path is invalid: {exception.Message}";
            return false;
        }

        string? capturePath = null;
        var capture = Program.FindOptionValue(args, "--vk-probe-capture");
        if (capture is not null)
        {
            try
            {
                capturePath = Path.GetFullPath(capture);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                error = $"--vk-probe-capture path is invalid: {exception.Message}";
                return false;
            }
        }

        var width = DefaultWidth;
        var height = DefaultHeight;
        var size = Program.FindOptionValue(args, "--vk-probe-size");
        if (size is not null && !GlProbeOptions.TryParseSize(size, out width, out height))
        {
            error = $"--vk-probe-size must be WIDTHxHEIGHT, not '{size}'.";
            return false;
        }

        if (!GlProbeOptions.TryParseRange(
                Program.FindOptionValue(args, "--vk-probe-frames"),
                DefaultFrames,
                1,
                100_000,
                "--vk-probe-frames",
                out var frames,
                out error))
        {
            return false;
        }

        if (!GlProbeOptions.TryParseRange(
                Program.FindOptionValue(args, "--vk-probe-warmup"),
                DefaultWarmupFrames,
                0,
                10_000,
                "--vk-probe-warmup",
                out var warmup,
                out error))
        {
            return false;
        }

        if (!GlProbeOptions.TryParseRange(
                Program.FindOptionValue(args, "--vk-probe-timeout"),
                DefaultTimeoutSeconds,
                1,
                600,
                "--vk-probe-timeout",
                out var timeout,
                out error))
        {
            return false;
        }

        VideoFilter filter = VideoFilter.Accurate;
        var filterText = Program.FindOptionValue(args, "--vk-probe-filter");
        if (filterText is not null && !GlProbeOptions.TryParseFilter(filterText, out filter))
        {
            error = $"--vk-probe-filter does not recognize '{filterText}'.";
            return false;
        }

        if (width is < 16 or > 16384 || height is < 16 or > 16384)
        {
            error = "The Vulkan probe output size must be between 16x16 and 16384x16384.";
            return false;
        }

        options = new VkProbeOptions(outputPath, capturePath, width, height, frames, warmup, filter, timeout);
        return true;
    }
}
