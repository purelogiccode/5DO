using System;
using System.Runtime.InteropServices;

namespace FiveDo.Host;

/// <summary>
/// Presentation-only crop from the four edges of a native 3DO frame.
/// </summary>
/// <remarks>
/// The crop hides outer pixels before presentation and native screenshots. It never
/// changes frame hashes, input, audio, or save states. Values are clamped to 0-32 pixels
/// per edge, matching the historical uniform overscan range.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CropInsets(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Gets the maximum crop per edge.</summary>
    public const int MaxPixels = 32;

    /// <summary>Gets the minimum visible width or height after cropping.</summary>
    public const int MinimumVisibleSize = 16;

    /// <summary>Gets an empty crop.</summary>
    public static CropInsets None => default;

    /// <summary>Creates a uniform crop from every edge.</summary>
    /// <param name="pixels">Crop in pixels.</param>
    /// <returns>A uniform crop.</returns>
    public static CropInsets Uniform(int pixels)
    {
        int value = Math.Clamp(pixels, 0, MaxPixels);
        return new CropInsets(value, value, value, value);
    }

    /// <summary>Gets a value indicating whether every edge is zero.</summary>
    public bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

    /// <summary>Gets the total horizontal crop.</summary>
    public int Horizontal => Left + Right;

    /// <summary>Gets the total vertical crop.</summary>
    public int Vertical => Top + Bottom;

    /// <summary>Clamps every edge into the supported range.</summary>
    /// <returns>A normalized crop.</returns>
    public CropInsets Clamp()
    {
        return new CropInsets(
            Math.Clamp(Left, 0, MaxPixels),
            Math.Clamp(Top, 0, MaxPixels),
            Math.Clamp(Right, 0, MaxPixels),
            Math.Clamp(Bottom, 0, MaxPixels));
    }

    /// <summary>Gets whether the crop leaves a usable image at the supplied source size.</summary>
    /// <param name="width">Source width.</param>
    /// <param name="height">Source height.</param>
    /// <returns><see langword="true"/> when the visible image is at least 16x16.</returns>
    public bool Fits(int width, int height)
    {
        CropInsets crop = Clamp();
        return width - crop.Horizontal >= MinimumVisibleSize
            && height - crop.Vertical >= MinimumVisibleSize;
    }
}
