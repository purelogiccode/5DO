using System;
using System.Globalization;
using System.IO;
using FiveDo.Video;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Command-line options for the GPU presentation probe.
/// </summary>
/// <remarks>
/// The probe is a developer/validation tool. It opens the real Avalonia OpenGL surface,
/// presents synthetic 320x240 VDL frames at a requested output size, and writes
/// per-frame present/GPU timings as JSON. It never loads a BIOS or disc.
/// </remarks>
internal sealed record GlProbeOptions(
    string OutputPath,
    string? CapturePath,
    int Width,
    int Height,
    int Frames,
    int WarmupFrames,
    VideoFilter Filter,
    bool Vsync,
    bool Offscreen,
    int TimeoutSeconds)
{
    /// <summary>Default output width used for the 4K measurement.</summary>
    public const int DefaultWidth = 3840;

    /// <summary>Default output height used for the 4K measurement.</summary>
    public const int DefaultHeight = 2160;

    /// <summary>Default number of presented frames.</summary>
    public const int DefaultFrames = 120;

    /// <summary>Default number of unmeasured warm-up frames.</summary>
    public const int DefaultWarmupFrames = 30;

    /// <summary>Default probe timeout.</summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>Parses the probe options from the process arguments.</summary>
    /// <param name="args">Process arguments.</param>
    /// <param name="options">Parsed options on success.</param>
    /// <param name="error">Validation error on failure.</param>
    /// <returns><see langword="true"/> when a valid probe request was found.</returns>
    public static bool TryParse(string[] args, out GlProbeOptions? options, out string? error)
    {
        options = null;
        error = null;

        var output = Program.FindOptionValue(args, "--gl-probe");
        if (string.IsNullOrWhiteSpace(output))
        {
            error = "--gl-probe requires an output JSON path.";
            return false;
        }

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(output);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"--gl-probe output path is invalid: {exception.Message}";
            return false;
        }

        string? capturePath = null;
        var capture = Program.FindOptionValue(args, "--gl-probe-capture");
        if (capture is not null)
        {
            try
            {
                capturePath = Path.GetFullPath(capture);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                error = $"--gl-probe-capture path is invalid: {exception.Message}";
                return false;
            }
        }

        var width = DefaultWidth;
        var height = DefaultHeight;
        var size = Program.FindOptionValue(args, "--gl-probe-size");
        if (size is not null && !TryParseSize(size, out width, out height))
        {
            error = $"--gl-probe-size must be WIDTHxHEIGHT, not '{size}'.";
            return false;
        }

        if (!TryParseRange(
                Program.FindOptionValue(args, "--gl-probe-frames"),
                DefaultFrames,
                1,
                100_000,
                "--gl-probe-frames",
                out var frames,
                out error))
        {
            return false;
        }

        if (!TryParseRange(
                Program.FindOptionValue(args, "--gl-probe-warmup"),
                DefaultWarmupFrames,
                0,
                10_000,
                "--gl-probe-warmup",
                out var warmup,
                out error))
        {
            return false;
        }

        if (!TryParseRange(
                Program.FindOptionValue(args, "--gl-probe-timeout"),
                DefaultTimeoutSeconds,
                1,
                600,
                "--gl-probe-timeout",
                out var timeout,
                out error))
        {
            return false;
        }

        var filter = VideoFilter.SharpBilinear;
        var filterText = Program.FindOptionValue(args, "--gl-probe-filter");
        if (filterText is not null && !TryParseFilter(filterText, out filter))
        {
            error = $"--gl-probe-filter does not recognize '{filterText}'.";
            return false;
        }

        if (width is < 16 or > 16384 || height is < 16 or > 16384)
        {
            error = "The probe output size must be between 16x16 and 16384x16384.";
            return false;
        }

        options = new GlProbeOptions(
            outputPath,
            capturePath,
            width,
            height,
            frames,
            warmup,
            filter,
            Program.HasFlag(args, "--gl-probe-vsync"),
            Program.HasFlag(args, "--gl-probe-offscreen"),
            timeout);
        return true;
    }

    private static bool TryParseSize(string text, out int width, out int height)
    {
        width = 0;
        height = 0;
        var separator = text.IndexOfAny(['x', 'X', '*', ',']);
        if (separator <= 0 || separator >= text.Length - 1)
        {
            return false;
        }

        return int.TryParse(
                text.AsSpan(0, separator),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out width)
            && int.TryParse(
                text.AsSpan(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out height);
    }

    private static bool TryParseRange(
        string? text,
        int defaultValue,
        int minimum,
        int maximum,
        string option,
        out int value,
        out string? error)
    {
        error = null;
        if (text is null)
        {
            value = defaultValue;
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            || value < minimum
            || value > maximum)
        {
            error = $"{option} must be an integer from {minimum} to {maximum}, not '{text}'.";
            return false;
        }

        return true;
    }

    private static bool TryParseFilter(string text, out VideoFilter filter)
    {
        var normalized = text.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        if (normalized.StartsWith("SHARP", StringComparison.Ordinal))
        {
            filter = VideoFilter.SharpBilinear;
            return true;
        }

        switch (normalized)
        {
            case "ACCURATE":
                filter = VideoFilter.Accurate;
                return true;
            case "NEAREST":
                filter = VideoFilter.Nearest;
                return true;
            case "CRT":
                filter = VideoFilter.Crt;
                return true;
            case "XBR":
            case "XBRLV2":
                filter = VideoFilter.Xbr;
                return true;
            case "HQX":
            case "HQX2X":
            case "HQ2X":
                filter = VideoFilter.Hqx;
                return true;
            case "HQX3X":
            case "HQ3X":
                filter = VideoFilter.Hqx3x;
                return true;
            case "HQX4X":
            case "HQ4X":
                filter = VideoFilter.Hqx4x;
                return true;
            default:
                filter = VideoFilter.SharpBilinear;
                return false;
        }
    }
}
