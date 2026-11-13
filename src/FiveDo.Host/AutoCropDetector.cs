using System;
using System.Runtime.InteropServices;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Host;

/// <summary>
/// Presentation-only crop detected from the visible content of a frame.
/// </summary>
/// <remarks>
/// Unlike the manual <see cref="CropInsets"/>, auto-crop can remove up to 25% of each
/// edge, matching 4DO's <c>frame.cpp</c> limit. The source frame, frame hashes, audio,
/// input, and save states are never modified.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct AutoCropInsets(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Gets an empty crop.</summary>
    public static AutoCropInsets None => default;

    /// <summary>Gets a value indicating whether every edge is zero.</summary>
    public bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

    /// <summary>Gets the total horizontal crop.</summary>
    public int Horizontal => Left + Right;

    /// <summary>Gets the total vertical crop.</summary>
    public int Vertical => Top + Bottom;

    /// <summary>Gets whether the crop leaves a usable image at the supplied source size.</summary>
    /// <param name="width">Source width.</param>
    /// <param name="height">Source height.</param>
    /// <returns><see langword="true"/> when the visible image is at least 16x16.</returns>
    public bool Fits(int width, int height)
    {
        return width - Horizontal >= CropInsets.MinimumVisibleSize
            && height - Vertical >= CropInsets.MinimumVisibleSize;
    }
}

/// <summary>
/// Detects blank borders on a VDL frame and applies 4DO's one-second hysteresis.
/// </summary>
/// <remarks>
/// The detector ports 4DO's <c>Get_Frame_Bitmap</c> comparison: a pixel counts as content
/// when any color component's top five bits are at least 15/31 (~47% brightness), and a
/// fully blank frame crops at the 25% limit. 4DO's <c>CropHelper</c> is ported as
/// <see cref="AutoCropTracker.Consider"/>: showing more pixels is accepted immediately,
/// while cropping more must stay stable for <see cref="ConsiderationMilliseconds"/>.
/// </remarks>
public static class AutoCropDetector
{
    /// <summary>Maximum crop per edge as a percentage of the source size.</summary>
    public const int MaxCropPercent = 25;

    /// <summary>
    /// 8-bit content threshold equivalent to 4DO's 5-bit <c>0xF</c> comparison.
    /// </summary>
    public const byte ContentThreshold = 0x78;

    /// <summary>Time a larger crop must remain stable before it is accepted.</summary>
    public const long ConsiderationMilliseconds = 1000;

    /// <summary>Detects the blank border of a completed frame.</summary>
    /// <param name="frame">Frame to inspect. The frame is not modified.</param>
    /// <returns>The detected crop, clamped to the 25% limit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="frame"/> is null.</exception>
    public static AutoCropInsets Detect(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        int width = Math.Clamp(frame.SourceWidth, 0, VdlLine.MaxPixels);
        int height = Math.Clamp(frame.SourceHeight, 0, VdlFrame.MaxLines);
        if (width == 0 || height == 0)
        {
            return AutoCropInsets.None;
        }

        int maxHorizontal = (int)((long)width * MaxCropPercent / 100);
        int maxVertical = (int)((long)height * MaxCropPercent / 100);
        int left = maxHorizontal;
        int top = maxVertical;
        int right = maxHorizontal;
        int bottom = maxVertical;

        for (var line = 0; line < height; line++)
        {
            VdlLine sourceLine = frame[line];
            Span<ushort> pixels = sourceLine.PixelBuffer;
            for (var x = 0; x < width; x++)
            {
                FrameAssembler.ExpandPixel(sourceLine, pixels[x], out var red, out var green, out var blue);
                if (!IsContent(red) && !IsContent(green) && !IsContent(blue))
                {
                    continue;
                }

                if (line < top)
                {
                    top = line;
                }

                if (x < left)
                {
                    left = x;
                }

                if (x > width - right - 1)
                {
                    right = width - x - 1;
                }

                if (line > height - bottom - 1)
                {
                    bottom = height - line - 1;
                }
            }
        }

        var crop = new AutoCropInsets(left, top, right, bottom);
        return crop.Fits(width, height) ? crop : AutoCropInsets.None;
    }

    private static bool IsContent(byte component)
    {
        return component >= ContentThreshold;
    }
}

/// <summary>
/// Tracks an auto-crop candidate over time and exposes the accepted crop.
/// </summary>
/// <remarks>
/// Ported from 4DO's <c>CropHelper</c>. Call <see cref="Consider"/> with the detected
/// crop and a monotonic millisecond timestamp, once per presented frame.
/// </remarks>
public sealed class AutoCropTracker
{
    private AutoCropInsets? _candidate;
    private long _candidateSince;

    /// <summary>Gets the currently accepted crop.</summary>
    public AutoCropInsets Current { get; private set; }

    /// <summary>Resets the tracker to an empty crop.</summary>
    public void Reset()
    {
        Current = AutoCropInsets.None;
        _candidate = null;
        _candidateSince = 0;
    }

    /// <summary>
    /// Applies 4DO's hysteresis to a detected crop and returns the accepted crop.
    /// </summary>
    /// <param name="detected">Crop detected on the current frame.</param>
    /// <param name="nowMilliseconds">Monotonic timestamp in milliseconds.</param>
    /// <returns>The crop that should be presented now.</returns>
    public AutoCropInsets Consider(AutoCropInsets detected, long nowMilliseconds)
    {
        if (detected == Current)
        {
            return Current;
        }

        if (detected.Left < Current.Left
            || detected.Top < Current.Top
            || detected.Right < Current.Right
            || detected.Bottom < Current.Bottom)
        {
            // The new crop would reveal pixels the current crop hides, so accept the
            // smaller value on every edge immediately.
            Current = new AutoCropInsets(
                Math.Min(detected.Left, Current.Left),
                Math.Min(detected.Top, Current.Top),
                Math.Min(detected.Right, Current.Right),
                Math.Min(detected.Bottom, Current.Bottom));
            _candidate = null;
            return Current;
        }

        if (_candidate is null || _candidate.Value != detected)
        {
            _candidate = detected;
            _candidateSince = nowMilliseconds;
            return Current;
        }

        if (nowMilliseconds - _candidateSince < AutoCropDetector.ConsiderationMilliseconds)
        {
            return Current;
        }

        Current = detected;
        _candidate = null;
        return Current;
    }
}
